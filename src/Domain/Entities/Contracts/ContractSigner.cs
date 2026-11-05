using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Contracts;

/// <summary>
/// Người nhận trong quy trình hợp đồng (Signer, Approver, Cc) theo thứ tự tuần tự (F03, F04)
/// kèm vị trí ô chữ ký trên trang PDF (đối với Signer).
/// </summary>
public sealed class ContractSigner
{
    private ContractSigner()
    {
    }

    public Guid Id { get; private set; }

    public Guid ContractId { get; private set; }

    public int SignOrder { get; private set; }

    public string FullName { get; private set; } = null!;

    public string Email { get; private set; } = null!;

    public RecipientRole Role { get; private set; }

    public string? Phone { get; private set; }

    public string? Organization { get; private set; }

    public string? Title { get; private set; }

    public SignerStatus Status { get; private set; }

    /// <summary>Trang PDF đặt ô chữ ký (đánh số từ 1). Với Approver hoặc Cc có thể là 1 hoặc mặc định.</summary>
    public int PageNumber { get; private set; }

    /// <summary>Tọa độ X (pt, gốc trái-dưới chuẩn PDF) của ô chữ ký.</summary>
    public float BoxX { get; private set; }

    /// <summary>Tọa độ Y (pt, gốc trái-dưới chuẩn PDF) của ô chữ ký.</summary>
    public float BoxY { get; private set; }

    public float BoxWidth { get; private set; }

    public float BoxHeight { get; private set; }

    public DateTimeOffset? SignedAt { get; private set; }

    public string? DeclineReason { get; private set; }

    public static ContractSigner Create(
        Guid contractId,
        int signOrder,
        string fullName,
        string email,
        int pageNumber,
        float boxX,
        float boxY,
        float boxWidth,
        float boxHeight,
        DateTimeOffset now,
        RecipientRole role = RecipientRole.Signer,
        string? phone = null,
        string? organization = null,
        string? title = null) => new()
    {
        Id = Guid.CreateVersion7(now),
        ContractId = contractId,
        SignOrder = signOrder,
        FullName = fullName.Trim(),
        Email = email.Trim().ToLowerInvariant(),
        Role = role,
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
        Organization = string.IsNullOrWhiteSpace(organization) ? null : organization.Trim(),
        Title = string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
        Status = SignerStatus.Waiting,
        PageNumber = pageNumber,
        BoxX = boxX,
        BoxY = boxY,
        BoxWidth = boxWidth,
        BoxHeight = boxHeight
    };

    public void ActivateTurn() => Status = SignerStatus.Current;

    public void MarkSigned(DateTimeOffset now)
    {
        Status = SignerStatus.Signed;
        SignedAt = now;
    }

    public void MarkDeclined(string reason, DateTimeOffset now)
    {
        Status = SignerStatus.Declined;
        DeclineReason = reason.Trim();
        SignedAt = now;
    }
}
