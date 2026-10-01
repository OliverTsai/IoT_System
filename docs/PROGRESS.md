# 專案進度與 AI 交接

最後更新：2026-10-01

## 目前閘門

- Current phase：`3 - 設備與遙測 REST 垂直切片`
- State：`awaiting_review`
- Owner action：請審查階段 3 的 API、驗證規則與整合測試；確認後由使用者自行 commit，再明確指示開始階段 4。
- Next phase：`4 - MQTT 資料接收與設備模擬器`

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
| 2 | ASP.NET Core Web API 與資料持久化基礎 | `accepted` |
| 3 | 設備與遙測 REST 垂直切片 | `awaiting_review` |
| 4 | MQTT 資料接收與設備模擬器 | `not_started` |
| 5 | Authentication、Authorization 與安全基線 | `not_started` |
| 6 | Vue 3 RWD 儀表板 | `not_started` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 建立設備新增、分頁列表、詳情與啟用狀態更新 API。
- 建立遙測寫入、最新值及依時間範圍分頁查詢 API；設備詳情也會帶回最新遙測。
- 以 request／response DTO 隔離資料實體，驗證設備識別碼、名稱、分頁、溫度、濕度及量測時間。
- 針對不存在設備、沒有遙測、重複識別碼與無效輸入回傳一致的 Problem Details。
- 為成功與失敗路徑加入 PostgreSQL 整合測試；每次測試建立並刪除獨立的 `iot_monitor_tests_*` 暫時資料庫。
- 補充 OpenAPI 結構驗證、`.http` 範例及 API 開發文件。

## 本階段驗證

- [x] `dotnet build IoTMonitor.slnx --no-restore` 成功：0 warnings、0 errors。
- [x] 設定 `IOT_MONITOR_TEST_CONNECTION_STRING` 後執行完整測試：11 passed、0 failed、0 skipped。
- [x] 設備建立／列表／詳情／狀態更新，以及遙測寫入／最新值／歷史查詢成功路徑均由 PostgreSQL 整合測試覆蓋。
- [x] 不存在設備、重複識別碼、無效設備資料、量測值、量測時間、查詢範圍及分頁均由整合測試覆蓋。
- [x] OpenAPI 測試確認設備與遙測路徑、request body，以及 201／400／409 等主要 response。
- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] PostgreSQL 與 Mosquitto 容器維持 healthy；測試後未留下 `iot_monitor_tests_*` 暫時資料庫。
- [x] `git diff --check` 通過；`.env` 維持忽略，追蹤內容未發現真實密碼或私鑰。

## 已知事項與風險

- `5432` 已由既有的 `holdem-backend-postgres-1` 使用；本專案固定以 `.env.example` 的 `5433` 避免衝突。
- MQTT 目前未啟用 TLS，且資料庫與 broker 會發布到 host port；此設定只適用本機開發，不可直接用於正式環境。
- Codex shell 未繼承 Docker PATH；目前 Docker CLI 位於 `C:\Users\COSH\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。
- MQTT 應用程式訂閱與設備模擬器尚未實作，屬於階段 4。
- Authentication 與 Authorization 尚未實作，屬於階段 5。
- 未設定 `IOT_MONITOR_TEST_CONNECTION_STRING` 時，6 個 PostgreSQL 整合測試會顯示為 skipped；完整測試指令已記錄於 `docs/API_DEVELOPMENT.md`。
- 舊 `WebApplication2` 目錄只剩被 Git 忽略的 `bin`、`obj`、`*.user` 等本機產生物，不屬於新的 solution。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若階段 3 尚未被使用者接受，只能修正階段 3 的審查意見，不得開始 MQTT 應用程式整合。
4. 使用者審查並自行 commit 後，只有在收到明確指示時才能將階段 3 標為 `accepted` 並開始階段 4。
