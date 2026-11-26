using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public interface IGoalSkillGenerator
{
    Task<SkillGenerationResult> GenerateAsync(
        string title,
        string description,
        DateOnly? targetDate,
        CancellationToken cancellationToken = default);
}

public sealed record SkillGenerationResult(
    IReadOnlyList<Skill> Skills,
    string? Error)
{
    public static SkillGenerationResult Success(IReadOnlyList<Skill> skills) => new(skills, null);
    public static SkillGenerationResult Failed(string error) => new([], error);
}

public interface IGoalAdviceClient
{
    Task<GoalAdviceResult> GetAdviceAsync(
        IReadOnlyList<Goal> goals,
        CancellationToken cancellationToken = default);
}

public sealed record GoalAdviceResult(
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> Advice,
    string? Error)
{
    public static GoalAdviceResult Disabled() => new(new Dictionary<Guid, IReadOnlyList<string>>(), null);
    public static GoalAdviceResult Success(IReadOnlyDictionary<Guid, IReadOnlyList<string>> advice) => new(advice, null);
    public static GoalAdviceResult Failed(string error) => new(new Dictionary<Guid, IReadOnlyList<string>>(), error);
}
