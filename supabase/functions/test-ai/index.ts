import { createClient } from "https://esm.sh/@supabase/supabase-js@2";

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

    const response = await fetch(
      `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent`,
      {
        method: "POST",
        headers: { "Content-Type": "application/json", "x-goog-api-key": apiKey },
        body: JSON.stringify({
          contents: [{ role: "user", parts: [{ text: "請只回覆 OK 兩個字。" }] }],
          generationConfig: { maxOutputTokens: 64 },
        }),
      },
    );

    if (!response.ok) {
      const detail = (await response.text()).trim().slice(0, 200);
      return json(
        { error: `模型 ${model} 回傳 ${response.status}${detail ? `：${detail}` : "。"}`, model },
        502,
      );
    }

    const data2 = await response.json();
    const parts: any[] = data2?.candidates?.[0]?.content?.parts ?? [];
    const reply = parts
      .filter((part) => part?.text && !part?.thought)
      .map((part) => String(part.text))
      .join("")
      .trim();

    if (!reply) {
      return json({ error: `模型 ${model} 沒有回傳內容。`, model }, 502);
    }

    return json({ ok: true, model, reply: reply.slice(0, 80) });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "AI 測試失敗。";
    return json({ error: message.slice(0, 160) }, 500);
  }
});
