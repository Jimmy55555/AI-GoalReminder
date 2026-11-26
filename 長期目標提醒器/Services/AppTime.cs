namespace 長期目標提醒器.Services;

public static class AppTime
{
    public static TimeZoneInfo Resolve(string id)
    {
        foreach (var candidate in new[] { id, "Asia/Taipei" })
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(candidate);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Local;
    }

    public static DateTime NowLocal(TimeZoneInfo timeZone)
    {
        return TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, timeZone).DateTime;
    }
}
