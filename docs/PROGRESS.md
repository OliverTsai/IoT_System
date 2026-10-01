# 專案進度與 AI 交接

最後更新：2026-09-30

## 目前閘門

- Current phase：`2 - ASP.NET Core Web API 與資料持久化基礎`
- State：`awaiting_review`
- Owner action：請審查階段 2 的程式碼與驗證結果；確認後由使用者自行 commit，再明確指示開始階段 3。
- Next phase：`3 - 設備與遙測 REST 垂直切片`

狀態定義：

- `not_started`：尚未開始。
- `in_progress`：AI 只能實作這個階段。
- `awaiting_review`：實作與驗證已完成，AI 必須停止等待使用者審查及提交。
- `accepted`：使用者已審查並提交；通常在下一階段開始時記錄。
- `blocked`：存在無法安全自行排除的阻礙。

## 階段總覽

| 階段 | 名稱 | 狀態 |
|---|---|---|
| 0 | Git 與 AI 協作基線 | `accepted` |
| 1 | 本機基礎設施 | `accepted` |
| 2 | ASP.NET Core Web API 與資料持久化基礎 | `awaiting_review` |
| 3 | 設備與遙測 REST 垂直切片 | `not_started` |
| 4 | MQTT 資料接收與設備模擬器 | `not_started` |
| 5 | Authentication、Authorization 與安全基線 | `not_started` |
| 6 | Vue 3 RWD 儀表板 | `not_started` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 將原本的 MVC 範本整理為 `src/IoTMonitor.Api` Web API 專案，並將 solution 更名為 `IoTMonitor.slnx`。
- 設定 Controllers、OpenAPI、Problem Details、狀態碼錯誤回應，以及 Console／Debug logging。
- 建立 PostgreSQL `IoTMonitorDbContext`、`Device`、`Telemetry`、`Alert` 實體與資料庫組態。
- 建立並套用 `20260930123440_InitialCreate` migration；資料庫包含 `devices`、`telemetry`、`alerts` 資料表。
- 建立 `/health/live` 與包含 PostgreSQL 連線檢查的 `/health/ready`。
- 建立 xUnit 與 `WebApplicationFactory` 測試基礎，覆蓋系統資訊、live health、OpenAPI 及 Problem Details。
- 以 User Secrets／環境變數文件化本機連線字串；repository 未加入本機 `.env` 或真實憑證。

## 本階段驗證

- [x] `dotnet build IoTMonitor.slnx --no-restore` 成功：0 warnings、0 errors。
- [x] `dotnet test IoTMonitor.slnx --no-build --no-restore` 通過：4 passed、0 failed。
- [x] 實際啟動 API 後，`GET /api/system` 與 `GET /openapi/v1.json` 回傳 200。
- [x] `GET /health/live` 與 `GET /health/ready` 回傳 200；readiness 已執行 PostgreSQL `SELECT 1`。
- [x] 初始 migration 已套用；再次執行 database update 顯示資料庫已是最新狀態。
- [x] `GET /api/not-found` 回傳 404 與 `application/problem+json`。
- [x] `git diff --check` 通過；`.env` 由 `.gitignore` 排除，追蹤內容未發現真實密碼或私鑰。

## 已知事項與風險

- `5432` 已由既有的 `holdem-backend-postgres-1` 使用；本專案固定以 `.env.example` 的 `5433` 避免衝突。
- MQTT 目前未啟用 TLS，且資料庫與 broker 會發布到 host port；此設定只適用本機開發，不可直接用於正式環境。
- Codex shell 未繼承 Docker PATH；目前 Docker CLI 位於 `C:\Users\COSH\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。
- 設備／遙測 CRUD 尚未實作，屬於階段 3；MQTT 應用程式訂閱與設備模擬器屬於階段 4。
- Authentication 與 Authorization 尚未實作，屬於階段 5。
- 舊 `WebApplication2` 目錄只剩被 Git 忽略的 `bin`、`obj`、`*.user` 等本機產生物，不屬於新的 solution。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若階段 2 尚未被使用者接受，只能修正階段 2 的審查意見，不得開始設備 CRUD 或 MQTT 應用程式整合。
4. 使用者審查並自行 commit 後，只有在收到明確指示時才能將階段 2 標為 `accepted` 並開始階段 3。
