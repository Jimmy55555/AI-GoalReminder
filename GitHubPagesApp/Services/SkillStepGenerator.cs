using System.Text.Json;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// 呼叫 generate-steps Edge Function，把單一技能拆成最小實際步驟。
/// 不限制步驟數量；只回傳步驟文字，由前端產生 Id 與完成狀態。
/// </summary>
public sealed class SkillStepGenerator(SupabaseClient supabase) : ISkillStepGenerator
{
    public async Task<StepGenerationResult> GenerateAsync(
        string goalTitle,
        string goalDescription,
        DateOnly? targetDate,
        string skillName,
        string skillNote,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await supabase.PostFunctionAsync(
                "generate-steps",
                new
                {
                    title = goalTitle,
                    description = goalDescription,
                    targetDate,
                    skillName,
                    note = skillNote
                },
                cancellationToken);

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            string? error = null;
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                if (root.TryGetProperty("error", out var errorProperty))
                {
                    error = errorProperty.GetString();
                }

                if (!response.IsSuccessStatusCode)
                {
                    return StepGenerationResult.Failed(
                        error ?? $"AI 服務回傳 {(int)response.StatusCode}，請稍後再試。");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    return StepGenerationResult.Failed(error);
                }

                if (!root.TryGetProperty("steps", out var stepsProperty))
                {
                    return StepGenerationResult.Failed("AI 回應格式不正確，請稍後再試。");
                }

                var titles = stepsProperty.EnumerateArray()
                    .Select(item => item.ValueKind == JsonValueKind.String
                        ? item.GetString()
                        : item.TryGetProperty("title", out var titleProperty) ? titleProperty.GetString() : null)
                    .Where(title => !string.IsNullOrWhiteSpace(title))
                    .Select(title => title!.Trim())
                    .ToArray();

                if (titles.Length == 0)
                {
                    return StepGenerationResult.Failed("AI 沒有產生步驟，請手動新增。");
                }

                return StepGenerationResult.Success(titles);
            }
            catch (JsonException)
            {
                return StepGenerationResult.Failed(!response.IsSuccessStatusCode
                    ? $"AI 服務回傳 {(int)response.StatusCode}，請稍後再試。"
                    : "AI 回應格式不正確，請稍後再試。");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException)
        {
            return StepGenerationResult.Failed("無法連線到 AI 服務，請檢查網路連線。");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AI 步驟生成失敗：{ex.Message}");
            return StepGenerationResult.Failed("AI 服務暫時無法使用，請稍後再試。");
        }
    }
}
