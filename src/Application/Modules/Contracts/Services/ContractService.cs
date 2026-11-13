using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Audit;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Media;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Audit;
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
    IIdentityVerifiedChecker identityChecker,
    ITenantVerifiedChecker tenantChecker,
    IMediaStorage fileStorage,
    IMediaSigner mediaSigner,
    IContractPdfGenerator pdfGenerator,
    IQuotaService quotaService,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IAuditLogger audit,
    IOptions<SmtpOptions> smtpOptions,
    TimeProvider timeProvider,
    ILogger<ContractService> logger) : IContractService
{
    private const string PngDataUrlPrefix = "data:image/png;base64,";
    private const int MaxSignatureBytes = 512 * 1024;

    // Nhật ký hash-chain của hợp đồng (F11, AUD-01): mỗi hợp đồng là một chuỗi riêng.
    private const string AuditResourceType = "Contract";
    private static readonly Dictionary<string, string> AuditTitles = new()
    {
        ["contract.created"] = "Khởi tạo hợp đồng nháp",
        ["contract.sent"] = "Gửi hợp đồng đi ký",
        ["contract.signed"] = "Ký hợp đồng",
        ["contract.approved"] = "Duyệt hợp đồng",
        ["contract.coordinated"] = "Điều phối người ký",
        ["contract.completed"] = "Hoàn tất hợp đồng",
        ["contract.declined"] = "Từ chối ký",
        ["contract.cancelled"] = "Hủy hợp đồng",
    };

    private sealed record ContractAuditPayload(string Detail, string FileHash);
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
        Result<Contract> accessResult = await GetAuthorizedContractAsync(tenantId, actorUserId, actorRole, contractId, cancel);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        Contract contract = accessResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        string documentUrl = mediaSigner.CreateDocumentUrl(tenantId, contract.Id, now);

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
                s.BoxHeight,
                s.Role.ToString(),
                s.Phone,
                s.Organization,
                s.Title))
            .ToList();

        ContractListItemDto summary = ToListItemDto(contract);
        IReadOnlyList<ContractEventDto> events = (await audit.ListForResourceAsync(
                tenantId, AuditResourceType, contract.Id.ToString(), cancel))
            .Select(ToEventDto)
            .ToList();

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
            events,
            documentUrl);
    }

    public async Task<Result<CreatedContractResponseDto>> CreateFromTemplateAsync(
        Guid tenantId,
        Guid userId,
        CreateFromTemplateRequestDto request,
        CancellationToken cancel = default)
    {
        if (!await identityChecker.IsVerifiedAsync(userId, cancel))
        {
            return IdentityErrors.IdentityNotVerified;
        }

        string trimmedTitle = request.Title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length is 0 or > 200)
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
        string code = await GenerateUniqueContractCodeAsync(now, cancel);

        byte[] pdfBytes = pdfGenerator.RenderTemplateToPdf(
            trimmedTitle,
            code,
            renderResult.Value,
            now);

        return await PersistDraftAsync(
            tenantId,
            userId,
            trimmedTitle,
            template.Id,
            code,
            pdfBytes,
            request.Signers,
            now,
            cancel);
    }

    public async Task<Result<CreatedContractResponseDto>> UploadDraftAsync(
        Guid tenantId,
        Guid userId,
        string title,
        ReadOnlyMemory<byte> pdfBytes,
        IReadOnlyList<SetSignerItemDto>? signers,
        CancellationToken cancel = default)
    {
        if (!await identityChecker.IsVerifiedAsync(userId, cancel))
        {
            return IdentityErrors.IdentityNotVerified;
        }

        string trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length is 0 or > 200)
        {
            return ContractErrors.InvalidContractTitle;
        }

        if (pdfBytes.IsEmpty || !IsValidPdf(pdfBytes.Span))
        {
            return ContractErrors.InvalidPdfFile;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        string code = await GenerateUniqueContractCodeAsync(now, cancel);

        return await PersistDraftAsync(
            tenantId,
            userId,
            trimmedTitle,
            templateId: null,
            code,
            pdfBytes,
            signers,
            now,
            cancel);
    }

    public async Task<Result> SetSignersAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        SetSignersRequestDto request,
        CancellationToken cancel = default)
    {
        Result<Contract> accessResult = await GetAuthorizedContractAsync(tenantId, actorUserId, actorRole, contractId, cancel);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        Contract contract = accessResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result setResult = contract.SetSigners(ToSpecs(request.Signers), now);
        if (setResult.IsFailure)
        {
            return setResult.Error;
        }

        await contracts.ReplaceSignersAsync(contract, contract.Signers, cancel);
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
        Result<Contract> accessResult = await GetAuthorizedContractAsync(tenantId, actorUserId, actorRole, contractId, cancel);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        // Người ký chỉ nên nhận lời mời từ doanh nghiệp đã được xác thực pháp nhân; bản nháp thì doanh nghiệp nào cũng soạn được.
        if (!await tenantChecker.IsVerifiedAsync(tenantId, cancel))
        {
            return TenantVerificationErrors.TenantNotVerified;
        }

        Contract contract = accessResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();

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

            ContractSigner? first = contract.Signers.FirstOrDefault(s => s.Status == SignerStatus.Current);
            await LogContractAsync(
                contract,
                actorUserId,
                "contract.sent",
                first is null
                    ? "Đã khóa nội dung hợp đồng"
                    : $"Đã khóa nội dung và gửi lượt #{first.SignOrder} tới {first.FullName} ({first.Email})",
                cancel);
            await contracts.SaveChangesAsync(cancel);
            return Result.Success();
        }, cancel);

        if (txResult.IsFailure)
        {
            return txResult.Error;
        }

        // Gửi email thông báo cho người ký/duyệt đầu tiên (bỏ qua Cc)
        ContractSigner? firstSigner = contract.Signers
            .Where(s => s.Role != RecipientRole.Cc)
            .OrderBy(s => s.SignOrder)
            .FirstOrDefault();

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
        Result<Contract> accessResult = await GetAuthorizedContractAsync(tenantId, actorUserId, actorRole, contractId, cancel);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        Contract contract = accessResult.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        Result cancelResult = contract.Cancel(now);
        if (cancelResult.IsFailure)
        {
            return cancelResult.Error;
        }

        await LogContractAsync(contract, actorUserId, "contract.cancelled", "Hợp đồng đã bị hủy và dừng luồng ký.", cancel);
        await contracts.SaveChangesAsync(cancel);
        return Result.Success();
    }

    public async Task<Result<byte[]>> GetDocumentBytesByTokenAsync(
        Guid contractId,
        string token,
        CancellationToken cancel = default)
    {
        DateTimeOffset now = timeProvider.GetUtcNow();
        if (!mediaSigner.TryValidateDocumentToken(token, now, out Guid tokenTenantId, out Guid tokenContractId) ||
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

    public async Task<Result> ProcessSignOrApproveAsync(
        Guid contractId,
        Guid signerId,
        string? signaturePng,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        ContractSigner? signer = contract.Signers.FirstOrDefault(s => s.Id == signerId);
        if (signer is null)
        {
            return ContractErrors.SignerNotFound;
        }

        // Bấm ký lần hai (hoặc gửi lại khi mất phản hồi) trả thành công mà không ghi thêm chữ ký (TC07).
        if (signer.Status == SignerStatus.Signed)
        {
            return Result.Success();
        }

        byte[]? signatureBytes = null;
        string? signatureKey = null;
        if (signer.Role == RecipientRole.Signer)
        {
            signatureBytes = DecodeSignaturePng(signaturePng);
            if (signatureBytes is null)
            {
                return SigningErrors.SignatureImageInvalid;
            }

            signatureKey = $"tenants/{contract.TenantId:D}/contracts/{contract.Id:D}/signatures/{signer.Id:D}.png";
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result advance = contract.SignOrApproveCurrent(signerId, now, signatureKey);
        if (advance.IsFailure)
        {
            return advance.Error;
        }

        if (signatureBytes is not null)
        {
            await fileStorage.SaveAsync(signatureKey!, signatureBytes, cancel);
        }

        bool isApprover = signer.Role == RecipientRole.Approver;
        await LogContractAsync(
            contract,
            null,
            isApprover ? "contract.approved" : "contract.signed",
            $"Lượt #{signer.SignOrder}: {signer.FullName} ({signer.Email}) đã {(isApprover ? "duyệt" : "ký")}",
            cancel);

        if (contract.Status == ContractStatus.Completed)
        {
            await LogContractAsync(contract, null, "contract.completed", "Tất cả người ký và duyệt đã hoàn thành lượt của mình.", cancel);
        }

        // Lưu trước rồi mới gửi email: lưu lỗi thì không ai nhận thư mời cho một lượt chưa được ghi nhận.
        await contracts.SaveChangesAsync(cancel);

        if (contract.Status == ContractStatus.Completed)
        {
            await NotifyCcRecipientsAsync(contract, cancel);
        }
        else if (contract.Signers.FirstOrDefault(s => s.Status == SignerStatus.Current) is { } next)
        {
            await TrySendFirstSignerNotificationAsync(contract, next, cancel);
        }

        return Result.Success();
    }

    public async Task<Result> CoordinateAsync(
        Guid contractId,
        Guid signerId,
        IReadOnlyList<SetSignerItemDto> recipients,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        ContractSigner? coordinator = contract.Signers.FirstOrDefault(s => s.Id == signerId);
        if (coordinator is null)
        {
            return ContractErrors.SignerNotFound;
        }

        // Gửi lại sau khi đã điều phối (mất phản hồi) trả thành công, không chèn người lần hai.
        if (coordinator.Role == RecipientRole.Coordinator && coordinator.Status == SignerStatus.Signed)
        {
            return Result.Success();
        }

        // Giữ đúng thứ tự người điều phối nhập, không sắp theo Order client gửi.
        List<ContractSignerSpec> specs = (recipients ?? [])
            .Select(s => new ContractSignerSpec(s.FullName, s.Email, Role: s.Role, Phone: s.Phone, Organization: s.Organization, Title: s.Title))
            .ToList();

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result coordinated = contract.Coordinate(signerId, specs, now);
        if (coordinated.IsFailure)
        {
            return coordinated.Error;
        }

        string addedList = string.Join(", ", contract.Signers
            .Where(s => s.SignOrder > coordinator.SignOrder && s.SignOrder <= coordinator.SignOrder + specs.Count)
            .OrderBy(s => s.SignOrder)
            .Select(s => $"#{s.SignOrder} {s.FullName} ({s.Email}, {RoleLabel(s.Role)})"));
        await LogContractAsync(
            contract,
            null,
            "contract.coordinated",
            $"Lượt #{coordinator.SignOrder}: {coordinator.FullName} ({coordinator.Email}) đã điều phối: {addedList}",
            cancel);

        await contracts.SaveChangesAsync(cancel);

        if (contract.Signers.FirstOrDefault(s => s.Status == SignerStatus.Current) is { } next)
        {
            await TrySendFirstSignerNotificationAsync(contract, next, cancel);
        }

        return Result.Success();
    }

    private static string RoleLabel(RecipientRole role) => role switch
    {
        RecipientRole.Approver => "duyệt",
        RecipientRole.Cc => "nhận bản sao",
        RecipientRole.Coordinator => "điều phối",
        _ => "ký",
    };

    public async Task<Result> DeclineAsync(
        Guid contractId,
        Guid signerId,
        string reason,
        CancellationToken cancel = default)
    {
        Contract? contract = await contracts.FindByIdAsync(contractId, cancel);
        if (contract is null)
        {
            return ContractErrors.ContractNotFound;
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        Result decline = contract.DeclineCurrent(signerId, reason, now);
        if (decline.IsFailure)
        {
            return decline.Error;
        }

        ContractSigner signer = contract.Signers.First(s => s.Id == signerId);
        await LogContractAsync(
            contract,
            null,
            "contract.declined",
            $"Lượt #{signer.SignOrder}: {signer.FullName} ({signer.Email}) từ chối. Lý do: {signer.DeclineReason}",
            cancel);
        await contracts.SaveChangesAsync(cancel);

        await NotifyCreatorDeclinedAsync(contract, signer, cancel);
        return Result.Success();
    }

    private async Task<Result<Contract>> GetAuthorizedContractAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        CancellationToken cancel)
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

        return contract;
    }

    private static List<ContractSignerSpec> ToSpecs(IReadOnlyList<SetSignerItemDto>? items) =>
        (items ?? [])
            .OrderBy(s => s.Order ?? int.MaxValue)
            .Select(s => new ContractSignerSpec(
                s.FullName,
                s.Email,
                s.PageNumber,
                s.BoxX,
                s.BoxY,
                s.BoxWidth,
                s.BoxHeight,
                s.Role,
                s.Phone,
                s.Organization,
                s.Title))
            .ToList();

    private async Task<Result<CreatedContractResponseDto>> PersistDraftAsync(
        Guid tenantId,
        Guid userId,
        string title,
        Guid? templateId,
        string code,
        ReadOnlyMemory<byte> pdfBytes,
        IReadOnlyList<SetSignerItemDto>? signers,
        DateTimeOffset now,
        CancellationToken cancel)
    {
        Guid contractId = Guid.CreateVersion7(now);
        string sha256Hex = Convert.ToHexStringLower(SHA256.HashData(pdfBytes.Span));
        string storageKey = $"tenants/{tenantId:D}/contracts/{contractId:D}/original.pdf";

        User? creator = await users.FindByIdAsync(userId, cancel);
        string creatorName = creator?.FullName ?? "Thành viên";

        Result<Contract> draftResult = Contract.CreateDraft(
            contractId,
            tenantId,
            code,
            title,
            templateId,
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

        // Kiểm người nhận trước khi lưu file và ghi DB: sai thì không để lại bản nháp dở dang.
        if (signers is not null)
        {
            Result setSigners = draftResult.Value.SetSigners(ToSpecs(signers), now);
            if (setSigners.IsFailure)
            {
                return setSigners.Error;
            }
        }

        await fileStorage.SaveAsync(storageKey, pdfBytes, cancel);
        await contracts.AddAsync(draftResult.Value, cancel);
        await LogContractAsync(draftResult.Value, userId, "contract.created", $"Người tạo: {creatorName}", cancel);

        return new CreatedContractResponseDto(contractId);
    }

    public async Task<Result> RemindCurrentSignerAsync(
        Guid tenantId,
        Guid actorUserId,
        TenantRole actorRole,
        Guid contractId,
        CancellationToken cancel = default)
    {
        Result<Contract> accessResult = await GetAuthorizedContractAsync(tenantId, actorUserId, actorRole, contractId, cancel);
        if (accessResult.IsFailure)
        {
            return accessResult.Error;
        }

        ContractSigner? current = accessResult.Value.Signers.FirstOrDefault(s => s.Status == SignerStatus.Current);
        if (current is null)
        {
            return ContractErrors.NoPendingSigner;
        }

        return await TrySendFirstSignerNotificationAsync(accessResult.Value, current, cancel)
            ? Result.Success()
            : ContractErrors.InvitationEmailFailed;
    }

    /// <summary>Gửi email mời ký/duyệt; trả false nếu gửi lỗi (đã ghi log) để luồng ký không bị chặn.</summary>
    private async Task<bool> TrySendFirstSignerNotificationAsync(
        Contract contract,
        ContractSigner firstSigner,
        CancellationToken cancel)
    {
        try
        {
            string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
            string safeTitle = WebUtility.HtmlEncode(contract.Title);
            string safeSigner = WebUtility.HtmlEncode(firstSigner.FullName);
            string roleLabel = firstSigner.Role switch
            {
                RecipientRole.Approver => "duyệt",
                RecipientRole.Coordinator => "điều phối",
                _ => "ký",
            };
            string signingToken = mediaSigner.CreateSigningToken(contract.Id, firstSigner.Id, timeProvider.GetUtcNow());
            string signUrl = $"{baseUrl}/sign/{Uri.EscapeDataString(signingToken)}";

            string html = $"""
                <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
                  <h2 style="color:#1b3a5c;margin-bottom:12px;">Yêu cầu {roleLabel} hợp đồng điện tử</h2>
                  <p>Xin chào <strong>{safeSigner}</strong>,</p>
                  <p>Bạn được mời {roleLabel} hợp đồng <strong>{safeTitle}</strong> (Mã: <code>{contract.Code}</code>) trên nền tảng Chữ Tín (TrustSign).</p>
                  <p>Hạn xử lý: <strong>{contract.DueAt:dd/MM/yyyy HH:mm}</strong></p>
                  <p style="margin:24px 0;">
                    <a href="{signUrl}" style="background:#1b3a5c;color:#ffffff;text-decoration:none;padding:12px 24px;border-radius:4px;display:inline-block;font-weight:600;">
                      Xem và {roleLabel} hợp đồng
                    </a>
                  </p>
                  <p style="font-size:13px;color:#5f6b7a;">Hoặc mở trực tiếp: <a href="{signUrl}">{signUrl}</a></p>
                  <p style="font-size:13px;color:#5f6b7a;">Mã băm SHA-256 gốc: <code>{contract.OriginalFileHash}</code></p>
                  <p style="font-size:12px;color:#5f6b7a;">Hệ thống hợp đồng điện tử Chữ Tín ({baseUrl}).</p>
                </div>
                """;

            await emailSender.SendAsync(
                new EmailMessage(firstSigner.Email, $"[Chữ Tín] Mời {roleLabel} hợp đồng {contract.Code} - {contract.Title}", html),
                cancel);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email mời ký/duyệt hợp đồng {ContractCode} tới {Email}", contract.Code, firstSigner.Email);
            return false;
        }
    }

    private async Task NotifyCcRecipientsAsync(Contract contract, CancellationToken cancel)
    {
        var ccSigners = contract.Signers.Where(s => s.Role == RecipientRole.Cc).ToList();
        if (ccSigners.Count == 0) return;

        string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
        string viewUrl = $"{baseUrl}/app/contracts/{contract.Id}";
        string safeTitle = WebUtility.HtmlEncode(contract.Title);

        foreach (ContractSigner cc in ccSigners)
        {
            try
            {
                string safeName = WebUtility.HtmlEncode(cc.FullName);
                string html = $"""
                    <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
                      <h2 style="color:#1b3a5c;margin-bottom:12px;">Hợp đồng điện tử đã hoàn tất</h2>
                      <p>Xin chào <strong>{safeName}</strong>,</p>
                      <p>Hợp đồng <strong>{safeTitle}</strong> (Mã: <code>{contract.Code}</code>) mà bạn được chỉ định nhận bản sao (Cc) đã được tất cả các bên ký duyệt hoàn tất.</p>
                      <p style="margin:24px 0;">
                        <a href="{viewUrl}" style="background:#1b3a5c;color:#ffffff;text-decoration:none;padding:10px 20px;border-radius:4px;display:inline-block;font-weight:600;">
                          Xem hợp đồng đã hoàn tất
                        </a>
                      </p>
                      <p style="font-size:13px;color:#5f6b7a;">Hoặc mở trực tiếp: <a href="{viewUrl}">{viewUrl}</a></p>
                      <p style="font-size:12px;color:#5f6b7a;">Hệ thống hợp đồng điện tử Chữ Tín ({baseUrl}).</p>
                    </div>
                    """;

                await emailSender.SendAsync(
                    new EmailMessage(cc.Email, $"[Chữ Tín] Bản sao hợp đồng hoàn tất: {contract.Code} - {contract.Title}", html),
                    cancel);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Không gửi được email thông báo hoàn tất tới người nhận Cc {Email}", cc.Email);
            }
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

        // Fallback không gian số lớn hơn kèm kiểm tra va chạm
        int fallbackNum = RandomNumberGenerator.GetInt32(10000, 999999);
        return $"HD-{now:yyyy}-{fallbackNum}";
    }

    private static bool IsValidPdf(ReadOnlySpan<byte> bytes) =>
        bytes.Length > 5 && bytes.Length <= Contract.MaxPdfSizeBytes && MediaContent.Detect(bytes) == MediaType.Pdf;

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

    private Task LogContractAsync(Contract contract, Guid? actorUserId, string action, string detail, CancellationToken cancel) =>
        audit.LogAsync(
            contract.TenantId,
            actorUserId,
            action,
            AuditResourceType,
            contract.Id.ToString(),
            new ContractAuditPayload(detail, contract.CurrentFileHash),
            cancel);

    private static ContractEventDto ToEventDto(AuditEvent e)
    {
        string detail = e.PayloadJson is null
            ? string.Empty
            : JsonSerializer.Deserialize<ContractAuditPayload>(e.PayloadJson)?.Detail ?? string.Empty;

        return new ContractEventDto(e.CreatedAt, AuditTitles.GetValueOrDefault(e.Action, e.Action), detail, e.Hash);
    }

    /// <summary>Giải mã ảnh chữ ký từ data URL hoặc base64; trả null nếu không phải PNG hoặc quá 512 KB.</summary>
    internal static byte[]? DecodeSignaturePng(string? signaturePng)
    {
        if (string.IsNullOrWhiteSpace(signaturePng))
        {
            return null;
        }

        string base64 = signaturePng.Trim();
        if (base64.StartsWith(PngDataUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            base64 = base64[PngDataUrlPrefix.Length..];
        }

        // Chặn trước khi giải mã: chuỗi base64 dài hơn mức 512 KB thì khỏi cấp bộ nhớ.
        if (base64.Length > (MaxSignatureBytes / 3 * 4) + 4)
        {
            return null;
        }

        byte[] buffer = new byte[base64.Length];
        if (!Convert.TryFromBase64String(base64, buffer, out int written) ||
            written <= 8 ||
            written > MaxSignatureBytes ||
            MediaContent.Detect(buffer.AsSpan(0, written)) != MediaType.Png)
        {
            return null;
        }

        return buffer[..written];
    }

    private async Task NotifyCreatorDeclinedAsync(Contract contract, ContractSigner signer, CancellationToken cancel)
    {
        try
        {
            User? creator = await users.FindByIdAsync(contract.CreatedByUserId, cancel);
            if (creator is null)
            {
                return;
            }

            string baseUrl = _smtpOptions.FrontendBaseUrl.TrimEnd('/');
            string viewUrl = $"{baseUrl}/app/contracts/{contract.Id}";
            string html = $"""
                <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
                  <h2 style="color:#1b3a5c;margin-bottom:12px;">Hợp đồng bị từ chối ký</h2>
                  <p>Xin chào <strong>{WebUtility.HtmlEncode(creator.FullName)}</strong>,</p>
                  <p><strong>{WebUtility.HtmlEncode(signer.FullName)}</strong> ({WebUtility.HtmlEncode(signer.Email)}) đã từ chối hợp đồng <strong>{WebUtility.HtmlEncode(contract.Title)}</strong> (Mã: <code>{contract.Code}</code>). Luồng ký đã dừng.</p>
                  <p>Lý do: {WebUtility.HtmlEncode(signer.DeclineReason ?? string.Empty)}</p>
                  <p style="margin:24px 0;">
                    <a href="{viewUrl}" style="background:#1b3a5c;color:#ffffff;text-decoration:none;padding:10px 20px;border-radius:4px;display:inline-block;font-weight:600;">
                      Xem hợp đồng
                    </a>
                  </p>
                  <p style="font-size:12px;color:#5f6b7a;">Hệ thống hợp đồng điện tử Chữ Tín ({baseUrl}).</p>
                </div>
                """;

            await emailSender.SendAsync(
                new EmailMessage(creator.Email, $"[Chữ Tín] Hợp đồng bị từ chối: {contract.Code} - {contract.Title}", html),
                cancel);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Không gửi được email báo từ chối hợp đồng {ContractCode}", contract.Code);
        }
    }
}
