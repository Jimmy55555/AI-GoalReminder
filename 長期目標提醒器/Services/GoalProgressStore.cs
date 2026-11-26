using System.Text.Json;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// 記錄哪些目標正在進行中。只有進行中的目標需要 AI 行動建議。
/// </summary>
public sealed class GoalProgressStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly string filePath;
    private readonly ILogger<GoalProgressStore> logger;
    private readonly object gate = new();
    private readonly Dictionary<Guid, bool> flags;

    public GoalProgressStore(
        IWebHostEnvironment environment,
        ILogger<GoalProgressStore> logger)
    {
        this.logger = logger;

        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        filePath = Path.Combine(folder, "goal-progress.json");
        flags = Load();
    }

    public event Action? Changed;

    public bool IsInProgress(Guid goalId)
    {
        lock (gate)
        {
            return flags.TryGetValue(goalId, out var value) ? value : Default(goalId);
        }
    }

    public void SetInProgress(Guid goalId, bool value)
    {
        lock (gate)
        {
            flags[goalId] = value;
            SaveLocked();
        }

        logger.LogInformation("目標 {GoalId} 進行狀態更新為 {State}", goalId, value ? "進行中" : "未進行");
        Changed?.Invoke();
    }

    public void Remove(Guid goalId)
    {
        lock (gate)
        {
            if (!flags.Remove(goalId))
            {
                return;
            }

            SaveLocked();
        }

        logger.LogInformation("移除目標 {GoalId} 的進行狀態", goalId);
        Changed?.Invoke();
    }

    private void SaveLocked()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
            File.WriteAllText(filePath, JsonSerializer.Serialize(flags, JsonOptions));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "無法寫入目標進行狀態 {Path}", filePath);
            throw;
        }
    }

    private static bool Default(Guid goalId) =>
        GoalSeedData.Goals.FirstOrDefault(goal => goal.Id == goalId)?.IsInProgress ?? false;

    private Dictionary<Guid, bool> Load()
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return [];
            }

            var json = File.ReadAllText(filePath);
            return JsonSerializer.Deserialize<Dictionary<Guid, bool>>(json, JsonOptions) ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "目標進行狀態讀取失敗，改用預設值。");
            return [];
        }
    }
}
