using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SwiftBets.Placement.Application.Identity;

namespace SwiftBets.Placement.Infrastructure.Identity;

/// <summary>Signs RS256 access tokens and publishes the public key as a JWKS; the private key never leaves this process.</summary>
public sealed partial class RsaTokenIssuer : ITokenIssuer, IDisposable
{
    private readonly RSA _rsa;
    private readonly RsaSecurityKey _key;
    private readonly IdentityOptions _options;
    private readonly TimeProvider _time;

    public RsaTokenIssuer(IOptions<IdentityOptions> options, IHostEnvironment environment, TimeProvider time, ILogger<RsaTokenIssuer> logger)
    {
        _options = options.Value;
        _time = time;
        _rsa = RSA.Create();
        if (!string.IsNullOrWhiteSpace(_options.SigningKeyPem))
        {
            _rsa.ImportFromPem(_options.SigningKeyPem);
        }
        else if (environment.IsDevelopment())
        {
            _rsa.KeySize = 2048;
            LogEphemeralKey(logger);
        }
        else
        {
            throw new InvalidOperationException("Identity:SigningKeyPem is required outside Development.");
        }

        var modulus = _rsa.ExportParameters(false).Modulus!;
        _key = new RsaSecurityKey(_rsa) { KeyId = Base64UrlEncoder.Encode(SHA256.HashData(modulus))[..16] };
    }

    public string Issuer => _options.Issuer;

    public string IssueAccessToken(string subject, IReadOnlyList<string> roles, TimeSpan lifetime)
    {
        var now = _time.GetUtcNow().UtcDateTime;
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")) };
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now + lifetime,
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.RsaSha256),
        });
    }

    public object Jwks()
    {
        var jwk = JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(_rsa.ExportParameters(false)) { KeyId = _key.KeyId });
        return new { keys = new[] { new { kty = jwk.Kty, use = "sig", alg = SecurityAlgorithms.RsaSha256, kid = jwk.Kid, n = jwk.N, e = jwk.E } } };
    }

    public void Dispose() => _rsa.Dispose();

    [LoggerMessage(Level = LogLevel.Warning, Message = "No Identity:SigningKeyPem configured; using a per-process development key. Tokens will not survive a restart.")]
    private static partial void LogEphemeralKey(ILogger logger);
}
