import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import { SMTPClient } from "npm:smtp@1.8.3";

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Content-Type": "application/json",
};

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: CORS });
}

function escapeHtml(value: unknown) {
  return String(value ?? "")
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;");
}

function buildBody(goals: any[], today: string) {
  const cards = goals.map((goal) => {
    const skills: any[] = goal.skills ?? [];
    const progress = skills.length === 0
      ? 0
      : Math.round(skills.reduce((sum, skill) => sum + (Number(skill.progress) || 0), 0) / skills.length);
    const mastered = skills.filter((skill) => skill.status === "Mastered").length;
    const learning = skills.filter((skill) => skill.status === "Learning").length;
    const notStarted = skills.filter((skill) => skill.status === "NotStarted").length;
    return `<table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="border:1px solid #e2e8f0;border-radius:12px;margin-bottom:12px;border-collapse:separate;"><tr><td style="padding:16px;">
      <table role="presentation" width="100%"><tr><td style="font-size:15px;font-weight:600;">${escapeHtml(goal.title)}</td><td align="right" style="font-size:19px;font-weight:700;color:#4f46e5;">${progress}%</td></tr></table>
      <div style="background:#e2e8f0;border-radius:999px;height:8px;margin:12px 0;"><div style="width:${progress}%;height:8px;border-radius:999px;background:#4f46e5;"></div></div>
      <div style="font-size:12px;color:#64748b;">已掌握 ${mastered} · 學習中 ${learning} · 未開始 ${notStarted}</div>
    </td></tr></table>`;
  }).join("");

  return `<!DOCTYPE html><html lang="zh-Hant"><head><meta charset="utf-8" /></head>
<body style="margin:0;padding:24px 12px;background:#eef2f7;font-family:'Segoe UI','Microsoft JhengHei',sans-serif;color:#0f172a;">
<table role="presentation" cellpadding="0" cellspacing="0" width="100%" style="max-width:600px;margin:0 auto;">
<tr><td style="background:#4f46e5;border-radius:16px 16px 0 0;padding:24px 28px;">
<div style="font-size:11px;letter-spacing:2px;color:#c7d2fe;">TEST DIGEST</div>
<div style="font-size:22px;font-weight:700;color:#fff;margin-top:6px;">長期目標提醒器</div>
<div style="font-size:13px;color:#e0e7ff;margin-top:8px;">${escapeHtml(today)} · 共 ${goals.length} 個目標</div>
</td></tr>
<tr><td style="background:#fff;border-radius:0 0 16px 16px;padding:20px;">${cards}
<div style="font-size:11px;color:#94a3b8;">這是一封測試信，由雲端寄出。</div>
</td></tr></table></body></html>`;
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return json({}, 204);
  if (req.method !== "POST") return json({ error: "只支援 POST。" }, 405);

  try {
    const auth = req.headers.get("Authorization") ?? "";
    const token = auth.replace(/^Bearer\s+/i, "").trim();
    if (!token) throw new Error("請先登入。");

    const supabase = createClient(
      Deno.env.get("SUPABASE_URL")!,
      Deno.env.get("SUPABASE_ANON_KEY")!,
      { global: { headers: { Authorization: `Bearer ${token}` } } },
    );
    const { data: authData, error: authError } = await supabase.auth.getUser(token);
    if (authError || !authData.user) throw new Error("登入已過期，請重新登入。");

    const { data: scheduleRows, error: scheduleError } = await supabase
      .from("goal_reminder_email_schedule")
      .select("*")
      .eq("owner_id", authData.user.id)
      .limit(1);
    const { data: goalRows, error: goalsError } = await supabase
      .from("goal_reminder_goals")
      .select("*")
      .eq("owner_id", authData.user.id);
    if (scheduleError || goalsError) throw scheduleError ?? goalsError;

    const schedule: any = scheduleRows?.[0];
    if (!schedule) return json({ error: "請先儲存寄信設定。" }, 400);

    const recipient = String(schedule.recipient ?? "").trim();
    if (!recipient) return json({ error: "請先設定收件者。" }, 400);

    const smtpUser = Deno.env.get("SMTP_USER") ?? "";
    const smtpPassword = Deno.env.get("SMTP_PASSWORD") ?? "";
    if (!smtpUser || !smtpPassword) return json({ error: "雲端寄件帳號尚未設定。" }, 500);

    const today = new Intl.DateTimeFormat("zh-TW", {
      timeZone: "Asia/Taipei",
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    }).format(new Date());

    const client = new SMTPClient({
      connection: {
        host: Deno.env.get("SMTP_HOST") ?? "smtp.gmail.com",
        port: Number(Deno.env.get("SMTP_PORT") ?? 587),
        tls: true,
        auth: `${smtpUser}:${smtpPassword}`,
      },
    });

    try {
      await client.sendAsync({
        text: "測試摘要信",
        from: `長期目標提醒器 <${smtpUser}>`,
        to: recipient,
        subject: `【目標摘要・測試】${today} · ${goalRows?.length ?? 0} 個目標`,
        html: buildBody(goalRows ?? [], today),
      });
    } finally {
      client.close();
    }

    return json({ ok: true });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "寄信失敗。";
    return json({ error: message.slice(0, 160) }, 500);
  }
});
