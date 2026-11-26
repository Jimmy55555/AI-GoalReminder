namespace 長期目標提醒器.Models;

public sealed record Goal
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public bool IsInProgress { get; init; } = true;
    public DateOnly? TargetDate { get; init; }
    public IReadOnlyList<Skill> Skills { get; init; } = [];

    public int ProgressPercent =>
        Skills.Count == 0 ? 0 : (int)Math.Round(Skills.Average(s => s.Progress));

    public int MasteredCount => Skills.Count(s => s.Status == SkillStatus.Mastered);
    public int LearningCount => Skills.Count(s => s.Status == SkillStatus.Learning);
    public int NotStartedCount => Skills.Count(s => s.Status == SkillStatus.NotStarted);

    public int? DaysRemaining => TargetDate?.DayNumber - DateOnly.FromDateTime(DateTime.Today).DayNumber;
}
