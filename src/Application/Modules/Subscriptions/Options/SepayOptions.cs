using System.ComponentModel.DataAnnotations;

namespace TrustSign.Application.Modules.Subscriptions;

public sealed class SepayOptions
{
    public const string SectionName = "Sepay";

    [Required]
    public string WebhookApiKey { get; set; } = string.Empty;

    [Required]
    public string BankAccount { get; set; } = string.Empty;

    [Required]
    public string BankName { get; set; } = string.Empty;

    [Required]
    public string BankCode { get; set; } = string.Empty;

    [Required]
    public string AccountName { get; set; } = string.Empty;

    public string QrTemplateUrl { get; set; } =
        "https://qr.sepay.vn/img?acc={acc}&bank={bank}&amount={amount}&des={des}&template=compact";
}
