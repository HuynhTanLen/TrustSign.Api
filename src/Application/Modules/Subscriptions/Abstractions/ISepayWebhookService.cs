using TrustSign.Common;

namespace TrustSign.Application.Modules.Subscriptions;

public interface ISepayWebhookService
{
    Task<Result> ProcessWebhookAsync(SepayWebhookPayload payload, CancellationToken cancel = default);
}
