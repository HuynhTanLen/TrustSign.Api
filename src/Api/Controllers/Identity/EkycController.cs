using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Http;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Media;
using TrustSign.Common;
using TrustSign.Common.Errors;
using TrustSign.Domain.Entities.Identity;

namespace TrustSign.Api.Controllers.Identity;

[ApiController]
[Route("api/ekyc")]
[Authorize]
public sealed class EkycController(IIdentityVerificationService ekyc) : ControllerBase
{
    [HttpGet("me")]
    public async Task<IActionResult> Mine(CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        return Ok(await ekyc.GetMineAsync(userId, cancel));
    }

    [HttpPost]
    [RequestSizeLimit(3 * IdentityVerification.MaxImageBytes + 64 * 1024)]
    public async Task<IActionResult> Submit([FromForm] EkycSubmitForm form, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        return await SubmitFormAsync(form, command => ekyc.SubmitAsync(userId, command, cancel), cancel);
    }

    [HttpPost("handoff")]
    public async Task<IActionResult> CreateHandoff(CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result<EkycHandoffDto> result = await ekyc.CreateHandoffAsync(userId, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [AllowAnonymous]
    [HttpGet("handoff/{token}")]
    public async Task<IActionResult> GetHandoff(string token, CancellationToken cancel)
    {
        Result<EkycHandoffInfoDto> result = await ekyc.GetHandoffAsync(token, cancel);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [AllowAnonymous]
    [HttpPost("handoff/{token}")]
    [RequestSizeLimit(3 * IdentityVerification.MaxImageBytes + 64 * 1024)]
    public Task<IActionResult> SubmitHandoff(string token, [FromForm] EkycSubmitForm form, CancellationToken cancel) =>
        SubmitFormAsync(form, command => ekyc.SubmitByHandoffAsync(token, command, cancel), cancel);

    [Authorize(Roles = "Admin")]
    [HttpGet("admin")]
    public async Task<IActionResult> List(CancellationToken cancel) =>
        Ok(await ekyc.ListAsync(cancel));

    [Authorize(Roles = "Admin")]
    [HttpGet("admin/{id:guid}/images/{kind}")]
    public async Task<IActionResult> Image(Guid id, EkycImageKind kind, CancellationToken cancel)
    {
        Result<byte[]> result = await ekyc.GetImageAsync(id, kind, cancel);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        // Ảnh CCCD là dữ liệu nhạy cảm: không cho trình duyệt hay proxy lưu đệm.
        Response.Headers.CacheControl = "no-store";
        return File(result.Value, MediaContent.ContentTypeOf(result.Value));
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("admin/{id:guid}/revoke")]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokeIdentityVerificationRequestDto request, CancellationToken cancel)
    {
        if (User.GetUserId() is not { } adminId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        Result result = await ekyc.RevokeAsync(adminId, id, request.Reason, cancel);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    public sealed class EkycSubmitForm
    {
        public string? QrText { get; init; }

        public string? FaceDistance { get; init; }

        public IFormFile? Selfie { get; init; }

        public IFormFile? Front { get; init; }

        public IFormFile? Back { get; init; }
    }

    private async Task<IActionResult> SubmitFormAsync(
        EkycSubmitForm form,
        Func<SubmitIdentityVerificationCommand, Task<Result<MyIdentityVerificationDto>>> submit,
        CancellationToken cancel)
    {
        if (form.Selfie is not { } selfie || form.Front is not { } front || form.Back is not { } back ||
            selfie.Length > IdentityVerification.MaxImageBytes ||
            front.Length > IdentityVerification.MaxImageBytes ||
            back.Length > IdentityVerification.MaxImageBytes)
        {
            return this.ToProblem(IdentityErrors.EkycInvalidImage);
        }

        Result<MyIdentityVerificationDto> result = await submit(new SubmitIdentityVerificationCommand(
            form.QrText ?? string.Empty,
            // Form dùng văn hoá hiện tại khi bind số; đọc invariant để "0.42" không thành 42 trên máy vi-VN.
            double.TryParse(form.FaceDistance, NumberStyles.Float, CultureInfo.InvariantCulture, out double distance) ? distance : double.NaN,
            await ReadAsync(selfie, cancel),
            await ReadAsync(front, cancel),
            await ReadAsync(back, cancel)));

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancel)
    {
        await using MemoryStream ms = new((int)file.Length);
        await file.CopyToAsync(ms, cancel);
        return ms.ToArray();
    }
}
