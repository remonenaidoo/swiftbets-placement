namespace SwiftBets.Identity.Application.Accounts;

public sealed record RegisterCommand(string Email, string Password, DateOnly DateOfBirth, string Country, string Currency);
