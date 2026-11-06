using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Audit;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Api.Tests;

public sealed class EkycTests
{
    private const string Qr = "079099012345|123456789|Nguyễn Văn An|15031999|Nam|12 Lê Lợi, Quận 1, TP. Hồ Chí Minh|01072021";
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3, 4, 5, 6];

    private readonly InMemoryVerifications _repo = new();
    private readonly InMemoryStorage _storage = new();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 5, 8, 0, 0, TimeSpan.Zero));
    private readonly InMemoryUsers _users = new();
    private readonly IdentityVerificationService _service;

    public EkycTests()
    {
        _service = new IdentityVerificationService(
            _repo,
            _users,
            _storage,
            new NoOpAuditLogger(),
            Options.Create(new ContractStorageOptions()),
            _clock);
    }

    [Fact]
    public async Task Handoff_link_submits_for_its_owner_and_expires()
    {
        User owner = User.Register("chu.tk@trustsign.vn", "Chủ Tài Khoản", "hash", _clock.GetUtcNow());
        _users.Items.Add(owner);
        string token = (await _service.CreateHandoffAsync(owner.Id, CancellationToken.None)).Value.Token;

        EkycHandoffInfoDto info = (await _service.GetHandoffAsync(token, CancellationToken.None)).Value;
        Assert.Equal("c***@trustsign.vn", info.MaskedEmail);
        Assert.Equal("None", info.Status);

        // Sửa một ký tự userId là chữ ký HMAC không còn khớp.
        string forged = (token[0] == 'a' ? 'b' : 'a') + token[1..];
        Assert.Equal(IdentityErrors.EkycHandoffInvalid, (await _service.GetHandoffAsync(forged, CancellationToken.None)).Error);

        Assert.True((await _service.SubmitByHandoffAsync(token, Command(0.4), CancellationToken.None)).IsSuccess);
        Assert.Equal(owner.Id, _repo.Items[0].UserId);
        Assert.Equal(IdentityErrors.EkycAlreadyVerified, (await _service.CreateHandoffAsync(owner.Id, CancellationToken.None)).Error);

        _clock.Current += IdentityVerificationService.HandoffLifetime + TimeSpan.FromSeconds(1);
        Assert.Equal(IdentityErrors.EkycHandoffInvalid, (await _service.GetHandoffAsync(token, CancellationToken.None)).Error);
    }

    [Fact]
    public void Qr_parses_chip_card_format()
    {
        CitizenQr? qr = CitizenQr.TryParse(Qr);

        Assert.NotNull(qr);
        Assert.Equal("079099012345", qr.CitizenId);
        Assert.Equal("Nguyễn Văn An", qr.FullName);
        Assert.Equal(new DateOnly(1999, 3, 15), qr.DateOfBirth);
        Assert.Equal(new DateOnly(2021, 7, 1), qr.IssuedOn);
        Assert.Equal("079*******45", CitizenQr.Mask(qr.CitizenId));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.com")]
    [InlineData("07909901234|x|Tên|15031999|Nam|Địa chỉ")]
    [InlineData("079099012345|x|Tên|31021999|Nam|Địa chỉ")]
    public void Qr_rejects_bad_input(string raw) => Assert.Null(CitizenQr.TryParse(raw));

    [Fact]
    public async Task Submit_stores_encrypted_images_and_blocks_reuse_of_citizen_id()
    {
        Guid first = Guid.NewGuid();
        Result<MyIdentityVerificationDto> ok = await _service.SubmitAsync(first, Command(0.42), CancellationToken.None);

        Assert.True(ok.IsSuccess);
        Assert.Equal("Verified", ok.Value.Status);
        Assert.All(_storage.Files.Values, stored => Assert.NotEqual(Jpeg, stored));

        Result<byte[]> image = await _service.GetImageAsync(_repo.Items[0].Id, EkycImageKind.Front, CancellationToken.None);
        Assert.Equal(Jpeg, image.Value);

        Assert.Equal(IdentityErrors.EkycAlreadyVerified, (await _service.SubmitAsync(first, Command(0.42), CancellationToken.None)).Error);
        Assert.Equal(IdentityErrors.EkycCitizenIdTaken, (await _service.SubmitAsync(Guid.NewGuid(), Command(0.42), CancellationToken.None)).Error);
    }

    [Fact]
    public async Task Submit_rejects_face_mismatch_and_allows_retry_after_revoke()
    {
        Guid user = Guid.NewGuid();
        Assert.Equal(IdentityErrors.EkycFaceMismatch, (await _service.SubmitAsync(user, Command(0.75), CancellationToken.None)).Error);
        Assert.Equal(IdentityErrors.EkycFaceMismatch, (await _service.SubmitAsync(user, Command(double.NaN), CancellationToken.None)).Error);

        Assert.True((await _service.SubmitAsync(user, Command(0.3), CancellationToken.None)).IsSuccess);
        Assert.True((await _service.RevokeAsync(Guid.NewGuid(), _repo.Items[0].Id, "Ảnh mờ", CancellationToken.None)).IsSuccess);

        MyIdentityVerificationDto mine = await _service.GetMineAsync(user, CancellationToken.None);
        Assert.Equal("Revoked", mine.Status);
        Assert.True((await _service.SubmitAsync(user, Command(0.3), CancellationToken.None)).IsSuccess);
    }

    private static SubmitIdentityVerificationCommand Command(double distance) => new(Qr, distance, Jpeg, Jpeg, Jpeg);

    private sealed class InMemoryVerifications : IIdentityVerificationRepository
    {
        public List<IdentityVerification> Items { get; } = [];

        public Task<IdentityVerification?> FindByIdAsync(Guid id, CancellationToken cancel) =>
            Task.FromResult(Items.FirstOrDefault(v => v.Id == id));

        public Task<IdentityVerification?> FindLatestByUserAsync(Guid userId, CancellationToken cancel) =>
            Task.FromResult(Items.Where(v => v.UserId == userId).OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.Id).FirstOrDefault());

        public Task<bool> IsCitizenIdVerifiedAsync(string citizenId, CancellationToken cancel) =>
            Task.FromResult(Items.Any(v => v.CitizenId == citizenId && v.Status == IdentityVerificationStatus.Verified));

        public Task<IReadOnlyList<(IdentityVerification Verification, string Email, string AccountName)>> ListWithUsersAsync(int take, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyList<(IdentityVerification, string, string)>>(Items.Select(v => (v, "a@b.vn", "A")).ToList());

        public Task AddAsync(IdentityVerification verification, CancellationToken cancel)
        {
            Items.Add(verification);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancel) => Task.CompletedTask;
    }

    private sealed class InMemoryStorage : IContractFileStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public Task SaveAsync(string storageKey, ReadOnlyMemory<byte> pdfBytes, CancellationToken cancel = default)
        {
            Files[storageKey] = pdfBytes.ToArray();
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancel = default) =>
            Task.FromResult(Files.GetValueOrDefault(storageKey));

        public string CreateShortLivedDocumentUrl(Guid tenantId, Guid contractId, DateTimeOffset now) => throw new NotSupportedException();

        public bool ValidateDocumentToken(string token, out Guid tenantId, out Guid contractId, DateTimeOffset now) => throw new NotSupportedException();

        public string CreateSigningToken(Guid contractId, Guid signerId, DateTimeOffset now) => throw new NotSupportedException();

        public bool ValidateSigningToken(string token, out Guid contractId, out Guid signerId, DateTimeOffset now) => throw new NotSupportedException();
    }

    private sealed class FakeClock(DateTimeOffset initial) : TimeProvider
    {
        public DateTimeOffset Current { get; set; } = initial;
        public override DateTimeOffset GetUtcNow() => Current;
    }

    private sealed class InMemoryUsers : IUserRepository
    {
        public List<User> Items { get; } = [];

        public Task<User?> FindByIdAsync(Guid id, CancellationToken cancel) => Task.FromResult(Items.FirstOrDefault(u => u.Id == id));

        public Task<IReadOnlyDictionary<Guid, User>> FindByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancel) =>
            Task.FromResult<IReadOnlyDictionary<Guid, User>>(Items.Where(u => ids.Contains(u.Id)).ToDictionary(u => u.Id));

        public Task<User?> FindByEmailAsync(string email, CancellationToken cancel) => Task.FromResult(Items.FirstOrDefault(u => u.Email == email));

        public Task AddAsync(User user, CancellationToken cancel)
        {
            Items.Add(user);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancel) => Task.CompletedTask;
    }

    private sealed class NoOpAuditLogger : IAuditLogger
    {
        public Task LogAsync(Guid? tenantId, Guid? actorUserId, string action, string resourceType, string resourceId, object? payload = null, CancellationToken cancel = default) =>
            Task.CompletedTask;
    }
}
