namespace SwiftBets.Placement.Application.Identity;

public sealed record UserRecord(Guid UserId, string Username, string PasswordHash, IReadOnlyList<string> Roles);
