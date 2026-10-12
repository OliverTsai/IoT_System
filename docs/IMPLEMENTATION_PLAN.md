# IoT 設備監控系統實作計畫

## 目標

建立一個可用於求職作品展示的 IoT 設備監控系統，實際呈現 ASP.NET Core、Vue、Web API、MQTT、PostgreSQL、安全驗證、RWD 與容器化能力。

預定資料流：

```text
Device Simulator
       │ MQTT
       ▼
Eclipse Mosquitto
       │ subscribe
       ▼
ASP.NET Core MQTT Worker ──► PostgreSQL
                                  │
                                  ▼
Vue 3 Dashboard ◄────────── ASP.NET Core Web API
```

## 預定 repository 結構

```text
src/
  IoTMonitor.Api/
  IoTMonitor.DeviceSimulator/
  iot-monitor-web/
tests/
  IoTMonitor.Api.Tests/
infra/
  mosquitto/
docs/
```

結構會依各階段逐步建立，不應一次產生沒有實際用途的空專案或抽象層。

## 階段 0：Git 與 AI 協作基線

範圍：

- 初始化 Git repository，預設分支為 `main`。
- 建立適用於 .NET、Visual Studio、Vue／Node 與秘密設定的 `.gitignore`。
- 建立 AI 階段閘門、實作計畫與進度追蹤文件。
- 保留現有 ASP.NET Core MVC 範本，不修改應用程式功能。

驗收條件：

- `git status` 不包含 `.vs`、`bin`、`obj` 或 `*.user`。
- AI 文件明確規定不得跨階段、不得自行 commit。
- 進度檔清楚指出目前狀態與下一個使用者動作。

## 階段 1：本機基礎設施

範圍：

- 建立 Docker Compose 開發環境。
- 啟動 PostgreSQL 與 Eclipse Mosquitto；此階段不容器化 API 與 Vue。
- 建立 Mosquitto 開發設定、持久化 volume 與健康檢查。
- 提供不含真實密碼的 `.env.example`。
- 文件化啟動、停止及驗證方式。

驗收條件：

- `docker compose config` 通過。
- PostgreSQL 與 Mosquitto 容器可正常啟動並通過健康檢查。
- 可以發布及訂閱一筆測試 MQTT 訊息。
- repository 中沒有真實密碼或憑證。

## 階段 2：ASP.NET Core Web API 與資料持久化基礎

範圍：

- 將通用名稱改為 `IoTMonitor`，整理為 API 專案，但避免不必要的多層架構。
- 設定 Controllers、OpenAPI、統一錯誤處理與健康檢查。
- 加入 PostgreSQL 的 EF Core provider 與 `DbContext`。
- 建立 `Device`、`Telemetry`、`Alert` 的初始資料模型與 migration。
- 設定開發環境連線字串，不將秘密提交到 Git。
- 建立測試專案與基本 application factory／資料庫測試基礎。

驗收條件：

- Solution build 成功且無警告。
- API 與資料庫 health check 正常。
- Migration 可以套用到階段 1 的本機 PostgreSQL。
- OpenAPI 文件可以顯示。
- 基本測試可以執行。

## 階段 3：設備與遙測 REST 垂直切片

範圍：

- 實作設備新增、列表、詳情、更新狀態。
- 實作遙測資料寫入，以及依設備、時間範圍查詢。
- 使用 DTO、輸入驗證、分頁與一致的錯誤回應。
- 處理不存在設備、重複識別碼、無效時間與無效量測值。
- 為主要成功路徑與錯誤路徑建立整合測試。

驗收條件：

- 可由 REST API 建立設備並寫入溫度／濕度資料。
- 可查詢設備最新值與歷史資料。
- OpenAPI 清楚描述 request、response 與錯誤狀態。
- 整合測試覆蓋主要成功與失敗案例。

## 階段 4：MQTT 資料接收與設備模擬器

範圍：

