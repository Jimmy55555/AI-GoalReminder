using System.Text.Json;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class GoalSkillGenerator(SupabaseClient supabase) : IGoalSkillGenerator
{
    public async Task<SkillGenerationResult> GenerateAsync(
        string title,
        string description,
        DateOnly? targetDate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await supabase.PostFunctionAsync(
                "generate-skills",
                new { title, description, targetDate },
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
                    return SkillGenerationResult.Failed(
                        error ?? $"AI 服務回傳 {(int)response.StatusCode}，請稍後再試。");
                }

                if (!string.IsNullOrWhiteSpace(error))
                {
                    return SkillGenerationResult.Failed(error);
                }

                if (!root.TryGetProperty("skills", out var skillsProperty))
                {
                    return SkillGenerationResult.Failed("AI 回應格式不正確，請稍後再試。");
                }

                var skills = skillsProperty.Deserialize<List<Skill>>(SupabaseClient.JsonOptions) ?? [];
                var normalized = skills
                    .Where(skill => !string.IsNullOrWhiteSpace(skill.Name))
                    .Select(skill => skill with { Id = skill.Id == Guid.Empty ? Guid.NewGuid() : skill.Id })
                    .Take(8)
                    .ToArray();

                if (normalized.Length == 0)
                {
                    return SkillGenerationResult.Failed("AI 沒有產生技能，請直接手動新增。");
                }

                return SkillGenerationResult.Success(normalized);
            }
            catch (JsonException)
            {
                return SkillGenerationResult.Failed(!response.IsSuccessStatusCode
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
            return SkillGenerationResult.Failed("無法連線到 AI 服務，請檢查網路連線。");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AI 技能生成失敗：{ex.Message}");
            return SkillGenerationResult.Failed("AI 服務暫時無法使用，請稍後再試。");
        }
    }
}
