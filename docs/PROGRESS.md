# 專案進度與 AI 交接

最後更新：2026-09-30

## 目前閘門

- Current phase：`1 - 本機基礎設施`
- State：`awaiting_review`
- Owner action：請檢查階段 1 的 Compose、Mosquitto 與操作文件；確認後由使用者建立 commit，再明確要求 AI 開始階段 2。
- Next phase：`2 - ASP.NET Core Web API 與資料持久化基礎`

狀態定義：

- `not_started`：尚未開始。
- `in_progress`：AI 只能實作這個階段。
- `awaiting_review`：實作與驗證已完成，AI 必須停止等待使用者審查及提交。
- `accepted`：使用者已審查並提交；通常在下一階段開始時記錄。
- `blocked`：存在無法安全自行排除的阻礙。

## 階段總覽

| 階段 | 名稱 | 狀態 |
|---|---|---|
| 0 | Git 與 AI 協作基線 | `accepted` |
| 1 | 本機基礎設施 | `awaiting_review` |
| 2 | ASP.NET Core Web API 與資料持久化基礎 | `not_started` |
| 3 | 設備與遙測 REST 垂直切片 | `not_started` |
| 4 | MQTT 資料接收與設備模擬器 | `not_started` |
| 5 | Authentication、Authorization 與安全基線 | `not_started` |
| 6 | Vue 3 RWD 儀表板 | `not_started` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 新增 `compose.yaml`，以 Docker Compose 啟動 PostgreSQL 17 與 Eclipse Mosquitto 2。
- 為兩個服務加入 named volume、health check、restart policy 與可設定的 host port。
- Mosquitto 禁止匿名連線，啟動時由環境變數產生容器內密碼檔。
- 新增 `.env.example`，只包含明確的假密碼；實際 `.env` 由使用者在本機建立且不納入 Git。
- 新增 `infra/mosquitto/config/mosquitto.conf` 與 `docs/LOCAL_DEVELOPMENT.md`。
- 因本機既有 PostgreSQL 容器占用 `5432`，本專案使用 host port `5433`，容器內仍使用 `5432`。
- PostgreSQL 與 Mosquitto 容器目前保持執行，方便使用者審查及手動測試。

## 本階段驗證

- [x] `docker compose --env-file .env.example config --quiet` 通過。
- [x] PostgreSQL 與 Mosquitto 均可啟動並顯示為 `healthy`。
- [x] PostgreSQL 正確帳密可由 Compose 網路連線，錯誤密碼會被拒絕。
- [x] MQTT 正確帳密可成功發布並接收一筆 JSON 遙測訊息。
- [x] MQTT 匿名發布會回傳 `not authorised`。
- [x] Container logs 未發現啟動錯誤或資料目錄權限問題。
- [x] `git diff --check` 通過。
- [x] Repository 不包含真實密碼或憑證。
- [ ] 使用者尚未完成階段 1 程式碼與設定審查。
- [ ] 使用者尚未建立階段 1 commit。

## 已知事項與風險

- 現有專案仍使用 `WebApplication2` 通用名稱；預定在階段 2 改為 `IoTMonitor`。
- 現有 ASP.NET Core 專案是 MVC View 範本，尚未提供 Web API、資料庫、MQTT、驗證或 Vue。
- `5432` 已由既有的 `holdem-backend-postgres-1` 使用；本專案固定以 `.env.example` 的 `5433` 避免衝突。
- MQTT 目前未啟用 TLS，且資料庫與 broker 會發布到 host port；此設定只適用本機開發，不可直接用於正式環境。
- Codex shell 未繼承 Docker PATH；目前 Docker CLI 位於 `C:\Users\COSH\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若本檔仍為 `awaiting_review`，只協助審查或修正階段 1，不得開始階段 2。
4. 只有在使用者已提交階段 1 並明確要求繼續後，才把階段 1 改為 `accepted`、階段 2 改為 `in_progress`，並開始階段 2。