- 加入 MQTT client 與 ASP.NET Core `BackgroundService`。
- 訂閱 `devices/{deviceId}/telemetry`。
- 驗證 topic、裝置身分、JSON payload、時間與數值範圍。
- 實作斷線重連、取消權杖、錯誤紀錄與基本重複訊息策略。
- 建立可設定發送頻率與設備數量的裝置模擬器。
- 為訊息解析及寫入流程建立測試。

驗收條件：

- 模擬器發布的訊息會經由 Mosquitto 寫入 PostgreSQL。
- 寫入結果可透過階段 3 的 API 查詢。
- Broker 暫時中斷後，服務能重連且不會崩潰。
- 無效訊息不會污染資料庫，並留下可診斷紀錄。

## 階段 5：Authentication、Authorization 與安全基線

範圍：

- 加入使用者登入與安全密碼雜湊。
- 實作 JWT 或安全 Cookie；選擇前需記錄理由。
- 建立 `Admin`、`Operator`、`Viewer` 角色或對應 policy。
- 保護設備、遙測與告警 API。
- 加入 CORS 白名單、rate limiting、安全標頭與敏感資訊遮蔽。
- 測試匿名、授權成功及權限不足情境。

驗收條件：

- 匿名使用者無法讀取受保護資料。
- `Viewer` 無法修改設備，`Admin` 可以。
- Token／Cookie 過期與錯誤登入有正確回應。
- Log、設定與 Git history 不包含秘密資料。

## 階段 6：Vue 3 RWD 儀表板

範圍：

- 建立 Vue 3、TypeScript、Vite 前端。
- 實作登入、登出與受保護路由。
- 建立設備列表、設備詳情、最新量測、歷史趨勢與告警列表。
- 處理 loading、empty、error、unauthorized 狀態。
- 完成桌面、平板與手機尺寸的 RWD。
- 建立重要元件與資料存取邏輯測試。

驗收條件：

- 使用者可登入並依權限查看或操作設備。
- 儀表板能顯示 API 的真實資料，而非硬編碼 mock。
- 主要頁面在常見桌面與手機寬度可正常操作。
- 前端 build、lint 與測試通過。

## 階段 7：告警與即時更新

範圍：

- 根據可設定閾值產生溫度／濕度告警。
- 實作告警確認與狀態查詢。
- 使用 SignalR 將最新遙測與告警推送至 Vue。
- 定義中斷後重新連線及資料補抓策略。
- 建立告警規則與即時更新測試。

驗收條件：

- 超過閾值時只產生符合規則的告警。
- Vue 不需重新整理即可看到新遙測與告警。
- SignalR 中斷後能恢復，且畫面不會永久遺漏資料。

## 階段 8：完整容器化、品質與作品集整理

範圍：

- 建立 API、Vue 與模擬器的多階段 Dockerfile。
- 由 Docker Compose 整合 API、Vue、PostgreSQL、Mosquitto 與模擬器。
- 加入 health check、啟動相依性、restart policy 與非 root 執行設定。
- 執行後端與前端的完整 build、test、lint 與基本端對端 smoke test。
- 補齊 README：架構、技術選擇、啟動方式、範例帳號、API 與 MQTT 範例。
- 檢查安全、錯誤處理、日誌、效能與作品展示內容。

驗收條件：

- 新環境可透過一組明確指令啟動整套系統。
- 健康檢查及 smoke test 通過。
- README 足以讓面試官重現主要功能。
- repository 不包含產物、秘密、無用程式或未解釋的 TODO。

## 階段 9：區域網路 MQTT 設備上線狀態

範圍：

- 允許以設定值將 Mosquitto host port 綁定到指定區域網路介面，預設仍只綁定 loopback。
- 將「管理員啟用／停用」與「設備最近是否有回報」分成兩個狀態。
- 以最近 30 秒內收到的遙測判定設備上線，不新增 presence topic、資料表或 migration。
- 讓設備列表、總覽與設備明細透過既有 SignalR 遙測事件立即更新最後回報時間，並在逾時後於前端轉為離線。
- 文件化使用同一 Wi-Fi 的手機 MQTT Client 發布假溫濕度資料之設定與驗證流程。
- 補上 API 與前端狀態判定測試，並確認原有模擬器及 MQTT 流程維持相容。

