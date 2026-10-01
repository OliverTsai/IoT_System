# API 開發環境

## 技術組合

- ASP.NET Core Web API / .NET 10
- Entity Framework Core 10
- Npgsql PostgreSQL provider
- OpenAPI
- xUnit 與 `WebApplicationFactory`

## 前置步驟

先依照 [`LOCAL_DEVELOPMENT.md`](LOCAL_DEVELOPMENT.md) 啟動 PostgreSQL：

```powershell
docker compose up -d
docker compose ps
```

PostgreSQL 對主機使用 `5433`，容器內仍使用 `5432`。

## 設定連線字串

API 不會從 repository 讀取資料庫密碼。請使用 .NET User Secrets 設定本機連線字串：

```powershell
dotnet user-secrets set `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "ConnectionStrings:IoTMonitor" `
  "Host=localhost;Port=5433;Database=iot_monitor;Username=iot_monitor;Password=<your-local-password>"
```

也可以設定 `ConnectionStrings__IoTMonitor` 環境變數。不得將真實密碼加入 `appsettings*.json`。

## 還原工具與套件

```powershell
dotnet tool restore
dotnet restore IoTMonitor.slnx
```

## Migration

建立新的 migration：

```powershell
dotnet ef migrations add <MigrationName> `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  --startup-project src/IoTMonitor.Api/IoTMonitor.Api.csproj
```

套用 migration：

```powershell
dotnet ef database update `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  --startup-project src/IoTMonitor.Api/IoTMonitor.Api.csproj
```

Migration 會修改資料庫結構。執行前必須確認目標連線字串是本機開發資料庫。

## Build 與測試

```powershell
dotnet build IoTMonitor.slnx --no-restore
dotnet test IoTMonitor.slnx --no-build
```

未設定測試資料庫時，只有不依賴資料庫的測試會執行，PostgreSQL 整合測試會顯示為 skipped。要執行完整測試，請指定本機 PostgreSQL 管理連線：

```powershell
$env:IOT_MONITOR_TEST_CONNECTION_STRING = `
  "Host=localhost;Port=5433;Database=postgres;Username=iot_monitor;Password=<your-local-password>"

dotnet test IoTMonitor.slnx --no-build

Remove-Item Env:IOT_MONITOR_TEST_CONNECTION_STRING
```

整合測試會建立名稱隨機的 `iot_monitor_tests_*` 暫時資料庫、套用 migration，並在測試結束後刪除，不會清除 `iot_monitor` 開發資料庫。

## 啟動 API

```powershell
dotnet run --project src/IoTMonitor.Api/IoTMonitor.Api.csproj --launch-profile https
```

預設開發端點：

- API 資訊：`https://localhost:7080/api/system`
- OpenAPI：`https://localhost:7080/openapi/v1.json`
- Process health：`https://localhost:7080/health/live`
- Database readiness：`https://localhost:7080/health/ready`

OpenAPI 端點只在 Development 環境啟用。

MQTT 遙測接收預設停用；設定方式、topic、payload 與模擬器操作請參考 [`MQTT_DEVELOPMENT.md`](MQTT_DEVELOPMENT.md)。

Cookie 登入、角色權限、Admin bootstrap 與 CSRF 操作請參考 [`AUTHENTICATION.md`](AUTHENTICATION.md)。除了文件列出的公開端點外，API 皆要求登入；所有會改變狀態的 request 還必須帶有效的 `X-CSRF-TOKEN`。

## 設備與遙測 API

| Method | Path | 說明 |
|---|---|---|
| `POST` | `/api/devices` | Admin 建立設備；`externalId` 不可重複。 |
| `GET` | `/api/devices?page=1&pageSize=20` | 已登入使用者分頁取得設備列表。 |
| `GET` | `/api/devices/{deviceId}` | 已登入使用者取得設備與最新一筆遙測。 |
| `PATCH` | `/api/devices/{deviceId}/status` | Admin 啟用或停用設備。 |
| `POST` | `/api/devices/{deviceId}/telemetry` | Admin／Operator 寫入溫度與濕度。 |
| `GET` | `/api/devices/{deviceId}/telemetry/latest` | 已登入使用者取得最新一筆遙測。 |
| `GET` | `/api/devices/{deviceId}/telemetry` | 已登入使用者依時間範圍分頁查詢遙測歷史。 |

列表端點的 `page` 從 1 開始，`pageSize` 允許 1 到 100。遙測歷史可以使用 ISO 8601 格式的 `fromUtc` 與 `toUtc`，範圍包含起訖時間。

溫度允許 -100 至 200°C、濕度允許 0 至 100%。`recordedAtUtc` 應使用含時區的 ISO 8601 格式，系統會轉換為 UTC，且不可超過伺服器目前時間五分鐘以上。驗證錯誤、找不到設備與重複識別碼會分別回傳 400、404、409 的 Problem Details。
