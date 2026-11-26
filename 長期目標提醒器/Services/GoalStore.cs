using System.Text.Json;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// 使用者可增刪的目標與技能。資料存放在 App_Data/goals.json，首次啟動由 GoalSeedData 初始化。
/// </summary>
public sealed class GoalStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly ILogger<GoalStore> logger;
    private readonly object gate = new();
    private readonly List<Goal> goals;

    public GoalStore(IWebHostEnvironment environment, ILogger<GoalStore> logger)
    {
        this.logger = logger;

        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        filePath = Path.Combine(folder, "goals.json");

        var loaded = Load();
        if (loaded is null)
        {
            goals = GoalSeedData.Goals.ToList();
            SaveLocked();
        }
        else
        {
            goals = loaded;
        }
    }

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

    public void AddGoal(Goal goal)
    {
        lock (gate)
        {
            goals.Add(goal);
            SaveLocked();
        }

        logger.LogInformation("新增目標 {Title}", goal.Title);
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
            SaveLocked();
        }

        logger.LogInformation("目標 {GoalId} 日期更新為 {TargetDate}", goalId, targetDate?.ToString("yyyy-MM-dd") ?? "未設定");
        Changed?.Invoke();
    }

    public void DeleteGoal(Guid goalId)
    {
        string? title;
        lock (gate)
        {
            var goal = goals.FirstOrDefault(goal => goal.Id == goalId);
            if (goal is null)
            {
                return;
            }

            title = goal.Title;
            goals.Remove(goal);
            SaveLocked();
        }

        logger.LogInformation("刪除目標 {Title}", title);
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

            goals[index] = goals[index] with { Skills = [.. goals[index].Skills, skill] };
            SaveLocked();
        }

        logger.LogInformation("目標 {GoalId} 新增技能 {Name}", goalId, skill.Name);
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

            var skillIndex = goals[index].Skills.ToList().FindIndex(skill => skill.Id == skillId);
            if (skillIndex < 0)
            {
                return;
            }

            var skills = goals[index].Skills.ToList();
            skills[skillIndex] = updated with { Id = skillId };
            goals[index] = goals[index] with { Skills = skills };
            SaveLocked();
        }

        logger.LogInformation("目標 {GoalId} 更新技能 {Name}", goalId, updated.Name);
        Changed?.Invoke();
    }

    public void DeleteSkill(Guid goalId, Guid skillId)
    {
        string? name;
        lock (gate)
        {
            var index = goals.FindIndex(goal => goal.Id == goalId);
            if (index < 0)
            {
                return;
            }

            var skill = goals[index].Skills.FirstOrDefault(skill => skill.Id == skillId);
            if (skill is null)
            {
                return;
            }

            name = skill.Name;
            goals[index] = goals[index] with
            {
                Skills = goals[index].Skills.Where(skill => skill.Id != skillId).ToArray()
            };
            SaveLocked();
        }

        logger.LogInformation("目標 {GoalId} 刪除技能 {Name}", goalId, name);
        Changed?.Invoke();
    }

    private void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            var temporary = filePath + ".tmp";
            File.WriteAllText(temporary, JsonSerializer.Serialize(goals, JsonOptions));
            File.Move(temporary, filePath, true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "無法寫入目標資料 {Path}", filePath);
            throw;
        }
    }

    private List<Goal>? Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<List<Goal>>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "目標資料讀取失敗，改用預設目標。");
            return null;
        }
    }
}
