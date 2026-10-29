namespace TrustSign.Application.Modules.Subscriptions;

public interface IPlanSeeder
{
    Task SeedDefaultPlansAsync(CancellationToken cancel = default);
}
