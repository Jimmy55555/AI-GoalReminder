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

async function verifyUser(req: Request) {
  const auth = req.headers.get("Authorization") ?? "";
  const token = auth.replace(/^Bearer\s+/i, "").trim();
  if (!token) throw new Error("請先登入。");

  const supabase = createClient(
    Deno.env.get("SUPABASE_URL")!,
    Deno.env.get("SUPABASE_ANON_KEY")!,
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
  if (req.method === "OPTIONS") return json({}, 204);
  if (req.method !== "POST") return json({ error: "只支援 POST。" }, 405);

  try {
    await verifyUser(req);

    const body = await req.json();
    const title = safeText(body.title, 120);
    const description = safeText(body.description, 500);
    const targetDate = safeText(body.targetDate, 20);
    if (!title) return json({ error: "請輸入目標標題。" }, 400);

    const apiKey = Deno.env.get("GEMINI_API_KEY") ?? "";
    if (!apiKey) return json({ error: "AI 金鑰尚未設定。" }, 500);

    const model = Deno.env.get("GEMINI_MODEL") ?? "gemini-4-hydrogen";
    const prompt = `你是學習規劃教練。目標標題：${title}。描述：${description}。目標日期：${targetDate || "未設定"}。
請產生 4~6 個可執行的技能或里程碑。用繁體中文。
只回傳 JSON 陣列，每項格式為 {"name":"技能名稱","status":"NotStarted","progress":0,"note":"簡短說明"}。
status 只能是 NotStarted、Learning、Mastered；progress 為 0~100 的整數；不要使用 markdown，不要加其他欄位。`;

    const schema = {
      type: "array",
      items: {
        type: "object",
        properties: {
          name: { type: "string" },
          status: { type: "string", enum: ["NotStarted", "Learning", "Mastered"] },
          progress: { type: "integer" },
          note: { type: "string" },
        },
        required: ["name", "status", "progress", "note"],
      },
    };

    const payloads = [
      {
        contents: [{ role: "user", parts: [{ text: prompt }] }],
        generationConfig: {
          responseFormat: { text: { mimeType: "APPLICATION_JSON", schema } },
          thinkingConfig: { thinkingLevel: "low" },
          maxOutputTokens: 4096,
        },
      },
      {
        contents: [{ role: "user", parts: [{ text: prompt }] }],
        generationConfig: {
          responseMimeType: "application/json",
          responseJsonSchema: schema,
          thinkingConfig: { thinkingLevel: "low" },
          maxOutputTokens: 4096,
        },
      },
    ];

    let text = "";
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
        if (response.status === 400) continue;
        console.error("Gemini error", response.status, await response.text());
        return json({ error: "AI 服務回傳錯誤。" }, 502);
      }

      const data = await response.json();
      const parts: any[] = data?.candidates?.[0]?.content?.parts ?? [];
      text = parts
        .filter((part) => part?.text && !part?.thought)
        .map((part) => String(part.text))
        .join("")
        .trim();
      if (text) break;
    }

    if (!text) return json({ error: "AI 沒有回傳內容，請稍後再試。" }, 502);

    const cleaned = text.replace(/^```json\s*/i, "").replace(/```$/, "").trim();
    const parsed = JSON.parse(cleaned);
    const rawSkills: any[] = Array.isArray(parsed) ? parsed : (parsed.skills ?? []);
    const skills = rawSkills
      .filter((item) => item && typeof item.name === "string" && item.name.trim())
      .slice(0, 8)
      .map((item) => ({
        id: crypto.randomUUID(),
        name: String(item.name).trim().slice(0, 80),
        status: ["NotStarted", "Learning", "Mastered"].includes(item.status) ? item.status : "NotStarted",
        progress: Math.max(0, Math.min(100, Number(item.progress) || 0)),
        note: String(item.note ?? "").trim().slice(0, 200),
      }));

    if (skills.length === 0) return json({ error: "AI 沒有產生技能，請手動新增。" }, 502);
    return json({ skills });
  } catch (error) {
    console.error(error);
    const message = error instanceof Error ? error.message : "AI 服務暫時無法使用。";
    return json({ error: message.slice(0, 120) }, 500);
  }
});
