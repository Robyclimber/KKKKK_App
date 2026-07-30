namespace RouteLab.Models;

public sealed class ClimbingMovementStep
{
    public int Sequence { get; init; }

    public ClimbingMovementPhase Phase { get; init; }

    public ClimbingBodyPart BodyPart { get; init; }

    public ClimbingMovementAction Action { get; init; }

    public int? FromHoleNumber { get; init; }

    public int? ToHoleNumber { get; init; }
}

public enum ClimbingMovementPhase
{
    EstablishSupport = 0,
    FootPreparation = 1,
    WeightTransfer = 2,
    HandMovement = 3,
    Stabilization = 4
}

public enum ClimbingBodyPart
{
    Body = 0,
    Feet = 1,
    LeftHand = 2,
    RightHand = 3
}

public enum ClimbingMovementAction
{
    MaintainContact = 0,
    Move = 1,
    TransferWeight = 2,
    Load = 3,
    Stabilize = 4
}
