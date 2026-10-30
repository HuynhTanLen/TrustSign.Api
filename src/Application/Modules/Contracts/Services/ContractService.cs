using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Contracts;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories;
using TrustSign.Domain.IRepositories.Contracts;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Contracts;

internal sealed class ContractService(
    IContractRepository contracts,
    IContractTemplateRepository templates,
    IUserRepository users,
    IContractFileStorage fileStorage,
    IContractPdfGenerator pdfGenerator,
    IQuotaService quotaService,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IOptions<SmtpOptions> smtpOptions,
    TimeProvider timeProvider,
    ILogger<ContractService> logger) : IContractService
{
    private static readonly byte[] PdfMagicBytes = "%PDF-"u8.ToArray();
    private readonly SmtpOptions _smtpOptions = smtpOptions.Value;

    public async Task<Result<IReadOnlyList<ContractListItemDto>>> ListAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        string? query,
        string? status,
        CancellationToken cancel = default)
    {
        ContractStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse(status.Trim(), ignoreCase: true, out ContractStatus parsed))
        {
            statusFilter = parsed;
        }

        // Nhân viên (Member) chỉ thấy hợp đồng mình tạo; Quản lý hợp đồng & Chủ DN thấy toàn bộ DN (TC20).
        Guid? creatorFilter = actorRole == TenantRole.Employee ? actorUserId : null;

        IReadOnlyList<Contract> list = await contracts.ListByTenantAsync(
            tenantId,
            creatorFilter,
            query,
            statusFilter,
            cancel);

        return list.Select(ToListItemDto).ToList();
    }

    public async Task<Result<ContractDetailDto>> GetByIdAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(tenantId, contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        if (actorRole == TenantRole.Employee && contract.CreatedByUserId != actorUserId)
        {
            return IdentityErrors.TenantAccessDenied;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string documentUrl = fileStorage.CreateShortLivedDocumentUrl(tenantId, contract.Id, now);

        IReadOnlyList<ContractSignerDto> signerDtos = contract.Signers
            .OrderBy(s => s.SignOrder)
            .Select(s => new ContractSignerDto(
                s.SignOrder,
                s.FullName,
                s.Email,
                s.Status.ToString(),
                s.SignedAt,
                s.DeclineReason,
                s.PageNumber,
                s.BoxX,
                s.BoxY,
                s.BoxWidth,
                s.BoxHeight))
            .ToList();

        ContractListItemDto summary = ToListItemDto(contract);
        IReadOnlyList<ContractEventDto> events = BuildTimelineEvents(contract);

        return new ContractDetailDto(
            summary.Id,
            summary.Code,
            summary.Title,
            summary.Partner,
            summary.CreatorName,
            summary.Status,
            summary.SentAt,
            summary.DueAt,
            summary.CurrentSigner,
            signerDtos,
            Signatures: [],
            Events: events,
            DocumentUrl: documentUrl);
    }

    public async Task<Result<CreatedContractResponseDto>> CreateFromTemplateAsync(
        Guid tenantId,
        Guid userId,
        CreateFromTemplateRequestDto request,
        CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 200)
        {
            return ContractErrors.InvalidContractTitle;
        }

        ContractTemplate? template = await templates.FindByIdAsync(tenantId, request.TemplateId, cancel);
        if (template is null)
        {
            return ContractErrors.TemplateNotFound;
        }

        Result<string> renderResult = template.Render(request.Values);
        if (renderResult.IsFailure)
        {
            return renderResult.Error;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid contractId = Guid.CreateVersion7(now);
        string code = await GenerateUniqueContractCodeAsync(now, cancel);

        byte[] pdfBytes = pdfGenerator.RenderTemplateToPdf(
            request.Title.Trim(),
            code,
            renderResult.Value,
            now);

        string sha256Hex = Convert.ToHexStringLower(SHA256.HashData(pdfBytes));
        string storageKey = $"tenants/{tenantId:D}/contracts/{contractId:D}/original.pdf";

        User? creator = await users.FindByIdAsync(userId, cancel);
        string creatorName = creator?.FullName ?? "Thành viên";

        Result<Contract> draftResult = Contract.CreateDraft(
            contractId,
            tenantId,
            code,
            request.Title,
            template.Id,
            storageKey,
            sha256Hex,
            pdfBytes.LongLength,
            userId,
            creatorName,
            now);

        if (draftResult.IsFailure)
        {
            return draftResult.Error;
        }

        await fileStorage.SaveAsync(storageKey, pdfBytes, cancel);
        await contracts.AddAsync(draftResult.Value, cancel);

        return new CreatedContractResponseDto(contractId);
    }

    public async Task<Result<CreatedContractResponseDto>> UploadDraftAsync(
        Guid tenantId,
        Guid userId,
        string title,
        ReadOnlyMemory<byte> pdfBytes,
        CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
        {
            return ContractErrors.InvalidContractTitle;
        }

        if (!IsValidPdf(pdfBytes.Span))
        {
            return ContractErrors.InvalidPdfFile;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Guid contractId = Guid.CreateVersion7(now);
        string code = await GenerateUniqueContractCodeAsync(now, cancel);
        string sha256Hex = Convert.ToHexStringLower(SHA256.HashData(pdfBytes.Span));
        string storageKey = $"tenants/{tenantId:D}/contracts/{contractId:D}/original.pdf";

        User? creator = await users.FindByIdAsync(userId, cancel);
        string creatorName = creator?.FullName ?? "Thành viên";

        Result<Contract> draftResult = Contract.CreateDraft(
            contractId,
            tenantId,
            code,
            title,
            templateId: null,
            storageKey,
            sha256Hex,
            pdfBytes.Length,
            userId,
            creatorName,
            now);

        if (draftResult.IsFailure)
        {
            return draftResult.Error;
        }

        await fileStorage.SaveAsync(storageKey, pdfBytes, cancel);
        await contracts.AddAsync(draftResult.Value, cancel);

        return new CreatedContractResponseDto(contractId);
    }

    public async Task<Result> SetSignersAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        SetSignersRequestDto request,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(tenantId, contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        if (actorRole == TenantRole.Employee && contract.CreatedByUserId != actorUserId)
        {
            return IdentityErrors.TenantAccessDenied;
        }

        IReadOnlyList<SetSignerItemDto> items = request.Signers ?? [];
        List<ContractSignerSpec> specs = items
            .OrderBy(s => s.Order ?? int.MaxValue)
            .Select(s => new ContractSignerSpec(
                s.FullName,
                s.Email,
                s.PageNumber,
                s.BoxX,
                s.BoxY,
                s.BoxWidth,
                s.BoxHeight))
            .ToList();

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result setResult = contract.SetSigners(specs, now);
        if (setResult.IsFailure)
        {
            return setResult.Error;
        }

        await contracts.ReplaceSignersAsync(contract, contract.Signers.ToList(), cancel);
        return Result.Success();
    }

    public async Task<Result> SendForSigningAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        SendContractRequestDto request,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(tenantId, contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        if (actorRole == TenantRole.Employee && contract.CreatedByUserId != actorUserId)
        {
            return IdentityErrors.TenantAccessDenied;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();

        // Gửi ký + trừ hạn mức trong cùng một database transaction (CON-06, TC15, TC21).
        // Nếu hết hạn mức hoặc thuê bao hết hạn, transaction rollback để hợp đồng vẫn ở trạng thái Nháp.
        Result txResult = await unitOfWork.ExecuteInTransactionAsync(async () =>
        {
            Result markSent = contract.MarkSent(request.DueAt, now);
            if (markSent.IsFailure)
            {
                return markSent.Error;
            }

            Result quota = await quotaService.ConsumeQuotaAsync(tenantId, cancel);
            if (quota.IsFailure)
            {
                return quota.Error;
            }

            await contracts.SaveChangesAsync(cancel);
            return Result.Success();
        }, cancel);

        if (txResult.IsFailure)
        {
            return txResult.Error;
        }

        // Gửi email thông báo cho người ký đầu tiên
        ContractSigner? firstSigner = contract.Signers.OrderBy(s => s.SignOrder).FirstOrDefault();
        if (firstSigner is not null)
        {
            await TrySendFirstSignerNotificationAsync(contract, firstSigner, cancel);
        }

        return Result.Success();
    }

    public async Task<Result> CancelAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(tenantId, contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        if (actorRole == TenantRole.Employee && contract.CreatedByUserId != actorUserId)
        {
            return IdentityErrors.TenantAccessDenied;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result cancelResult = contract.Cancel(now);
        if (cancelResult.IsFailure)
        {
            return cancelResult.Error;
        }

        await contracts.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result<byte[]>> GetDocumentBytesByTokenAsync(
        Guid contractId,
        string token,
        CancellationToken cancel = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!fileStorage.ValidateDocumentToken(token, out Guid tokenTenantId, out Guid tokenContractId, now) ||
            tokenContractId != contractId)
        {
            return ContractErrors.DocumentAccessDenied;
        }

        Contract? contract = await contracts.FindByIdAsync(tokenTenantId, contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        byte[]? bytes = await fileStorage.ReadAsync(contract.StorageKey, cancel);
        if (bytes is null)
        {
            return ContractErrors.ContractNotFound;
        }

        return bytes;
    }

    private async Task TrySendFirstSignerNotificationAsync(
        Contract contract,
        ContractSigner firstSigner,
        CancellationToken cancel)
    {
        try
        {
            string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
            string safeTitle = WebUtility.HtmlEncode(contract.Title);
            string safeSigner = WebUtility.HtmlEncode(firstSigner.FullName);

            string html = $"""
                <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
                  <h2 style="color:#1f5fa8;margin-bottom:12px;">Yêu cầu ký hợp đồng điện tử</h2>
                  <p>Xin chào <strong>{safeSigner}</strong>,</p>
                  <p>Bạn được mời ký hợp đồng <strong>{safeTitle}</strong> (Mã: <code>{contract.Code}</code>) trên nền tảng Chữ Tín (TrustSign).</p>
                  <p>Hạn ký: <strong>{contract.DueAt:dd/MM/yyyy HH:mm}</strong></p>
                  <p style="font-size:13px;color:#5f6b7a;">Mã băm SHA-256 gốc: <code>{contract.OriginalFileHash}</code></p>
                  <p style="font-size:12px;color:#5f6b7a;">Hệ thống hợp đồng điện tử Chữ Tín ({baseUrl}).</p>
                </div>
                """;

            await emailSender.SendAsync(
                new EmailMessage(firstSigner.Email, $"[Chữ Tín] Mời ký hợp đồng {contract.Code} - {contract.Title}", html),
                cancel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email mời ký hợp đồng {ContractCode} tới {Email}", contract.Code, firstSigner.Email);
        }
    }

    private async Task<string> GenerateUniqueContractCodeAsync(DateTimeOffset now, CancellationToken cancel)
    {
        for (int i = 0; i < 10; i++)
        {
            int num = RandomNumberGenerator.GetInt32(1000, 9999);
            string code = $"HD-{now:yyyy}-{num}";
            if (!await contracts.CodeExistsAsync(code, cancel))
            {
                return code;
            }
        }

        return $"HD-{now:yyyy}-{now.ToUnixTimeMilliseconds() % 100000:D5}";
    }

    private static bool IsValidPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length > PdfMagicBytes.Length &&
        bytes.Length <= Contract.MaxPdfSizeBytes &&
        bytes[..PdfMagicBytes.Length].SequenceEqual(PdfMagicBytes);

    private static ContractListItemDto ToListItemDto(Contract c)
    {
        IReadOnlyList<ContractSigner> ordered = c.Signers.OrderBy(s => s.SignOrder).ToList();
        string partner = ordered.FirstOrDefault()?.FullName ?? "Chưa chọn người ký";
        string? currentSigner = ordered.FirstOrDefault(s => s.Status == SignerStatus.Current)?.FullName;

        return new ContractListItemDto(
            c.Id,
            c.Code,
            c.Title,
            partner,
            c.CreatorName,
            c.Status.ToString(),
            c.SentAt,
            c.DueAt,
            currentSigner);
    }

    private static IReadOnlyList<ContractEventDto> BuildTimelineEvents(Contract c)
    {
        List<ContractEventDto> events = [];
        string prevHash = new('0', 64);

        prevHash = AppendEvent(
            events,
            prevHash,
            c.CreatedAt,
            "Khởi tạo hợp đồng nháp",
            $"Người tạo: {c.CreatorName} · SHA-256 tài liệu: {c.OriginalFileHash[..16]}…");

        if (c.SentAt.HasValue)
        {
            ContractSigner? first = c.Signers.OrderBy(s => s.SignOrder).FirstOrDefault();
            prevHash = AppendEvent(
                events,
                prevHash,
                c.SentAt.Value,
                "Gửi hợp đồng đi ký",
                first is not null
                    ? $"Đã khóa nội dung và gửi lượt ký #1 tới {first.FullName} ({first.Email})"
                    : "Đã khóa nội dung hợp đồng");
        }

        if (c.CancelledAt.HasValue)
        {
            AppendEvent(
                events,
                prevHash,
                c.CancelledAt.Value,
                "Hủy hợp đồng",
                "Hợp đồng đã bị hủy và dừng luồng ký.");
        }

        return events;
    }

    private static string AppendEvent(
        List<ContractEventDto> list,
        string prevHash,
        DateTimeOffset at,
        string title,
        string detail)
    {
        string canonical = $"{prevHash}|{at.ToUnixTimeMilliseconds()}|{title}|{detail}";
        string hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
        list.Add(new ContractEventDto(at, title, detail, hash));
        return hash;
    }
}
