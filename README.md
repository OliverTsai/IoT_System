# IoT Device Monitor

這個 repository 會從 ASP.NET Core MVC 練習專案，分階段演進為完整的 IoT 設備監控系統。

目前採用人工審查閘門：AI 每完成一個階段就必須停止，由使用者閱讀程式碼、確認並自行提交 Git commit，之後才能開始下一階段。

## 協作文件

- AI 工作規範：[`AGENTS.md`](AGENTS.md)
- 實作順序與驗收條件：[`docs/IMPLEMENTATION_PLAN.md`](docs/IMPLEMENTATION_PLAN.md)
- 目前進度與交接資訊：[`docs/PROGRESS.md`](docs/PROGRESS.md)
- 本機基礎設施操作：[`docs/LOCAL_DEVELOPMENT.md`](docs/LOCAL_DEVELOPMENT.md)
- API 開發與 migration：[`docs/API_DEVELOPMENT.md`](docs/API_DEVELOPMENT.md)
- MQTT 遙測與設備模擬器：[`docs/MQTT_DEVELOPMENT.md`](docs/MQTT_DEVELOPMENT.md)
- Authentication、角色授權與安全操作：[`docs/AUTHENTICATION.md`](docs/AUTHENTICATION.md)
- Vue 前端啟動與驗證：[`docs/FRONTEND_DEVELOPMENT.md`](docs/FRONTEND_DEVELOPMENT.md)

目前已建立 ASP.NET Core Web API、PostgreSQL、設備與遙測 REST API、MQTT 背景接收服務、設備模擬器、Cookie Authentication／角色授權安全基線，以及 Vue 3 RWD 監控介面。
