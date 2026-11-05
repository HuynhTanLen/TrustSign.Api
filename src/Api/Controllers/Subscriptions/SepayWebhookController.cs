using System.Security.Cryptography;
using System.Text;
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
        if (!IsValidWebhookSecret(authHeader, expectedKey))
        {
            return Unauthorized(new { message = "Khóa API SePay không hợp lệ." });
        }

        Result result = await webhookService.ProcessWebhookAsync(payload, cancel);
        return result.IsSuccess
            ? Ok(new { success = true })
            : StatusCode(StatusCodes.Status500InternalServerError, new { message = result.Error.Message });
    }

    private static bool IsValidWebhookSecret(string? authHeader, string expectedKey)
    {
        if (string.IsNullOrWhiteSpace(authHeader) || string.IsNullOrWhiteSpace(expectedKey))
        {
            return false;
        }

        ReadOnlySpan<char> tokenSpan = authHeader.AsSpan().Trim();
        if (tokenSpan.StartsWith("Apikey ", StringComparison.OrdinalIgnoreCase))
        {
            tokenSpan = tokenSpan["Apikey ".Length..].Trim();
        }
        else if (tokenSpan.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            tokenSpan = tokenSpan["Bearer ".Length..].Trim();
        }

        byte[] providedBytes = Encoding.UTF8.GetBytes(tokenSpan.ToString());
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedKey);

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
