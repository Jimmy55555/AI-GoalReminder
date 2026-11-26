using System.Globalization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public static class DigestSchedule
{
    public static TimeSpan ParseSendTime(string? value) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var time)
            ? time.ToTimeSpan()
            : new TimeSpan(8, 0, 0);

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
                candidate = new DateTime(candidate.Year, candidate.Month, day) + ParseSendTime(schedule.SendTime);
                if (candidate <= nowLocal)
                {
                    candidate = new DateTime(candidate.Year, candidate.Month, day).AddMonths(1) + ParseSendTime(schedule.SendTime);
                }

                break;
        }

        return candidate;
    }

    public static bool IsDue(EmailSchedule schedule, DateTime localNow)
    {
        if (!schedule.Enabled)
        {
            return false;
        }

        return schedule.Frequency switch
        {
            EmailFrequency.Weekly => localNow.DayOfWeek == schedule.WeeklyDay,
            EmailFrequency.Monthly => localNow.Day == Math.Clamp(schedule.MonthlyDay, 1, 28),
            _ => true
        };
    }

    public static string Describe(EmailSchedule schedule) => schedule.Frequency switch
    {
        EmailFrequency.Weekly => $"每週 {WeekDayText(schedule.WeeklyDay)} {schedule.SendTime}",
        EmailFrequency.Monthly => $"每月 {Math.Clamp(schedule.MonthlyDay, 1, 28)} 日 {schedule.SendTime}",
        _ => $"每天 {schedule.SendTime}"
    };

    private static string WeekDayText(DayOfWeek day) => day switch
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
