using System.Net;
using Microsoft.Extensions.Logging;
using TrustSign.Application.Modules.Audit;
using TrustSign.Application.Modules.Media;
using TrustSign.Application.Modules.Notifications;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Identity;

internal sealed class TenantVerificationService(
    ITenantRepository tenants,
    IUserRepository users,
    IMediaStorage storage,
    IMediaCipher cipher,
    IEmailSender emailSender,
    IAuditLogger audit,
    TimeProvider timeProvider,
    ILogger<TenantVerificationService> logger) : ITenantVerificationService, ITenantVerifiedChecker
{
    private sealed record RejectPayload(string? Reason);

    public async Task<bool> IsVerifiedAsync(Guid tenantId, CancellationToken cancel) =>
        (await tenants.FindByIdAsync(tenantId, cancel))?.VerificationStatus == TenantVerificationStatus.Verified;

    public async Task<Result<TenantDto>> SubmitAsync(Guid tenantId, Guid actorUserId, byte[] license, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return IdentityErrors.TenantNotFound;
        }

        Membership? membership = await tenants.FindMembershipAsync(tenantId, actorUserId, cancel);
        if (membership is null || membership.Role != TenantRole.TenantOwner)
        {
            return IdentityErrors.TenantAccessDenied;
        }

        if (!IsLicense(license))
        {
            return TenantVerificationErrors.LicenseInvalid;
        }

        Result submitted = tenant.SubmitVerification(timeProvider.GetUtcNow());
        if (submitted.IsFailure)
        {
            return submitted.Error;
        }

        // Kiểm tra sau SubmitVerification để lỗi "thiếu hồ sơ" luôn được báo trước lỗi "trùng mã số thuế".
        if (await tenants.TaxCodeInUseAsync(tenant.TaxCode!, tenant.Id, cancel))
        {
            return TenantVerificationErrors.TaxCodeTaken;
        }

        // Giấy tờ pháp lý nhạy cảm như ảnh CCCD: lưu mã hóa, chỉ quản trị viên nền tảng đọc được.
        await storage.SaveAsync(tenant.BusinessLicenseKey!, cipher.Encrypt(license), cancel);
        await audit.LogAsync(tenant.Id, actorUserId, "Tenant.VerificationSubmitted", "Tenant", tenant.Id.ToString(), cancel: cancel);
        await tenants.SaveChangesAsync(cancel);

        return ToDto(tenant, membership.Role);
    }

    public async Task<Result> ApproveAsync(Guid adminUserId, Guid tenantId, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return AdminErrors.TenantNotFound;
        }

        Result approved = tenant.ApproveVerification(timeProvider.GetUtcNow());
        if (approved.IsFailure)
        {
            return approved.Error;
        }

        await audit.LogAsync(tenant.Id, adminUserId, "Admin.TenantVerified", "Tenant", tenant.Id.ToString(), cancel: cancel);
        await tenants.SaveChangesAsync(cancel);
        await NotifyOwnerAsync(
            tenant,
            "Doanh nghiệp của bạn đã được xác thực",
            $"Doanh nghiệp <strong>{WebUtility.HtmlEncode(tenant.Name)}</strong> đã được xác thực. Từ bây giờ bạn có thể gửi hợp đồng đi ký.",
            cancel);
        return Result.Success();
    }

    public async Task<Result> RejectAsync(Guid adminUserId, Guid tenantId, string? reason, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return AdminErrors.TenantNotFound;
        }

        Result rejected = tenant.RejectVerification(reason);
        if (rejected.IsFailure)
        {
            return rejected.Error;
        }

        await audit.LogAsync(
            tenant.Id, adminUserId, "Admin.TenantVerificationRejected", "Tenant", tenant.Id.ToString(),
            new RejectPayload(tenant.VerificationRejectReason), cancel);
        await tenants.SaveChangesAsync(cancel);
        await NotifyOwnerAsync(
            tenant,
            "Hồ sơ xác thực doanh nghiệp chưa được duyệt",
            $"Hồ sơ của <strong>{WebUtility.HtmlEncode(tenant.Name)}</strong> chưa được duyệt. Lý do: {WebUtility.HtmlEncode(tenant.VerificationRejectReason)}. " +
            "Bạn có thể chỉnh thông tin và nộp lại tại Cài đặt &gt; Doanh nghiệp.",
            cancel);
        return Result.Success();
    }

    public async Task<Result<byte[]>> GetLicenseAsync(Guid tenantId, CancellationToken cancel)
    {
        Tenant? tenant = await tenants.FindByIdAsync(tenantId, cancel);
        if (tenant is null)
        {
            return AdminErrors.TenantNotFound;
        }

        byte[]? sealedBytes = tenant.BusinessLicenseKey is null ? null : await storage.ReadAsync(tenant.BusinessLicenseKey, cancel);
        return sealedBytes is null ? TenantVerificationErrors.LicenseNotFound : cipher.Decrypt(sealedBytes);
    }

    private async Task NotifyOwnerAsync(Tenant tenant, string subject, string bodyHtml, CancellationToken cancel)
    {
        try
        {
            User? owner = await users.FindByIdAsync(tenant.CreatedByUserId, cancel);
            if (owner is null)
            {
                return;
            }

            string html = $"""
                <div style="font-family:'Segoe UI',Roboto,Arial,sans-serif;max-width:560px;margin:0 auto;color:#1f2328;line-height:1.6;">
                  <h2 style="color:#1b3a5c;margin-bottom:12px;">{WebUtility.HtmlEncode(subject)}</h2>
                  <p>Xin chào <strong>{WebUtility.HtmlEncode(owner.FullName)}</strong>,</p>
                  <p>{bodyHtml}</p>
                </div>
                """;
            await emailSender.SendAsync(new EmailMessage(owner.Email, $"[Chữ Tín] {subject}", html), cancel);
        }
        catch (Exception ex)
        {
            // Thông báo hỏng không được làm hỏng quyết định đã ghi.
            logger.LogWarning(ex, "Không gửi được email kết quả xác thực doanh nghiệp {TenantId}", tenant.Id);
        }
    }

    private static bool IsLicense(byte[] bytes) =>
        bytes.Length is > 8 and <= TenantVerificationErrors.MaxLicenseBytes &&
        MediaContent.Detect(bytes) is MediaType.Pdf or MediaType.Jpeg or MediaType.Png;

    private static TenantDto ToDto(Tenant tenant, TenantRole role) => new(
        tenant.Id,
        tenant.Name,
        tenant.TaxCode,
        tenant.Address,
        tenant.ContactEmail,
        tenant.Status.ToString(),
        role.ToString(),
        tenant.CreatedAt,
        tenant.RepresentativeName,
        tenant.RepresentativeTitle,
        tenant.Phone,
        tenant.VerificationStatus.ToString(),
        tenant.VerificationRejectReason);
}
