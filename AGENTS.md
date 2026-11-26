# 長期目標提醒器 · AI 編輯指引

Blazor 專案，修改前先讀這份。

## 硬性約束
- 優先用 Tailwind utility class，不要手寫 CSS（Tailwind 走 Play CDN，設定在 `Components/App.razor`）

## 設計約定
- 柔和漸層玻璃；共用背景 + 3 個移動模糊色塊，不要整頁換色或各面板各自動畫
- 側欄桌面收合 `w-16`、不右凸，收合時標籤／目標名稱隱藏、`»` 置中
- 手機用抽屜式覆蓋層（`Services/SidebarState.cs`），不吃掉主內容寬度
- 統計列 `grid-cols-1 sm:grid-cols-3`，觸控目標 40–44px
- 改 Razor 後同時檢查 912px 與 390px

## 主要檔案
- `Components/App.razor` — Tailwind 設定與動畫
- `Components/Layout/MainLayout.razor` — header、背景
- `Components/GoalSidebar.razor` — 側欄與抽屜
- `Components/GoalDashboard.razor` — 首頁內容
- `Components/Pages/Settings.razor` — 設定頁
- `Services/` — SidebarState、EmailScheduleStore、DigestSchedule、DailyGoalDigestWorker、EmailService
- `Models/` — Goal、Skill、GoalSeedData、EmailSchedule
