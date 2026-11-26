namespace 長期目標提醒器.Models;

public sealed class Skill
{
    public string Name { get; init; } = "";
    public SkillStatus Status { get; init; }
    public int Progress { get; init; }
    public string Note { get; init; } = "";
}

public enum SkillStatus
{
    NotStarted,
    Learning,
    Mastered
}
