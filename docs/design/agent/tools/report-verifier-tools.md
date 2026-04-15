# Report Verifier Tools

## 設計方向

- `Report Verifier` 沿用 `Review Verifier` 的 verdict 工具
- 不另外建立新的 verdict 結構
- `SubmitReviewVerdict` 只表達是否通過，不負責描述下一步路由
- 下一步由程式邏輯根據目前階段決定

## SubmitReviewVerdict

提交 `Report Verifier` 的驗證結果。

- 使用時機：`Report Verifier` 完成檢查後，用這個工具明確表示通過或退回。
- 參數：
  - `approved`：驗證決定。`true` 代表通過，`false` 代表退回。
  - `message`：驗證說明。
    - 若 `approved = true`，簡短說明為什麼本次聚合結果可接受。
    - 若 `approved = false`，明確說明 `Report Aggregator` 需要修正、補強或重整的內容。
- 回傳：通常只需回傳提交成功狀態。

## 路由規則

- `SubmitReviewVerdict` 不負責描述下一步路由。
- 如果 `approved = true`，程式邏輯將此 rule flow 標記為完成。
- 如果 `approved = false`，程式邏輯會把 `message` 轉成 system-controlled user input，送回同一個 `Report Aggregator`。
- `Report Verifier` 不直接與 `Report Aggregator` 對話，也不自行管理流程跳轉。

## 輸入原則

- `Report Verifier` 的固定上下文由 prompt 佔位符提供。
- 會隨每次 plan iteration 改變的內容，不應直接寫進固定 prompt。
- 當前 flow issues 是固定輸入，應由 prompt 佔位符提供。
- 當前 `RuleReportDiff` 會隨首次提交與後續回退重提而改變，應由程式邏輯透過固定 prefix 加上 system-controlled user input 提供。
- 固定 prefix 第一版定義為：
  `The following content is the current report diff from the Report Aggregator. Approve it if acceptable. Reject it if more work is required, and explain why.`

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

- 2026-04-12：建立 `Report Verifier` 工具說明，沿用 `SubmitReviewVerdictArgs`。
