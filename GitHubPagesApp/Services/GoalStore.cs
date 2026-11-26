using System.Text.Json.Serialization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// Supabase 版目標儲存。每次登入／重新載入都重抓雲端資料，不留離線資料庫。
/// </summary>
public sealed class GoalStore(
    SupabaseClient supabase,
    SessionStore sessionStore)
{
    private const string Table = "rest/v1/goal_reminder_goals";
    private readonly object gate = new();
    private List<Goal> goals = [];

    public event Action? Changed;

    public IReadOnlyList<Goal> Goals
    {
        get
        {
            lock (gate)
            {
                return goals.ToArray();
            }
        }
    }

    public Goal? Get(Guid goalId)
    {
        lock (gate)
        {
            return goals.FirstOrDefault(goal => goal.Id == goalId);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var rows = await supabase.GetAsync<List<GoalRow>>($"{Table}?select=*", cancellationToken) ?? [];

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(sessionStore.UserId))
        {
            await SeedAsync(cancellationToken);
            rows = await supabase.GetAsync<List<GoalRow>>($"{Table}?select=*", cancellationToken) ?? [];
        }

        lock (gate)
        {
            goals = rows.Select(ToGoal).ToList();
        }

        Changed?.Invoke();
    }

    public void AddGoal(Goal goal)
    {
        lock (gate)
        {
            goals.Add(goal);
        }

        RunAsync(supabase.PostAsync<GoalRow>(Table, ToRow(goal)));
        Changed?.Invoke();
    }

    public void SetTargetDate(Guid goalId, DateOnly? targetDate)
    {
        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            goals[index] = goals[index] with { TargetDate = targetDate };
            RunAsync(supabase.PatchAsync<GoalRow>(
                $"{Table}?id=eq.{goalId}",
                new { target_date = targetDate }));
        }

        Changed?.Invoke();
    }

    public void DeleteGoal(Guid goalId)
    {
        lock (gate)
        {
            goals.RemoveAll(goal => goal.Id == goalId);
        }

        RunAsync(supabase.DeleteAsync($"{Table}?id=eq.{goalId}"));
        Changed?.Invoke();
    }

    public void AddSkill(Guid goalId, Skill skill)
    {
        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            var skills = goals[index].Skills.ToList();
            skills.Add(skill);
            goals[index] = goals[index] with { Skills = skills.ToArray() };
            RunAsync(supabase.PatchAsync<GoalRow>(
                $"{Table}?id=eq.{goalId}",
                new { skills = skills }));
        }

        Changed?.Invoke();
    }

    public void UpdateSkill(Guid goalId, Guid skillId, Skill updated)
    {
        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            var skills = goals[index].Skills.ToList();
            var skillIndex = skills.FindIndex(skill => skill.Id == skillId);
            if (skillIndex < 0)
            {
                return;
            }

            skills[skillIndex] = updated with { Id = skillId };
            goals[index] = goals[index] with { Skills = skills.ToArray() };
            RunAsync(supabase.PatchAsync<GoalRow>(
                $"{Table}?id=eq.{goalId}",
                new { skills }));
        }

        Changed?.Invoke();
    }

    public void DeleteSkill(Guid goalId, Guid skillId)
    {
        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            var skills = goals[index].Skills.Where(skill => skill.Id != skillId).ToList();
            goals[index] = goals[index] with { Skills = skills.ToArray() };
            RunAsync(supabase.PatchAsync<GoalRow>(
                $"{Table}?id=eq.{goalId}",
                new { skills }));
        }

        Changed?.Invoke();
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var ownerId = sessionStore.UserId;
        if (string.IsNullOrWhiteSpace(ownerId))
        {
            return;
        }

        var rows = GoalSeedData.Goals.Select(goal => new
        {
            id = goal.Id,
            owner_id = ownerId,
            title = goal.Title,
            description = goal.Description,
            is_in_progress = goal.IsInProgress,
            target_date = goal.TargetDate,
            skills = goal.Skills
        }).ToArray();

        await supabase.PostAsync<GoalRow[]>(Table, rows, cancellationToken: cancellationToken);

        var progressRows = GoalSeedData.Goals.Select(goal => new
        {
            goal_id = goal.Id,
            owner_id = ownerId,
            is_in_progress = goal.IsInProgress
        }).ToArray();
        await supabase.PostAsync<object>(
            "rest/v1/goal_reminder_progress?on_conflict=goal_id",
            progressRows,
            "resolution=merge-duplicates",
            cancellationToken);
    }

    private object ToRow(Goal goal) => new
    {
        id = goal.Id,
        owner_id = sessionStore.UserId,
        title = goal.Title,
        description = goal.Description,
        is_in_progress = goal.IsInProgress,
        target_date = goal.TargetDate,
        skills = goal.Skills
    };

    private static Goal ToGoal(GoalRow row) => new()
    {
        Id = row.Id,
        Title = row.Title,
        Description = row.Description,
        IsInProgress = row.IsInProgress,
        TargetDate = row.TargetDate,
        Skills = row.Skills ?? []
    };

    private static void RunAsync(Task task) =>
        _ = RunInternalAsync(task);

    private static async Task RunInternalAsync(Task task)
    {
        try
        {
            await task;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Supabase 寫入失敗：{ex.Message}");
        }
    }

    private sealed class GoalRow
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("description")]
        public string Description { get; set; } = "";

        [JsonPropertyName("is_in_progress")]
        public bool IsInProgress { get; set; }

        [JsonPropertyName("target_date")]
        public DateOnly? TargetDate { get; set; }

        [JsonPropertyName("skills")]
        public List<Skill>? Skills { get; set; }
    }
}
