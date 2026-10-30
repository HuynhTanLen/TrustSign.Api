using TrustSign.Common.Enums;

namespace TrustSign.Domain.Entities.Contracts;

/// <summary>
/// Người ký hợp đồng theo thứ tự tuần tự 1..5 (F03, F04) kèm vị trí ô chữ ký trên trang PDF.
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

    public SignerStatus Status { get; private set; }

    /// <summary>Trang PDF đặt ô chữ ký (đánh số từ 1).</summary>
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
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(now),
        ContractId = contractId,
        SignOrder = signOrder,
        FullName = fullName.Trim(),
        Email = email.Trim().ToLowerInvariant(),
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
