using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// WASM 版不直接連 SMTP；測試信透過 Supabase Edge Function 用 HTTPS 寄出。
/// </summary>
public sealed class EmailService(SupabaseClient supabase)
{
    public bool IsConfigured(EmailSchedule schedule) =>
        !string.IsNullOrWhiteSpace(schedule.Recipient);

    public async Task SendGoalDigestAsync(
        IReadOnlyList<Goal> goals,
        EmailSchedule schedule,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(schedule))
        {
            throw new SupabaseException("請先設定收件者。");
        }

        using var response = await supabase.PostFunctionAsync(
            "send-test-digest",
            new { recipient = schedule.Recipient },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new SupabaseException(
                string.IsNullOrWhiteSpace(body) ? "測試信寄送失敗。" : Truncate(body, 200),
                response.StatusCode);
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
