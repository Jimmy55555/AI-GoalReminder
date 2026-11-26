using System.Text;
using System.Text.Encodings.Web;
using 長期目標提醒器.Models;

namespace 長期目標提醒器.Services;

/// <summary>
/// 信件固定使用淺色 HTML（深色模式不影響信件）。WASM 測試信與 Mailer 共用同一份排版。
/// </summary>
public static class DigestEmailHtml
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static string BuildBody(
        IReadOnlyList<Goal> goals,
        DateTime today,
        EmailSchedule schedule,
        GoalAdviceResult? advice = null,
        Func<Guid, bool>? isInProgress = null)
    {
        var html = new StringBuilder();

        html.AppendLine("<!DOCTYPE html>");
        html.AppendLine("<html lang=\"zh-Hant\"><head><meta charset=\"utf-8\" /></head>");
        html.AppendLine("<body style=\"margin:0;padding:24px 12px;background:#eef2f7;font-family:'Segoe UI','Microsoft JhengHei',-apple-system,sans-serif;color:#0f172a;\">");
        html.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"max-width:600px;margin:0 auto;border-collapse:separate;\">");

        html.AppendLine("<tr><td style=\"background:#4f46e5;border-radius:16px 16px 0 0;padding:24px 28px;\">");
        html.AppendLine("<div style=\"font-size:11px;letter-spacing:2px;color:#c7d2fe;text-transform:uppercase;\">Daily Digest</div>");
        html.AppendLine("<div style=\"font-size:22px;font-weight:700;color:#ffffff;margin-top:6px;\">長期目標提醒器</div>");
        html.AppendLine($"<div style=\"font-size:13px;color:#e0e7ff;margin-top:8px;\">{today:yyyy/MM/dd} · 共 {goals.Count} 個目標</div>");
        html.AppendLine("</td></tr>");

        if (!string.IsNullOrWhiteSpace(advice?.Error))
        {
            html.AppendLine("<tr><td style=\"background:#fffbeb;padding:12px 28px;color:#92400e;font-size:12px;line-height:1.7;\">");
            html.AppendLine($"⚠️ 進行中目標的 AI 行動建議暫時無法產生（{Encoder.Encode(advice.Error)}），其餘摘要內容照常提供。");
            html.AppendLine("</td></tr>");
        }

        html.AppendLine("<tr><td style=\"background:#ffffff;border-radius:0 0 16px 16px;padding:20px 20px 8px;\">");

        foreach (var goal in goals)
        {
            var title = Encoder.Encode(goal.Title);
            var progress = goal.ProgressPercent;
            var inProgress = isInProgress?.Invoke(goal.Id) ?? goal.IsInProgress;
            var statusBadge = inProgress
                ? "<span style=\"margin-left:6px;padding:1px 6px;border-radius:999px;background:#ecfdf5;color:#047857;font-size:10px;font-weight:600;white-space:nowrap;\">進行中</span>"
                : "<span style=\"margin-left:6px;padding:1px 6px;border-radius:999px;background:#f1f5f9;color:#64748b;font-size:10px;font-weight:600;white-space:nowrap;\">未進行</span>";

            var details = new List<string>();
            if (goal.DaysRemaining is { } days)
            {
                details.Add(days >= 0
                    ? $"剩餘 <strong style=\"color:#0f172a;\">{days}</strong> 天"
                    : $"已超過 <strong style=\"color:#b91c1c;\">{-days}</strong> 天");
            }

            details.Add($"已掌握 <strong style=\"color:#047857;\">{goal.MasteredCount}</strong>");
            details.Add($"學習中 <strong style=\"color:#b45309;\">{goal.LearningCount}</strong>");
            details.Add($"未開始 <strong style=\"color:#64748b;\">{goal.NotStartedCount}</strong>");

            html.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\" style=\"border:1px solid #e2e8f0;border-radius:12px;margin-bottom:12px;border-collapse:separate;\">");
            html.AppendLine("<tr><td style=\"padding:16px 16px 14px;\">");
            html.AppendLine("<table role=\"presentation\" cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tr>");
            html.AppendLine($"<td style=\"font-size:15px;font-weight:600;\">{title} {statusBadge}</td>");
            html.AppendLine($"<td align=\"right\" style=\"font-size:19px;font-weight:700;color:#4f46e5;white-space:nowrap;\">{progress}%</td>");
            html.AppendLine("</tr></table>");

            html.AppendLine("<div style=\"background:#e2e8f0;border-radius:999px;height:8px;margin:12px 0 12px;\">");
            html.AppendLine($"<div style=\"width:{progress}%;height:8px;border-radius:999px;background:#4f46e5;font-size:0;line-height:0;\">&nbsp;</div>");
            html.AppendLine("</div>");

            html.AppendLine($"<div style=\"font-size:12px;color:#64748b;line-height:1.7;\">{string.Join(" · ", details)}</div>");

            IReadOnlyList<string>? actions = null;
            if (advice is not null && advice.Advice.TryGetValue(goal.Id, out var suggestedActions))
            {
                actions = suggestedActions;
            }

            if (inProgress && actions is { Count: > 0 })
            {
                html.AppendLine("<div style=\"margin-top:12px;border-radius:10px;background:#eef2ff;padding:10px 12px;\">");
                html.AppendLine("<div style=\"font-size:11px;font-weight:700;color:#4338ca;letter-spacing:1px;\">AI 行動建議</div>");
                html.AppendLine("<ol style=\"margin:6px 0 0;padding-left:18px;font-size:12px;color:#3730a3;line-height:1.8;\">");
                foreach (var action in actions)
                {
                    html.AppendLine($"<li>{Encoder.Encode(action)}</li>");
                }

                html.AppendLine("</ol></div>");
            }

            html.AppendLine("</td></tr></table>");
        }

        html.AppendLine("<div style=\"font-size:11px;color:#94a3b8;line-height:1.7;padding:8px 4px 16px;\">");
        html.AppendLine($"這封信由「長期目標提醒器」{Encoder.Encode(DigestSchedule.Describe(schedule))} 自動寄出。");
        html.AppendLine("</div>");
        html.AppendLine("</td></tr></table>");
        html.AppendLine("</body></html>");

        return html.ToString();
    }
}
