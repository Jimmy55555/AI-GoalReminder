using System.Text.Json;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class GoalAdviceClient(SupabaseClient supabase) : IGoalAdviceClient
{
    public async Task<GoalAdviceResult> GetAdviceAsync(
        IReadOnlyList<Goal> goals,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await supabase.PostFunctionAsync(
                "goal-advice",
                new { goals },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return GoalAdviceResult.Failed("AI 服務暫時無法使用。");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.TryGetProperty("error", out var errorProperty))
            {
                var error = errorProperty.GetString();
                if (!string.IsNullOrWhiteSpace(error))
                {
                    return GoalAdviceResult.Failed(error);
                }
            }

            var advice = new Dictionary<Guid, IReadOnlyList<string>>();
            if (root.TryGetProperty("advice", out var adviceProperty) &&
                adviceProperty.ValueKind == JsonValueKind.Object)
            {
                foreach (var item in adviceProperty.EnumerateObject())
                {
                    if (!Guid.TryParse(item.Name, out var goalId) || item.Value.ValueKind != JsonValueKind.Array)
                    {
                        continue;
                    }

                    var actions = item.Value.EnumerateArray()
                        .Select(element => element.GetString())
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Take(3)
                        .Select(value => value!)
                        .ToArray();

                    if (actions.Length > 0)
                    {
                        advice[goalId] = actions;
                    }
                }
            }

            return GoalAdviceResult.Success(advice);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AI 建議失敗：{ex.Message}");
            return GoalAdviceResult.Failed("AI 服務暫時無法使用。");
        }
    }
}
