namespace RouteLab.Models;

public sealed class BiomechanicalPoseRequest
{
    public required WallDefinition Wall { get; init; }

    public required ClimberProfileDefinition Climber { get; init; }

    public required WallHoleDefinition LeftHand { get; init; }

    public required WallHoleDefinition RightHand { get; init; }

    public required WallHoleDefinition FirstFoot { get; init; }

    public required WallHoleDefinition SecondFoot { get; init; }

    public double WallInclinationDegrees { get; init; }
}
