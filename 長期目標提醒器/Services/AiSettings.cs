namespace 長期目標提醒器.Services;

public sealed class AiSettings
{
    public const string SectionName = "Ai";

    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gemini-4-hydrogen";
    public int TimeoutSeconds { get; init; } = 8;
}
