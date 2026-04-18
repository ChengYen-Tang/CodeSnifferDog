# Report Aggregator Tools

## 設計方向

- `Report Aggregator` 維護某條 `rule` 在整個 repo 範圍內的 `RuleReportIssue` 集合
- 來源有兩份：
  - 當前 `plan item + rule flow` 通過 verifier 的 `RuleReviewIssue` 集合，由系統主動提供給 agent
  - 目前已存在的該 `rule` latest snapshot，系統會在每條 `plan item + rule flow` 開始時複製成這條 flow 自己的 working report
- `Report Aggregator` 的責任是把前者整理進後者
- repo-level 的 report issue 與 review-stage issue 使用相同欄位內容，但擁有自己的 `RuleReportIssueId`

## GetRuleReportIssues

讀取目前整個 repo 範圍下，某條 `rule` 的 working `RuleReportIssue` 集合。

- 使用時機：`Report Aggregator` 需要取得目前已存在的 rule-level issue 集合時。
- 參數：無。
- 回傳：目前該 `rule` 的所有 `RuleReportIssue`，每筆都應包含可供後續更新或刪除的識別值。

## GetRuleReportIssue

讀取該 `rule` 的單筆既有總 issue。

- 使用時機：`Report Aggregator` 需要查看某筆既有總 issue 的完整內容時。
- 參數：
  - `ruleReportIssueId`：要讀取的總 issue 識別值。
- 回傳：對應的完整 `RuleReportIssue` 內容。

## CreateRuleReportIssue

在該 `rule` 的 working report 中新增一筆 `RuleReportIssue`。

- 使用時機：`Report Aggregator` 判斷某筆 flow issue 無法與既有總 issue 合併時。
- 參數：直接使用 `RuleReviewIssue` 所需欄位。
- 回傳：系統自動產生的 `RuleReportIssueId`。

## UpdateRuleReportIssue

更新該 `rule` 的既有總 issue。

- 使用時機：`Report Aggregator` 判斷某筆 flow issue 應與既有總 issue 合併時。
- 參數：
  - `ruleReportIssueId`
  - 更新後的完整 `RuleReportIssue` 欄位內容
- 回傳：通常只需回傳更新成功狀態。

## DeleteRuleReportIssue

刪除該 `rule` 的既有總 issue。

- 使用時機：第一版通常較少用，保留給未來重整或 verifier 回退後需要移除總 issue 的情況。
- 參數：
  - `ruleReportIssueId`
- 回傳：通常只需回傳刪除成功狀態。

## 路由原則

- `Report Aggregator` 只負責維護當前 `plan item + rule flow` 自己的 working `RuleReportIssue` 集合。
- 它不決定下一步是否完成，這由後續 `Report Verifier` 與程式邏輯處理。
- `NoIssueConclusion` 不會進入 `Report Aggregator`。
- 當前 flow 的 `RuleReviewIssue` 集合由系統主動提供，不透過工具讀取。

## Merge / Dedupe 原則

第一版不追求完美語意去重，而是採可實作、可理解的合併規則。

### 主判斷欄位

以下欄位用來判斷兩筆 issue 是否描述同一個 underlying issue：

- `IssueType`
- `WhyThisIsAProblem`
- `SuggestedFixDirection`

若這三者高度一致，應優先考慮合併。

### 輔助判斷欄位

以下欄位可作為輔助訊號，提高或降低「其實是同一個問題」的信心：

- `CrossScopeAnalysis`
- `ReviewStrategy`
- `FileOrFunction`
- `RelevantCodePatternOrExpression`

這些欄位有參考價值，但不應作為唯一去重依據。

### 不應作為唯一 identity 的欄位

以下欄位不應作為唯一去重依據，因為不同 scope 可能從不同入口觀察到同一個 underlying issue：

- `FileOrFunction`
- `RelevantCodePatternOrExpression`

### Merge 後的欄位處理

若判定兩筆 issue 應合併，第一版建議如下：

- `IssueType`：保留既有值或較明確版本
- `WhyThisIsAProblem`：保留較完整、較清楚的版本
- `SuggestedFixDirection`：保留較完整、且與問題本質一致的版本
- `FileOrFunction`：整合補充，不只保留單一入口
- `RelevantCodePatternOrExpression`：整合補充
- `FollowUpFiles`：聯集
- `ScopeCoverage`：保留較完整版本
- `CrossScopeAnalysis`：保留較完整版本
- `ReviewStrategy`：保留較完整版本或整合成較完整描述
- `Confidence`：保留較高信心版本，或選較可信的保守表達

## C# Tool Shapes

與 `Rule Review Agent` 的 CRUD 工具相同，本文件延續同樣的 shape 風格。
若需參考欄位結構，請見 [rule-review-output-model.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\models\rule-review-output-model.md)。

```csharp
namespace CodeSnifferDog.Agent.Tools;

public sealed class CreateRuleReportIssueArgs
{
    public required string IssueType { get; init; }
    public required string FileOrFunction { get; init; }
    public required string RelevantCodePatternOrExpression { get; init; }
    public required string WhyThisIsAProblem { get; init; }
    public required string Confidence { get; init; }
    public required string FollowUpFiles { get; init; }
    public required string SuggestedFixDirection { get; init; }
    public required string ScopeCoverage { get; init; }
    public required string CrossScopeAnalysis { get; init; }
    public required string ReviewStrategy { get; init; }
}

public sealed class CreateRuleReportIssueResult
{
    public required string RuleReportIssueId { get; init; }
}

public sealed class GetRuleReportIssueArgs
{
    public required string RuleReportIssueId { get; init; }
}

// `ListRuleReportIssues` 與 `ListRuleReviewIssues` 類似，不需要輸入參數，
// 回傳目前該 rule 在 working report 中的所有 `RuleReportIssue`，每筆包含識別值。

public sealed class UpdateRuleReportIssueArgs
{
    public required string RuleReportIssueId { get; init; }
    public required string IssueType { get; init; }
    public required string FileOrFunction { get; init; }
    public required string RelevantCodePatternOrExpression { get; init; }
    public required string WhyThisIsAProblem { get; init; }
    public required string Confidence { get; init; }
    public required string FollowUpFiles { get; init; }
    public required string SuggestedFixDirection { get; init; }
    public required string ScopeCoverage { get; init; }
    public required string CrossScopeAnalysis { get; init; }
    public required string ReviewStrategy { get; init; }
}

public sealed class DeleteRuleReportIssueArgs
{
    public required string RuleReportIssueId { get; init; }
}
```

## 變更紀錄

- 2026-04-12：建立 `Report Aggregator` 工具與第一版 merge / dedupe 原則。
- 2026-04-12：補充 repo-level issue CRUD 工具的完整參數說明與 C# shape。
