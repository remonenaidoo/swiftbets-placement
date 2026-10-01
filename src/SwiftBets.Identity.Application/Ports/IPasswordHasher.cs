namespace SwiftBets.Identity.Application.Ports;

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string hash, string password);
}
