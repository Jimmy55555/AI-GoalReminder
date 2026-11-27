import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

function getServiceKey() {
  const raw = Deno.env.get("SUPABASE_SECRET_KEYS");
  if (raw) {
    const keys = JSON.parse(raw);
    return String(keys.default ?? Object.values(keys)[0] ?? "");
  }
  return Deno.env.get("SUPABASE_SERVICE_ROLE_KEY") ?? "";
}

function serviceClient() {
  const key = getServiceKey();
  return createClient(Deno.env.get("SUPABASE_URL")!, key, {
    global: { headers: { Authorization: `Bearer ${key}` } },
  });
}

// Google 的每日額度是太平洋午夜重置（台灣 16:00），所以要用 Pacific 日期記帳。
export function quotaDay() {
  const parts = new Intl.DateTimeFormat("en-US", {
    timeZone: "America/Los_Angeles",
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
  }).formatToParts(new Date());
  const value = (type: string) => parts.find((part) => part.type === type)?.value ?? "";
  return `${value("year")}-${value("month")}-${value("day")}`;
}

// 只有 generateContent 回 2xx 才呼叫；失敗不消耗額度，所以不記。
export async function bumpUsage() {
  try {
    const { error } = await serviceClient()
      .rpc("goal_reminder_ai_usage_bump", { p_day: quotaDay() });
    if (error) console.error("AI usage bump failed", error.message);
  } catch (error) {
    console.error("AI usage bump failed", error);
  }
}

export async function readUsage() {
  const { data, error } = await serviceClient()
    .from("goal_reminder_ai_usage")
    .select("used, quota_limit")
    .eq("quota_day", quotaDay())
    .maybeSingle();
  if (error) throw error;
  return {
    used: Number(data?.used ?? 0),
    limit: Number(data?.quota_limit ?? 20),
  };
}
