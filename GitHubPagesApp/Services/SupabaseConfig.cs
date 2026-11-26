namespace 長期目標提醒器.Services;

public sealed class SupabaseConfig
{
    public string Url { get; init; } = "";
    public string AnonKey { get; init; } = "";
    public string Schema { get; init; } = "goal_reminder";
    public string TimeZone { get; init; } = "Taipei Standard Time";
}

public static class ConfigurationExtensions
{
    public static SupabaseConfig GetSupabaseConfig(this IConfiguration configuration) => new()
    {
        Url = (configuration["Supabase:Url"] ?? "").Trim().TrimEnd('/'),
        AnonKey = (configuration["Supabase:AnonKey"] ?? "").Trim(),
        Schema = (configuration["Supabase:Schema"] ?? "goal_reminder").Trim(),
        TimeZone = (configuration["Email:TimeZone"] ?? "Taipei Standard Time").Trim()
    };
}
