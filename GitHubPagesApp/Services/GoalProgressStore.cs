using System.Text.Json.Serialization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class GoalProgressStore(
    SupabaseClient supabase,
    SessionStore sessionStore)
{
    private const string Table = "rest/v1/goal_reminder_progress";
    private readonly object gate = new();
    private readonly Dictionary<Guid, bool> flags = [];

    public event Action? Changed;

    public bool IsInProgress(Guid goalId)
    {
        lock (gate)
        {
            return flags.TryGetValue(goalId, out var value) ? value : Default(goalId);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var rows = await supabase.GetAsync<List<ProgressRow>>($"{Table}?select=*", cancellationToken) ?? [];

        lock (gate)
        {
            flags.Clear();
            foreach (var row in rows)
            {
                flags[row.GoalId] = row.IsInProgress;
            }
        }

        Changed?.Invoke();
    }

    public async Task SetInProgress(Guid goalId, bool value)
    {
        await supabase.PostAsync<ProgressRow>(
            $"{Table}?on_conflict=goal_id",
            new { goal_id = goalId, owner_id = sessionStore.UserId, is_in_progress = value },
            "resolution=merge-duplicates");

        lock (gate)
        {
            flags[goalId] = value;
        }

        Changed?.Invoke();
    }

    public async Task Remove(Guid goalId)
    {
        await supabase.DeleteAsync($"{Table}?goal_id=eq.{goalId}");

        lock (gate)
        {
            flags.Remove(goalId);
        }

        Changed?.Invoke();
    }

    private static bool Default(Guid goalId) =>
        GoalSeedData.Goals.FirstOrDefault(goal => goal.Id == goalId)?.IsInProgress ?? false;

    private sealed class ProgressRow
    {
        [JsonPropertyName("goal_id")]
        public Guid GoalId { get; set; }

        [JsonPropertyName("is_in_progress")]
        public bool IsInProgress { get; set; }
    }
}
