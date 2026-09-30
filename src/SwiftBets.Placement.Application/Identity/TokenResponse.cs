namespace SwiftBets.Placement.Application.Identity;

public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn, string? RefreshToken);
