using System.Text.Json.Serialization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// Supabase 版目標儲存。每次登入／重新載入都重抓雲端資料，不留離線資料庫。
/// 所有寫入都先等雲端成功，再更新畫面資料；失敗直接拋給呼叫端顯示錯誤。
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

    public async Task AddGoal(Goal goal)
    {
        await supabase.PostAsync<GoalRow>(Table, ToRow(goal));

        lock (gate)
        {
            goals.Add(goal);
        }

        Changed?.Invoke();
    }

    public async Task SetTargetDate(Guid goalId, DateOnly? targetDate)
    {
        await supabase.PatchAsync<GoalRow>(
            $"{Table}?id=eq.{goalId}",
            new { target_date = targetDate });

        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index >= 0)
            {
                goals[index] = goals[index] with { TargetDate = targetDate };
            }
        }

        Changed?.Invoke();
    }

    public async Task DeleteGoal(Guid goalId)
    {
        await supabase.DeleteAsync($"{Table}?id=eq.{goalId}");

        lock (gate)
        {
            goals.RemoveAll(goal => goal.Id == goalId);
        }

        Changed?.Invoke();
    }

    public async Task AddSkill(Guid goalId, Skill skill)
    {
        Skill[]? updatedSkills = null;

        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            updatedSkills = [.. goals[index].Skills, skill.Normalized()];
        }

        await supabase.PatchAsync<GoalRow>(
            $"{Table}?id=eq.{goalId}",
            new { skills = updatedSkills });

        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index >= 0)
            {
                goals[index] = goals[index] with { Skills = updatedSkills! };
            }
        }

        Changed?.Invoke();
    }

    public async Task UpdateSkill(Guid goalId, Guid skillId, Skill updated)
    {
        Skill[]? updatedSkills = null;

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

            skills[skillIndex] = updated.Normalized() with { Id = skillId };
            updatedSkills = skills.ToArray();
        }

        await supabase.PatchAsync<GoalRow>(
            $"{Table}?id=eq.{goalId}",
            new { skills = updatedSkills });

        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index >= 0)
            {
                goals[index] = goals[index] with { Skills = updatedSkills! };
            }
        }

        Changed?.Invoke();
    }

    public async Task DeleteSkill(Guid goalId, Guid skillId)
    {
        Skill[]? updatedSkills = null;

        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            updatedSkills = goals[index].Skills.Where(skill => skill.Id != skillId).ToArray();
        }

        await supabase.PatchAsync<GoalRow>(
            $"{Table}?id=eq.{goalId}",
            new { skills = updatedSkills });

        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index >= 0)
            {
                goals[index] = goals[index] with { Skills = updatedSkills! };
            }
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
        Skills = (row.Skills ?? []).Select(skill => skill.Normalized()).ToArray()
    };

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
