namespace TrustSign.Application.Modules.Identity;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string passwordHash);
}
