namespace TrustSign.Application.Modules.Notifications;

public sealed record EmailMessage(string To, string Subject, string HtmlBody);
