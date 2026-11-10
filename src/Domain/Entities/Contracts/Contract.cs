using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Common.Validation;

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

        if (!specs.Any(s => HasSignatureBox(s.Role)))
        {
            return ContractErrors.AtLeastOneSignerRequired;
        }

        List<ContractSigner> built = new(specs.Count);
        for (int i = 0; i < specs.Count; i++)
        {
            ContractSignerSpec spec = specs[i];
            string fullName = spec.FullName?.Trim() ?? string.Empty;
            string email = spec.Email?.Trim() ?? string.Empty;

            int order = i + 1;
            if (ValidateIdentity(fullName, email, order) is { } identityError)
            {
                return identityError;
            }

            int page = spec.PageNumber ?? 1;

            // Tọa độ mặc định xếp ngang/dọc ở cuối trang A4 (595 x 842 pt) nếu chưa chỉ định
            float defaultX = 55f + ((i % 2) * 260f);
            float defaultY = 140f - ((i / 2) * 65f);
            float boxX = spec.BoxX ?? defaultX;
            float boxY = spec.BoxY ?? defaultY;
            float boxW = spec.BoxWidth ?? 180f;
            float boxH = spec.BoxHeight ?? 55f;

            // Người điều phối giữ sẵn ô ký cho người ký bên đối tác mà họ sẽ chỉ định.
            if (HasSignatureBox(spec.Role))
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

        if (!_signers.Any(s => HasSignatureBox(s.Role)))
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
    public Result SignOrApproveCurrent(Guid signerId, DateTimeOffset now, string? signatureStorageKey = null)
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

        if (current.Role == RecipientRole.Coordinator)
        {
            return ContractErrors.CoordinatorMustCoordinate;
        }

        current.MarkSigned(now, signatureStorageKey);
        AdvanceAfter(current, now);
        return Result.Success();
    }

    /// <summary>
    /// Người điều phối (đang tới lượt) khai báo người duyệt/ký của bên mình. Những người này được chèn ngay sau
    /// lượt điều phối theo thứ tự khai báo, các lượt phía sau lùi xuống; người ký nhận ô ký bên tạo đã đặt cho người điều phối.
    /// </summary>
    public Result Coordinate(Guid coordinatorId, IReadOnlyList<ContractSignerSpec> recipients, DateTimeOffset now)
    {
        if (Status is not (ContractStatus.Pending or ContractStatus.Signing))
        {
            return ContractErrors.ContractNotEditable;
        }

        ContractSigner? coordinator = _signers.FirstOrDefault(s => s.Id == coordinatorId);
        if (coordinator is null)
        {
            return ContractErrors.SignerNotFound;
        }

        if (coordinator.Role != RecipientRole.Coordinator)
        {
            return ContractErrors.NotCoordinator;
        }

        if (coordinator.Status != SignerStatus.Current)
        {
            return ContractErrors.NotSignerTurn;
        }

        if (recipients is null ||
            recipients.Count == 0 ||
            _signers.Count + recipients.Count > MaxSigners ||
            recipients.Count(r => r.Role == RecipientRole.Signer) != 1 ||
            recipients.Any(r => r.Role == RecipientRole.Coordinator))
        {
            return ContractErrors.InvalidCoordination;
        }

        int baseOrder = coordinator.SignOrder;
        List<ContractSigner> added = new(recipients.Count);
        for (int i = 0; i < recipients.Count; i++)
        {
            ContractSignerSpec spec = recipients[i];
            string fullName = spec.FullName?.Trim() ?? string.Empty;
            string email = spec.Email?.Trim() ?? string.Empty;
            int order = baseOrder + i + 1;
            if (ValidateIdentity(fullName, email, order) is { } identityError)
            {
                return identityError;
            }

            bool signs = spec.Role == RecipientRole.Signer;
            added.Add(ContractSigner.Create(
                Id,
                order,
                fullName,
                email,
                signs ? coordinator.PageNumber : 1,
                signs ? coordinator.BoxX : 0,
                signs ? coordinator.BoxY : 0,
                signs ? coordinator.BoxWidth : 0,
                signs ? coordinator.BoxHeight : 0,
                now,
                spec.Role,
                spec.Phone,
                spec.Organization,
                spec.Title));
        }

        foreach (ContractSigner later in _signers.Where(s => s.SignOrder > baseOrder))
        {
            later.ShiftOrder(added.Count);
        }

        _signers.AddRange(added);
        coordinator.MarkSigned(now);
        AdvanceAfter(coordinator, now);
        return Result.Success();
    }

    private void AdvanceAfter(ContractSigner current, DateTimeOffset now)
    {
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
    }

    private static bool HasSignatureBox(RecipientRole role) =>
        role is RecipientRole.Signer or RecipientRole.Coordinator;

    private static Error? ValidateIdentity(string fullName, string email, int order)
    {
        if (fullName.Length is 0 or > 100)
        {
            return ContractErrors.InvalidSignerName(order);
        }

        return EmailValidator.IsValid(email) ? null : ContractErrors.InvalidSignerEmail(order);
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
