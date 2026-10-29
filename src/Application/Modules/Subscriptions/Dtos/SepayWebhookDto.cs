using System.Text.Json.Serialization;

namespace TrustSign.Application.Modules.Subscriptions;

public sealed record SepayWebhookPayload(
    [property: JsonPropertyName("id")] long Id,
    [property: JsonPropertyName("gateway")] string? Gateway,
    [property: JsonPropertyName("transactionDate")] string? TransactionDate,
    [property: JsonPropertyName("accountNumber")] string? AccountNumber,
    [property: JsonPropertyName("code")] string? Code,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("transferType")] string? TransferType,
    [property: JsonPropertyName("transferAmount")] decimal TransferAmount,
    [property: JsonPropertyName("accumulated")] decimal Accumulated,
    [property: JsonPropertyName("subAccount")] string? SubAccount,
    [property: JsonPropertyName("referenceCode")] string? ReferenceCode,
    [property: JsonPropertyName("description")] string? Description);
