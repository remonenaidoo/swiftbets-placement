namespace SwiftBets.Identity.Application.Tokens;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken);
