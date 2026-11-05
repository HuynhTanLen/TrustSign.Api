namespace TrustSign.Application.Modules.Audit;

public interface IAuditLogger
{
    Task LogAsync(
        Guid? tenantId,
        Guid? actorUserId,
        string action,
        string resourceType,
        string resourceId,
        object? payload = null,
        CancellationToken cancel = default);
}