驗收條件：

- 未發布遙測的已啟用設備顯示為離線，且不再把 `IsActive` 誤標為「運作中」。
- 手機在同一區域網路中可透過電腦的私有 IP 與 port 1883 發布既有 MQTT payload。
- 有效遙測寫入後，管理介面不需重新整理即可顯示設備上線及最新量測。
- 停止發布超過 30 秒後，管理介面自動顯示設備離線。
- Broker 的區域網路綁定需明確設定；預設設定不會擴大既有網路暴露面。
- 後端 build／test 與前端 type-check／lint／test／build 通過。

## 階段 10：可擴充通訊協定接入架構

目標：

- 以最小重構將目前僅支援 MQTT 的接收流程整理為可註冊的通訊協定 adapter；新增協定時不需要修改既有 adapter 或共用遙測寫入流程。
- 讓 Admin 在前端建立設備時，能從後端提供的協定清單選擇該設備的通訊協定。

範圍：

- 在設備資料加入穩定的協定 key 與協定設定 JSON；既有資料 migration 回填為 `mqtt`，且新建設備未指定時仍以 `mqtt` 相容既有 API 呼叫端。
- 定義小型 `IDeviceTelemetryProtocolAdapter` 契約與 registry／coordinator。每個 adapter 負責協定描述、設定驗證及接收生命週期；共用的資料持久化、告警與 SignalR 仍沿用 `TelemetryIngestionService`。
- 將現有 MQTT background service、parser 與 processor 收斂為第一個 adapter，保留既有 topic、payload、QoS、重連與 message ID 去重行為。
- MQTT 訊息只能寫入協定為 `mqtt` 且已啟用的設備，避免不同協定共用 external id 時發生錯誤路由。
- 提供唯讀的支援協定目錄 API，回傳穩定 key、顯示名稱與建立設備所需的非敏感欄位描述；前端建立表單由此產生協定選單及設定欄位，不硬編碼協定清單。
- 設備建立、列表與詳情契約回傳協定 key；只有 Admin 可提交協定設定，回應與 log 不得洩漏秘密值。
- 本階段只支援「建立時選擇協定」，不實作既有設備在線切換協定；REST 遙測端點保留為管理／測試入口，不視為設備的可選通訊協定。
- 補上 migration、registry／coordinator、MQTT 相容性、API 驗證與前端建立表單測試，並更新 API、前端與通訊協定開發文件。

驗收條件：

- 既有設備與未帶新欄位的建立請求皆維持 `mqtt`，原有 MQTT、REST、告警、SignalR 及設備上線判定測試通過。
- Admin 可在新增設備表單看到後端公布的協定並建立 MQTT 設備；Viewer／Operator 無法藉由新端點修改設備設定。
- 未知協定 key、格式錯誤或不符合 adapter 規則的設定會回傳一致的 validation response，且不會寫入資料庫。
- coordinator 可透過 DI 發現 adapter；使用測試 adapter 證明新增協定不需修改 coordinator 或 MQTT adapter，且單一 adapter 失敗不會停止其他 adapter。
- MQTT 收到指向非 MQTT、未知或停用設備的訊息時會拒絕寫入並留下不含秘密的可診斷紀錄。
- migration 可在既有資料庫套用，且回退前已明確說明 JSON 設定欄位的資料影響；repository 不含設備密碼、Token 或真實端點秘密。
- 後端 build／test 與前端 type-check／lint／test／build 通過，既有 MQTT smoke test 維持成功。

審查與驗證方向：

