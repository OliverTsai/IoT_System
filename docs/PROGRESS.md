# 專案進度與 AI 交接

最後更新：2026-09-30

## 目前閘門

- Current phase：`0 - Git 與 AI 協作基線`
- State：`awaiting_review`
- Owner action：請檢查本階段檔案，確認流程是否符合期待；確認後由使用者建立初始 commit，再明確要求 AI 開始階段 1。
- Next phase：`1 - 本機基礎設施`

狀態定義：

- `not_started`：尚未開始。
- `in_progress`：AI 只能實作這個階段。
- `awaiting_review`：實作與驗證已完成，AI 必須停止等待使用者審查及提交。
- `accepted`：使用者已審查並提交；通常在下一階段開始時記錄。
- `blocked`：存在無法安全自行排除的阻礙。

## 階段總覽

| 階段 | 名稱 | 狀態 |
|---|---|---|
| 0 | Git 與 AI 協作基線 | `awaiting_review` |
| 1 | 本機基礎設施 | `not_started` |
| 2 | ASP.NET Core Web API 與資料持久化基礎 | `not_started` |
| 3 | 設備與遙測 REST 垂直切片 | `not_started` |
| 4 | MQTT 資料接收與設備模擬器 | `not_started` |
| 5 | Authentication、Authorization 與安全基線 | `not_started` |
| 6 | Vue 3 RWD 儀表板 | `not_started` |
| 7 | 告警與即時更新 | `not_started` |
| 8 | 完整容器化、品質與作品集整理 | `not_started` |

## 本階段完成內容

- 在 repository 根目錄初始化 Git，預設分支為 `main`。
- 新增 `.gitignore`，排除 Visual Studio、.NET、Node、測試產物與本機秘密設定。
- 新增 `AGENTS.md`，定義強制階段閘門與 AI／使用者責任邊界。
- 新增 `docs/IMPLEMENTATION_PLAN.md`，定義階段 0 至 8 的範圍與驗收條件。
- 新增本進度檔與根目錄 README。
- 未修改現有 ASP.NET Core 應用程式功能。

## 本階段驗證

- [x] Git repository 已初始化於正確的 solution 根目錄。
- [x] 預設分支是 `main`。
- [x] `.vs`、`bin`、`obj`、`*.user` 不出現在 Git 追蹤候選中。
- [x] 階段閘門明確要求 AI 在 `awaiting_review` 時停止。
- [x] 階段閘門明確禁止 AI 自行 commit 或跨階段。
- [ ] 使用者尚未完成程式碼與文件審查。
- [ ] 使用者尚未建立初始 commit。

## 已知事項與風險

- 現有專案仍使用 `WebApplication2` 通用名稱；預定在階段 2 改為 `IoTMonitor`。
- 現有 ASP.NET Core 專案是 MVC View 範本，尚未提供 Web API、資料庫、MQTT、驗證或 Vue。
- 技術方向已先記錄，但任何正式相依套件仍需在加入前說明並取得使用者同意。

## 恢復工作檢查表

下一位 AI 或下一次工作開始時：

1. 閱讀 `AGENTS.md` 與 `docs/IMPLEMENTATION_PLAN.md`。
2. 執行 `git status --short --branch` 及 `git log -1 --oneline`。
3. 若本檔仍為 `awaiting_review`，只協助審查或修正階段 0，不得開始階段 1。
4. 只有在使用者已提交階段 0 並明確要求繼續後，才把階段 0 改為 `accepted`、階段 1 改為 `in_progress`，並開始階段 1。

