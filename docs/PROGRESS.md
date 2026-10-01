# 專案進度與 AI 交接

最後更新：2026-10-01

## 目前閘門

- Current phase：`5 - Authentication、Authorization 與安全基線`
- State：`awaiting_review`
- Owner action：請審查階段 5 的 Cookie／CSRF 流程、角色 policy、使用者 bootstrap 與安全設定；確認後由使用者自行 commit，再明確指示開始階段 6。
- Next phase：`6 - Vue 3 RWD 儀表板`

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
| 1 | 本機基礎設施 | `accepted` |
| 2 | ASP.NET Core Web API 與資料持久化基礎 | `accepted` |
| 3 | 設備與遙測 REST 垂直切片 | `accepted` |
| 4 | MQTT 資料接收與設備模擬器 | `accepted` |
| 5 | Authentication、Authorization 與安全基線 | `awaiting_review` |
| 6 | Vue 3 RWD 儀表板 | `not_started` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 採用 ASP.NET Core Cookie Authentication；使用 `HttpOnly`、`Secure`、`SameSite=None`、`__Host-` Cookie，固定 30 分鐘過期且不滑動展延。
- 新增 `users` 資料表、正規化 username 唯一索引、ASP.NET Core `PasswordHasher`、security stamp 與一次性 Admin bootstrap。
- 建立 `Admin`、`Operator`、`Viewer` 角色：所有角色可讀，Admin 可管理設備與使用者，Admin／Operator 可由 REST 寫入遙測。
- 建立 CSRF token、login、logout、current user 與 Admin user management API；錯誤登入使用不揭露帳號存在性的通用回應。
- 所有 Controller 預設要求登入，公開 system、health、Development OpenAPI 與 CSRF 端點使用明確例外；未來告警 Controller 也會預設受保護。
- 所有非安全 HTTP method 都要求 antiforgery token；加入精確 CORS 白名單、全域與登入 rate limit、安全標頭及非 Development HSTS。
- 建立並套用 `AddAuthenticationSecurity` migration；補齊安全操作、bootstrap、Cookie／CSRF 與角色權限文件。
- 既有 REST 與 MQTT 整合測試改為以真實登入 Cookie 與 CSRF token 操作，並新增 Authentication／Authorization 安全測試。

## 本階段驗證

- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] 設定本機測試資料庫後執行 Release 完整測試：36 passed、0 failed、0 skipped。
- [x] 匿名讀取設備回傳 401；Viewer 可讀但修改回傳 403；Admin 可建立設備與使用者。
- [x] 錯誤密碼回傳通用 401；Cookie 超過設定期限後回傳 401；缺少 CSRF token 的修改 request 回傳 400。
- [x] 建立使用者後資料庫只儲存加鹽 password hash，response 不包含密碼或 hash。
- [x] Auth Cookie 包含 Secure、HttpOnly、SameSite=None；response 包含安全標頭。
- [x] 允許的 CORS origin 精確回傳 credentials headers；未設定 origin 不會取得 allow-origin header。
- [x] 登入超過每分鐘 5 次限制時回傳 429 Problem Details。
- [x] `AddAuthenticationSecurity` 已套用至本機開發資料庫，migration history 同時包含初始與階段 5 migration。
- [x] `dotnet format IoTMonitor.slnx --verify-no-changes --no-restore` 通過。
- [x] PostgreSQL 與 Mosquitto 容器 healthy；整合測試未留下 `iot_monitor_tests_*` 暫時資料庫。
- [x] `git diff --check` 通過；`.env` 維持忽略，追蹤內容未發現真實密碼或私鑰。

## 已知事項與風險

- `5432` 已由既有的 `holdem-backend-postgres-1` 使用；本專案固定以 `.env.example` 的 `5433` 避免衝突。
- MQTT 目前未啟用 TLS，且資料庫與 broker 會發布到 host port；此設定只適用本機開發，不可直接用於正式環境。
- Codex shell 未繼承 Docker PATH；目前 Docker CLI 位於 `C:\Users\COSH\AppData\Local\Programs\DockerDesktop\resources\bin\docker.exe`。
- MQTT 預設停用，必須使用 User Secrets 或環境變數提供本機 broker 帳密並啟用。
- message id 去重目前只適用單一 API 執行個體，且 API 重啟後會清空；跨執行個體／重啟去重預定未來改用資料庫唯一鍵或分散式儲存。
- Bootstrap Admin 預設停用且 repository 沒有預設密碼；首次本機登入前需依 `docs/AUTHENTICATION.md` 使用 User Secrets 建立 Admin，建立後立即移除 bootstrap password。
- Auth 與 antiforgery Cookie 強制 Secure，本機登入流程必須使用 HTTPS；Vue 必須使用 credentials 並在登入後重新取得 CSRF token。
- Cookie 加密依賴 ASP.NET Core Data Protection key；階段 8 容器化時必須持久化且保護 key ring，否則容器重建會使現有 Cookie 失效。
- 尚未建立告警 API；目前的全域 Controller authorization 會先保護未來端點，告警確認的細部 policy 於階段 7 實作。
- 未設定 `IOT_MONITOR_TEST_CONNECTION_STRING` 時，17 個 PostgreSQL 整合測試會顯示為 skipped；完整測試指令已記錄於 `docs/API_DEVELOPMENT.md`。
- 舊 `WebApplication2` 目錄只剩被 Git 忽略的 `bin`、`obj`、`*.user` 等本機產生物，不屬於新的 solution。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若階段 5 尚未被使用者接受，只能修正階段 5 的審查意見，不得開始 Vue 前端。
4. 使用者審查並自行 commit 後，只有在收到明確指示時才能將階段 5 標為 `accepted` 並開始階段 6。
