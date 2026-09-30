using System.ComponentModel.DataAnnotations;

namespace SwiftBets.Placement.Application.Placing;

public sealed class PlacementOptions
{
    public const string SectionName = "Placement";

    /// <summary>After this, an unfinished saga is the sweeper's to complete or compensate.</summary>
    [Range(5, 600)]
    public int SagaDeadlineSeconds { get; set; } = 30;

    [Range(1, 60)]
    public int SweepIntervalSeconds { get; set; } = 5;
}
