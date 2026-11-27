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

public interface ISkillStepGenerator
{
    Task<StepGenerationResult> GenerateAsync(
        string goalTitle,
        string goalDescription,
        DateOnly? targetDate,
        string skillName,
        string skillNote,
        CancellationToken cancellationToken = default);
}

public sealed record StepGenerationResult(
    IReadOnlyList<string> Titles,
    string? Error)
{
    public static StepGenerationResult Success(IReadOnlyList<string> titles) => new(titles, null);
    public static StepGenerationResult Failed(string error) => new([], error);
}

public interface IGoalAdviceClient
{
    Task<GoalAdviceResult> GetAdviceAsync(
        IReadOnlyList<Goal> goals,
        CancellationToken cancellationToken = default);

    Task<AiTestResult> TestAsync(CancellationToken cancellationToken = default);
}

public sealed record AiTestResult(
    bool Ok,
    string Model,
    string Message,
    int? Remaining = null,
    int Limit = 20,
    int Used = 0,
    string ResetText = "")
{
    public static AiTestResult Success(
        string model,
        string message,
        int? remaining = null,
        int limit = 20,
        int used = 0,
        string resetText = "") =>
        new(true, model, message, remaining, limit, used, resetText);

    public static AiTestResult Failed(
        string message,
        string model = "",
        int? remaining = null,
        int limit = 20,
        int used = 0,
        string resetText = "") =>
        new(false, model, message, remaining, limit, used, resetText);
}

public sealed record GoalAdviceResult(
    IReadOnlyDictionary<Guid, IReadOnlyList<string>> Advice,
    string? Error)
{
    public static GoalAdviceResult Disabled() => new(new Dictionary<Guid, IReadOnlyList<string>>(), null);
    public static GoalAdviceResult Success(IReadOnlyDictionary<Guid, IReadOnlyList<string>> advice) => new(advice, null);
    public static GoalAdviceResult Failed(string error) => new(new Dictionary<Guid, IReadOnlyList<string>>(), error);
}
