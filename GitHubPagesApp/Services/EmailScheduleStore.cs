using System.Globalization;
using System.Text.Json.Serialization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class EmailScheduleStore(
    SupabaseClient supabase,
    SessionStore sessionStore)
{
    private const string Table = "rest/v1/goal_reminder_email_schedule";
    private readonly object gate = new();
    private EmailSchedule current = new();

    public event Action? Changed;

    public EmailSchedule Current
    {
        get
        {
            lock (gate)
            {
                return Copy(current);
            }
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        var rows = await supabase.GetAsync<List<ScheduleRow>>($"{Table}?select=*&limit=1", cancellationToken) ?? [];

        if (rows.Count == 0 && !string.IsNullOrWhiteSpace(sessionStore.UserId))
        {
            await PersistAsync(Normalize(new EmailSchedule()), cancellationToken);
            rows = await supabase.GetAsync<List<ScheduleRow>>($"{Table}?select=*&limit=1", cancellationToken) ?? [];
        }

        var loaded = rows.FirstOrDefault();
        lock (gate)
        {
            current = loaded is null ? new EmailSchedule() : ToSchedule(loaded);
        }

        Changed?.Invoke();
    }

    public async Task SaveAsync(EmailSchedule schedule)
    {
        var normalized = Normalize(schedule);
        await PersistAsync(normalized);

        lock (gate)
        {
            current = normalized;
        }

        Changed?.Invoke();
    }

    private async Task PersistAsync(EmailSchedule schedule, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionStore.UserId))
        {
            return;
        }

        await supabase.PostAsync<ScheduleRow>(
            $"{Table}?on_conflict=owner_id",
            new
            {
                owner_id = sessionStore.UserId,
                enabled = schedule.Enabled,
                send_time = schedule.SendTime,
                frequency = schedule.Frequency,
                weekly_day = (int)schedule.WeeklyDay,
                monthly_day = schedule.MonthlyDay,
                recipient = schedule.Recipient,
                ai_enabled = schedule.AiEnabled
            },
            "resolution=merge-duplicates",
            cancellationToken);
    }

    private static EmailSchedule Normalize(EmailSchedule schedule) => new()
    {
        Enabled = schedule.Enabled,
        SendTime = DigestSchedule.ParseSendTime(schedule.SendTime).ToString(@"hh\:mm", CultureInfo.InvariantCulture),
        Frequency = schedule.Frequency,
        WeeklyDay = schedule.WeeklyDay,
        MonthlyDay = Math.Clamp(schedule.MonthlyDay, 1, 28),
        Recipient = (schedule.Recipient ?? "").Trim(),
        AiEnabled = schedule.AiEnabled
    };

    private static EmailSchedule Copy(EmailSchedule source) => new()
    {
        Enabled = source.Enabled,
        SendTime = source.SendTime,
        Frequency = source.Frequency,
        WeeklyDay = source.WeeklyDay,
        MonthlyDay = source.MonthlyDay,
        Recipient = source.Recipient,
        AiEnabled = source.AiEnabled
    };

    private static EmailSchedule ToSchedule(ScheduleRow row) => Normalize(new EmailSchedule
    {
        Enabled = row.Enabled,
        SendTime = row.SendTime,
        Frequency = row.Frequency,
        WeeklyDay = (DayOfWeek)Math.Clamp(row.WeeklyDay, 0, 6),
        MonthlyDay = row.MonthlyDay,
        Recipient = row.Recipient,
        AiEnabled = row.AiEnabled
    });

    private sealed class ScheduleRow
    {
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; } = true;

        [JsonPropertyName("send_time")]
        public string SendTime { get; set; } = "08:00";

        [JsonPropertyName("frequency")]
        public EmailFrequency Frequency { get; set; } = EmailFrequency.Daily;

        [JsonPropertyName("weekly_day")]
        public int WeeklyDay { get; set; } = 1;

        [JsonPropertyName("monthly_day")]
        public int MonthlyDay { get; set; } = 1;

        [JsonPropertyName("recipient")]
        public string Recipient { get; set; } = "";

        [JsonPropertyName("ai_enabled")]
        public bool AiEnabled { get; set; }
    }
}
