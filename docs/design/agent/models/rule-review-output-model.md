# Rule Review Issue Model

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `Rule Review Agent` 提交給 `Review Verifier Agent` 的 issue 結構
- 文件狀態：草稿
- 最後更新：2026-04-12

## 設計方向

- 只定義一個主要結構
- 只保留必要欄位
- 欄位型別盡量單純，以 `string` 為主
- 欄位代表「必須涵蓋的大方向」
- 每個欄位內的細節怎麼整理、如何補充，由 Agent 自行決定

## C# Structure

```csharp
namespace CodeSnifferDog.Agent.Messages;

public sealed class RuleReviewIssue
{
    public required string IssueType { get; init; }
    public required string FileOrFunction { get; init; }
    public required string RelevantCodePatternOrExpression { get; init; }
    public required string WhyThisIsAProblem { get; init; }
    public required string Confidence { get; init; }
    public required string FollowUpFiles { get; init; }
    public required string SuggestedFixDirection { get; init; }
    public required string ReviewStrategy { get; init; }
    public required string ScopeCoverage { get; init; }
    public required string CrossScopeAnalysis { get; init; }
}
```

## 欄位意圖

- `IssueType`
  問題類型。

- `FileOrFunction`
  相關的檔案或函式資訊。

- `RelevantCodePatternOrExpression`
  相關的程式碼片段、模式或表達式。

- `WhyThisIsAProblem`
  說明這為什麼是問題。

- `Confidence`
  此問題判斷的信心程度，預期值為 `High`、`Medium` 或 `Low`。

- `FollowUpFiles`
  若需要後續追查，相關的 follow-up files。

- `SuggestedFixDirection`
  建議修正方向。

- `ReviewStrategy`
  說明這次 review 是怎麼查的。

- `ScopeCoverage`
  讓 agent 自己說明：
  - scope entry files 哪些有看
  - 哪些沒看
  - 沒看的原因
  - 它認為 coverage 是否足夠

- `CrossScopeAnalysis`
  讓 agent 說明：
  - 有沒有跨 scope
  - 看了哪些 follow-up files
  - 為什麼要跨
  - 如果沒跨，為什麼認為不需要

## 變更紀錄

- 2026-04-12：建立簡化版 `Rule Review Agent` issue 模型，採單一結構與字串欄位。
