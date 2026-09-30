using SwiftBets.Placement.Application.Identity;

namespace SwiftBets.Placement.Infrastructure.Wallet;

/// <summary>Placement is its own issuer, so its Service token for the wallet is minted locally and refreshed before expiry.</summary>
public sealed class ServiceTokenCache(ITokenIssuer issuer, TimeProvider time)
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);
    private readonly Lock _gate = new();
    private (string Token, DateTimeOffset RenewAt) _current;

    public string Token
    {
        get
        {
            lock (_gate)
            {
                if (_current.Token is null || time.GetUtcNow() >= _current.RenewAt)
                {
                    _current = (issuer.IssueAccessToken("client:placement", ["Service"], Lifetime), time.GetUtcNow() + (Lifetime / 2));
                }

                return _current.Token;
            }
        }
    }
}
