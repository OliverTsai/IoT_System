# AI 協作規範

本檔案適用於整個 repository。任何 AI 在修改程式碼前，都必須先閱讀本檔案、`docs/IMPLEMENTATION_PLAN.md` 與 `docs/PROGRESS.md`。

## 每次開始工作的必要檢查

1. 執行 `git status --short --branch`，保留使用者尚未提交的變更。
2. 閱讀 `docs/PROGRESS.md`，確認目前階段與狀態。
3. 只閱讀並實作 `docs/IMPLEMENTATION_PLAN.md` 中目前階段的範圍。
4. 修改前先檢查相關程式、設定、測試與相鄰檔案。
5. 若進度檔與實際程式碼不一致，先向使用者說明，不可自行跳階段。

## 強制階段閘門

- 一次只能處理一個階段，不得在同一輪工作中提前實作下一階段。
- 開始實作時，將目前階段狀態改為 `in_progress`。
- 只有當該階段驗收條件全部達成，才可將狀態改為 `awaiting_review`。
- 進入 `awaiting_review` 後必須停止實作，回報變更、驗證結果、未驗證項目與審查重點；不得開始下一階段。
- AI 不得自行執行 commit、push、merge、rebase、tag、release 或部署。Git commit 由使用者完成，除非使用者另外明確要求。
- 下一階段必須同時滿足以下條件才可開始：
  1. 使用者已檢查程式碼並明確說「繼續」、「開始下一階段」或同等意思。
  2. 前一階段的變更已由使用者提交，且工作目錄沒有屬於前一階段的未提交變更。
- 如果目前狀態是 `awaiting_review`，AI 可以回答問題或依審查意見修正目前階段，但不能跨階段。
- 審查修正完成後仍維持 `awaiting_review`，再次停止等待使用者確認與提交。

## 實作原則

- 以可運作的垂直切片為優先，避免一次建立過多抽象層。
- 每個階段只做達成該階段驗收條件所需的最小完整修改。
- 保留既有行為與使用者變更；不要進行無關重構或大範圍格式化。
- 新增正式相依套件前，先說明用途、版本與替代方案，取得使用者同意後再加入。
- 機密資料不得寫入 repository；提供 `.env.example` 或範例設定時只能使用假值。
- 資料庫 migration、外部服務寫入與其他可能影響資料的操作，執行前需取得使用者明確同意。

## 階段完成時的必要工作

1. 執行與修改範圍相稱的 build、test、lint 或實際功能驗證。
2. 檢查 `git diff` 與 `git status`，排除意外或無關變更。
3. 更新 `docs/PROGRESS.md`：狀態、完成內容、驗證結果、已知問題與下一步。
4. 將狀態設為 `awaiting_review` 並停止。
5. 最終回報必須列出：修改內容、原因、驗證、尚未驗證項目、審查建議與下一階段名稱。

## 技術方向

- Backend：ASP.NET Core Web API（目前目標 .NET 10）
- Frontend：Vue 3、TypeScript、Vite
- Database：PostgreSQL
- MQTT broker：Eclipse Mosquitto
- Local orchestration：Docker Compose
- Authentication：在核心資料流完成後加入 JWT 或安全 Cookie，搭配角色／Policy 授權

若要改變上述方向，必須先更新實作計畫、記錄原因並取得使用者同意。

