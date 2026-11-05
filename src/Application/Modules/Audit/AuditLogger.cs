using System.Text.Json;
using TrustSign.Domain.Entities.Audit;
using TrustSign.Domain.IRepositories.Audit;
using TrustSign.Domain.IRepositories.Identity;

namespace TrustSign.Application.Modules.Audit;

internal sealed class AuditLogger(
    IAuditRepository auditRepository,
    IUserRepository userRepository,
    TimeProvider timeProvider) : IAuditLogger
{
    public async Task LogAsync(
        Guid? tenantId,
        Guid? actorUserId,
        string action,
        string resourceType,
        string resourceId,
        object? payload = null,
        CancellationToken cancel = default)
    {
        string? actorEmail = null;
        if (actorUserId.HasValue)
        {
            var user = await userRepository.FindByIdAsync(actorUserId.Value, cancel);
            actorEmail = user?.Email;
        }

        string? payloadJson = payload is null ? null : JsonSerializer.Serialize(payload);
        DateTimeOffset now = timeProvider.GetUtcNow();

        AuditEvent? latest = await auditRepository.GetLatestForTenantAsync(tenantId, cancel);
        string? prevHash = latest?.Hash;

        AuditEvent auditEvent = AuditEvent.Create(
            tenantId,
            actorUserId,
            actorEmail,
            action,
            resourceType,
            resourceId,
            payloadJson,
            prevHash,
            now);

        await auditRepository.AddAsync(auditEvent, cancel);
    }
}
