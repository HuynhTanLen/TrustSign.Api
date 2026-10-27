namespace TrustSign.Application.Modules.Identity;

public interface IAdminSeeder
{
    Task SeedAsync(CancellationToken cancel);
}
