// Gemini / Edge Function 的錯誤轉成使用者看得懂且可採取行動的訊息。
export async function geminiError(status: number, raw: string, model = "") {
  const text = raw.trim();
  let detail = text;
  try {
    const parsed = JSON.parse(text);
    detail = String(parsed?.error?.message ?? parsed?.message ?? text);
  } catch {
    // 不是 JSON 就用原文
  }

  if (status === 429) {
    const combined = `${detail} ${text}`;
    if (/PerDay|per day|daily|quota exceeded/i.test(combined)) {
      return "今日 AI 生成額度已用完，請 16:00 後再試。";
    }
    if (/PerMinute|per minute|rate limit/i.test(combined)) {
      return "AI 請求太頻繁，請稍後再試。";
    }
    return "AI 服務額度已用完或請求過於頻繁，請稍後再試。";
  }
  if (status === 401 || status === 403) {
    return "AI 金鑰無效或沒有權限，請檢查 Supabase 設定。";
  }
  if (status === 400) {
    return "AI 請求格式錯誤，請稍後再試。";
  }
  if (status >= 500) {
    return "AI 服務暫時故障，請稍後再試。";
  }
  const suffix = model ? `（${model}）` : "";
  return `AI 服務回傳 ${status}${suffix}，請稍後再試。`;
}

export function geminiFailureMessage(error: unknown) {
  if (error instanceof TypeError) {
    return "無法連線到 AI 服務，請檢查網路連線。";
  }
  if (error instanceof SyntaxError) {
    return "AI 回應格式異常，請稍後再試。";
  }
  if (error instanceof Error) {
    return error.message.slice(0, 120);
  }
  return "AI 服務暫時無法使用，請稍後再試。";
}
