using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Media;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Api.Tests;

/// <summary>Xác thực pháp nhân của doanh nghiệp: quy tắc trạng thái và luồng nộp, duyệt, từ chối.</summary>
public sealed class TenantVerificationTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 9, 0, 0, TimeSpan.Zero);
    private static readonly byte[] Pdf = [.. "%PDF-1.7 giay dang ky kinh doanh"u8];

    private static Tenant FullTenant(Guid ownerId) =>
        Tenant.Create("Công ty TNHH A", "0100109106", "1 Lê Lợi, Hà Nội", null, ownerId, Now, "Nguyễn Văn A");

    // ---------- Quy tắc trên thực thể ----------

    [Fact]
    public void New_tenant_is_unverified_and_submit_needs_full_profile()
    {
        Tenant thin = Tenant.Create("Công ty B", null, null, null, Guid.NewGuid(), Now);

        Assert.Equal(TenantVerificationStatus.Unverified, thin.VerificationStatus);
        Assert.Equal(TenantVerificationErrors.ProfileIncomplete, thin.SubmitVerification(Now).Error);
        Assert.Equal(TenantVerificationStatus.Unverified, thin.VerificationStatus);
    }

    [Fact]
    public void Pending_or_verified_tenant_cannot_submit_again()
    {
        Tenant tenant = FullTenant(Guid.NewGuid());

        Assert.True(tenant.SubmitVerification(Now).IsSuccess);
        Assert.Equal(TenantVerificationErrors.AlreadySubmitted, tenant.SubmitVerification(Now).Error);

        Assert.True(tenant.ApproveVerification(Now).IsSuccess);
        Assert.Equal(TenantVerificationErrors.AlreadySubmitted, tenant.SubmitVerification(Now).Error);
    }

    [Fact]
    public void Approve_and_reject_only_work_on_a_pending_request()
    {
        Tenant tenant = FullTenant(Guid.NewGuid());

        Assert.Equal(TenantVerificationErrors.NotPending, tenant.ApproveVerification(Now).Error);
        Assert.Equal(TenantVerificationErrors.NotPending, tenant.RejectVerification("Thiếu con dấu").Error);

        tenant.SubmitVerification(Now);
        Assert.Equal(TenantVerificationErrors.RejectReasonRequired, tenant.RejectVerification("   ").Error);
        Assert.True(tenant.RejectVerification("  Thiếu con dấu  ").IsSuccess);
        Assert.Equal(TenantVerificationStatus.Rejected, tenant.VerificationStatus);
        Assert.Equal("Thiếu con dấu", tenant.VerificationRejectReason);
    }

    [Fact]
    public void Rejected_tenant_can_resubmit_and_the_reason_is_cleared()
    {
        Tenant tenant = FullTenant(Guid.NewGuid());
        tenant.SubmitVerification(Now);
        tenant.RejectVerification("Ảnh mờ");

        Assert.True(tenant.SubmitVerification(Now.AddDays(1)).IsSuccess);

        Assert.Equal(TenantVerificationStatus.Pending, tenant.VerificationStatus);
        Assert.Null(tenant.VerificationRejectReason);
    }

    [Fact]
    public void Changing_name_or_tax_code_drops_verification_but_changing_phone_does_not()
    {
        Tenant tenant = FullTenant(Guid.NewGuid());
        tenant.SubmitVerification(Now);
        tenant.ApproveVerification(Now);

        tenant.Update(tenant.Name, tenant.TaxCode, tenant.Address, null, "0912345678", tenant.RepresentativeName, null);
        Assert.Equal(TenantVerificationStatus.Verified, tenant.VerificationStatus);

        tenant.Update(tenant.Name, "0100109106-001", tenant.Address, null, "0912345678", tenant.RepresentativeName, null);
        Assert.Equal(TenantVerificationStatus.Unverified, tenant.VerificationStatus);
        Assert.Null(tenant.VerifiedAt);
    }

    // ---------- Luồng dịch vụ ----------

    [Fact]
    public async Task Owner_submits_license_which_is_stored_encrypted_and_admin_reads_it_back()
    {
        Fixture f = new();

        Result<TenantDto> result = await f.Service.SubmitAsync(f.Tenant.Id, f.OwnerId, Pdf, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("Pending", result.Value.VerificationStatus);
        byte[] stored = f.Storage.Files[Tenant.BusinessLicenseKeyFor(f.Tenant.Id)];
        Assert.False(stored.AsSpan().SequenceEqual(Pdf));
        Result<byte[]> read = await f.Service.GetLicenseAsync(f.Tenant.Id, CancellationToken.None);
        Assert.Equal(Pdf, read.Value);
    }

    [Fact]
    public async Task Non_owner_and_bad_files_are_refused_without_changing_state()
    {
        Fixture f = new();

        Result<TenantDto> notOwner = await f.Service.SubmitAsync(f.Tenant.Id, Guid.NewGuid(), Pdf, CancellationToken.None);
        Result<TenantDto> textFile = await f.Service.SubmitAsync(f.Tenant.Id, f.OwnerId, [.. "chi la van ban thuong"u8], CancellationToken.None);
        Result<TenantDto> tooBig = await f.Service.SubmitAsync(
            f.Tenant.Id, f.OwnerId, [.. Pdf, .. new byte[TenantVerificationErrors.MaxLicenseBytes]], CancellationToken.None);

        Assert.Equal(IdentityErrors.TenantAccessDenied, notOwner.Error);
        Assert.Equal(TenantVerificationErrors.LicenseInvalid, textFile.Error);
        Assert.Equal(TenantVerificationErrors.LicenseInvalid, tooBig.Error);
        Assert.Equal(TenantVerificationStatus.Unverified, f.Tenant.VerificationStatus);
        Assert.Empty(f.Storage.Files);
    }

    [Fact]
    public async Task Tax_code_already_taken_by_another_tenant_blocks_submission()
    {
        Fixture f = new();
        f.TaxCodeTaken = true;

        Result<TenantDto> result = await f.Service.SubmitAsync(f.Tenant.Id, f.OwnerId, Pdf, CancellationToken.None);

        Assert.Equal(TenantVerificationErrors.TaxCodeTaken, result.Error);
    }

    [Fact]
    public async Task Approve_verifies_the_tenant_and_emails_the_owner_and_reject_does_the_same_with_reason()
    {
        Fixture f = new();
        await f.Service.SubmitAsync(f.Tenant.Id, f.OwnerId, Pdf, CancellationToken.None);

        Assert.True((await f.Service.ApproveAsync(Guid.NewGuid(), f.Tenant.Id, CancellationToken.None)).IsSuccess);
        Assert.True(await f.Service.IsVerifiedAsync(f.Tenant.Id, CancellationToken.None));
        Assert.Contains("đã được xác thực", f.Emails.Sent.Single().Subject);

        Fixture g = new();
        await g.Service.SubmitAsync(g.Tenant.Id, g.OwnerId, Pdf, CancellationToken.None);
        Assert.True((await g.Service.RejectAsync(Guid.NewGuid(), g.Tenant.Id, "Thiếu con dấu", CancellationToken.None)).IsSuccess);
        Assert.False(await g.Service.IsVerifiedAsync(g.Tenant.Id, CancellationToken.None));
        Assert.Contains("Thiếu con dấu", g.Emails.Sent.Single().HtmlBody);
    }

    // ---------- Dựng đối tượng giả ----------

    private sealed class Fixture
    {
        public Guid OwnerId { get; } = Guid.NewGuid();

        public Tenant Tenant { get; }

        public bool TaxCodeTaken { get; set; }

        public MemoryStorage Storage { get; } = new();

        public RecordingEmails Emails { get; } = new();

        public TenantVerificationService Service { get; }

        public Fixture()
        {
            Tenant = FullTenant(OwnerId);
            Membership owner = Membership.Create(Tenant.Id, OwnerId, TenantRole.TenantOwner, Now);
            User ownerUser = User.Register("chu@a.vn", "Nguyễn Văn A", "hash", Now);

            ITenantRepository tenants = Stub.Create<ITenantRepository>(new()
            {
                [nameof(ITenantRepository.FindByIdAsync)] = _ => Task.FromResult<Tenant?>(Tenant),
                [nameof(ITenantRepository.FindMembershipAsync)] = a =>
                    Task.FromResult<Membership?>((Guid)a[1]! == OwnerId ? owner : null),
                [nameof(ITenantRepository.TaxCodeInUseAsync)] = _ => Task.FromResult(TaxCodeTaken),
            });
            IUserRepository users = Stub.Create<IUserRepository>(new()
            {
                [nameof(IUserRepository.FindByIdAsync)] = _ => Task.FromResult<User?>(ownerUser),
            });

            Service = new TenantVerificationService(
                tenants,
                users,
                Storage,
                new AesMediaCipher(Options.Create(new MediaOptions())),
                Emails,
                new InMemoryAuditLogger(),
                new FixedClock(Now),
                NullLogger<TenantVerificationService>.Instance);
        }
    }

    private sealed class MemoryStorage : IMediaStorage
    {
        public Dictionary<string, byte[]> Files { get; } = [];

        public Task SaveAsync(string storageKey, ReadOnlyMemory<byte> bytes, CancellationToken cancel = default)
        {
            Files[storageKey] = bytes.ToArray();
            return Task.CompletedTask;
        }

        public Task<byte[]?> ReadAsync(string storageKey, CancellationToken cancel = default) =>
            Task.FromResult(Files.GetValueOrDefault(storageKey));

        public Task DeleteAsync(string storageKey, CancellationToken cancel = default)
        {
            Files.Remove(storageKey);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmails : IEmailSender
    {
        public List<EmailMessage> Sent { get; } = [];

        public Task SendAsync(EmailMessage message, CancellationToken cancel)
        {
            Sent.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>Đối tượng giả cho interface lớn: chỉ viết các hàm test dùng, hàm còn lại trả giá trị mặc định.</summary>
    private class Stub : DispatchProxy
    {
        private Dictionary<string, Func<object?[], object?>> _handlers = [];

        public static T Create<T>(Dictionary<string, Func<object?[], object?>> handlers) where T : class
        {
            T proxy = DispatchProxy.Create<T, Stub>();
            ((Stub)(object)proxy)._handlers = handlers;
            return proxy;
        }

        protected override object? Invoke(MethodInfo? method, object?[]? args)
        {
            if (_handlers.TryGetValue(method!.Name, out Func<object?[], object?>? handler))
            {
                return handler(args ?? []);
            }

            Type returnType = method.ReturnType;
            if (returnType == typeof(Task))
            {
                return Task.CompletedTask;
            }

            if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
            {
                Type inner = returnType.GetGenericArguments()[0];
                object? value = inner.IsValueType ? Activator.CreateInstance(inner) : null;
                return typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(inner).Invoke(null, [value]);
            }

            return returnType.IsValueType && returnType != typeof(void) ? Activator.CreateInstance(returnType) : null;
        }
    }
}
