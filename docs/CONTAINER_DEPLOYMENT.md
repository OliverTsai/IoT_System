# 完整容器環境

Docker Compose 會整合 PostgreSQL、Eclipse Mosquitto、ASP.NET Core API、Vue/Nginx 與 .NET 設備模擬器。此設定以本機作品展示為目標，不應直接視為公網正式環境設定。

## 服務與資料流

| Service | Container port | Host port | 說明 |
|---|---:|---:|---|
| `web` | 8080 / 8443 | 8080 / 8443 | Vue 靜態檔、HTTPS、REST/SignalR reverse proxy。 |
| `api` | 8080 | 未發布 | Web API、MQTT subscriber、SignalR、migration。 |
| `postgres` | 5432 | 5433 | PostgreSQL；host port 只綁 `127.0.0.1`。 |
| `mosquitto` | 1883 | 1883 | MQTT broker；host port 只綁 `127.0.0.1`。 |
| `simulator` | — | — | 持續發布 `sim-device-*` 遙測。 |

啟動順序由 health condition 控制：PostgreSQL／Mosquitto → API → Web／Simulator。API 健康前已完成 migration、Bootstrap Admin 與 demo device seed。

## 初次啟動

```powershell
Copy-Item .env.example .env
```

編輯 `.env` 並更換所有範例密碼。密碼不能包含換行；PostgreSQL 密碼應避免使用分號，以免破壞 connection string 格式。

```powershell
docker compose config --quiet
docker compose up -d --build --wait
docker compose ps
```

成功後瀏覽器開啟：

```text
https://localhost:8443
```

Nginx 映像在 build 時產生 localhost 自簽憑證，第一次使用時瀏覽器會顯示警告。它只供本機展示，沒有被加入 repository。

## 啟動時的自動工作

- `Database:MigrateOnStartup=true`：API 啟動前套用尚未執行的 EF Core migration。
- `Authentication:BootstrapAdmin:Enabled=true`：只有在 username 不存在時建立 Admin，不會覆寫既有密碼。
- `DemoData:Enabled=true`：建立與模擬器相符的設備；重啟時只補缺少的設備。
- API 訂閱 `devices/+/telemetry`，模擬器等 API healthy 後才開始發布。
- Data Protection key 寫入 `data_protection_keys` volume，容器重建不會立即使既有 Cookie 失效。

應用程式的 `appsettings.json` 預設關閉 migration 與 demo seed；只有 Compose 明確開啟。

## Smoke test

需要 PowerShell 7：

```powershell
pwsh ./scripts/smoke-test.ps1
```

Script 從 process environment 或 `.env` 取得 Bootstrap username/password，不會輸出密碼。若既有 volume 中的 Admin 密碼已變更，請同步更新 `.env`，或使用仍有效的帳號設定執行環境變數後再測試。

驗證範圍：

1. Nginx/Vue、CSP、OpenAPI 與 API health。
2. CSRF token、Secure Cookie 登入及受保護 REST。
3. SignalR negotiation 要求有效登入身分。
4. Simulator → Mosquitto → API worker → PostgreSQL → REST 查詢。
5. 正常登出。

## 常用操作

```powershell
docker compose logs -f api web simulator
docker compose restart api
docker compose stop simulator
docker compose start simulator
docker compose down
```

只啟動本機開發基礎設施：

```powershell
docker compose up -d postgres mosquitto
```

修改 API、Web 或模擬器後重建：

```powershell
docker compose up -d --build --wait api web simulator
```

## 重設資料

下列操作會永久刪除本專案的資料庫、broker persistence 與 Data Protection keys：

```powershell
docker compose down --volumes
```

刪除 key ring 會讓所有既有登入 Cookie 失效。執行前先用 `docker volume ls --filter label=com.docker.compose.project=iot-monitor` 確認目標。

## 安全設定

- `api`、`web`、`simulator` 以非 root 使用者執行。
- 自製容器設定 `read_only`、`cap_drop: ALL`、`no-new-privileges`，只開放必要的 tmpfs／volume。
- API port 只在 Compose network 內 expose；外部 REST 與 SignalR 必須經過 Nginx HTTPS。
- Nginx 負責 TLS termination 與 HTTP→HTTPS redirect；Compose 以 `ReverseProxy__TerminatesTls=true` 關閉 API 對內部 HTTP 的重複轉向。
- PostgreSQL 與 MQTT host port 只綁 loopback，避免直接暴露到區域網路。
- Broker 禁止匿名使用者，密碼檔只存在容器的 `/tmp`。
- Nginx 設定 CSP、frame denial、MIME sniffing protection 與 referrer policy。
- Log 使用 rotation，避免本機長期執行無限制成長。

本機 Compose 仍透過 environment 傳遞密碼，且 Linux volume 中的 Data Protection key 未使用外部 KMS 加密。正式環境必須改用 secret manager、受信任 TLS 憑證、加密 key storage，並關閉 demo seed、Bootstrap Admin 及公開 OpenAPI。

## 故障排除

### Web 不健康

```powershell
docker compose logs web
docker compose exec web nginx -T
```

確認 `WEB_HTTP_PORT`／`WEB_HTTPS_PORT` 未被占用；變更 HTTPS host port 後必須加 `--build`，讓 Nginx redirect 設定同步更新。

若 Docker 回報 `port is already allocated`，先找出占用該 port 的容器：

```powershell
docker ps --filter publish=8080
```

不要任意停止不相關的容器；改用 `.env` 的 `WEB_HTTP_PORT` 選擇可用 port，例如 `8083`。HTTP port 只負責轉向，主要入口仍是 `https://localhost:<WEB_HTTPS_PORT>`。

### API 不健康

```powershell
docker compose logs api
docker compose exec postgres sh -c 'pg_isready -U "$POSTGRES_USER" -d "$POSTGRES_DB"'
```

常見原因是 `.env` 密碼與既有 PostgreSQL volume 初始化密碼不同。更改 `.env` 不會自動改變既有資料庫使用者密碼。

需要保留資料時，在互動式提示中輸入與 `.env` 的 `POSTGRES_PASSWORD` 相同的密碼，再重建 API：

```powershell
docker compose exec postgres sh -c 'psql --username "$POSTGRES_USER" --dbname postgres --command "\password $POSTGRES_USER"'
docker compose up -d --force-recreate --wait api web simulator
```

只有確定不需要既有資料時，才能使用 `docker compose down --volumes` 重新初始化；這會刪除本專案的 PostgreSQL、MQTT 與 Data Protection volume 資料。

### 無法登入

- 確認使用 `.env` 的 Bootstrap username/password。
- Bootstrap 只建立不存在的帳號，不會重設已存在帳號的密碼。
- 確認使用 `https://localhost:<WEB_HTTPS_PORT>`，Secure Cookie 不會透過 HTTP 登入。
- 每個來源 IP 每分鐘預設只允許五次登入嘗試。

### 沒有遙測

```powershell
docker compose logs mosquitto api simulator
```

`DEMO_DATA_ENABLED`、`SIMULATOR_DEVICE_PREFIX` 與 `SIMULATOR_DEVICE_COUNT` 必須匹配。API 只接受已存在且啟用的設備。
