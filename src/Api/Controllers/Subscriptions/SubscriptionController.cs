using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/subscription")]
public sealed class SubscriptionController(
    IPlanService planService,
    ISubscriptionService subscriptionService,
    IInvoicePdfService pdfService) : ControllerBase
{
    [HttpGet("plans")]
    public async Task<IActionResult> GetPlans(CancellationToken cancel)
    {
        Result<IReadOnlyList<PlanDto>> result = await planService.GetPlansAsync(includeInactive: false, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent(CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<CurrentSubscriptionDto> result = await subscriptionService.GetCurrentSubscriptionAsync(tenantId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("invoices")]
    public async Task<IActionResult> GetInvoices(CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<IReadOnlyList<InvoiceDto>> result = await subscriptionService.GetInvoicesAsync(tenantId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpPost("invoices")]
    public async Task<IActionResult> CreateInvoice([FromBody] CreateInvoiceRequestDto request, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<CheckoutDto> result = await subscriptionService.CreateInvoiceAsync(tenantId, userId, request, cancel);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("invoices/{code}")]
    public async Task<IActionResult> GetInvoiceStatus(string code, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<InvoiceStatusDto> result = await subscriptionService.GetInvoiceStatusAsync(tenantId, code, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize]
    [HttpGet("invoices/{code}/pdf")]
    public async Task<IActionResult> DownloadInvoicePdf(string code, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<byte[]> result = await pdfService.GenerateInvoiceReceiptPdfAsync(code, tenantId, cancel);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        return File(result.Value, "application/pdf", $"TrustSign-Invoice-{code.Trim().ToUpperInvariant()}.pdf");
    }
}
