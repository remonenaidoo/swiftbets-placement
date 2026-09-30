namespace SwiftBets.Placement.Application.Identity;

public interface ITokenIssuer
{
    string IssueAccessToken(string subject, IReadOnlyList<string> roles, TimeSpan lifetime);

    object Jwks();

    string Issuer { get; }
}
