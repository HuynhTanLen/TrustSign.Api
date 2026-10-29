using System.ComponentModel.DataAnnotations;

namespace TrustSign.Application.Modules.Subscriptions;

public sealed class SepayOptions
{
    public const string SectionName = "Sepay";

    [Required]
    public string WebhookApiKey { get; set; } = "trustsign_sepay_dev_secret";

    [Required]
    public string BankAccount { get; set; } = "0359851608";

    [Required]
    public string BankName { get; set; } = "MBBank";

    [Required]
    public string BankCode { get; set; } = "MB";

    [Required]
    public string AccountName { get; set; } = "CHU TIN";

    public string QrTemplateUrl { get; set; } =
        "https://qr.sepay.vn/img?acc={acc}&bank={bank}&amount={amount}&des={des}&template=compact";
}
