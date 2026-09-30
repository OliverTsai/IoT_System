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

## 變更計畫的規則

- 階段順序或技術選擇需要調整時，先說明原因、影響與替代方案。
- 使用者同意後，先修改本檔案與 `docs/PROGRESS.md`，才開始受影響的實作。
- 新需求預設加入未開始的階段；不得默默擴張目前階段範圍。

