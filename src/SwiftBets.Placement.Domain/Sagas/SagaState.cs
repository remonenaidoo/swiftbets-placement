namespace SwiftBets.Placement.Domain.Sagas;

/// <summary>Durable placement progress. Non-terminal intents past their deadline are resolved by the orphan sweeper.</summary>
public enum SagaState : byte
{
    Started = 1,
    Reserved = 2,
    Persisted = 3,
    Completed = 4,
    Compensated = 5,
    Rejected = 6,

    /// <summary>Claimed by the sweeper; the live saga can no longer advance and must release anything it reserved.</summary>
    Compensating = 7,
}
