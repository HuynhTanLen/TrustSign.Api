using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Errors;
using TrustSign.Api.Security;
using TrustSign.Application.Modules.Identity;
using TrustSign.Common;
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
    public async Task<IActionResult> Submit(
        [FromForm] string? qrText,
        [FromForm] string? faceDistance,
        [FromForm] IFormFile? selfie,
        [FromForm] IFormFile? front,
        [FromForm] IFormFile? back,
        CancellationToken cancel)
    {
        if (User.GetUserId() is not { } userId)
        {
            return this.ToProblem(IdentityErrors.SessionInvalid);
        }

        if (selfie is null || front is null || back is null ||
            selfie.Length > IdentityVerification.MaxImageBytes ||
            front.Length > IdentityVerification.MaxImageBytes ||
            back.Length > IdentityVerification.MaxImageBytes)
        {
            return this.ToProblem(IdentityErrors.EkycInvalidImage);
        }

        Result<MyIdentityVerificationDto> result = await ekyc.SubmitAsync(
            userId,
            new SubmitIdentityVerificationCommand(
                qrText ?? string.Empty,
                // Form dùng văn hoá hiện tại khi bind số; đọc invariant để "0.42" không thành 42 trên máy vi-VN.
                double.TryParse(faceDistance, NumberStyles.Float, CultureInfo.InvariantCulture, out double distance) ? distance : double.NaN,
                await ReadAsync(selfie, cancel),
                await ReadAsync(front, cancel),
                await ReadAsync(back, cancel)),
            cancel);

        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

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
        string contentType = result.Value[0] == 0x89 ? "image/png" : "image/jpeg";
        return File(result.Value, contentType);
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

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancel)
    {
        await using MemoryStream ms = new((int)file.Length);
        await file.CopyToAsync(ms, cancel);
        return ms.ToArray();
    }
}
