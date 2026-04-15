# Review Verifier Tools

## SubmitReviewVerdict

提交 `Review Verifier Agent` 的驗證結果。

- 使用時機：`Review Verifier Agent` 完成檢查後，用這個工具明確表示通過或退回。
- 參數：
  - `approved`：驗證決定。`true` 代表通過，`false` 代表退回。
  - `message`：驗證說明。
    - 若 `approved = true`，簡短說明為什麼可以進入下一階段。
    - 若 `approved = false`，明確說明 `Rule Review Agent` 需要補什麼、查什麼、修正什麼。
- 回傳：通常只需回傳提交成功狀態。

## 路由規則

- `SubmitReviewVerdict` 不負責描述下一步路由。
- 下一步由程式邏輯根據 `approved` 與當前 review result 類型決定。
- 如果 `approved = true` 且當前 review result 為 issue 清單，程式邏輯將此 flow 推進到 `Report Aggregator`。
- 如果 `approved = true` 且當前 review result 為 `NoIssueConclusion`，程式邏輯直接結束此 rule flow。
- 如果 `approved = false`，程式邏輯會把 `message` 轉成 system-controlled user input，送回同一個 `Rule Review Agent`。
- `Review Verifier Agent` 不直接與 `Rule Review Agent` 對話，也不自行管理流程跳轉。

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

- 2026-04-12：建立 `Review Verifier Agent` 的單一 verdict 工具設計。
- 2026-04-12：將 verdict 決定欄位改為 `bool`。
