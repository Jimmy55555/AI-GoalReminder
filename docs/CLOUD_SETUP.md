# 雲端版設定指南（GitHub Pages + Supabase）

本機版（InteractiveServer）保留不动；雲端版是新增的 `GitHubPagesApp`、`CloudCore`、`Mailer`。
資料唯一來源是 Supabase；前端每次開啟／登入都會重抓，不留離線 DB。

## 0. _repo 設定_

- GitHub Free 的 Pages 要求 repo 公開。請確認 repo 內沒有任何金鑰。
- Settings → Pages → Build and deployment → Source 選 **GitHub Actions**。

## 1. Supabase 專案

1. 建立免費 Supabase 專案。
2. SQL Editor 執行 `supabase/schema.sql`。
3. Authentication → Providers → Email 保持啟用；關閉「Allow new users to sign up」（只留自己一個帳號）。
4. Authentication → Users → Add user → 建立自己的 email + 密碼，複製該使用者的 **UUID**（後面匯入要用）。

## 2. 前端設定（公開資訊）

編輯 `GitHubPagesApp/wwwroot/appsettings.json`：

- `Supabase:Url`：Supabase 專案左上 **Connect** 顯示的 Project URL
- `Supabase:PublishableKey`：同一個 Connect 對話框的 publishable key（`sb_publishable_...`）

如果沒看到，到 **Settings → API Keys → Publishable and secret API keys**，按 **Create new API keys** 建立。publishable key 可公開；安全性靠 RLS。**不要把 `sb_secret_...` 放進這個檔案。**

## 3. 一次性匯入本機資料

在本機 PowerShell 執行（金鑰不要貼到聊天或 repo）：

```powershell
$env:SUPABASE_URL = "https://<project>.supabase.co"
$env:SUPABASE_SECRET_KEY = "<secret_key>"
$env:SUPABASE_USER_ID = "<步驟 1 的使用者 UUID>"
dotnet run --project Mailer/Mailer.csproj -c Release -- import "長期目標提醒器/App_Data"
```

會把 `goals.json`、`goal-progress.json`、`email-schedule.json` 寫入 Supabase。
日後 Chat 中的修改只需在網頁操作，不需再匯入。

## 4. Supabase Edge Functions

部署四個函式：

```powershell
supabase functions deploy generate-skills
supabase functions deploy goal-advice
supabase functions deploy send-test-digest
supabase functions deploy daily-digest
```

`daily-digest` 的 `verify_jwt` 要設為 `false`，由 `CRON_SECRET` 驗證 pg_cron 呼叫。

在 Supabase Dashboard → Edge Functions → Secrets 設定：

- `SMTP_USER`：Gmail 地址
- `SMTP_PASSWORD`：Gmail 應用程式密碼
- `SMTP_HOST`：`smtp.gmail.com`（預設值）
- `SMTP_PORT`：`587`（預設值）
- `CRON_SECRET`：自訂隨機字串，給 pg_cron 呼叫時使用
- `GEMINI_API_KEY`：Gemini API 金鑰（選用，AI 建議用）
- `GEMINI_MODEL`：`gemini-4-hydrogen`（選用）

> 測試信與正式摘要信都在 Edge Function 寄出；兩邊共用同一組 SMTP Secrets。

## 5. GitHub Secrets

Repo → Settings → Secrets and variables → Actions，新增：

| 名稱 | 值 |
|---|---|
| `SUPABASE_URL` | `https://<project>.supabase.co` |
| `SUPABASE_SECRET_KEY` | `sb_secret_...`（只放這裡） |

若還沒建立新 key，可暫時改用舊的 `SUPABASE_SERVICE_ROLE_KEY`，程式會自動讀取。
`SMTP_USER` / `SMTP_PASSWORD` / `GEMINI_API_KEY` 只保留給手動执行的 Mailer；正式寄信已改用 Supabase Secrets。

## 6. 部署與寄信

- push 到 `main` 會自動部署 Pages。
- 正式寄信由 Supabase `pg_cron` 每分鐘呼叫 `daily-digest` Edge Function；`daily-digest.yml` 只保留手動觸發。
- `pg_cron` 使用 UTC；函式內部會轉成台北時區，依設定頁的啟用、寄送時間、頻率、星期／日期與收件者寄信。
- 寄送時間會落在最近的半小時（`:00` 或 `:30`）；過了時間的任一分鐘都會補寄，`email_send_log` 以 `(owner_id, sent_on)` 保證同一天只寄一次。
- 建立 cron job（在 SQL Editor 執行，`CRON_SECRET` 改成你設的值）：

```sql
create extension if not exists pg_cron;
create extension if not exists pg_net;

select cron.schedule(
  'goal-reminder-daily-digest',
  '* * * * *',
  $$
  select net.http_post(
    url := 'https://<project>.supabase.co/functions/v1/daily-digest',
    headers := jsonb_build_object(
      'Content-Type', 'application/json',
      'x-cron-secret', '<CRON_SECRET>'
    ),
    body := '{}'::jsonb,
    timeout_milliseconds := 30000
  ) as request_id;
  $$
);
```

## 7. 已知差異

- 本機版與雲端版都用同一份 Supabase 資料；寄信由 Supabase pg_cron 統一處理。
- Free 方案專案若閒置超過一週會暫停，暫停期間 cron 不會執行；恢復後會補寄當天還沒寄的摘要信。
- 主题、側欄、Razor 元件與本機版共用同一份程式碼；深色不閃白由 `wwwroot/index.html` 的 pre-paint script 處理。
- 首次登入會自動建立 3 個種子目標（只有新帳號、資料表為空時）。

## 8. 本機開發

```powershell
dotnet run --project GitHubPagesApp/GitHubPagesApp.csproj
```

網頁會要求登入；未登入時不會讀寫任何資料。
