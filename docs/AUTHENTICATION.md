# Authentication 與安全基線

## 技術選擇

API 使用 ASP.NET Core Cookie Authentication，而不是讓瀏覽器端 JavaScript 保存 JWT。

選擇 Cookie 的原因：

- 認證票證只存在 `HttpOnly` Cookie，Vue 程式無法直接讀取，降低 token 被前端注入程式碼竊取的風險。
- Cookie 使用 `Secure`、`SameSite=None` 與 `__Host-` 前綴；跨 origin 的 Vue 開發環境仍可使用，但 API 必須透過 HTTPS。
- 所有會改變狀態的 Controller request 都必須帶 antiforgery token，避免跨站請求偽造。
- Cookie 固定 30 分鐘過期、不滑動展延；伺服器會在每次請求確認使用者仍啟用，且角色與 security stamp 未變更。

若未來需要提供非瀏覽器第三方 client，再另外評估短效 access token、refresh token rotation 與撤銷策略，不在目前瀏覽器儀表板的需求內。

## Role 與權限

| 操作 | Viewer | Operator | Admin |
|---|---:|---:|---:|
| 讀取設備與遙測 | ✓ | ✓ | ✓ |
| REST 寫入遙測 |  | ✓ | ✓ |
| 建立設備、更新設備狀態 |  |  | ✓ |
| 建立及列出使用者 |  |  | ✓ |

所有 Controller 預設都需要登入。`/api/system`、health checks、Development OpenAPI，以及取得 CSRF token 的端點有明確標示為公開。因此未來新增告警 Controller 時，沒有額外設定也會先受到登入保護，再依告警操作加入更嚴格的 policy。

## 套用 migration

階段 5 的 migration 會新增 `users` 資料表：

```powershell
dotnet ef database update `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  --startup-project src/IoTMonitor.Api/IoTMonitor.Api.csproj
```

執行前請確認 User Secrets 中的 `ConnectionStrings:IoTMonitor` 指向本機開發資料庫。

## 建立第一個 Admin

Bootstrap 預設停用，而且 repository 不包含預設帳號或密碼。先以 User Secrets 設定一次性 Admin：

```powershell
dotnet user-secrets set `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "Authentication:BootstrapAdmin:Enabled" `
  "true"

dotnet user-secrets set `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "Authentication:BootstrapAdmin:Username" `
  "admin"

dotnet user-secrets set `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "Authentication:BootstrapAdmin:Password" `
  "<choose-a-strong-local-password>"
```

密碼必須為 12 至 128 字元，並包含英文大寫、小寫、數字與符號。啟動 API 後，服務只會在相同正規化 username 不存在時建立 Admin，絕不覆寫既有密碼。

建立完成後立即移除 bootstrap 密碼並停用功能：

```powershell
dotnet user-secrets remove `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "Authentication:BootstrapAdmin:Password"

dotnet user-secrets set `
  --project src/IoTMonitor.Api/IoTMonitor.Api.csproj `
  "Authentication:BootstrapAdmin:Enabled" `
  "false"
```

後續帳號只能由 Admin 呼叫 `POST /api/users` 建立，密碼會使用 ASP.NET Core `PasswordHasher` 儲存為加鹽雜湊，API response 不會包含 hash。

完整 Compose 會從 `.env` 將相同設定傳入 API。全新 volume 啟動時建立一次 Admin；username 已存在時不會覆寫密碼。不要直接在資料庫插入明文 `password_hash`。操作方式請參考 [`CONTAINER_DEPLOYMENT.md`](CONTAINER_DEPLOYMENT.md)。

## Cookie 登入與 CSRF 流程

瀏覽器 client 必須在跨 origin request 設定 credentials，例如 Fetch 的 `credentials: "include"`。

1. `GET /api/auth/csrf`，保留 response Cookie，並取得 response body 的 `token`。
2. `POST /api/auth/login` 時加入 `X-CSRF-TOKEN: <token>` header。
3. 登入後重新呼叫 `GET /api/auth/csrf`。身分改變後，先前的 token 不再使用。
4. 所有 `POST`、`PUT`、`PATCH`、`DELETE` request 都帶目前的 `X-CSRF-TOKEN`。
5. 使用 `GET /api/auth/me` 恢復登入狀態，使用 `POST /api/auth/logout` 登出。

錯誤密碼回傳通用的 `401 Invalid credentials`，不揭露 username 是否存在。匿名存取受保護資料回傳 `401`，角色不足回傳 `403`，CSRF token 無效回傳 `400`，登入嘗試超過限制回傳 `429`。

## 其他安全設定

- CORS 只允許 `Security:AllowedOrigins` 列出的完整 origin，預設為本機 Vite 的 HTTP／HTTPS 5173 port，且允許 credentials。
- 全域 rate limit 預設每個使用者或來源 IP 每分鐘 120 次；登入另外限制每個來源 IP 每分鐘 5 次。
- API 加入 `X-Content-Type-Options`、`X-Frame-Options`、`Referrer-Policy`、`Permissions-Policy` 與 restrictive CSP。
- 非 Development 環境啟用 HSTS。
- Cookie 加密依賴 ASP.NET Core Data Protection key。容器環境將 key ring 放在只供非 root API 使用者寫入的 `data_protection_keys` volume；刪除 volume 會讓既有登入 Cookie 全部失效。正式環境仍應使用 KMS 或憑證加密保護 key material。
- 設定與 log 不應記錄密碼、Cookie、CSRF token 或原始認證 header。
