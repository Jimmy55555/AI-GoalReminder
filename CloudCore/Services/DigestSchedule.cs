using System.Globalization;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public static class DigestSchedule
{
    // GitHub Actions 每 30 分鐘跑一次，所以實際寄送時間落在 :00 或 :30。
    private static TimeSpan CheckTime(TimeSpan sendTime) =>
        new(sendTime.Hours, sendTime.Minutes < 30 ? 0 : 30, 0);

    public static TimeSpan ParseSendTime(string? value) =>
        TimeOnly.TryParse(value, CultureInfo.InvariantCulture, out var time)
            ? time.ToTimeSpan()
            : new TimeSpan(8, 0, 0);

    public static DateTime ComputeNext(EmailSchedule schedule, TimeZoneInfo timeZone, DateTime? fromLocal = null)
    {
        var nowLocal = fromLocal ?? AppTime.NowLocal(timeZone);
        var sendTime = CheckTime(ParseSendTime(schedule.SendTime));
        var candidate = nowLocal.Date + sendTime;

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
                candidate = new DateTime(candidate.Year, candidate.Month, day) + sendTime;
                if (candidate <= nowLocal)
                {
                    candidate = new DateTime(candidate.Year, candidate.Month, day).AddMonths(1) + sendTime;
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

        var dateOk = schedule.Frequency switch
        {
            EmailFrequency.Weekly => localNow.DayOfWeek == schedule.WeeklyDay,
            EmailFrequency.Monthly => localNow.Day == Math.Clamp(schedule.MonthlyDay, 1, 28),
            _ => true
        };

        if (!dateOk)
        {
            return false;
        }

        var sendTime = CheckTime(ParseSendTime(schedule.SendTime));
        return localNow.Hour == sendTime.Hours && localNow.Minute == sendTime.Minutes;
    }

    public static string Describe(EmailSchedule schedule) => schedule.Frequency switch
    {
        EmailFrequency.Weekly => $"每週 {WeekDayText(schedule.WeeklyDay)} {CheckTime(ParseSendTime(schedule.SendTime)):hh\\:mm}",
        EmailFrequency.Monthly => $"每月 {Math.Clamp(schedule.MonthlyDay, 1, 28)} 日 {CheckTime(ParseSendTime(schedule.SendTime)):hh\\:mm}",
        _ => $"每天 {CheckTime(ParseSendTime(schedule.SendTime)):hh\\:mm}"
    };

    private static string WeekDayText(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "一",
        DayOfWeek.Tuesday => "二",
        DayOfWeek.Wednesday => "三",
        DayOfWeek.Thursday => "四",
        DayOfWeek.Friday => "五",
        DayOfWeek.Saturday => "六",
        DayOfWeek.Sunday => "日",
        _ => "日"
    };
}
