using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace TrustSign.Application.Modules.Notifications;

internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    private readonly SmtpOptions _options = options.Value;

    public async Task SendAsync(EmailMessage message, CancellationToken cancel)
    {
        using MailMessage mail = new()
        {
            From = new MailAddress(_options.FromEmail, _options.FromName, Encoding.UTF8),
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.HtmlBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        mail.To.Add(new MailAddress(message.To));

        using SmtpClient client = new(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_options.Username))
        {
            client.Credentials = new NetworkCredential(_options.Username, _options.Password);
        }

        try
        {
            await client.SendMailAsync(mail, cancel);
            logger.LogInformation("Đã gửi email '{Subject}' tới {Recipient}.", message.Subject, message.To);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Không thể kết nối SMTP ({Host}:{Port}) để gửi email '{Subject}' tới {Recipient}. Nội dung: {Body}",
                _options.Host,
                _options.Port,
                message.Subject,
                message.To,
                message.HtmlBody);
        }
    }
}
