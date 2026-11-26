using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Options;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

public sealed class EmailService(IOptions<EmailSettings> options, ILogger<EmailService> logger)
{
    private readonly EmailSettings settings = options.Value;
    private readonly HtmlEncoder encoder = HtmlEncoder.Default;

    public bool IsConfigured(EmailSchedule schedule) =>
        !string.IsNullOrWhiteSpace(settings.Sender)
        && !string.IsNullOrWhiteSpace(settings.Password)
        && !string.IsNullOrWhiteSpace(schedule.Recipient);

    public async Task SendGoalDigestAsync(IReadOnlyList<Goal> goals, EmailSchedule schedule, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(schedule))
        {
            logger.LogWarning("寄信設定不完整，略過目標摘要信。");
            return;
        }

        var timeZone = AppTime.Resolve(settings.TimeZone);
        var today = AppTime.NowLocal(timeZone);

        using var message = new MailMessage
        {
            From = new MailAddress(settings.Sender, "長期目標提醒器"),
            Subject = $"【目標摘要】{today:yyyy/MM/dd} · {goals.Count} 個目標",
            SubjectEncoding = Encoding.UTF8,
            Body = BuildBody(goals, today, schedule),
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = true
        };
        message.To.Add(schedule.Recipient);

        using var client = new SmtpClient(settings.SmtpHost, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Credentials = new NetworkCredential(settings.Sender, settings.Password)
        };

        await client.SendMailAsync(message, cancellationToken);
        logger.LogInformation("目標摘要信已寄出至 {Recipient}", schedule.Recipient);
    }

    internal string BuildBody(IReadOnlyList<Goal> goals, DateTime today, EmailSchedule schedule)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"zh-Hant\"><head><meta charset=\"utf-8\" /></head>");
        html.AppendLine("<body style=\"margin:0;padding:24px 12px;background:#eef2f7;font-family:'Segoe UI','Microsoft JhengHei',-apple-system,sans-serif;color:#0f172a;\">");
        html.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"max-width:600px;margin:0 auto;border-collapse:separate;\">");

        // Header
        html.AppendLine("<tr><td style=\"background:#4f46e5;border-radius:16px 16px 0 0;padding:24px 28px;\">");
        html.AppendLine("<div style=\"font-size:11px;letter-spacing:2px;color:#c7d2fe;text-transform:uppercase;\">Daily Digest</div>");
        html.AppendLine("<div style=\"font-size:22px;font-weight:700;color:#ffffff;margin-top:6px;\">長期目標提醒器</div>");
        html.AppendLine($"<div style=\"font-size:13px;color:#e0e7ff;margin-top:8px;\">{today:yyyy/MM/dd} · 共 {goals.Count} 個目標</div>");
        html.AppendLine("</td></tr>");

        // Cards
        html.AppendLine("<tr><td style=\"background:#ffffff;border-radius:0 0 16px 16px;padding:20px 20px 8px;\">");

        foreach (var goal in goals)
        {
            var title = encoder.Encode(goal.Title);
            var icon = encoder.Encode(goal.Icon);
            var progress = goal.ProgressPercent;
            var remaining = goal.DaysRemaining >= 0
                ? $"剩餘 <strong style=\"color:#0f172a;\">{goal.DaysRemaining}</strong> 天"
                : $"已超過 <strong style=\"color:#b91c1c;\">{-goal.DaysRemaining}</strong> 天";

            html.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"border:1px solid #e2e8f0;border-radius:12px;margin-bottom:12px;border-collapse:separate;\">");
            html.AppendLine("<tr><td style=\"padding:16px 16px 14px;\">");
            html.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>");
            html.AppendLine($"<td style=\"font-size:15px;font-weight:600;\">{icon} {title}</td>");
            html.AppendLine($"<td align=\"right\" style=\"font-size:19px;font-weight:700;color:#4f46e5;white-space:nowrap;\">{progress}%</td>");
            html.AppendLine("</tr></table>");

            html.AppendLine("<div style=\"background:#e2e8f0;border-radius:999px;height:8px;margin:12px 0 12px;\">");
            html.AppendLine($"<div style=\"width:{progress}%;height:8px;border-radius:999px;background:#4f46e5;font-size:0;line-height:0;\">&nbsp;</div>");
            html.AppendLine("</div>");

            html.AppendLine($"<div style=\"font-size:12px;color:#64748b;line-height:1.7;\">{remaining} · 已掌握 <strong style=\"color:#047857;\">{goal.MasteredCount}</strong> · 學習中 <strong style=\"color:#b45309;\">{goal.LearningCount}</strong> · 未開始 <strong style=\"color:#64748b;\">{goal.NotStartedCount}</strong></div>");
            html.AppendLine("</td></tr></table>");
        }

        html.AppendLine("<div style=\"font-size:11px;color:#94a3b8;line-height:1.7;padding:8px 4px 16px;\">");
        html.AppendLine($"這封信由「長期目標提醒器」{encoder.Encode(DigestSchedule.Describe(schedule))} 自動寄出。");
        html.AppendLine("</div>");
        html.AppendLine("</td></tr></table>");
        html.AppendLine("</body></html>");

        return html.ToString();
    }
}
