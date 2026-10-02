# 本機開發環境

本專案可以使用完整容器模式，也可以只用 Docker 執行 PostgreSQL／Mosquitto，再由 IDE 啟動 API 與 Vue。

## 前置需求

- Docker Desktop 與 Docker Compose v2。
- 本機開發模式另需 .NET 10 SDK、Node.js 22.12 以上與 npm 10 以上。
- 預設 host ports：PostgreSQL `5433`、MQTT `1883`、Web HTTP `8080`、Web HTTPS `8443`。

## 建立本機設定

```powershell
Copy-Item .env.example .env
```

更換 `.env` 中 PostgreSQL、MQTT 與 Bootstrap Admin 的三個範例密碼。`.env` 已被 Git 忽略，不要將內容貼到聊天、log、issue 或 commit。

## 完整容器模式

```powershell
docker compose config --quiet
docker compose up -d --build --wait
docker compose ps
pwsh ./scripts/smoke-test.ps1
```

瀏覽器使用 `https://localhost:8443`。完整說明與安全界線請參考 [`CONTAINER_DEPLOYMENT.md`](CONTAINER_DEPLOYMENT.md)。

## IDE／本機程式模式

只啟動基礎設施，避免與本機 API／Vite 重複：

```powershell
docker compose up -d postgres mosquitto
docker compose ps postgres mosquitto
```

接著依序參考：

- [`API_DEVELOPMENT.md`](API_DEVELOPMENT.md)：連線字串、migration、API 啟動。
- [`MQTT_DEVELOPMENT.md`](MQTT_DEVELOPMENT.md)：啟用 subscriber 與本機模擬器。
- [`FRONTEND_DEVELOPMENT.md`](FRONTEND_DEVELOPMENT.md)：Vite 開發伺服器。

## MQTT broker 驗證

第一個 PowerShell 等待訊息：

```powershell
docker compose exec mosquitto sh -c 'mosquitto_sub -h 127.0.0.1 -p 1883 -u "$MQTT_USERNAME" -P "$MQTT_PASSWORD" -t "healthcheck/manual" -C 1 -W 10'
```

第二個 PowerShell發布：

```powershell
docker compose exec mosquitto sh -c 'mosquitto_pub -h 127.0.0.1 -p 1883 -u "$MQTT_USERNAME" -P "$MQTT_PASSWORD" -t "healthcheck/manual" -m healthy -q 1'
```

## 停止與重設

保留資料停止：

```powershell
docker compose down
```

查看本專案 volumes：

```powershell
docker volume ls --filter label=com.docker.compose.project=iot-monitor
```

`docker compose down --volumes` 會永久刪除 PostgreSQL、Mosquitto 與 Data Protection key ring，只能在確定不需要資料時執行。

## 本機安全範圍

- 所有 host ports 都只綁定 `127.0.0.1`。
- MQTT 未啟用 TLS；容器網路內由帳密保護，只適合本機開發。
- Nginx 自簽憑證只用於本機展示，正式環境必須替換為受信任憑證。
- Compose `.env` 不等同正式 secret manager，不應用於公網部署。
