namespace 長期目標提醒器.Models;

public sealed record Skill
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; init; } = "";
    public SkillStatus Status { get; init; }
    public int Progress { get; init; }
    public string Note { get; init; } = "";
    public IReadOnlyList<SkillStep> Steps { get; init; } = [];

    /// <summary>
    /// 步驟是唯一狀態來源：只保留連續完成的Prefix，並同步進度與狀態。
    /// </summary>
    public Skill Normalized()
    {
        var ordered = Steps.ToArray();
        var locked = false;
        var steps = new SkillStep[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            var done = ordered[i].Done && !locked;
            if (!done)
            {
                locked = true;
            }

            steps[i] = done == ordered[i].Done ? ordered[i] : ordered[i] with { Done = done };
        }

        var doneCount = steps.Count(step => step.Done);
        var progress = steps.Length == 0
            ? 0
            : (int)Math.Round(doneCount * 100.0 / steps.Length);
        var status = steps.Length == 0
            ? SkillStatus.NotStarted
            : doneCount == steps.Length
                ? SkillStatus.Mastered
                : doneCount > 0
                    ? SkillStatus.Learning
                    : SkillStatus.NotStarted;

        return this with { Steps = steps, Progress = progress, Status = status };
    }
}

public sealed record SkillStep
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = "";
    public bool Done { get; init; }
}

public enum SkillStatus
{
    NotStarted,
    Learning,
    Mastered
}
