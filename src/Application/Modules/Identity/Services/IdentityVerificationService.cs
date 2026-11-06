using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Audit;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

/// <summary>
/// eKYC (F28): trình duyệt đọc mã QR trên CCCD và so khớp khuôn mặt bằng model mở, máy chủ kiểm tra lại dữ liệu QR,
/// chặn trùng số CCCD và lưu ba ảnh đã mã hoá AES-256-GCM.
/// </summary>
internal sealed class IdentityVerificationService : IIdentityVerificationService
{
    private const int NonceSize = 12;
    private const int TagSize = 16;

    private readonly IIdentityVerificationRepository _verifications;
    // Dùng lại kho file của hợp đồng (cùng thư mục gốc, khác thư mục con "ekyc/") thay vì dựng thêm một kho riêng.
    private readonly IContractFileStorage _storage;
    private readonly IAuditLogger _audit;
    private readonly TimeProvider _time;
    private readonly byte[] _imageKey;
    private readonly byte[] _handoffKey;
    private readonly IUserRepository _users;

    public static readonly TimeSpan HandoffLifetime = TimeSpan.FromMinutes(15);

    public IdentityVerificationService(
        IIdentityVerificationRepository verifications,
        IUserRepository users,
        IContractFileStorage storage,
        IAuditLogger audit,
        IOptions<ContractStorageOptions> storageOptions,
        TimeProvider time)
    {
        _verifications = verifications;
        _users = users;
        _storage = storage;
        _audit = audit;
        _time = time;
        // Khoá ảnh suy ra từ bí mật lưu trữ sẵn có, nên không phải thêm cấu hình mới; đổi bí mật đó là mất khả năng đọc ảnh cũ.
        _imageKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(storageOptions.Value.DocumentTokenSecret),
            32,
            info: "trustsign-ekyc-images-v1"u8.ToArray());
        // Khoá riêng cho link chuyển sang điện thoại, để token ký hợp đồng không bao giờ dùng thay được.
        _handoffKey = HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            Encoding.UTF8.GetBytes(storageOptions.Value.DocumentTokenSecret),
            32,
            info: "trustsign-ekyc-handoff-v1"u8.ToArray());
    }

    public async Task<Result<EkycHandoffDto>> CreateHandoffAsync(Guid userId, CancellationToken cancel)
    {
        if ((await GetMineAsync(userId, cancel)).Status == nameof(IdentityVerificationStatus.Verified))
        {
            return IdentityErrors.EkycAlreadyVerified;
        }

        DateTimeOffset expiresAt = _time.GetUtcNow() + HandoffLifetime;
        string payload = $"{userId:N}.{expiresAt.ToUnixTimeSeconds()}";
        return new EkycHandoffDto($"{payload}.{SignHandoff(payload)}", expiresAt);
    }

    public async Task<Result<EkycHandoffInfoDto>> GetHandoffAsync(string token, CancellationToken cancel)
    {
        if (ReadHandoff(token) is not var (userId, expiresAt) || await _users.FindByIdAsync(userId, cancel) is not { } user)
        {
            return IdentityErrors.EkycHandoffInvalid;
        }

        MyIdentityVerificationDto mine = await GetMineAsync(userId, cancel);
        return new EkycHandoffInfoDto(MaskEmail(user.Email), mine.Status, expiresAt);
    }

    public Task<Result<MyIdentityVerificationDto>> SubmitByHandoffAsync(
        string token,
        SubmitIdentityVerificationCommand command,
        CancellationToken cancel) =>
        ReadHandoff(token) is var (userId, _)
            ? SubmitAsync(userId, command, cancel)
            : Task.FromResult<Result<MyIdentityVerificationDto>>(IdentityErrors.EkycHandoffInvalid);

    /// <summary>Token dạng <c>userId.exp.hmac</c>; trả null khi sai chữ ký hoặc đã hết hạn.</summary>
    internal (Guid UserId, DateTimeOffset ExpiresAt)? ReadHandoff(string? token)
    {
        string[] parts = (token ?? string.Empty).Split('.');
        if (parts.Length != 3 ||
            !Guid.TryParseExact(parts[0], "N", out Guid userId) ||
            !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long exp) ||
            !CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(parts[2]),
                Encoding.ASCII.GetBytes(SignHandoff($"{parts[0]}.{parts[1]}"))))
        {
            return null;
        }

        DateTimeOffset expiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
        return _time.GetUtcNow() <= expiresAt ? (userId, expiresAt) : null;
    }

    private string SignHandoff(string payload) =>
        Base64Url.EncodeToString(HMACSHA256.HashData(_handoffKey, Encoding.ASCII.GetBytes(payload)));

    private static string MaskEmail(string email)
    {
        int at = email.IndexOf('@');
        return at <= 1 ? email : $"{email[0]}***{email[at..]}";
    }

    public async Task<MyIdentityVerificationDto> GetMineAsync(Guid userId, CancellationToken cancel)
    {
        IdentityVerification? latest = await _verifications.FindLatestByUserAsync(userId, cancel);
        return latest is null ? new MyIdentityVerificationDto("None") : ToMine(latest);
    }

    public async Task<Result<MyIdentityVerificationDto>> SubmitAsync(
        Guid userId,
        SubmitIdentityVerificationCommand command,
        CancellationToken cancel)
    {
        CitizenQr? qr = CitizenQr.TryParse(command.QrText);
        if (qr is null)
        {
            return IdentityErrors.EkycInvalidQr;
        }

        if (!IsImage(command.Selfie) || !IsImage(command.Front) || !IsImage(command.Back))
        {
            return IdentityErrors.EkycInvalidImage;
        }

        // shortcut: điểm so khớp do trình duyệt tính nên máy chủ chỉ kiểm tra ngưỡng; quản trị viên xem ảnh và thu hồi nếu nghi giả mạo.
        // Muốn chặn hẳn thì phải so khớp lại phía máy chủ hoặc gọi dịch vụ eKYC có chống giả mạo (liveness).
        if (!double.IsFinite(command.FaceDistance) || command.FaceDistance < 0 ||
            command.FaceDistance > IdentityVerification.MaxFaceDistance)
        {
            return IdentityErrors.EkycFaceMismatch;
        }

        IdentityVerification? latest = await _verifications.FindLatestByUserAsync(userId, cancel);
        if (latest?.Status == IdentityVerificationStatus.Verified)
        {
            return IdentityErrors.EkycAlreadyVerified;
        }

        if (await _verifications.IsCitizenIdVerifiedAsync(qr.CitizenId, cancel))
        {
            return IdentityErrors.EkycCitizenIdTaken;
        }

        DateTimeOffset now = _time.GetUtcNow();
        IdentityVerification verification = IdentityVerification.Create(
            userId, qr.CitizenId, qr.FullName, qr.DateOfBirth, qr.Gender, qr.Address, qr.IssuedOn,
            command.FaceDistance, now);

        await _storage.SaveAsync(verification.SelfieKey, Encrypt(command.Selfie), cancel);
        await _storage.SaveAsync(verification.FrontKey, Encrypt(command.Front), cancel);
        await _storage.SaveAsync(verification.BackKey, Encrypt(command.Back), cancel);
        await _verifications.AddAsync(verification, cancel);

        // Không ghi số CCCD vào audit: chuỗi hash không xoá được nên chỉ giữ mã hồ sơ.
        await _audit.LogAsync(null, userId, "Ekyc.Verified", "IdentityVerification", verification.Id.ToString(), cancel: cancel);
        return ToMine(verification);
    }

    public async Task<IReadOnlyList<AdminIdentityVerificationDto>> ListAsync(CancellationToken cancel)
    {
        var rows = await _verifications.ListWithUsersAsync(200, cancel);
        return rows.Select(r => new AdminIdentityVerificationDto(
            r.Verification.Id,
            r.Verification.UserId,
            r.Email,
            r.AccountName,
            r.Verification.CitizenId,
            r.Verification.FullName,
            r.Verification.DateOfBirth,
            r.Verification.Gender,
            r.Verification.Address,
            r.Verification.IssuedOn,
            r.Verification.FaceDistance,
            r.Verification.Status.ToString(),
            r.Verification.CreatedAt,
            r.Verification.RevokedAt,
            r.Verification.RevokeReason)).ToList();
    }

    public async Task<Result<byte[]>> GetImageAsync(Guid verificationId, EkycImageKind kind, CancellationToken cancel)
    {
        IdentityVerification? verification = await _verifications.FindByIdAsync(verificationId, cancel);
        if (verification is null)
        {
            return IdentityErrors.EkycNotFound;
        }

        string key = kind switch
        {
            EkycImageKind.Selfie => verification.SelfieKey,
            EkycImageKind.Front => verification.FrontKey,
            _ => verification.BackKey
        };

        byte[]? sealedBytes = await _storage.ReadAsync(key, cancel);
        return sealedBytes is null ? IdentityErrors.EkycNotFound : Decrypt(sealedBytes);
    }

    public async Task<Result> RevokeAsync(Guid adminUserId, Guid verificationId, string? reason, CancellationToken cancel)
    {
        IdentityVerification? verification = await _verifications.FindByIdAsync(verificationId, cancel);
        if (verification is null)
        {
            return IdentityErrors.EkycNotFound;
        }

        Result revoked = verification.Revoke(adminUserId, reason, _time.GetUtcNow());
        if (revoked.IsFailure)
        {
            return revoked;
        }

        await _verifications.SaveChangesAsync(cancel);
        await _audit.LogAsync(null, adminUserId, "Ekyc.Revoked", "IdentityVerification", verification.Id.ToString(), cancel: cancel);
        return Result.Success();
    }

    private static MyIdentityVerificationDto ToMine(IdentityVerification v) => new(
        v.Status.ToString(),
        CitizenQr.Mask(v.CitizenId),
        v.FullName,
        v.DateOfBirth,
        v.CreatedAt,
        v.RevokeReason);

    private static bool IsImage(byte[] bytes) =>
        bytes.Length is > 8 and <= IdentityVerification.MaxImageBytes &&
        ((bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) ||
         (bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47));

    /// <summary>Định dạng lưu: nonce (12) | tag (16) | bản mã.</summary>
    internal byte[] Encrypt(byte[] plain)
    {
        byte[] output = new byte[NonceSize + TagSize + plain.Length];
        Span<byte> nonce = output.AsSpan(0, NonceSize);
        RandomNumberGenerator.Fill(nonce);
        using AesGcm aes = new(_imageKey, TagSize);
        aes.Encrypt(nonce, plain, output.AsSpan(NonceSize + TagSize), output.AsSpan(NonceSize, TagSize));
        return output;
    }

    internal byte[] Decrypt(byte[] sealedBytes)
    {
        byte[] plain = new byte[sealedBytes.Length - NonceSize - TagSize];
        using AesGcm aes = new(_imageKey, TagSize);
        aes.Decrypt(
            sealedBytes.AsSpan(0, NonceSize),
            sealedBytes.AsSpan(NonceSize + TagSize),
            sealedBytes.AsSpan(NonceSize, TagSize),
            plain);
        return plain;
    }
}
