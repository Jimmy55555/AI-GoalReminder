import { createClient } from "https://esm.sh/@supabase/supabase-js@2";
import { bumpUsage } from "../_shared/aiUsage.ts";
import { geminiError, geminiFailureMessage } from "../_shared/geminiError.ts";

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

async function verifyUser(req: Request) {
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
  return data.user;
}

function safeText(value: unknown, max = 120) {
  return typeof value === "string" ? value.trim().slice(0, max) : "";
}

Deno.serve(async (req) => {
  if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: CORS });
  if (req.method !== "POST") return json({ error: "只支援 POST。" }, 405);

  try {
    await verifyUser(req);

    const body = await req.json();
    const title = safeText(body.title, 120);
    const description = safeText(body.description, 500);
    const targetDate = safeText(body.targetDate, 20);
    const skillName = safeText(body.skillName, 120);
    const note = safeText(body.note, 300);
    if (!title || !skillName) return json({ error: "缺少目標或技能資訊。" }, 400);

    const apiKey = Deno.env.get("GEMINI_API_KEY") ?? "";
    if (!apiKey) return json({ error: "AI 金鑰尚未設定。" }, 500);

    const model = Deno.env.get("GEMINI_MODEL") ?? "gemini-4-hydrogen";
    const prompt = `你是學習步驟規劃教練。目標標題：${title}。描述：${description}。目標日期：${targetDate || "未設定"}。技能：${skillName}。技能備註：${note || "無"}。
請把這個技能拆成最小、可實際執行的步驟，照順序排列，每一步只做一件事。用繁體中文。
不要限制步驟數量，依實際需要產生足夠的步驟；太複雜時請繼續拆分。
只回傳 JSON 陣列，每項格式為 {"title":"步驟說明"}。
不要使用 markdown，不要加其他欄位。`;

    const schema = {
      type: "array",
      items: {
        type: "object",
        properties: {
          title: { type: "string" },
        },
        required: ["title"],
      },
    };

    const payloads = [
      {
        contents: [{ role: "user", parts: [{ text: prompt }] }],
        generationConfig: {
          responseFormat: { text: { mimeType: "APPLICATION_JSON", schema } },
          thinkingConfig: { thinkingLevel: "low" },
          maxOutputTokens: 8192,
        },
      },
      {
        contents: [{ role: "user", parts: [{ text: prompt }] }],
        generationConfig: {
          responseMimeType: "application/json",
          responseJsonSchema: schema,
          thinkingConfig: { thinkingLevel: "low" },
          maxOutputTokens: 8192,
        },
      },
    ];

    let text = "";
    let lastError = "AI 沒有回傳內容，請稍後再試。";
    for (const payload of payloads) {
      const response = await fetch(
        `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent`,
        {
          method: "POST",
          headers: { "Content-Type": "application/json", "x-goog-api-key": apiKey },
          body: JSON.stringify(payload),
        },
      );
      if (!response.ok) {
        const raw = await response.text();
        if (response.status === 400) {
          lastError = await geminiError(400, raw, model);
          continue;
        }
        console.error("Gemini error", response.status, raw);
        return json({ error: await geminiError(response.status, raw, model) }, 502);
      }
      await bumpUsage();

      const data = await response.json();
      const parts: any[] = data?.candidates?.[0]?.content?.parts ?? [];
      text = parts
        .filter((part) => part?.text && !part?.thought)
        .map((part) => String(part.text))
        .join("")
        .trim();
      if (text) break;
    }

    if (!text) return json({ error: lastError }, 502);

    const cleaned = text.replace(/^```json\s*/i, "").replace(/```$/, "").trim();
    const parsed = JSON.parse(cleaned);
    const rawSteps: any[] = Array.isArray(parsed) ? parsed : (parsed.steps ?? []);
    const steps = rawSteps
      .filter((item) => item && typeof item.title === "string" && item.title.trim())
      .map((item) => ({
        id: crypto.randomUUID(),
        title: String(item.title).trim().slice(0, 300),
        done: false,
      }));

    if (steps.length === 0) return json({ error: "AI 沒有產生步驟，請手動新增。" }, 502);
    return json({ steps });
  } catch (error) {
    console.error(error);
    return json({ error: geminiFailureMessage(error) }, 500);
  }
});