1. 檢查協定抽象是否只處理接入差異，沒有複製資料庫寫入、告警或 SignalR 邏輯。
2. 以測試 adapter 實際驗證擴充點，確認新增協定的必要變更集中在新 adapter、DI 註冊與必要文件。
3. 由前端建立未明確選擇及明確選擇 MQTT 的設備，確認預設值、API response、列表與詳情一致。
4. 對未知 key、無效 JSON、非 MQTT 設備發布 MQTT 及 adapter 例外進行負向測試，確認不會誤寫或拖垮其他接收流程。
5. 審查協定設定的輸出與 log，確認秘密欄位不回傳、不記錄；未來若協定需要憑證，只保存 secret reference，不把秘密直接存入 JSON。

## 階段 11：Modbus TCP 垂直切片

目標：

- 透過階段 10 的擴充點加入第二個實際通訊協定，以可重現的 Modbus TCP 測試證明架構可用，而不是只建立抽象層。

範圍：

- 以 `modbus-tcp` 作為第二個協定；本階段不處理需要實體序列埠的 Modbus RTU。
- 新增 Modbus TCP adapter，定期輪詢指派給它且已啟用的設備，讀取溫度與濕度 holding registers，正規化後交由既有 `TelemetryIngestionService`。
- 協定設定至少包含 host、port、unit id、溫度／濕度 register 位址與縮放倍率；輪詢週期、timeout、重試及最大並行數由伺服器端設定限制，不允許前端提供無界值。
- 對可連線目的地加入明確 allowlist／網段限制，限制 payload、register 範圍與連線時間；失敗時不得寫入部分或預設遙測，也不得在 log 洩漏敏感設定。
- 在協定目錄公布 Modbus TCP 的非敏感欄位描述，讓同一個設備建立表單可選擇 `modbus-tcp` 並輸入必要設定。
- 建立可決定性回傳 registers 的本機測試伺服器／fixture，涵蓋正常讀取、timeout、斷線重連、非法回應與設備停用；若加入正式環境套件，實作前須先提出用途、版本、替代方案及維護風險並取得使用者同意。
- 更新 Docker Compose smoke test 或提供等價的一鍵本機驗證流程，並更新架構圖、設定範例與 Modbus TCP 操作文件。

驗收條件：

- Admin 可由前端建立 Modbus TCP 設備，無效 endpoint、unit id、register 或縮放設定會在建立時被拒絕。
- 本機測試伺服器提供已知 register 值後，adapter 能輪詢並寫入正確的溫度／濕度；資料可由既有 REST API 查詢，前端亦能透過既有 SignalR 流程即時更新。
- 停用設備後不再輪詢；timeout、連線中斷或格式錯誤只影響該設備，不寫入錯誤資料，也不停止 MQTT 或其他 Modbus 設備。
- 未在 allowlist 的目的地不會發起連線；測試及 log 不包含真實設備位址、密碼或其他秘密。
- 既有 MQTT 設備、模擬器、REST 寫入、告警與 30 秒上線狀態行為沒有迴歸。
- 後端 build／test、前端 type-check／lint／test／build，以及包含 MQTT、Modbus TCP、PostgreSQL 的 smoke test 通過。

審查與驗證方向：

1. 先以固定 register 值做端對端驗收，逐項核對位址、資料型別、byte order、signedness 與縮放結果。
2. 模擬 timeout、斷線、短封包、錯誤 transaction／unit id 與非法值，確認錯誤隔離、重試節流及取消權杖均正確。
3. 同時運行 MQTT 與 Modbus TCP 測試設備，確認兩者各自寫入正確設備，且任一協定失敗不影響另一協定。
4. 檢查輪詢查詢與連線數是否有上限，停用或刪除設備後是否及時停止輪詢，避免背景工作洩漏。
5. 審查目的地 allowlist、設定遮蔽與相依套件決策；確認目前結果只宣稱支援 Modbus TCP，不誤稱已支援 Modbus RTU 或完整功能碼集合。

## 變更計畫的規則

- 階段順序或技術選擇需要調整時，先說明原因、影響與替代方案。
- 使用者同意後，先修改本檔案與 `docs/PROGRESS.md`，才開始受影響的實作。
- 新需求預設加入未開始的階段；不得默默擴張目前階段範圍。
