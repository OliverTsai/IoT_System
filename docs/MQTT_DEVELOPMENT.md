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

完整 Compose 會啟用 subscriber、建立與模擬器 prefix/count 相符的 demo devices，並在 API healthy 後啟動容器化模擬器，因此全新環境不需手動建立設備。appsettings 與 IDE 模式仍維持預設停用。

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

## 同一 Wi-Fi 的手機驗證

手機不需要公網固定 IP；手機與執行 Docker Compose 的電腦位於同一個可信任 Wi-Fi 時，可以直接連到電腦的私有 IPv4。這個驗證使用 MQTT TCP port `1883`，不是管理網站的 HTTP port `8080`／`8083`。

一般情況下 `.env` 應維持：

```text
MQTT_BIND_ADDRESS=127.0.0.1
```

需要手機測試時，先以 `ipconfig` 找出電腦 Wi-Fi 介面的 IPv4，例如 `192.168.1.50`，再暫時改為：

```text
MQTT_BIND_ADDRESS=192.168.1.50
```

重新建立 broker port binding：

```powershell
docker compose up -d --force-recreate --wait mosquitto
```

Windows 防火牆只應允許私人網路的 TCP 1883；不要設定路由器 port forwarding，也不要將未啟用 TLS 的 broker 暴露到公網。測試結束後將 `MQTT_BIND_ADDRESS` 改回 `127.0.0.1` 並再次重建 Mosquitto。

接著：

1. 由 Admin 在管理頁面建立 external id 為 `phone-demo-001` 的啟用設備。
2. 手機安裝可發布 MQTT 訊息的 client，使用下列連線設定：

   ```text
   Host: 192.168.1.50
   Port: 1883
   Username: .env 的 MQTT_USERNAME
   Password: .env 的 MQTT_PASSWORD
   Client ID: phone-demo-001
   TLS: off
   ```

3. 以 QoS 1 發布到：

   ```text
   devices/phone-demo-001/telemetry
   ```

4. Payload 沿用既有契約；每次發布需使用新的 UUID，時間需替換為目前 UTC：

   ```json
   {
     "messageId": "772d156a-7967-44b2-aa93-cfdd853d7d43",
     "temperatureCelsius": 26.5,
     "humidityPercent": 58.2,
     "recordedAtUtc": "2026-10-07T08:00:00Z"
   }
   ```

   可在電腦 PowerShell 產生欄位值：

   ```powershell
   [guid]::NewGuid().ToString()
   [DateTimeOffset]::UtcNow.ToString("O")
   ```

設備的「已啟用／已停用」是管理狀態；「運作中／離線」則由最近一次遙測的 API 接收時間判定。收到有效資料後會立即顯示運作中，超過 30 秒沒有新資料便顯示離線。第一版不會根據 MQTT TCP 連線本身自動建立未知設備，未知 external id 仍會被拒絕。

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
