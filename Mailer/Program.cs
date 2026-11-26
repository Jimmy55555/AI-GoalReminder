using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using 長期目標提醒器.Models;
using 長期目標提醒器.Services;

var mode = args.FirstOrDefault() ?? "digest";

try
{
    var url = Require("SUPABASE_URL").TrimEnd('/');
    var serviceKey = Require("SUPABASE_SERVICE_ROLE_KEY");

    using var httpClient = new HttpClient { BaseAddress = new Uri(url) };
    var supabase = new SupabaseClient(httpClient, serviceKey);

    if (mode.Equals("import", StringComparison.OrdinalIgnoreCase))
    {
        await ImportAsync(supabase, args.ElementAtOrDefault(1) ?? "App_Data");
        return;
    }

    await SendDailyDigestsAsync(supabase);
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex);
    Environment.ExitCode = 1;
}

static string Require(string name, string? fallback = null)
{
    var value = Environment.GetEnvironmentVariable(name);
    if (string.IsNullOrWhiteSpace(value))
    {
        value = fallback;
    }

    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"缺少環境變數 {name}。");
    }

    return value;
}

static async Task ImportAsync(SupabaseClient supabase, string folder)
{
    var ownerId = Require("SUPABASE_USER_ID");
    var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    var goalsPath = Path.Combine(folder, "goals.json");
    var progressPath = Path.Combine(folder, "goal-progress.json");
    var schedulePath = Path.Combine(folder, "email-schedule.json");

    IReadOnlyList<Goal> goals = File.Exists(goalsPath)
        ? JsonSerializer.Deserialize<List<Goal>>(await File.ReadAllTextAsync(goalsPath), options) ?? []
        : GoalSeedData.Goals;

    var flags = File.Exists(progressPath)
        ? JsonSerializer.Deserialize<Dictionary<Guid, bool>>(await File.ReadAllTextAsync(progressPath), options) ?? []
        : GoalSeedData.Goals.ToDictionary(goal => goal.Id, goal => goal.IsInProgress);

    var schedule = File.Exists(schedulePath)
        ? JsonSerializer.Deserialize<EmailSchedule>(await File.ReadAllTextAsync(schedulePath), options) ?? new()
        : new EmailSchedule();

    var goalRows = goals.Select(goal => new
    {
        id = goal.Id,
        owner_id = ownerId,
        title = goal.Title,
        description = goal.Description,
        is_in_progress = goal.IsInProgress,
        target_date = goal.TargetDate,
        skills = goal.Skills
    }).ToArray();

    await supabase.PostAsync<object>(
        "rest/v1/goal_reminder_goals?on_conflict=id",
        goalRows,
        "resolution=merge-duplicates");

    var progressRows = goals.Select(goal => new
    {
        goal_id = goal.Id,
        owner_id = ownerId,
        is_in_progress = flags.GetValueOrDefault(goal.Id, goal.IsInProgress)
    }).ToArray();

    await supabase.PostAsync<object>(
        "rest/v1/goal_reminder_progress?on_conflict=goal_id",
        progressRows,
        "resolution=merge-duplicates");

    await supabase.PostAsync<object>(
        "rest/v1/goal_reminder_email_schedule?on_conflict=owner_id",
        new
        {
            owner_id = ownerId,
            enabled = schedule.Enabled,
            send_time = schedule.SendTime,
            frequency = schedule.Frequency,
            weekly_day = (int)schedule.WeeklyDay,
            monthly_day = schedule.MonthlyDay,
            recipient = schedule.Recipient,
            ai_enabled = schedule.AiEnabled
        },
        "resolution=merge-duplicates");

    Console.WriteLine($"已匯入 {goals.Count} 個目標、{progressRows.Length} 筆進行狀態與寄信設定。");
}

