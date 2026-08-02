using SQLite;

namespace RouteLab.Persistence.Entities;

[Table("workout_steps")]
public sealed class WorkoutStepEntity
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int WorkoutEntityId { get; set; }

    [Indexed]
    public string StepId { get; set; } = string.Empty;

    public int StepType { get; set; }

    public string Name { get; set; } = string.Empty;

    public int WorkSeconds { get; set; }

    public int InitialRestSeconds { get; set; }

    public int FinalRestSeconds { get; set; }

    public int Repetitions { get; set; }

    public int Sequence { get; set; }

    public string PayloadJson { get; set; } = string.Empty;
}

