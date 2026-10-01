# Vue 前端開發

階段 6 前端位於 `src/iot-monitor-web`，使用 Vue 3、TypeScript、Vite 與 Vue Router。畫面直接讀取 IoT Monitor API，不包含假設備或假遙測資料。

## 前置需求

- Node.js 22.12 以上版本。
- npm 10 以上版本。
- PostgreSQL 與 API 已依 `LOCAL_DEVELOPMENT.md`、`API_DEVELOPMENT.md` 啟動。
- 已依 `AUTHENTICATION.md` 建立可登入帳號。
- 本機 ASP.NET Core HTTPS 開發憑證已受信任：

```powershell
dotnet dev-certs https --trust
```

## 安裝與啟動

```powershell
Set-Location src/iot-monitor-web
Copy-Item .env.example .env.local
npm ci
npm run dev
```

瀏覽器開啟 `http://localhost:5173`。預設 API 位址是 `https://localhost:7080`，需要變更時只修改未納入 Git 的 `.env.local`：

```dotenv
VITE_API_BASE_URL=https://localhost:7080
```

不要把帳號、密碼、Cookie 或 CSRF token 放入 Vite 環境變數。所有 `VITE_` 開頭的值都會進入瀏覽器端程式碼。

## 登入與權限行為

前端使用後端簽發的 `HttpOnly` Cookie，不會把認證資訊存入 localStorage。API client 會：

1. 所有 request 使用 `credentials: include`。
2. 登入前取得 CSRF token，登入後因身分改變而重新取得。
3. 對 `POST`、`PATCH` 等修改 request 帶 `X-CSRF-TOKEN`。
4. CSRF token 無效時重新取得並重試一次。
5. 受保護 request 回傳 401 時清除前端登入狀態，導回登入頁。

角色呈現：

| 功能 | Viewer | Operator | Admin |
|---|---:|---:|---:|
| 查看總覽、設備與遙測 | ✓ | ✓ | ✓ |
| 查看告警 | ✓ | ✓ | ✓ |
| 確認告警 |  | ✓ | ✓ |
| 新增設備、啟用或停用設備 |  |  | ✓ |

Operator 的 REST 遙測寫入權限目前沒有對應的瀏覽器表單；設備通常由 MQTT 上報資料。

## 畫面與資料範圍

- 總覽：設備數量、啟用數量、最新平均溫度／濕度及最近設備。
- 設備：分頁清單；Admin 可新增設備。
- 設備明細：最新量測、最近 50 筆趨勢與歷史表格；Admin 可切換設備狀態。
- 告警：可依確認狀態與嚴重程度篩選；Operator／Admin 可確認告警。
- 所有主要資料頁面都有 loading、empty、error 狀態，未授權會回到登入頁。

## 即時更新

登入後前端會以相同的 Cookie 連線 `/hubs/monitoring`。總覽、設備明細與告警中心會立即套用新遙測及告警事件；初次連線及重新連線後會使用 REST API 補抓，避免連線空窗造成永久遺漏。側邊欄與告警頁會顯示目前的即時連線狀態。

完整的事件、閾值與重連策略請參考 [`ALERTS_REALTIME.md`](ALERTS_REALTIME.md)。

## 品質檢查

```powershell
Set-Location src/iot-monitor-web
npm run type-check
npm run lint
npm run test
npm run build
npm audit
```

`npm run build` 的輸出位於被 Git 忽略的 `dist`。階段 8 才會建立前端 Dockerfile，並把 Vue、API、PostgreSQL、Mosquitto 與模擬器納入完整 Compose。

## 常見問題

### 登入頁顯示無法連線

確認 API 正在 `https://localhost:7080` 執行，並直接在瀏覽器開啟該位址確認開發憑證已受信任。若 API 使用不同位址，更新 `.env.local` 後重新啟動 Vite。

### 登入後仍回到登入頁

確認瀏覽器接受 API 的 Secure Cookie，且前端 origin 已包含在 API 的 `Security:AllowedOrigins`。預設允許 `http://localhost:5173` 與 `https://localhost:5173`，不要混用 `localhost` 與 `127.0.0.1`。

### 畫面沒有遙測

設備建立後仍需透過 MQTT 模擬器或 REST API 寫入量測。請依 `MQTT_DEVELOPMENT.md` 啟動 broker、API MQTT subscriber 與設備模擬器。