static async Task SendDailyDigestsAsync(SupabaseClient supabase)
{
    var timeZone = AppTime.Resolve(Require("EMAIL_TIME_ZONE", "Taipei Standard Time"));
    var localNow = AppTime.NowLocal(timeZone);
    var today = DateOnly.FromDateTime(localNow);

    var goals = await supabase.GetAsync<List<GoalRow>>("rest/v1/goal_reminder_goals?select=*") ?? [];
    var progress = await supabase.GetAsync<List<ProgressRow>>("rest/v1/goal_reminder_progress?select=*") ?? [];
    var schedules = await supabase.GetAsync<List<ScheduleRow>>("rest/v1/goal_reminder_email_schedule?select=*") ?? [];
    var sent = await supabase.GetAsync<List<SendLogRow>>(
        $"rest/v1/goal_reminder_email_send_log?select=*&sent_on=eq.{today:yyyy-MM-dd}") ?? [];

    var gemini = new GeminiClient(
        new HttpClient { BaseAddress = new Uri("https://generativelanguage.googleapis.com/v1beta/") },
        message => Console.Error.WriteLine(message));

    var sender = Require("SMTP_USER");
    var password = Require("SMTP_PASSWORD");
    var host = Require("SMTP_HOST", "smtp.gmail.com");
    var port = int.TryParse(Environment.GetEnvironmentVariable("SMTP_PORT"), out var parsedPort) ? parsedPort : 587;
    var model = Require("GEMINI_MODEL", "gemini-4-hydrogen");
    var timeout = int.TryParse(Environment.GetEnvironmentVariable("GEMINI_TIMEOUT_SECONDS"), out var parsedTimeout)
        ? parsedTimeout
        : 20;
    var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "";

    foreach (var scheduleRow in schedules)
    {
        var ownerId = scheduleRow.OwnerId;
        var schedule = ToSchedule(scheduleRow);

        if (!DigestSchedule.IsDue(schedule, localNow))
        {
            Console.WriteLine($"略過 {ownerId}：今天不是寄送日。");
            continue;
        }

        if (sent.Any(row => row.OwnerId == ownerId && row.SentOn == today))
        {
            Console.WriteLine($"略過 {ownerId}：今天已寄出。");
            continue;
        }

        if (string.IsNullOrWhiteSpace(schedule.Recipient))
        {
            Console.WriteLine($"略過 {ownerId}：尚未設定收件者。");
            continue;
        }

        var ownerGoals = goals
            .Where(row => row.OwnerId == ownerId)
            .Select(ToGoal)
            .ToArray();

        if (ownerGoals.Length == 0)
        {
            Console.WriteLine($"略過 {ownerId}：沒有目標。");
            continue;
        }

        var ownerProgress = progress
            .Where(row => row.OwnerId == ownerId)
            .ToDictionary(row => row.GoalId, row => row.IsInProgress);

        var goalsForAdvice = ownerGoals
            .Select(goal => goal with { IsInProgress = ownerProgress.GetValueOrDefault(goal.Id, goal.IsInProgress) })
            .ToArray();

        var advice = schedule.AiEnabled
            ? await gemini.GetAdviceAsync(goalsForAdvice, apiKey, model, timeout)
            : GoalAdviceResult.Disabled();

        var body = DigestEmailHtml.BuildBody(
            ownerGoals,
            localNow,
            schedule,
            advice,
            goalId => ownerProgress.GetValueOrDefault(goalId, false));

        using var message = new MailMessage
        {
            From = new MailAddress(sender, "長期目標提醒器"),
            Subject = $"【目標摘要】{localNow:yyyy/MM/dd} · {ownerGoals.Length} 個目標",
            SubjectEncoding = Encoding.UTF8,
            Body = body,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(schedule.Recipient);

        using var smtp = new SmtpClient(host, port)
        {
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(sender, password),
            Timeout = 30_000
        };
        await smtp.SendMailAsync(message);

        await supabase.PostAsync<object>(
            "rest/v1/goal_reminder_email_send_log?on_conflict=owner_id,sent_on",
            new { owner_id = ownerId, sent_on = today },
            "resolution=merge-duplicates");

        Console.WriteLine($"已寄出摘要信至 {schedule.Recipient}。");
    }
}

static EmailSchedule ToSchedule(ScheduleRow row)
{
    var schedule = new EmailSchedule
    {
        Enabled = row.Enabled,
        SendTime = row.SendTime,
        Frequency = row.Frequency,
        WeeklyDay = (DayOfWeek)Math.Clamp(row.WeeklyDay, 0, 6),
        MonthlyDay = row.MonthlyDay,
        Recipient = row.Recipient,
        AiEnabled = row.AiEnabled
    };

    schedule.SendTime = DigestSchedule.ParseSendTime(schedule.SendTime)
        .ToString(@"hh\:mm", CultureInfo.InvariantCulture);
    schedule.MonthlyDay = Math.Clamp(schedule.MonthlyDay, 1, 28);
    schedule.Recipient = (schedule.Recipient ?? "").Trim();
    return schedule;
}

static Goal ToGoal(GoalRow row) => new()
{
    Id = row.Id,
    Title = row.Title,
    Description = row.Description,
    IsInProgress = row.IsInProgress,
    TargetDate = row.TargetDate,
    Skills = row.Skills ?? []
};

internal sealed class GoalRow
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = "";

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

internal sealed class ProgressRow
{
    [JsonPropertyName("goal_id")]
    public Guid GoalId { get; set; }

    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = "";

    [JsonPropertyName("is_in_progress")]
    public bool IsInProgress { get; set; }
}

internal sealed class ScheduleRow
{
    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = "";

    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [JsonPropertyName("send_time")]
    public string SendTime { get; set; } = "08:00";

    [JsonPropertyName("frequency")]
    public EmailFrequency Frequency { get; set; }

    [JsonPropertyName("weekly_day")]
    public int WeeklyDay { get; set; }

    [JsonPropertyName("monthly_day")]
    public int MonthlyDay { get; set; }

    [JsonPropertyName("recipient")]
    public string Recipient { get; set; } = "";

    [JsonPropertyName("ai_enabled")]
    public bool AiEnabled { get; set; }
}

internal sealed class SendLogRow
{
    [JsonPropertyName("owner_id")]
    public string OwnerId { get; set; } = "";

    [JsonPropertyName("sent_on")]
    public DateOnly SentOn { get; set; }
}
