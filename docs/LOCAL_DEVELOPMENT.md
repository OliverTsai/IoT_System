# 本機基礎設施

階段 1 只使用 Docker Compose 執行 PostgreSQL 與 Eclipse Mosquitto。API、Vue 與設備模擬器會在後續階段加入。

## 前置需求

- Docker Desktop 已安裝並啟動。
- `docker info` 與 `docker compose version` 可以成功執行。
- 預設的 TCP `5433` 與 `1883` 連接埠未被占用；需要時可在 `.env` 改成其他 host port。PostgreSQL 容器內仍使用標準的 `5432`。

## 建立本機設定

第一次啟動前，從範例建立不納入 Git 的 `.env`：

```powershell
Copy-Item .env.example .env
```

接著將 `.env` 中兩個 `replace_with_a_local_password` 換成本機專用密碼。不要在聊天訊息、commit、README、issue 或 log 中貼出實際密碼。

## 驗證 Compose 設定

```powershell
docker compose config
```

如果只想驗證已提交的範例設定，不建立 `.env`：

```powershell
docker compose --env-file .env.example config
```

## 啟動與查看狀態

```powershell
docker compose up -d
docker compose ps
docker compose logs postgres mosquitto
```

兩個服務均應顯示為 `healthy`。

## MQTT 發布／訂閱驗證

開啟第一個 PowerShell，讓 Mosquitto 容器等待一筆訊息：

```powershell
docker compose exec mosquitto sh -c 'mosquitto_sub -h 127.0.0.1 -p 1883 -u "$MQTT_USERNAME" -P "$MQTT_PASSWORD" -t "devices/demo-001/telemetry" -C 1 -W 10'
```

再開啟第二個 PowerShell 發布測試訊息：

```powershell
docker compose exec mosquitto sh -c 'mosquitto_pub -h 127.0.0.1 -p 1883 -u "$MQTT_USERNAME" -P "$MQTT_PASSWORD" -t "devices/demo-001/telemetry" -m "{\"temperature\":25.4,\"humidity\":61,\"timestamp\":\"2026-09-30T00:00:00Z\"}" -q 1'
```

第一個 PowerShell 收到相同 JSON 即表示 broker 的 authentication、publish 與 subscribe 都正常。

## 停止或重設

保留資料並停止容器：

```powershell
docker compose down
```

查看 named volumes：

```powershell
docker volume ls --filter label=com.docker.compose.project=iot-monitor
```

`docker compose down --volumes` 會刪除 PostgreSQL 與 Mosquitto 的本機持久化資料，因此只有在確定不需要資料時才可執行；AI 不得自行執行這個指令。

## 安全範圍

- Broker 禁止匿名連線，密碼檔在容器啟動時由 `.env` 值產生，不會寫入 repository。
- 目前 MQTT listener 未啟用 TLS，只適用於本機開發，不應直接暴露至區域網路或公網。
- PostgreSQL 與 MQTT host ports 是為後續本機 API 與測試保留；正式部署時必須重新檢視是否需要對 host 公開。
