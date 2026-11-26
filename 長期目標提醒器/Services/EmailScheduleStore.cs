using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class EmailScheduleStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string filePath;
    private readonly ILogger<EmailScheduleStore> logger;
    private readonly object gate = new();
    private EmailSchedule current;

    public EmailScheduleStore(
        IWebHostEnvironment environment,
        IOptions<EmailSettings> options,
        ILogger<EmailScheduleStore> logger)
    {
        this.logger = logger;

        var folder = Path.Combine(environment.ContentRootPath, "App_Data");
        filePath = Path.Combine(folder, "email-schedule.json");

        var defaults = new EmailSchedule
        {
            Enabled = options.Value.Enabled,
            SendTime = options.Value.SendTime,
            Recipient = options.Value.Recipient
        };

        current = Load(defaults);
    }

    public EmailSchedule Current
    {
        get
        {
            lock (gate)
            {
                return current;
            }
        }
    }

    public event Action? Changed;

    public void Save(EmailSchedule schedule)
    {
        var normalized = Normalize(schedule);

        lock (gate)
        {
            current = normalized;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
                File.WriteAllText(filePath, JsonSerializer.Serialize(normalized, JsonOptions));
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "無法寫入寄信排程設定檔 {Path}", filePath);
                throw;
            }
        }

        logger.LogInformation(
            "寄信排程已更新：{Description}，收件者 {Recipient}，{State}",
            DigestSchedule.Describe(normalized),
            normalized.Recipient,
            normalized.Enabled ? "啟用" : "停用");

        Changed?.Invoke();
    }

    private EmailSchedule Load(EmailSchedule defaults)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return Normalize(defaults);
            }

            var json = File.ReadAllText(filePath);
            var loaded = JsonSerializer.Deserialize<EmailSchedule>(json, JsonOptions);

            return loaded is null ? Normalize(defaults) : Normalize(loaded);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "寄信排程設定檔讀取失敗，改用預設值。");
            return Normalize(defaults);
        }
    }

    private static EmailSchedule Normalize(EmailSchedule schedule)
    {
        return new EmailSchedule
        {
            Enabled = schedule.Enabled,
            SendTime = DigestSchedule.ParseSendTime(schedule.SendTime).ToString(@"hh\:mm", System.Globalization.CultureInfo.InvariantCulture),
            Frequency = schedule.Frequency,
            WeeklyDay = schedule.WeeklyDay,
            MonthlyDay = Math.Clamp(schedule.MonthlyDay, 1, 28),
            Recipient = (schedule.Recipient ?? "").Trim()
        };
    }
}
