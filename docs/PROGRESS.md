# 專案進度與 AI 交接

最後更新：2026-10-13

## 目前閘門

- Current phase：`10 - 可擴充通訊協定接入架構`
- State：`awaiting_review`
- Owner action：使用者以一般瀏覽器信任 localhost 開發憑證後，審查階段 10 的 adapter 邊界、migration、API 與前端表單；PostgreSQL 與運行中容器的端對端驗證已完成，審查後由使用者自行提交，或明確要求 AI 提交。
- Next phase：`11 - Modbus TCP 垂直切片`；只有階段 10 經使用者驗收並提交後才能開始。

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
| 9 | 區域網路 MQTT 設備上線狀態 | `accepted` |
| 10 | 可擴充通訊協定接入架構 | `awaiting_review` |
| 11 | Modbus TCP 垂直切片 | `not_started` |

## 後續階段目標（尚未實作）

### 階段 11：Modbus TCP 垂直切片

- 進度：`not_started`；只有階段 10 進入 `awaiting_review`、經使用者審查提交並明確要求繼續後才能開始。
- 目標：新增 `modbus-tcp` adapter，輪詢啟用設備的 holding registers，將溫度／濕度正規化後送入既有資料流；不把 Modbus RTU 納入此階段。
- 測試方式：以本機可決定性 Modbus TCP server／fixture 回傳固定 registers，驗證正常讀取、timeout、斷線、非法回應、停用設備、重連及 MQTT 並行運作。
- 安全邊界：限制允許目的地、register 範圍、timeout、輪詢頻率及並行數；協定設定與 log 不保存或輸出秘密。若需加入正式相依套件，開始實作前另行提出版本與替代方案取得同意。
- 審查驗證：端對端核對 register 位址、資料型別、byte order、signedness 與縮放；確認 Modbus 故障不寫入錯誤資料、不拖垮 MQTT，且 REST、SignalR、告警與上線判定維持相容。

## 本階段完成內容

- 新增 `IDeviceTelemetryProtocolAdapter`、`DeviceProtocolRegistry` 與 `ProtocolIngestionCoordinator`；協定由 DI 發現，單一 adapter 結束或失敗不會停止其他 adapter，並會在節流後重啟。
- 將原有 MQTT background service 改為第一個 protocol adapter，保留 broker 連線、訂閱、QoS、重連、payload 解析及 message ID 去重；資料寫入、告警與 SignalR 仍只走既有 `TelemetryIngestionService`。
- 設備新增 `protocol_key` 與 `protocol_settings` JSONB migration；既有資料與未帶新欄位的 request 預設為 `mqtt`／空物件，並建立 `(protocol_key, is_active)` index。
- 新增受保護的 `GET /api/device-protocols` 目錄，回傳 key、顯示名稱、預設值及非敏感欄位描述；設定上限為 16 KiB，由 adapter 驗證並正規化，設備 response 與 log 不輸出設定 JSON。
- 設備建立 API 驗證未知協定及協定設定；MQTT processor 會拒絕指向非 MQTT、未知或停用設備的訊息。REST 遙測入口維持相容，不視為可選設備協定。
- Vue 新增設備表單由後端目錄產生協定選單與設定欄位，設備列表及明細顯示協定；目前只支援建立時選擇，不提供既有設備在線切換。
- 新增 registry、coordinator、MQTT 設定與路由、API 契約、前端表單及 API client 測試；更新 README、API、前端、MQTT 與協定擴充文件，未新增正式相依套件。
- PostgreSQL 實測發現 `timestamp with time zone` 為微秒精度；設備上線測試改以 1 微秒容差比較 API 寫入回應與重新查詢結果，避免把資料庫正常的精度截斷誤判為資料錯誤。

## 本階段驗證

- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] 未連 PostgreSQL 的 `dotnet test IoTMonitor.slnx -c Release --no-build --no-restore`：42 passed、0 failed、23 skipped；registry、coordinator、MQTT adapter 與 OpenAPI 測試通過。
- [x] `dotnet ef migrations script AddAuthenticationSecurity AddDeviceProtocolArchitecture ...` 成功產生向前 SQL，既有資料會回填 `mqtt` 與空 JSON 物件。
- [x] `dotnet ef migrations has-pending-model-changes ...` 回報沒有 migration 之後的模型變更。
- [x] `npm run type-check` 與 `npm run lint` 通過。
- [x] `npm run test` 通過：5 test files、11 tests passed；包含協定目錄驅動的設備建立表單。
- [x] `npm run build` 通過：76 modules transformed，production assets 成功產生。
- [x] 以 `127.0.0.1:5433` 的 PostgreSQL 執行完整 `dotnet test IoTMonitor.slnx -c Release --no-restore`：65 passed、0 failed、0 skipped；暫存資料庫實際套用全部 migration。
- [x] 對運行中的 `https://localhost:8443` 容器登入、讀取協定目錄並建立 `stage10-sql-20261012221522`；API 列表／詳情皆回傳 `mqtt` 且未輸出設定，再直接查 PostgreSQL 確認 `protocol_key=mqtt`、`protocol_settings={}`。
- [x] 以等價 smoke 流程驗證容器 health、Vue/CSP、Authentication、REST、協定目錄、SignalR negotiate、MQTT simulator 與 PostgreSQL 遙測全數通過；容器前端 bundle 亦包含 `/api/device-protocols` 與 `protocolKey`。
- [ ] `scripts/smoke-test.ps1` 本身在目前 Codex shell 的 Windows Schannel 回報 `SEC_E_NO_CREDENTIALS`，因此改用 Python OpenSSL 執行相同端點流程；應在使用者的一般 PowerShell 再確認腳本本身。Docker Desktop 正在執行，但此 shell 仍找不到 Docker CLI，未由 CLI 再次重建映像。
- [ ] Codex 內嵌瀏覽器會拒絕容器的 localhost 自簽憑證，依安全規範不能代為略過警告；Admin 前端表單的最後視覺操作需由使用者在已信任憑證的一般瀏覽器確認。

## 建議審查重點

1. 檢查 adapter 是否只處理接入與設定差異，沒有複製 `TelemetryIngestionService` 的資料庫、告警或 SignalR 邏輯。
2. 審查 migration 的 Up／Down：Up 已由完整 PostgreSQL 測試與運行中資料庫驗證；Down 會刪除協定設定資料，回退前仍須確認可捨棄或已備份。
3. 由 Admin 使用前端以預設及明確 MQTT 建立設備，確認目錄載入、列表／明細顯示與 validation Problem Details；Viewer／Operator 只能讀取目錄。
4. 發布 MQTT 到 MQTT、非 MQTT、未知及停用設備，確認只有第一種會寫入；再模擬 adapter 例外確認其他 adapter 不受影響。
5. 審查 API response、validation 與 log，確認沒有輸出 `protocol_settings`；完整 PostgreSQL 與等價容器 smoke 已通過，提交前再以一般 PowerShell／瀏覽器確認原始 smoke script 與畫面操作。

## 已知事項與風險

- 階段 10 目前只有 MQTT adapter；Modbus TCP 尚未實作，必須等階段 10 經審查提交後才能開始階段 11。
- 設備協定只能在建立時選擇；`protocol_settings` 不由 API 回傳。未來若需修改或切換，必須另外定義停用、驗證及背景工作交接流程。
- PostgreSQL、migration、容器 API 與 MQTT smoke 已在真實本機基礎設施驗證；為保留可複查證據，資料庫目前留有測試設備 `stage10-sql-20261012221522`。
- 目前 Codex shell 仍沒有 Docker CLI；`scripts/smoke-test.ps1` 又受 Windows Schannel `SEC_E_NO_CREDENTIALS` 影響，但相同 HTTPS／Authentication／REST／SignalR／MQTT／PostgreSQL 流程已用 Python OpenSSL 通過。這是驗證工具環境差異，仍建議使用者在一般 PowerShell 重跑原始腳本。
- 階段 9 的同 Wi-Fi 手機 MQTT 發布未由本輪 AI 親自驗證；使用者指示進入階段 10 時，階段 9 程式碼已存在提交 `b41f49d`。
- `AddDeviceProtocolArchitecture` 的 Down migration 會刪除協定 key 與設定資料；只可在確認資料可捨棄或已備份時回退。
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
- 範例 PostgreSQL host port 維持 `5433` 以避免既有環境衝突；本輪已確認 `127.0.0.1:5433` 可連線並完成整合測試與直接 SQL 查詢。
- 本輪 Codex shell 可直接使用 .NET 與 Node.js 工具，但沒有可用的 Docker CLI；Docker Desktop 程序及既有 Compose runtime 正常，不影響一般已安裝 Docker CLI 的 PowerShell 環境。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md`、`docs/IMPLEMENTATION_PLAN.md` 與本檔案。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 階段 10 目前為 `awaiting_review`；只能回答問題或修正本階段，不得提前實作階段 11。
4. PostgreSQL 完整測試與容器等價 smoke 已通過；使用者以一般 PowerShell 重跑 `scripts/smoke-test.ps1`，並在信任 localhost 開發憑證的瀏覽器完成前端表單視覺驗收。
5. 使用者審查並提交階段 10 後，仍須明確要求繼續，才可將階段 11 改為 `in_progress`；若 Modbus 實作需要正式套件，必須先取得同意。
