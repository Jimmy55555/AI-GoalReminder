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

部署三個函式：

```powershell
supabase functions deploy generate-skills
supabase functions deploy goal-advice
supabase functions deploy send-test-digest
```

在 Supabase Dashboard → Edge Functions → Secrets 設定：

- `GEMINI_API_KEY`：Gemini API 金鑰
- `GEMINI_MODEL`：`gemini-4-hydrogen`（可由 GitHub Actions Variables 覆蓋）
- `SMTP_USER`：Gmail 地址
- `SMTP_PASSWORD`：Gmail 應用程式密碼
- `SMTP_HOST`：`smtp.gmail.com`
- `SMTP_PORT`：`587`

> 若 Edge Function 不允許 npm specifier，`send-test-digest` 會失敗；每日寄信仍由 GitHub Actions + .NET SMTP 執行，不受影響。

## 5. GitHub Secrets

Repo → Settings → Secrets and variables → Actions，新增：

| 名稱 | 值 |
|---|---|
| `SUPABASE_URL` | `https://<project>.supabase.co` |
| `SUPABASE_SECRET_KEY` | `sb_secret_...`（只放這裡） |

若還沒建立新 key，可暫時改用舊的 `SUPABASE_SERVICE_ROLE_KEY`，程式會自動讀取。
| `SMTP_USER` | Gmail 地址 |
| `SMTP_PASSWORD` | Gmail 應用程式密碼 |

Variables 可選：`GEMINI_MODEL`（預設 `gemini-4-hydrogen`）。

## 6. 部署與寄信

- push 到 `main` 會自動部署 Pages。
- 每日 00:00 UTC（台北 08:00）執行 `daily-digest.yml`；也可在 Actions 页面按 **Run workflow** 手動寄一次。
- `email_send_log` 以 `(owner_id, sent_on)` 保證同一天不會重複寄。

## 7. 已知差異

- 雲端每日寄信固定在台北 08:00；設定頁的「寄送時間」只影響每週／每月的判定與本機版。
- 主题、側欄、Razor 元件與本機版共用同一份程式碼；深色不閃白由 `wwwroot/index.html` 的 pre-paint script 處理。
- 首次登入會自動建立 3 個種子目標（只有新帳號、資料表為空時）。

## 8. 本機開發

```powershell
dotnet run --project GitHubPagesApp/GitHubPagesApp.csproj
```

網頁會要求登入；未登入時不會讀寫任何資料。
