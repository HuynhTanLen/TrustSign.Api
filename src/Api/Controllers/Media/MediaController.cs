using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TrustSign.Api.Http;
using TrustSign.Application.Modules.Contracts;
using TrustSign.Application.Modules.Identity;
using TrustSign.Application.Modules.Media;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Media;

/// <summary>
/// Chỗ duy nhất phục vụ file cho thẻ iframe/img (không gửi được header Bearer) bằng token ký ngắn hạn.
/// Mỗi loại file do module sở hữu nó quyết định ai được xem; controller này chỉ chuyển token cho module đó.
/// </summary>
[ApiController]
[Route("api/media")]
public sealed class MediaController(IContractService contractService, IUserService userService) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet(MediaKinds.ContractDocuments + "/{id:guid}")]
    public async Task<IActionResult> ContractDocument(Guid id, [FromQuery] string? token, CancellationToken cancel)
    {
        Result<byte[]> result = await contractService.GetDocumentBytesByTokenAsync(id, token ?? string.Empty, cancel);
        return result.IsSuccess ? File(result.Value, MediaContent.ContentType(MediaType.Pdf)) : this.ToProblem(result.Error);
    }

    [AllowAnonymous]
    [HttpGet(MediaKinds.Avatars + "/{userId:guid}")]
    public async Task<IActionResult> Avatar(Guid userId, [FromQuery] string? token, CancellationToken cancel)
    {
        Result<byte[]> result = await userService.GetAvatarByTokenAsync(userId, token ?? string.Empty, cancel);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        // Ảnh chỉ nằm trong cache riêng của trình duyệt; nosniff để không bao giờ bị hiểu thành HTML/script.
        Response.Headers.CacheControl = "private, max-age=300";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(result.Value, MediaContent.ContentTypeOf(result.Value));
    }
}
