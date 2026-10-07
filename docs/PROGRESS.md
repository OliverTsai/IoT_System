# 專案進度與 AI 交接

最後更新：2026-10-07

## 目前閘門

- Current phase：`9 - 區域網路 MQTT 設備上線狀態`
- State：`awaiting_review`
- Owner action：使用者審查階段 9 的程式碼，並以同一 Wi-Fi 手機執行一次實機 MQTT 發布；確認後由使用者自行提交，或明確要求 AI 提交。
- Next phase：無；後續若要自行開發手機 App、加入 MQTT over WebSocket 或 TLS，需另立新階段。

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
| 8 | 完整容器化、品質與作品集整理 | `accepted` |
| 9 | 區域網路 MQTT 設備上線狀態 | `awaiting_review` |

## 本階段完成內容

- `DeviceResponse` 與 `DeviceDetailsResponse` 新增 `lastSeenAtUtc`、`isOnline`；由現有 telemetry 的最新 `received_at_utc` 計算，不新增資料表或 migration。
- 新增共用 `DevicePresence` 規則：已啟用設備在最近 30 秒內有有效遙測即為上線，否則為離線。
- Vue 將管理狀態與連線狀態分開顯示；設備列表新增最後回報時間，總覽的「運作中」改為實際近期有回報的設備數。
- 設備列表、總覽及明細沿用 `TelemetryReceived` SignalR 事件即時更新最後回報；前端每秒重新判定，超過 30 秒不需重新整理即可轉為離線。
- Mosquitto host port 改由 `MQTT_BIND_ADDRESS` 控制，預設仍是 `127.0.0.1`；使用者可明確設定電腦私有 IPv4 供同 Wi-Fi 手機測試。
- 補上 API 整合測試、後端／前端單元測試，以及手機 MQTT Client 的 host、port、topic、payload、防火牆與還原操作文件。
- 更新 README、完整容器文件、MQTT 文件、實作計畫及 `.env.example`；未加入新正式相依套件。

## 本階段驗證

- [x] `docker compose --env-file .env.example config --quiet` 通過，新的 bind address 插值語法正確。
- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] 使用現有 PostgreSQL 執行完整 `dotnet test`：57 passed、0 failed、0 skipped；包含 30 秒上線／離線 API 整合測試。
- [x] `npm run type-check` 與 `npm run lint` 通過。
- [x] `npm run test` 通過：4 test files、9 tests passed。
- [x] `npm run build` 通過：76 modules transformed，production assets 成功產生。
- [x] API 與 Vue/Nginx 映像成功重建，PostgreSQL、Mosquitto、API、Web、Simulator 全部維持 healthy／running。
- [x] `scripts/smoke-test.ps1` 通過：HTTPS、Authentication、REST、SignalR negotiation、MQTT 與 PostgreSQL 端對端鏈路正常。
- [x] 重建後 API 成功訂閱 `devices/+/telemetry`；API／Web logs 沒有啟動錯誤或未處理例外。
- [x] `git diff --check` 沒有 whitespace error；僅顯示 Windows 工作目錄的 LF→CRLF 提示。
- [ ] 尚未由實體手機跨 Wi-Fi 發布；需由使用者依 `docs/MQTT_DEVELOPMENT.md` 完成最終人工驗收。

## 建議審查重點

1. 確認新建但未回報的設備顯示「離線」，不是「運作中」。
2. 確認 simulator 每五秒回報時顯示「運作中」，停止 simulator 超過 30 秒後自動顯示「離線」。
3. 暫時將 `.env` 的 `MQTT_BIND_ADDRESS` 設為電腦私有 IPv4，依 `docs/MQTT_DEVELOPMENT.md` 使用手機 MQTT Client 發布一筆資料。
4. 確認手機 topic 中的 external id 必須先由 Admin 建立，未知或停用設備仍會被 API 拒絕。
5. 實機驗收後將 bind address 還原為 `127.0.0.1`，再由使用者提交；AI 尚未 commit。

## 已知事項與風險

- Nginx 映像使用 build 時產生的 localhost 自簽憑證，只適合本機展示；正式部署必須改用受信任憑證或平台 TLS ingress。
- MQTT 尚未啟用 TLS；PostgreSQL host port 固定綁 `127.0.0.1`，broker 預設也綁 loopback，但可明確改綁私有 IPv4 供同 Wi-Fi 測試，不可直接照搬到公網環境。
- Compose 透過 environment 傳遞密碼，Data Protection key 存於 Docker volume 但未由 KMS 加密；正式環境應使用 secret manager 與受保護的 key storage。
- Compose 為方便展示而啟用 Bootstrap Admin、Demo Data 與 OpenAPI；正式環境應全部關閉，migration 也應改由一次性部署工作執行。
- 自動 migration 適用目前單一 API replica；多 replica 同時啟動前應改用獨立 migration job。
- SignalR 與 MQTT message ID 去重仍以單一 API 執行個體為假設；橫向擴充需加入 backplane 與分散式去重。
- 目前每筆超標量測都會建立新告警，尚未合併事件區間或加入冷卻時間；`DeviceOffline` 類型也尚未定義心跳期限。
- 此階段的「上線」代表最近 30 秒內有成功寫入的遙測，不代表 broker TCP session；單純連線但未發布資料仍顯示離線。
- 30 秒門檻目前是前後端共用的固定值；若未來提供可設定的發布間隔，應將門檻改為共享設定或由 API 回傳有效期限。
- 手機測試仍使用共用 MQTT 帳密與未加密 1883，只適合可信任區網；正式設備接入需加入 TLS、每設備認證與 topic ACL。
- 本機 `5432` 已由其他專案使用，因此範例 PostgreSQL host port 為 `5433`。
- Codex shell 未繼承 Docker／.NET PATH；自動驗證使用已確認的完整執行檔路徑，不影響一般 PowerShell 使用者環境。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md`、`docs/IMPLEMENTATION_PLAN.md` 與本檔案。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 階段 9 目前為 `awaiting_review`；除非使用者提出審查修正，不得繼續擴張功能或自行提交。
4. 使用者需完成同 Wi-Fi 手機 MQTT 實機驗收；確認並提交後可將階段 9 改為 `accepted`。
