import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import { readUsage } from "../_shared/aiUsage.ts";

const CORS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Headers": "authorization, apikey, content-type, x-client-info",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Content-Type": "application/json",
};

function json(body: unknown, status = 200) {
  return new Response(JSON.stringify(body), { status, headers: CORS });
}

function getPublishableKey() {
  const raw = Deno.env.get("SUPABASE_PUBLISHABLE_KEYS");
  if (raw) {
    const keys = JSON.parse(raw);
    return String(keys.default ?? Object.values(keys)[0] ?? "");
  }
  return Deno.env.get("SUPABASE_ANON_KEY") ?? "";
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: CORS });
  if (req.method !== "POST") return json({ error: "只支援 POST。" }, 405);

  try {
    const auth = req.headers.get("Authorization") ?? "";
    const token = auth.replace(/^Bearer\s+/i, "").trim();
    if (!token) throw new Error("請先登入。");

    const supabase = createClient(
      Deno.env.get("SUPABASE_URL")!,
      getPublishableKey(),
      { global: { headers: { Authorization: `Bearer ${token}` } } },
    );
    const { data, error } = await supabase.auth.getUser(token);
    if (error || !data.user) throw new Error("登入已過期，請重新登入。");

    const model = Deno.env.get("GEMINI_MODEL") ?? "gemini-4-hydrogen";
    const apiKey = Deno.env.get("GEMINI_API_KEY") ?? "";
    if (!apiKey) {
      return json({ error: "AI 金鑰尚未設定（GEMINI_API_KEY）。", model }, 500);
    }

    // countTokens 不跑推論：免費、不計入 generateContent 的 RPM/TPM/RPD。
    const response = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:countTokens`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json", "x-goog-api-key": apiKey },
        body: JSON.stringify({
          contents: [{ role: "user", parts: [{ text: "OK" }] }],
        }),
      },
    );

    if (!response.ok) {
      const raw = (await response.text()).trim();
      console.error("Gemini countTokens error", response.status, raw.slice(0, 300));
      let message = `模型 ${model} 回傳 ${response.status}。`;
      if (response.status === 429) {
        message = raw.includes("quota")
          ? "AI 測試額度已用完，請稍後再試。"
          : `AI 請求過於頻繁（${response.status}），請稍後再試。`;
      } else if (raw) {
        message += ` ${raw.slice(0, 120)}`;
      }
      return json({ error: message, model }, response.status === 429 ? 429 : 502);
    }

    const usage = await readUsage();
    const remaining = Math.max(0, usage.limit - usage.used);
    return json({
      ok: true,
      model,
      used: usage.used,
      limit: usage.limit,
      remaining,
      resetText: "16:00",
    });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "AI 測試失敗。";
    return json({ error: message.slice(0, 160) }, 500);
  }
});
