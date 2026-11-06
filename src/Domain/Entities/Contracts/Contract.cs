using TrustSign.Common;
using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Contracts;

public sealed record ContractSignerSpec(
    string FullName,
    string Email,
    int? PageNumber = null,
    float? BoxX = null,
    float? BoxY = null,
    float? BoxWidth = null,
    float? BoxHeight = null,
    RecipientRole Role = RecipientRole.Signer,
    string? Phone = null,
    string? Organization = null,
    string? Title = null);

/// <summary>
/// Hợp đồng điện tử thuộc một doanh nghiệp (F02–F05, F09, F22, F23).
/// </summary>
public sealed class Contract
{
    public const int MaxSigners = 5;
    public const long MaxPdfSizeBytes = 10 * 1024 * 1024; // 10 MB

    private readonly List<ContractSigner> _signers = [];

    private Contract()
    {
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public string Code { get; private set; } = null!;

    public string Title { get; private set; } = null!;

    public ContractStatus Status { get; private set; }

    public Guid? TemplateId { get; private set; }

    public string StorageKey { get; private set; } = null!;

    /// <summary>Mã băm SHA-256 (64 ký tự hex thường) của file PDF gốc lúc khởi tạo/gửi ký (F02, F05).</summary>
    public string OriginalFileHash { get; private set; } = null!;

    /// <summary>Mã băm SHA-256 của file PDF hiện hành (cập nhật sau mỗi lượt ký PAdES).</summary>
    public string CurrentFileHash { get; private set; } = null!;

    public long FileSizeBytes { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public string CreatorName { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? SentAt { get; private set; }

    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public IReadOnlyList<ContractSigner> Signers => _signers;

    public static Result<Contract> CreateDraft(
        Guid id,
        Guid tenantId,
        string code,
        string title,
        Guid? templateId,
        string storageKey,
        string fileHashSha256,
        long fileSizeBytes,
        Guid createdByUserId,
        string creatorName,
        DateTimeOffset now)
    {
        string trimmedTitle = title?.Trim() ?? string.Empty;
        if (trimmedTitle.Length is 0 or > 200)
        {
            return ContractErrors.InvalidContractTitle;
        }

        if (string.IsNullOrWhiteSpace(storageKey) ||
            string.IsNullOrWhiteSpace(fileHashSha256) ||
            fileSizeBytes is <= 0 or > MaxPdfSizeBytes)
        {
            return ContractErrors.InvalidPdfFile;
        }

        string normalizedHash = fileHashSha256.Trim().ToLowerInvariant();

        return new Contract
        {
            Id = id,
            TenantId = tenantId,
            Code = code.Trim().ToUpperInvariant(),
            Title = trimmedTitle,
            Status = ContractStatus.Draft,
            TemplateId = templateId,
            StorageKey = storageKey.Trim(),
            OriginalFileHash = normalizedHash,
            CurrentFileHash = normalizedHash,
            FileSizeBytes = fileSizeBytes,
            CreatedByUserId = createdByUserId,
            CreatorName = string.IsNullOrWhiteSpace(creatorName) ? "Thành viên" : creatorName.Trim(),
            CreatedAt = now
        };
    }

    /// <summary>
    /// Cấu hình danh sách người nhận theo thứ tự 1..5 và tọa độ ô chữ ký (với Signer).
    /// Bắt buộc hợp đồng phải có ít nhất một người ký (Signer).
    /// </summary>
    public Result SetSigners(IReadOnlyList<ContractSignerSpec> specs, DateTimeOffset now)
    {
        if (Status != ContractStatus.Draft)
        {
            return ContractErrors.ContractNotEditable;
        }

        if (specs is null || specs.Count is 0 or > MaxSigners)
        {
            return ContractErrors.InvalidSigners;
        }

        if (!specs.Any(s => s.Role == RecipientRole.Signer))
        {
            return ContractErrors.AtLeastOneSignerRequired;
        }

        List<ContractSigner> built = new(specs.Count);
        for (int i = 0; i < specs.Count; i++)
        {
            ContractSignerSpec spec = specs[i];
            string fullName = spec.FullName?.Trim() ?? string.Empty;
            string email = spec.Email?.Trim() ?? string.Empty;

            if (fullName.Length is 0 or > 100 || !EmailValidator.IsValid(email))
            {
                return ContractErrors.InvalidSigners;
            }

            int order = i + 1;
            int page = spec.PageNumber ?? 1;

            // Tọa độ mặc định xếp ngang/dọc ở cuối trang A4 (595 x 842 pt) nếu chưa chỉ định
            float defaultX = 55f + ((i % 2) * 260f);
            float defaultY = 140f - ((i / 2) * 65f);
            float boxX = spec.BoxX ?? defaultX;
            float boxY = spec.BoxY ?? defaultY;
            float boxW = spec.BoxWidth ?? 180f;
            float boxH = spec.BoxHeight ?? 55f;

            if (spec.Role == RecipientRole.Signer)
            {
                if (page < 1 || boxX < 0 || boxY < 0 || boxW <= 10 || boxH <= 10)
                {
                    return ContractErrors.InvalidSignatureBox;
                }
            }
            else
            {
                // Với Approver hoặc Cc không cần ô ký
                page = 1;
                boxX = 0;
                boxY = 0;
                boxW = 0;
                boxH = 0;
            }

            built.Add(ContractSigner.Create(
                Id,
                order,
                fullName,
                email,
                page,
                boxX,
                boxY,
                boxW,
                boxH,
                now,
                spec.Role,
                spec.Phone,
                spec.Organization,
                spec.Title));
        }

        _signers.Clear();
        _signers.AddRange(built);
        return Result.Success();
    }

    /// <summary>
    /// Chuyển hợp đồng từ Nháp sang Chờ ký, khóa nội dung và kích hoạt lượt của người đầu tiên
    /// (người đầu tiên có vai trò Signer hoặc Approver, bỏ qua Cc).
    /// </summary>
    public Result MarkSent(DateTimeOffset dueAt, DateTimeOffset now)
    {
        if (Status != ContractStatus.Draft)
        {
            return ContractErrors.ContractNotEditable;
        }

        if (_signers.Count is 0 or > MaxSigners)
        {
            return ContractErrors.InvalidSigners;
        }

        if (!_signers.Any(s => s.Role == RecipientRole.Signer))
        {
            return ContractErrors.AtLeastOneSignerRequired;
        }

        if (dueAt <= now)
        {
            return ContractErrors.InvalidDueDate;
        }

        Status = ContractStatus.Pending;
        SentAt = now;
        DueAt = dueAt;

        // Bỏ qua người nhận vai trò Cc khi kích hoạt lượt
        ContractSigner? first = _signers
            .Where(s => s.Role != RecipientRole.Cc)
            .OrderBy(s => s.SignOrder)
            .FirstOrDefault();

        first?.ActivateTurn();

        return Result.Success();
    }

    /// <summary>
    /// Ký hoặc duyệt lượt hiện tại, chuyển sang người tiếp theo (bỏ qua Cc).
    /// Nếu là người cuối cùng trong luồng ký/duyệt -> tự động chuyển trạng thái hoàn tất (Completed).
    /// </summary>
    public Result SignOrApproveCurrent(Guid signerId, DateTimeOffset now)
    {
        if (Status is not (ContractStatus.Pending or ContractStatus.Signing))
        {
            return ContractErrors.ContractNotEditable;
        }

        ContractSigner? current = _signers.FirstOrDefault(s => s.Id == signerId);
        if (current is null)
        {
            return ContractErrors.SignerNotFound;
        }

        if (current.Status != SignerStatus.Current)
        {
            return ContractErrors.NotSignerTurn;
        }

        if (current.Role == RecipientRole.Cc)
        {
            return ContractErrors.CcCannotSignOrApprove;
        }

        current.MarkSigned(now);

        // Tìm người tiếp theo trong luồng ký/duyệt (bỏ qua Cc)
        ContractSigner? next = _signers
            .Where(s => s.Role != RecipientRole.Cc && s.SignOrder > current.SignOrder && s.Status == SignerStatus.Waiting)
            .OrderBy(s => s.SignOrder)
            .FirstOrDefault();

        if (next is not null)
        {
            Status = ContractStatus.Signing;
            next.ActivateTurn();
        }
        else
        {
            Status = ContractStatus.Completed;
            CompletedAt = now;
        }

        return Result.Success();
    }

    /// <summary>
    /// Người ký hoặc duyệt từ chối -> hợp đồng chuyển sang Declined.
    /// </summary>
    public Result DeclineCurrent(Guid signerId, string reason, DateTimeOffset now)
    {
        if (Status is not (ContractStatus.Pending or ContractStatus.Signing))
        {
            return ContractErrors.ContractCannotCancel;
        }

        ContractSigner? current = _signers.FirstOrDefault(s => s.Id == signerId);
        if (current is null)
        {
            return ContractErrors.SignerNotFound;
        }

        if (current.Status != SignerStatus.Current)
        {
            return ContractErrors.NotSignerTurn;
        }

        if (current.Role == RecipientRole.Cc)
        {
            return ContractErrors.CcCannotSignOrApprove;
        }

        current.MarkDeclined(reason, now);
        Status = ContractStatus.Declined;
        return Result.Success();
    }

    /// <summary>
    /// Hủy hợp đồng đang Chờ ký hoặc Đang ký (F09, TC08).
    /// </summary>
    public Result Cancel(DateTimeOffset now)
    {
        // Bản nháp chưa gửi ai và chưa trừ hạn mức nên huỷ thẳng được.
        if (Status is not (ContractStatus.Draft or ContractStatus.Pending or ContractStatus.Signing))
        {
            return ContractErrors.ContractCannotCancel;
        }

        Status = ContractStatus.Cancelled;
        CancelledAt = now;
        return Result.Success();
    }

    public bool MarkExpiredIfDue(DateTimeOffset now)
    {
        if (Status is not (ContractStatus.Pending or ContractStatus.Signing) ||
            !DueAt.HasValue ||
            now < DueAt.Value)
        {
            return false;
        }

        Status = ContractStatus.Expired;
        return true;
    }
}
