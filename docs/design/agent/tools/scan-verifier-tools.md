# Scan Verifier Tools

## 設計方向

- `Scan Verifier Agent` 沿用統一的 verdict 工具
- 不另外建立新的 verdict 結構
- `SubmitReviewVerdict` 只表達是否通過，不負責描述下一步路由
- 下一步由程式邏輯根據目前階段決定

## SubmitReviewVerdict

提交 `Scan Verifier Agent` 的驗證結果。

- 使用時機：`Scan Verifier Agent` 完成檢查後，用這個工具明確表示通過或退回。
- 參數：
  - `approved`：驗證決定。`true` 代表通過，`false` 代表退回。
  - `message`：驗證說明。
    - 若 `approved = true`，簡短說明為什麼本次 scan 結果可接受。
    - 若 `approved = false`，明確說明 `Scan Agent` 需要補掃、修正或刪除的內容。
- 回傳：通常只需回傳提交成功狀態。

## 輸入原則

- `Scan Verifier Agent` 的固定上下文由 prompt 佔位符提供。
- 會隨每次驗證變動的內容，不應直接寫進固定 prompt。
- `ListScanProjects` 的目前結果應由程式邏輯透過固定 prefix 加上 system-controlled user input 提供。
- 第一次驗證與回退後再次驗證，使用相同的輸入格式。
- 固定 prefix 第一版定義為：
  `The following content is the current scan result from the Scan Agent. Approve it if acceptable. Reject it if more work is required, and explain why.`

## 路由規則

- `SubmitReviewVerdict` 不負責描述下一步路由。
- 如果 `approved = true`，程式邏輯將 scan 階段標記為完成，進入 `Project Plan Agent` loop。
- 如果 `approved = false`，程式邏輯會把 `message` 轉成 system-controlled user input，送回同一個 `Scan Agent`。
- `Scan Verifier Agent` 不直接與 `Scan Agent` 對話，也不自行管理流程跳轉。

## C# Tool Shape

```csharp
namespace CodeSnifferDog.Agent.Tools;

public sealed class SubmitReviewVerdictArgs
{
    public required bool Approved { get; init; }
    public required string Message { get; init; }
}
```

## 變更紀錄

- 2026-04-13：建立 `Scan Verifier Agent` 工具說明，沿用 `SubmitReviewVerdictArgs`。
