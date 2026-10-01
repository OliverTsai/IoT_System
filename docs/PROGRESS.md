# 專案進度與 AI 交接

最後更新：2026-10-01

## 目前閘門

- Current phase：`6 - Vue 3 RWD 儀表板`
- State：`awaiting_review`
- Owner action：請審查階段 6 的登入流程、API client、權限顯示、設備頁面、趨勢圖與 RWD；確認後由使用者自行 commit，再明確指示開始階段 7。
- Next phase：`7 - 告警與即時更新`

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
| 5 | Authentication、Authorization 與安全基線 | `accepted` |
| 6 | Vue 3 RWD 儀表板 | `awaiting_review` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 建立 Vue 3、TypeScript、Vite 與 Vue Router 前端；正式相依只有 Vue 與 Vue Router，套件版本由 lockfile 固定。
- 實作 Cookie Authentication 登入、登出、目前使用者恢復及受保護路由；前端不保存認證 token。
- 建立共用 API client：跨 origin request 帶 Cookie、在記憶體管理 CSRF token、身分改變後刷新 token，並統一處理 Problem Details 與 401。
- 建立總覽、分頁設備清單與設備明細；所有數值都讀取真實 API，沒有硬編碼設備或遙測 mock。
- Admin 可建立設備及啟用／停用設備；Viewer、Operator 不顯示無權限的設備管理操作。
- 設備明細提供最新量測、最近 50 筆溫濕度 SVG 趨勢圖與歷史表格。
- 所有主要頁面處理 loading、empty、error、not found 與 session expired；版面支援桌面、平板與手機。
- 告警頁明確顯示後端能力尚未啟用，不製造假告警；閾值、告警 API 與 SignalR 仍依計畫於階段 7 實作。
- 建立 API client 與趨勢圖元件測試，並補上前端啟動、權限、安全注意事項及驗證文件。

## 本階段驗證

- [x] `npm run type-check` 通過。
- [x] `npm run lint` 通過，0 errors、0 warnings。
- [x] `npm run test` 通過：2 test files、5 tests passed。
- [x] `npm run build` 通過：47 modules transformed，production assets 成功產生。
- [x] `npm audit` 通過：0 vulnerabilities。
- [x] `dotnet build IoTMonitor.slnx -c Release --no-restore` 成功：0 warnings、0 errors。
- [x] Vite 開發伺服器可於 `http://localhost:5173` 啟動，首頁 smoke request 回傳 200 與 app root。
- [x] 以瀏覽器實際檢查登入頁的 1440 × 900 桌面版與 390 × 844 手機版，內容無溢出且表單可完整操作。
- [x] API client 測試涵蓋登入前後 CSRF 刷新、Cookie credentials、401 通知及 CSRF 失效後單次重試。
- [x] 趨勢圖測試涵蓋排序與折線輸出，以及資料不足狀態。

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
- 前端告警頁目前只顯示能力說明；真實告警列表需等階段 7 的告警 API 完成。
- 前端 production build 目前是靜態產物，尚無 Dockerfile 或反向代理設定；這些工作屬於階段 8。
- 未設定 `IOT_MONITOR_TEST_CONNECTION_STRING` 時，17 個 PostgreSQL 整合測試會顯示為 skipped；完整測試指令已記錄於 `docs/API_DEVELOPMENT.md`。
- 舊 `WebApplication2` 目錄只剩被 Git 忽略的 `bin`、`obj`、`*.user` 等本機產生物，不屬於新的 solution。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若階段 6 尚未被使用者接受，只能修正階段 6 的審查意見，不得開始告警或 SignalR。
4. 使用者審查並自行 commit 後，只有在收到明確指示時才能將階段 6 標為 `accepted` 並開始階段 7。
