namespace RouteLab.Models;

public sealed class ClimberProfileDefinition
{
    public const string DefaultProfileId = "default";

    public string Id { get; set; } = DefaultProfileId;

    public string Name { get; set; } = "Persona predefinita";

    public double HeightMm { get; set; } = 1750d;

    public double ArmSpanMm { get; set; } = 1750d;

    public double MassKg { get; set; } = 70d;

    public double BodyDistanceFromWallMm { get; set; } = 180d;

    public bool IsDefault =>
        string.Equals(Id, DefaultProfileId, StringComparison.OrdinalIgnoreCase);

    public ClimberProfileDefinition Clone()
    {
        return new ClimberProfileDefinition
        {
            Id = Id,
            Name = Name,
            HeightMm = HeightMm,
            ArmSpanMm = ArmSpanMm,
            MassKg = MassKg,
            BodyDistanceFromWallMm = BodyDistanceFromWallMm
        };
    }
}
