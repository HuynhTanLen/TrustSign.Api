using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;
using TrustSign.Common.Enums;
using TrustSign.Domain.Entities.Contracts;

namespace TrustSign.Api.Controllers.Contracts;

[ApiController]
[Route("api/contracts")]
public sealed class ContractsController(IContractService contractService) : ControllerBase
{
    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? query,
        [FromQuery] string? status,
        CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId ||
            User.GetTenantRole() is not { } role)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<IReadOnlyList<ContractListItemDto>> result =
            await contractService.ListAsync(tenantId, userId, role, query, status, cancel);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId ||
            User.GetTenantRole() is not { } role)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<ContractDetailDto> result =
            await contractService.GetByIdAsync(tenantId, userId, role, id, cancel);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpPost("from-template")]
    public async Task<IActionResult> CreateFromTemplate(
        [FromBody] CreateFromTemplateRequestDto request,
        CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result<CreatedContractResponseDto> result =
            await contractService.CreateFromTemplateAsync(tenantId, userId, request, cancel);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpPost("upload")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<IActionResult> Upload(
        [FromForm] IFormFile? file,
        [FromForm] string? title,
        CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        if (file is null || file.Length is <= 0 or > Contract.MaxPdfSizeBytes)
        {
            return this.ToProblem(ContractErrors.InvalidPdfFile);
        }

        await using MemoryStream ms = new((int)file.Length);
        await file.CopyToAsync(ms, cancel);

        Result<CreatedContractResponseDto> result =
            await contractService.UploadDraftAsync(tenantId, userId, title ?? string.Empty, ms.ToArray(), cancel);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpPut("{id:guid}/signers")]
    public async Task<IActionResult> SetSigners(
        Guid id,
        [FromBody] SetSignersRequestDto request,
        CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId ||
            User.GetTenantRole() is not { } role)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result result = await contractService.SetSignersAsync(tenantId, userId, role, id, request, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpPost("{id:guid}/send")]
    public async Task<IActionResult> Send(
        Guid id,
        [FromBody] SendContractRequestDto request,
        CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId ||
            User.GetTenantRole() is not { } role)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result result = await contractService.SendForSigningAsync(tenantId, userId, role, id, request, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [Authorize(Policy = AuthorizationPolicies.TenantMember)]
    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancel)
    {
        if (User.GetTenantId() is not { } tenantId ||
            User.GetUserId() is not { } userId ||
            User.GetTenantRole() is not { } role)
        {
            return this.ToProblem(IdentityErrors.TenantAccessDenied);
        }

        Result result = await contractService.CancelAsync(tenantId, userId, role, id, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    [AllowAnonymous]
    [HttpGet("{id:guid}/document")]
    public async Task<IActionResult> GetDocument(
        Guid id,
        [FromQuery] string? token,
        CancellationToken cancel)
    {
        Result<byte[]> result = await contractService.GetDocumentBytesByTokenAsync(id, token ?? string.Empty, cancel);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        return File(result.Value, "application/pdf");
    }
}
