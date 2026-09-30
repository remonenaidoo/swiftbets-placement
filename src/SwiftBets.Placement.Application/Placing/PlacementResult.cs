namespace SwiftBets.Placement.Application.Placing;

/// <summary>An HTTP-ready outcome: the status and JSON body are stored with the intent so a retried request gets the identical answer.</summary>
public sealed record PlacementResult(int Status, string Body, bool Replayed);
