namespace RouteLab.Models;

public sealed class BiomechanicalCenterOfMassResult
{
    public double CenterX { get; init; }

    public double CenterY { get; init; }

    public double EffectiveCenterX { get; init; }

    public double EffectiveCenterY { get; init; }

    public double WallNormalDistanceMm { get; init; }

    public double NormalGravityForceNewton { get; init; }

    public double GravityTorqueNewtonMeter { get; init; }

    public double ReachPenalty { get; init; }

    public bool IsReachFeasible => ReachPenalty <= 0.05d;
}
