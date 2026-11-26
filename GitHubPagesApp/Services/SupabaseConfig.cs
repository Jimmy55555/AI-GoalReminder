namespace 長期目標提醒器.Services;

public sealed class SupabaseConfig
{
    public string Url { get; init; } = "";
    public string PublishableKey { get; init; } = "";
    public string TimeZone { get; init; } = "Taipei Standard Time";
}

public static class ConfigurationExtensions
{
    public static SupabaseConfig GetSupabaseConfig(this IConfiguration configuration) => new()
    {
        Url = (configuration["Supabase:Url"] ?? "").Trim().TrimEnd('/'),
        PublishableKey = (configuration["Supabase:PublishableKey"] ?? "").Trim(),
        TimeZone = (configuration["Email:TimeZone"] ?? "Taipei Standard Time").Trim()
    };
}
