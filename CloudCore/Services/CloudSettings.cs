namespace 長期目標提醒器.Services;

public sealed class EmailSettings
{
    public const string SectionName = "Email";

    public string SmtpHost { get; init; } = "smtp.gmail.com";
    public int Port { get; init; } = 587;
    public bool EnableSsl { get; init; } = true;
    public string Sender { get; init; } = "";
    public string Password { get; init; } = "";
    public string Recipient { get; init; } = "";
    public string SendTime { get; init; } = "08:00";
    public string TimeZone { get; init; } = "Taipei Standard Time";
    public bool Enabled { get; init; } = true;
}

public sealed class AiSettings
{
    public const string SectionName = "Ai";

    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gemini-4-hydrogen";
    public int TimeoutSeconds { get; init; } = 15;
}

public sealed class SupabaseSettings
{
    public string Url { get; init; } = "";
    public string PublishableKey { get; init; } = "";
    public string SecretKey { get; init; } = "";
}
