# MQTT 遙測與設備模擬器

## 資料流

```text
IoTMonitor.DeviceSimulator
        │ QoS 1 publish
        ▼
Eclipse Mosquitto ── devices/{externalDeviceId}/telemetry
        │ subscribe
        ▼
IoTMonitor.Api BackgroundService ──► PostgreSQL ──► REST API
              │                         │
              └── 告警規則 ────────────┴──► SignalR ──► Vue
```

API 的 MQTT 接收功能預設停用，避免沒有 broker 或本機秘密設定時影響一般 API 與測試。Mosquitto 必須先依照 [`LOCAL_DEVELOPMENT.md`](LOCAL_DEVELOPMENT.md) 啟動。

## 啟用 API MQTT 接收

使用 User Secrets 設定本機 broker，不要把真實密碼加入 `appsettings*.json`：

```powershell
dotnet user-secrets set --project src/IoTMonitor.Api/IoTMonitor.Api.csproj "Mqtt:Enabled" "true"
dotnet user-secrets set --project src/IoTMonitor.Api/IoTMonitor.Api.csproj "Mqtt:Host" "localhost"
dotnet user-secrets set --project src/IoTMonitor.Api/IoTMonitor.Api.csproj "Mqtt:Port" "1883"
dotnet user-secrets set --project src/IoTMonitor.Api/IoTMonitor.Api.csproj "Mqtt:Username" "iot_monitor"
dotnet user-secrets set --project src/IoTMonitor.Api/IoTMonitor.Api.csproj "Mqtt:Password" "<your-local-password>"
```

接著依照 [`API_DEVELOPMENT.md`](API_DEVELOPMENT.md) 啟動 API。成功時會看到已連線並訂閱 `devices/+/telemetry` 的 log。

也可以用 `Mqtt__Enabled`、`Mqtt__Host`、`Mqtt__Port`、`Mqtt__Username`、`Mqtt__Password` 等環境變數覆寫設定。

## Topic 與 payload

Topic 中使用設備的 `externalId`，不是資料庫 UUID：

```text
devices/{externalDeviceId}/telemetry
```

Payload：

```json
{
  "messageId": "0c2fa900-cdba-44bd-94ec-167a7533de49",
  "temperatureCelsius": 24.8,
  "humidityPercent": 52.4,
  "recordedAtUtc": "2026-10-01T02:00:00Z"
}
```

規則：

- `messageId` 必須是非空 UUID，用於基本重複訊息判斷。
- 溫度允許 -100 至 200°C，濕度允許 0 至 100%。
- `recordedAtUtc` 會轉換為 UTC，且不可超過伺服器目前時間五分鐘以上。
- Topic 中的設備必須存在且為啟用狀態。
- Payload 上限為 4 KiB；無效 JSON、topic、設備或數值只會留下不含 payload 的診斷 log，不會寫入資料庫。

API 會在記憶體中保留最近 10 分鐘、最多 10,000 組 `(externalDeviceId, messageId)`。這能處理單一 API 執行個體常見的 QoS 1 重送；API 重啟或未來水平擴充後，需改用資料庫唯一鍵或分散式儲存，才能提供跨執行個體去重。

## 建立模擬設備

模擬器預設發布 `sim-device-001`。若要模擬三台設備，先透過 API 建立相同的 external id：

```powershell
$apiBase = "https://localhost:7080"

1..3 | ForEach-Object {
  $externalId = "sim-device-{0:D3}" -f $_
  $body = @{
    externalId = $externalId
    name = "Simulated device $_"
  } | ConvertTo-Json

  Invoke-RestMethod `
    -Uri "$apiBase/api/devices" `
    -Method Post `
    -ContentType "application/json" `
    -Body $body
}
```

## 啟動模擬器

模擬器使用環境變數，不會自動讀取 `.env`：

```powershell
$env:MQTT_HOST = "localhost"
$env:MQTT_PORT = "1883"
$env:MQTT_USERNAME = "iot_monitor"
$env:MQTT_PASSWORD = "<your-local-password>"
$env:SIMULATOR_DEVICE_PREFIX = "sim-device"
$env:SIMULATOR_DEVICE_COUNT = "3"
$env:SIMULATOR_INTERVAL_SECONDS = "5"
$env:SIMULATOR_MESSAGES_PER_DEVICE = "0"

dotnet run --project src/IoTMonitor.DeviceSimulator/IoTMonitor.DeviceSimulator.csproj
```

`SIMULATOR_MESSAGES_PER_DEVICE=0` 代表持續發送，按 `Ctrl+C` 停止；設為 `1` 可讓每台設備只發送一筆，適合 smoke test。

可調整項目：

| 環境變數 | 預設值 | 說明 |
|---|---:|---|
| `MQTT_HOST` | `localhost` | Broker host。 |
| `MQTT_PORT` | `1883` | Broker port。 |
| `MQTT_USERNAME` | 必填 | Broker 帳號。 |
| `MQTT_PASSWORD` | 必填 | Broker 密碼。 |
| `MQTT_CLIENT_ID` | 程序 ID 組成 | 模擬器 MQTT client id。 |
| `SIMULATOR_DEVICE_PREFIX` | `sim-device` | external id 前綴。 |
| `SIMULATOR_DEVICE_COUNT` | `1` | 模擬設備數，1 到 1,000。 |
| `SIMULATOR_INTERVAL_SECONDS` | `5` | 每批發送間隔秒數。 |
| `SIMULATOR_MESSAGES_PER_DEVICE` | `0` | 每台發送筆數；0 代表持續。 |
| `MQTT_RECONNECT_DELAY_SECONDS` | `5` | 斷線重試間隔秒數。 |

寫入後可透過階段 3 API 查詢：

```http
GET https://localhost:7080/api/devices/{deviceId}/telemetry/latest
GET https://localhost:7080/api/devices/{deviceId}/telemetry?page=1&pageSize=20
```

## 手動驗證重連

保持 API 與模擬器執行，短暫停止並重新啟動 broker：

```powershell
docker compose stop mosquitto
docker compose start mosquitto
docker compose ps mosquitto
```

API 與模擬器應記錄斷線與重試，broker 恢復後自動連線，後續訊息可再次由 REST API 查到。
