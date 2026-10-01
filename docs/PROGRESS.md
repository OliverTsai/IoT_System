# 專案進度與 AI 交接

最後更新：2026-10-01

## 目前閘門

- Current phase：`4 - MQTT 資料接收與設備模擬器`
- State：`awaiting_review`
- Owner action：請審查階段 4 的 MQTT 接收流程、模擬器、重連與去重策略；確認後由使用者自行 commit，再明確指示開始階段 5。
- Next phase：`5 - Authentication、Authorization 與安全基線`

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
| 3 | 設備與遙測 REST 垂直切片 | `accepted` |
| 4 | MQTT 資料接收與設備模擬器 | `awaiting_review` |
| 5 | Authentication、Authorization 與安全基線 | `not_started` |
| 6 | Vue 3 RWD 儀表板 | `not_started` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- API 與設備模擬器加入使用者同意的 `MQTTnet 5.2.0.1603`。
- 建立可設定的 MQTT `BackgroundService`，以 QoS 1 訂閱 `devices/+/telemetry`，支援取消、手動確認及斷線重連。
- 建立 topic／JSON payload／設備狀態／溫濕度／量測時間驗證；無效訊息不寫入資料庫，且 log 不包含原始 payload。
- 建立 10 分鐘、最多 10,000 組 message id 的記憶體去重策略；資料庫失敗時會釋放 reservation，讓 broker 重送可再次處理。
- 建立獨立的 `IoTMonitor.DeviceSimulator`，可設定設備數、external id 前綴、發送頻率、發送筆數及重連間隔。
- 修正 Mosquitto password 暫存檔造成容器 stop/start 後 restart loop 的問題。
- 新增 MQTT parser 與 PostgreSQL processor 測試，以及完整本機操作文件。

## 本階段驗證

- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] 設定測試資料庫後執行 Release 完整測試：25 passed、0 failed、0 skipped。
- [x] parser 測試涵蓋有效訊息、topic、JSON、溫濕度範圍及未來時間；processor 測試涵蓋寫入、拒絕及重複訊息。
- [x] 模擬器訊息實際經 Mosquitto 寫入 PostgreSQL，並由 REST latest／history API 查得。
- [x] 實際無效濕度訊息只產生診斷 warning；同一 message id 發送兩次只新增一筆遙測。
- [x] 實際停止 broker 後 API 與模擬器持續執行並重試；broker 恢復後兩者自動連線且繼續寫入。
- [x] 修正後的 Mosquitto 容器再次執行 stop/start 可恢復為 healthy。
- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] NuGet vulnerability audit 未發現目前來源已知的易受攻擊套件。
- [x] 端到端設備及其遙測已清除；整合測試未留下 `iot_monitor_tests_*` 暫時資料庫。
- [x] `git diff --check` 通過；`.env` 維持忽略，追蹤內容未發現真實密碼或私鑰。

## 已知事項與風險

- `5432` 已由既有的 `holdem-backend-postgres-1` 使用；本專案固定以 `.env.example` 的 `5433` 避免衝突。
- MQTT 目前未啟用 TLS，且資料庫與 broker 會發布到 host port；此設定只適用本機開發，不可直接用於正式環境。
- Codex shell 未繼承 Docker PATH；目前 Docker CLI 位於 `C:\Users\COSH\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。
- Authentication 與 Authorization 尚未實作，屬於階段 5。
- MQTT 預設停用，必須使用 User Secrets 或環境變數提供本機 broker 帳密並啟用。
- message id 去重目前只適用單一 API 執行個體，且 API 重啟後會清空；跨執行個體／重啟去重預定未來改用資料庫唯一鍵或分散式儲存。
- 未設定 `IOT_MONITOR_TEST_CONNECTION_STRING` 時，6 個 PostgreSQL 整合測試會顯示為 skipped；完整測試指令已記錄於 `docs/API_DEVELOPMENT.md`。
- 舊 `WebApplication2` 目錄只剩被 Git 忽略的 `bin`、`obj`、`*.user` 等本機產生物，不屬於新的 solution。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若階段 4 尚未被使用者接受，只能修正階段 4 的審查意見，不得開始 Authentication 或 Authorization。
4. 使用者審查並自行 commit 後，只有在收到明確指示時才能將階段 4 標為 `accepted` 並開始階段 5。
