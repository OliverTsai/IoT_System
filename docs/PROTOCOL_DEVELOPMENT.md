# 通訊協定擴充架構

## 目前範圍

設備會保存穩定的 `protocolKey` 與由對應 adapter 驗證、正規化後的 JSON 設定。既有設備及沒有傳入 `protocolKey` 的舊版建立請求會使用 `mqtt`，因此原有 MQTT 與 REST 操作維持相容。

目前只有 MQTT adapter。Modbus TCP 屬於下一階段，尚未實作；REST 遙測端點仍是管理與測試入口，不是設備可選的通訊協定。

```text
ProtocolIngestionCoordinator
        │ discovers enabled adapters through DI
        ▼
IDeviceTelemetryProtocolAdapter
        │ normalized telemetry
        ▼
TelemetryIngestionService ──► PostgreSQL / Alerts / SignalR
```

## 協定目錄與設備建立

已登入使用者可讀取：

```http
GET /api/device-protocols
```

目錄只回傳協定 key、顯示名稱、是否為預設值，以及建立設備所需的非敏感欄位描述。Vue 新增設備表單由這份資料產生選單與設定欄位，不自行維護協定清單。

Admin 建立 MQTT 設備的完整 request：

```json
{
  "externalId": "factory-a.sensor-01",
  "name": "一廠環境感測器",
  "protocolKey": "mqtt",
  "protocolSettings": {}
}
```

`protocolKey` 與 `protocolSettings` 皆可省略，省略時等同上述 MQTT 設定。未知 key、超過 16 KiB 的設定，或不符合 adapter 規則的內容會回傳 validation Problem Details，且不寫入資料庫。協定設定不包含於設備 API response，也不得寫入 log。

設備建立後不能在線切換協定。若未來需要切換，應另立階段定義停用、設定驗證、背景工作交接與既有遙測歸屬，不能只修改資料庫欄位。

## 新增 adapter 的最小步驟

1. 實作 `IDeviceTelemetryProtocolAdapter`，提供唯一的小寫 kebab-case key、公開欄位描述、啟用狀態、設定驗證及可取消的 `RunAsync`。
2. 透過 DI 將實作註冊為 `IDeviceTelemetryProtocolAdapter`。不需修改 coordinator、MQTT adapter 或 `TelemetryIngestionService`。
3. 只把已驗證且已正規化的量測交給 `TelemetryIngestionService`，不要在 adapter 重複資料庫、告警或 SignalR 邏輯。
4. 為設定成功／失敗、停用設備、取消、斷線與 adapter 故障隔離建立測試。
5. 更新協定文件及 smoke test。若需要新的正式相依套件，須先說明版本、用途、替代方案與維護風險並取得同意。

## 安全界線

- JSON 設定只能保存非敏感值或 secret reference，不得保存密碼、Token、私鑰或憑證內容。
- adapter 必須限制輸入大小、目的地、timeout、重試、輪詢頻率與並行量；不能因 Admin 可建立設備就忽略 SSRF 或資源耗盡風險。
- response、validation message 與結構化 log 不得輸出完整設定 JSON。
- coordinator 會分別監督啟用的 adapter；單一 adapter 結束或丟出例外時，其他 adapter 會繼續運作，失敗的 adapter 會在延遲後重新啟動。

## Migration

`AddDeviceProtocolArchitecture` 新增：

- `devices.protocol_key`：既有資料回填 `mqtt`。
- `devices.protocol_settings`：`jsonb`，既有資料回填空物件。
- `(protocol_key, is_active)` index：供輪詢型 adapter 查找啟用設備。

向下回退會刪除這兩個欄位及其中設定資料；執行 migration 前仍須確認目標是本機／測試資料庫並完成必要備份。
