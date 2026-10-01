namespace SwiftBets.Identity.Domain;

/// <summary>Stored as a byte; the numbers are persisted and must never be renumbered.</summary>
public enum AccountStatus : byte
{
    Active = 0,
    Suspended = 1,
    Closed = 2,
    SelfExcluded = 3,
}
