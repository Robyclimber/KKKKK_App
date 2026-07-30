namespace RouteLab.Models;

public sealed class ClimbingMovementPlan
{
    public ClimbingExecutionMode ExecutionMode { get; init; } = ClimbingExecutionMode.Static;

    public ClimbingMovementType PrimaryMovement { get; init; } = ClimbingMovementType.Frontal;

    public IReadOnlyList<ClimbingMovementType> MovementTypes { get; init; } =
        new[] { ClimbingMovementType.Frontal };

    public IReadOnlyList<ClimbingBalanceTechnique> BalanceTechniques { get; init; } =
        Array.Empty<ClimbingBalanceTechnique>();

    public IReadOnlyList<ClimbingMovementStep> Steps { get; init; } =
        Array.Empty<ClimbingMovementStep>();
}
