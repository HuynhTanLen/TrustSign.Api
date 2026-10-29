using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TrustSign.Application.Modules.Subscriptions;
using TrustSign.Common;

namespace TrustSign.Api.Controllers.Subscriptions;

[ApiController]
[Route("api/subscription/webhooks/sepay")]
public sealed class SepayWebhookController(
    ISepayWebhookService webhookService,
    IOptions<SepayOptions> sepayOptions) : ControllerBase
{
    private readonly SepayOptions _options = sepayOptions.Value;

    [HttpPost]
    public async Task<IActionResult> HandleWebhook([FromBody] SepayWebhookPayload payload, CancellationToken cancel)
    {
        string? authHeader = Request.Headers.Authorization.ToString();
        string expectedKey = _options.WebhookApiKey;

        // SePay hỗ trợ cấu hình gửi kèm header: Authorization: Apikey <Secret> hoặc Bearer <Secret>
        bool isAuthorized = !string.IsNullOrWhiteSpace(authHeader) &&
            (authHeader.Equals($"Apikey {expectedKey}", StringComparison.Ordinal) ||
             authHeader.Equals($"Bearer {expectedKey}", StringComparison.Ordinal) ||
             authHeader.Equals(expectedKey, StringComparison.Ordinal));

        if (!isAuthorized)
        {
            return Unauthorized(new { message = "Khóa API SePay không hợp lệ." });
        }

        Result result = await webhookService.ProcessWebhookAsync(payload, cancel);
        return result.IsSuccess ? Ok(new { success = true }) : StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Error.Message });
    }
}
