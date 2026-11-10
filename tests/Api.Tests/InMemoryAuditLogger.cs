using System.Text.Json;
using TrustSign.Application.Modules.Audit;
using TrustSign.Domain.Entities.Audit;

namespace TrustSign.Api.Tests;

/// <summary>Bản trong bộ nhớ của AuditLogger: nối chuỗi theo từng đối tượng giống EfAuditRepository.</summary>
internal sealed class InMemoryAuditLogger(TimeProvider? clock = null) : IAuditLogger
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public List<AuditEvent> Events { get; } = [];

    public Task LogAsync(
        Guid? tenantId,
        Guid? actorUserId,
        string action,
        string resourceType,
        string resourceId,
        object? payload = null,
        CancellationToken cancel = default)
    {
        List<AuditEvent> chain = Events.Where(e => e.ResourceType == resourceType && e.ResourceId == resourceId).ToList();
        AuditEvent? tip = chain.FirstOrDefault(e => !chain.Any(n => n.PrevHash == e.Hash));
        Events.Add(AuditEvent.Create(
            tenantId,
            actorUserId,
            null,
            action,
            resourceType,
            resourceId,
            payload is null ? null : JsonSerializer.Serialize(payload),
            tip?.Hash,
            _clock.GetUtcNow()));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<AuditEvent>> ListForResourceAsync(
        Guid? tenantId,
        string resourceType,
        string resourceId,
        CancellationToken cancel = default) =>
        Task.FromResult(AuditEvent.OrderChain(
            Events.Where(e => e.TenantId == tenantId && e.ResourceType == resourceType && e.ResourceId == resourceId)));
}
