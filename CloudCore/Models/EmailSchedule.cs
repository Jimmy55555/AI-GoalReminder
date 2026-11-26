namespace 長期目標提醒器.Models;

public sealed class EmailSchedule
{
    public bool Enabled { get; set; } = true;
    public string SendTime { get; set; } = "08:00";
    public EmailFrequency Frequency { get; set; } = EmailFrequency.Daily;
    public DayOfWeek WeeklyDay { get; set; } = DayOfWeek.Monday;
    public int MonthlyDay { get; set; } = 1;
    public string Recipient { get; set; } = "";
    public bool AiEnabled { get; set; }
}

public enum EmailFrequency
{
    Daily,
    Weekly,
    Monthly
}
