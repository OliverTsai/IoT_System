# 專案進度與 AI 交接

最後更新：2026-10-02

## 目前閘門

- Current phase：`8 - 完整容器化、品質與作品集整理`
- State：`awaiting_review`
- Owner action：使用者審查階段 8 的程式碼與文件；確認後由使用者自行提交，或明確要求 AI 提交。
- Next phase：無；階段 8 為目前計畫的最後階段。新的功能或部署需求應先新增計畫並取得使用者同意。

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
| 7 | 告警與即時更新 | `accepted` |
| 8 | 完整容器化、品質與作品集整理 | `awaiting_review` |

## 本階段完成內容

- 為 API、Vue/Nginx 與設備模擬器建立 .NET 10／Node 24 多階段 Dockerfile，並加入 repository 層級 `.dockerignore`。
- Docker Compose 現在以一組指令整合 PostgreSQL、Mosquitto、API、Vue/Nginx 與模擬器，包含 health check、啟動相依性、restart policy、log rotation 與具名 volumes。
- 自製容器全部以非 root 使用者執行，並設定唯讀 root filesystem、`cap_drop: ALL`、`no-new-privileges` 及必要的 tmpfs／volume。
- Nginx 提供 Vue SPA、localhost 自簽 HTTPS、HTTP→HTTPS redirect、安全標頭，以及 REST、OpenAPI、health 與 SignalR WebSocket reverse proxy。
- API 支援設定式啟動 migration、持久化 Data Protection key ring、Compose TLS termination、選擇性 OpenAPI，以及冪等的展示設備 seed；這些自動行為在一般 appsettings 預設關閉。
- Compose 展示環境會建立 Bootstrap Admin 與匹配模擬器的展示設備，讓 MQTT 遙測在首次啟動後即可從 UI／REST 觀察。
- 新增 `scripts/smoke-test.ps1`，涵蓋 process/database health、Vue/CSP、OpenAPI、CSRF/Cookie 登入、受保護 REST、SignalR negotiation、MQTT→API→PostgreSQL 遙測鏈路與登出。
- 重寫作品集 README，補上系統架構、技術選擇、一組指令啟動、範例帳號、角色/API/MQTT 範例、安全邊界與 repository 導覽。
- 新增完整容器操作手冊，並同步更新本機、API、Authentication、MQTT 與前端開發文件。
- 補上 Demo Data 設定驗證測試；完整測試數由 46 增加為 52。
- 移除容器紀錄中的 Alpine GSSAPI 函式庫錯誤、反向代理下重複 HTTPS redirect 警告，以及重複設定 Antiforgery 快取標頭的警告。

## 本階段驗證

- [x] `docker compose --env-file .env.example config --quiet` 通過。
- [x] API、Vue/Nginx、模擬器三個映像均成功建置。
- [x] 使用隔離專案 `iot-monitor-stage8-test` 與空白 volumes 啟動五個服務；PostgreSQL migration、Bootstrap Admin、demo seed、MQTT 訂閱及所有 health checks 成功。
- [x] `scripts/smoke-test.ps1` 通過：HTTPS、Authentication、REST、SignalR negotiation、MQTT 與 PostgreSQL 端對端鏈路正常。
- [x] API、Web、Simulator 容器分別以 uid 1654、101、1654 執行；Data Protection volume 已實際產生 key 檔。
- [x] 最終 API 紀錄中沒有 GSSAPI 載入錯誤、HTTPS port 判定警告、Antiforgery header 警告或未處理例外。
- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] 使用隔離 PostgreSQL 執行完整 `dotnet test`：52 passed、0 failed、0 skipped；測試資料庫會在結束時清除。
- [x] `npm run type-check` 與 `npm run lint` 通過。
- [x] `npm run test` 通過：3 test files、7 tests passed。
- [x] `npm run build` 通過：75 modules transformed，production assets 成功產生。
- [x] `npm audit`：0 vulnerabilities。
- [x] `dotnet list IoTMonitor.slnx package --vulnerable --include-transitive`：沒有已知易受攻擊的直接或傳遞套件。
- [x] `git diff --check` 沒有 whitespace error；僅顯示 Windows 工作目錄的 LF→CRLF 提示。
- [x] 驗證完成後確認沒有 `iot_monitor_tests_*` 資料庫殘留，並移除 `iot-monitor-stage8-test` 的 containers、network 與 volumes。
- [x] 以既有 PostgreSQL volume 實際驗證密碼輪替修復；保留資料同步角色密碼後，使用 `WEB_HTTP_PORT=8083` 避開其他容器占用，完整 smoke test 再次通過。

## 建議審查重點

1. 閱讀 `compose.yaml` 與三個 Dockerfile，確認服務依賴、網路暴露面及容器限制。
2. 閱讀 `Program.cs` 的 migration、Data Protection、OpenAPI、Demo Data 與 reverse proxy 設定。
3. 依 README 將 `.env.example` 複製為 `.env` 並替換三組範例密碼，再執行完整啟動與 smoke test。
4. 確認 README、`docs/CONTAINER_DEPLOYMENT.md` 與各開發文件足以讓面試官重現主要功能。
5. 審查完成後再由使用者提交；目前尚未建立任何 commit。

## 已知事項與風險

- Nginx 映像使用 build 時產生的 localhost 自簽憑證，只適合本機展示；正式部署必須改用受信任憑證或平台 TLS ingress。
- MQTT 尚未啟用 TLS；PostgreSQL 與 broker 的 host port 僅綁 `127.0.0.1` 供本機工具使用，不可直接照搬到公網環境。
- Compose 透過 environment 傳遞密碼，Data Protection key 存於 Docker volume 但未由 KMS 加密；正式環境應使用 secret manager 與受保護的 key storage。
- Compose 為方便展示而啟用 Bootstrap Admin、Demo Data 與 OpenAPI；正式環境應全部關閉，migration 也應改由一次性部署工作執行。
- 自動 migration 適用目前單一 API replica；多 replica 同時啟動前應改用獨立 migration job。
- SignalR 與 MQTT message ID 去重仍以單一 API 執行個體為假設；橫向擴充需加入 backplane 與分散式去重。
- 目前每筆超標量測都會建立新告警，尚未合併事件區間或加入冷卻時間；`DeviceOffline` 類型也尚未定義心跳期限。
- 本機 `5432` 已由其他專案使用，因此範例 PostgreSQL host port 為 `5433`。
- Codex shell 未繼承 Docker／.NET PATH；自動驗證使用已確認的完整執行檔路徑，不影響一般 PowerShell 使用者環境。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md`、`docs/IMPLEMENTATION_PLAN.md` 與本檔案。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 階段 8 目前為 `awaiting_review`；除非使用者提出審查修正，不得繼續擴張功能或自行提交。
4. 若使用者確認並已提交，可將階段 8 改為 `accepted`；新需求應先建立新的計畫階段。
