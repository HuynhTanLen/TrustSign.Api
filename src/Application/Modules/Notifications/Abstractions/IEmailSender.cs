namespace TrustSign.Application.Modules.Notifications;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancel);
}
