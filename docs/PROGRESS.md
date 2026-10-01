# 專案進度與 AI 交接

最後更新：2026-10-01

## 目前閘門

- Current phase：`7 - 告警與即時更新`
- State：`awaiting_review`
- Owner action：使用者審查階段 7 的告警、SignalR 與 Vue 即時更新程式碼，確認後自行提交；AI 不得開始階段 8。
- Next phase：`8 - 完整容器化、品質與作品集整理`

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
| 4 | MQTT 資料接收與設備模擬器 | `accepted` |
| 5 | Authentication、Authorization 與安全基線 | `accepted` |
| 6 | Vue 3 RWD 儀表板 | `accepted` |
| 7 | 告警與即時更新 | `awaiting_review` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 新增可由設定覆寫的溫度與濕度 Warning／Critical 閾值；等於邊界時安全，每筆量測每項指標最多產生一筆符合最高嚴重度的告警。
- REST 與 MQTT 遙測共用同一個寫入服務，遙測及其告警在同一次資料庫交易中持久化，成功後才發布即時事件。
- 實作告警分頁、設備／類型／嚴重度／確認狀態篩選，以及 Operator／Admin 可用的冪等告警確認 API；Viewer 維持唯讀。
- 告警確認時間先正規化為 PostgreSQL 微秒精度，確保第一次與重複確認的 API 回應完全一致。
- 建立需要登入的 SignalR Hub，發布 `TelemetryReceived`、`AlertRaised` 與 `AlertAcknowledged`；發布失敗不回滾已持久化資料。
- Vue 加入 SignalR client、連線狀態與持續重試；初次連線或重連成功時以 REST 補抓，避免斷線期間永久遺漏資料。
- 總覽、設備明細及告警中心會即時更新；前端依 ID 去重及依時間排序，告警頁提供篩選、分頁與依角色顯示確認操作。
- 補上告警規則、REST／MQTT 告警流程、權限、SignalR negotiation、前端事件派送與 REST 補抓測試，以及完整操作文件。
- 既有初始 migration 已包含 `alerts` 資料表，本階段不需要新增或執行 migration。

## 本階段驗證

- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] 使用隔離的 PostgreSQL 測試資料庫執行 `dotnet test`：46 tests passed、0 failed、0 skipped；完成後確認沒有 `iot_monitor_tests_*` 資料庫殘留。
- [x] 告警整合測試涵蓋安全量測、Critical 告警、查詢篩選、Viewer 禁止確認、Operator 確認與重複確認；MQTT 超標量測亦會產生告警。
- [x] `npm run type-check` 通過。
- [x] `npm run lint` 通過，0 errors、0 warnings。
- [x] `npm run test` 通過：3 test files、7 tests passed。
- [x] `npm run build` 通過：75 modules transformed，production assets 成功產生。
- [x] `npm audit` 通過：0 vulnerabilities。
- [x] 前端測試涵蓋 SignalR 遙測／告警事件派送、初次連線與重連後 REST 補抓，以及告警查詢與確認的 CSRF request。

## 已知事項與風險

- `5432` 已由既有的 `holdem-backend-postgres-1` 使用；本專案固定以 `.env.example` 的 `5433` 避免衝突。
- MQTT 目前未啟用 TLS，且資料庫與 broker 會發布到 host port；此設定只適用本機開發，不可直接用於正式環境。
- Codex shell 未繼承 Docker PATH；目前 Docker CLI 位於 `C:\Users\COSH\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。
- MQTT 預設停用，必須使用 User Secrets 或環境變數提供本機 broker 帳密並啟用。
- message id 去重目前只適用單一 API 執行個體，且 API 重啟後會清空；跨執行個體／重啟去重預定未來改用資料庫唯一鍵或分散式儲存。
- Bootstrap Admin 預設停用且 repository 沒有預設密碼；首次本機登入前需依 `docs/AUTHENTICATION.md` 使用 User Secrets 建立 Admin，建立後立即移除 bootstrap password。
- Auth 與 antiforgery Cookie 強制 Secure，本機登入流程必須使用 HTTPS；Vue 必須使用 credentials 並在登入後重新取得 CSRF token。
- Cookie 加密依賴 ASP.NET Core Data Protection key；階段 8 容器化時必須持久化且保護 key ring，否則容器重建會使現有 Cookie 失效。
- 目前每一筆超標量測都會建立新告警，尚未合併為告警事件區間或加入冷卻時間；這是階段 7 的明確行為，實際產品可再依需求調整。
- `DeviceOffline` 類型已保留，但尚未定義設備心跳期限，因此目前不會自動產生離線告警。
- SignalR 目前使用單一 API 執行個體的 in-process Hub；若未來橫向擴充，需要加入 Redis 或受管 SignalR backplane。
- 前端 production build 目前是靜態產物，尚無 Dockerfile 或反向代理設定；這些工作屬於階段 8。
- 本階段已由自動測試驗證即時事件處理與重連補抓策略，但未使用真實登入帳號進行瀏覽器端的 SignalR 端對端操作；階段 8 的整套 smoke test 應補驗證此路徑。
- 未設定 `IOT_MONITOR_TEST_CONNECTION_STRING` 時，20 個 PostgreSQL 整合測試會顯示為 skipped；完整測試指令已記錄於 `docs/API_DEVELOPMENT.md`。
- 舊 `WebApplication2` 目錄只剩被 Git 忽略的 `bin`、`obj`、`*.user` 等本機產生物，不屬於新的 solution。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 階段 7 目前為 `awaiting_review`；只能回答問題或修正階段 7 的審查意見，不得開始容器化或作品集整理。
4. 使用者審查並自行 commit 後，只有在收到明確指示時才能將階段 7 標為 `accepted` 並開始階段 8。
