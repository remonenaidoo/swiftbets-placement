namespace SwiftBets.Identity.Application.Ports;

public interface ITokenIssuer
{
    string Issuer { get; }

    string IssueAccessToken(string subject, IReadOnlyList<string> roles, IReadOnlyList<string> permissions, TimeSpan lifetime);

    object Jwks();
}
