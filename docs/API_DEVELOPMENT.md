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
