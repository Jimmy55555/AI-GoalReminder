using System.Globalization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public static class DigestSchedule
{
    public static TimeSpan ParseSendTime(string? value)
    {
        return TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var time)
            ? time.ToTimeSpan()
            : new TimeSpan(8, 0, 0);
    }

    public static DateTime ComputeNext(EmailSchedule schedule, TimeZoneInfo timeZone, DateTime? fromLocal = null)
    {
        var nowLocal = fromLocal ?? AppTime.NowLocal(timeZone);
        var candidate = nowLocal.Date + ParseSendTime(schedule.SendTime);

        if (candidate <= nowLocal)
        {
            candidate = candidate.AddDays(1);
        }

        switch (schedule.Frequency)
        {
            case EmailFrequency.Weekly:
                while (candidate.DayOfWeek != schedule.WeeklyDay)
                {
                    candidate = candidate.AddDays(1);
                }

                break;

            case EmailFrequency.Monthly:
                var day = Math.Clamp(schedule.MonthlyDay, 1, 28);
                while (candidate.Day != day)
                {
                    candidate = candidate.AddDays(1);
                }

                break;
        }

        return candidate;
    }

    public static TimeSpan DelayUntilNext(EmailSchedule schedule, TimeZoneInfo timeZone)
    {
        var nowLocal = AppTime.NowLocal(timeZone);
        var delay = ComputeNext(schedule, timeZone, nowLocal) - nowLocal;
        return delay > TimeSpan.Zero ? delay : TimeSpan.FromSeconds(1);
    }

    public static string Describe(EmailSchedule schedule)
    {
        var frequency = schedule.Frequency switch
        {
            EmailFrequency.Weekly => $"每週{DescribeDayOfWeek(schedule.WeeklyDay)}",
            EmailFrequency.Monthly => $"每月 {Math.Clamp(schedule.MonthlyDay, 1, 28)} 號",
            _ => "每天"
        };

        return $"{frequency} {ParseSendTime(schedule.SendTime):hh\\:mm}";
    }

    public static string DescribeDayOfWeek(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "一",
        DayOfWeek.Tuesday => "二",
        DayOfWeek.Wednesday => "三",
        DayOfWeek.Thursday => "四",
        DayOfWeek.Friday => "五",
        DayOfWeek.Saturday => "六",
        _ => "日"
    };
}
