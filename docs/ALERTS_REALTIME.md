# 告警與 SignalR 即時更新

階段 7 讓 REST 與 MQTT 遙測共用相同的寫入流程。每筆有效量測會先寫入 PostgreSQL、依設定評估告警，再於交易成功後發布 SignalR 事件。

## 告警閾值

預設設定位於 `src/IoTMonitor.Api/appsettings.json`：

| 指標 | Warning 範圍 | Critical 範圍 |
|---|---:|---:|
| 溫度 | 10–35°C | 0–45°C |
| 濕度 | 30–75% | 15–90% |

數值等於邊界時不產生告警；超出 Warning 邊界時產生 Warning，超出外層 Critical 邊界時產生 Critical。同一筆量測最多產生一筆溫度告警及一筆濕度告警。設定 `AlertRules:Enabled=false` 可停止產生新告警，不會刪除既有資料。

本機需要不同閾值時，使用 User Secrets 或環境變數覆寫，不要建立包含正式設定的額外檔案。例如：

```powershell
dotnet user-secrets set --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "AlertRules:TemperatureMaximumCelsius" "32"

$env:AlertRules__HumidityMaximumPercent = "70"
```

Warning 最小值必須小於最大值，且必須位於 Critical 範圍內；溫度整體限制為 -100 至 200°C，濕度整體限制為 0 至 100%。API 啟動時會驗證設定，無效設定會拒絕啟動。

## 告警 API

所有端點都需要 Cookie Authentication：

| Method | Path | 權限 | 說明 |
|---|---|---|---|
| `GET` | `/api/alerts` | Viewer 以上 | 分頁及篩選告警。 |
| `PATCH` | `/api/alerts/{alertId}/acknowledge` | Operator／Admin | 確認告警；重複呼叫維持第一次確認時間。 |

查詢參數：

- `deviceId`：設備 UUID。
- `type`：`TemperatureOutOfRange`、`HumidityOutOfRange` 或 `DeviceOffline`。
- `severity`：`Information`、`Warning` 或 `Critical`。
- `acknowledged`：`true` 或 `false`。
- `page`、`pageSize`：頁碼從 1 開始，每頁 1 到 100 筆。

確認告警屬於狀態修改，除了登入 Cookie 外仍必須帶有效的 `X-CSRF-TOKEN`。`DeviceOffline` 已保留在資料模型中，但階段 7 尚未定義離線心跳期限，因此目前只會由溫度與濕度規則產生告警。

## SignalR Hub

已登入的瀏覽器會連線：

```text
https://localhost:7080/hubs/monitoring
```

Hub 不提供瀏覽器可呼叫的修改方法，只由伺服器發布三種事件：

| 事件 | Payload | 發布時機 |
|---|---|---|
| `TelemetryReceived` | `TelemetryResponse` | REST 或 MQTT 遙測完成持久化後。 |
| `AlertRaised` | `AlertResponse` | 新告警與遙測在同一交易完成後。 |
| `AlertAcknowledged` | `AlertResponse` | Operator／Admin 完成確認後。 |

即時發布失敗不會回滾已完成的資料庫交易；伺服器會記錄 warning，瀏覽器則靠 REST 補抓恢復正確狀態。

## 斷線與資料補抓策略

Vue 使用 `@microsoft/signalr` 的自動重連，依序在 0、2、5、10 秒嘗試。內建重連耗盡或初次連線失敗後，應用程式仍會以 2、5、10、30 秒上限持續嘗試。

每次初次連線或重新連線成功後，各畫面都會重新呼叫 REST API：

- 總覽重新取得最近設備與待確認告警。
- 設備明細重新取得設備資訊與最近 50 筆量測。
- 告警中心重新取得目前篩選條件與頁碼。

SignalR 事件負責低延遲更新；REST 補抓負責填補連線建立前或中斷期間可能漏掉的事件。前端依遙測／告警 ID 去重，並依發生時間重新排序。

## 資料庫

`alerts` 資料表與設備關聯已包含在初始 migration，因此本階段不需要新增或執行 migration。告警確認只更新 `acknowledged_at_utc`。

## 驗證

後端規則與 API 測試涵蓋安全邊界、Warning、Critical、停用規則、REST／MQTT 告警產生、查詢篩選、角色權限及冪等確認。前端測試涵蓋即時事件派送、重連補抓通知，以及告警 API 的 CSRF 操作。
