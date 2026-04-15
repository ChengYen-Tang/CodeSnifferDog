# Rule Review Issue Tools

## CreateRuleReviewIssue

建立一筆新的 rule review issue。

- 使用時機：`Rule Review Agent` 發現一個新的 issue，並要把它寫入系統時。
- 回傳：系統自動產生的 `RuleReviewIssueId`。
- 參數：
  - `issueType`：問題類型。
  - `fileOrFunction`：相關的檔案或函式資訊。
  - `relevantCodePatternOrExpression`：相關的程式碼片段、模式或表達式。
  - `whyThisIsAProblem`：說明這為什麼是問題。
  - `confidence`：此問題判斷的信心程度，預期值為 `High`、`Medium`、`Low`。
  - `followUpFiles`：若需要後續追查，相關的 follow-up files。
  - `suggestedFixDirection`：建議修正方向。
  - `scopeCoverage`：讓 agent 自己說明：
    - scope entry files 哪些有看
    - 哪些沒看
    - 沒看的原因
    - 它認為 coverage 是否足夠
  - `crossScopeAnalysis`：讓 agent 說明：
    - 有沒有跨 scope
    - 看了哪些 follow-up files
    - 為什麼要跨
    - 如果沒跨，為什麼認為不需要
  - `reviewStrategy`：讓 agent 簡短說它這次怎麼查的。

## GetRuleReviewIssue

讀取單筆既有的 rule review issue。

- 使用時機：agent 需要查看某筆 issue 的完整內容時。
- 參數：
  - `ruleReviewIssueId`：要讀取的 issue 識別值。
- 回傳：對應的完整 issue 內容。

## ListRuleReviewIssues

列出目前這個 agent / task item 範圍內的所有 rule review issues。

- 使用時機：agent 需要查看目前已存在的 issues 時。
- 參數：無。
- 回傳：目前範圍內所有 issues 的清單，每筆都應包含 `RuleReviewIssueId`。

## UpdateRuleReviewIssue

更新既有的 rule review issue。

- 使用時機：agent 要補充、修正或重整既有 issue 內容時。
- 參數：
  - `ruleReviewIssueId`：要更新的 issue 識別值。
  - `issueType`：更新後的問題類型。
  - `fileOrFunction`：更新後的檔案或函式資訊。
  - `relevantCodePatternOrExpression`：更新後的相關程式碼片段、模式或表達式。
  - `whyThisIsAProblem`：更新後的問題說明。
  - `confidence`：更新後的信心程度。
  - `followUpFiles`：更新後的 follow-up files。
  - `suggestedFixDirection`：更新後的建議修正方向。
  - `scopeCoverage`：更新後的 scope coverage，內容應包含：
    - scope entry files 哪些有看
    - 哪些沒看
    - 沒看的原因
    - 它認為 coverage 是否足夠
  - `crossScopeAnalysis`：更新後的 cross-scope analysis，內容應包含：
    - 有沒有跨 scope
    - 看了哪些 follow-up files
    - 為什麼要跨
    - 如果沒跨，為什麼認為不需要
  - `reviewStrategy`：更新後的 review strategy。
- 回傳：通常只需回傳更新成功狀態。

## DeleteRuleReviewIssue

刪除既有的 rule review issue。

- 使用時機：agent 判斷某筆 issue 應被移除時。
- 參數：
  - `ruleReviewIssueId`：要刪除的 issue 識別值。
- 回傳：通常只需回傳刪除成功狀態。

## SubmitNoIssueConclusion

提交「本次 review 未發現 issue」的結論。

- 使用時機：`Rule Review Agent` 在完成檢查後，判斷目前 scope 範圍內沒有可提交的 issue 時。
- 參數：
  - `reviewStrategy`：說明這次 review 是怎麼查的。
  - `scopeCoverage`：讓 agent 自己說明：
    - scope entry files 哪些有看
    - 哪些沒看
    - 沒看的原因
    - 它認為 coverage 是否足夠
  - `crossScopeAnalysis`：讓 agent 說明：
    - 有沒有跨 scope
    - 看了哪些 follow-up files
    - 為什麼要跨
    - 如果沒跨，為什麼認為不需要
  - `whyNoIssueWasFound`：說明為何本次檢查沒有發現可提交的 issue。
- 回傳：通常只需回傳提交成功狀態。

### 狀態規則

- 如果目前 `ListRuleReviewIssues` 結果中存在任何 issue，`SubmitNoIssueConclusion` 必須失敗。
- 如果先前已成功呼叫 `SubmitNoIssueConclusion`，之後又新增任何 issue，系統應自動重置 `NoIssueConclusion` 狀態。
- 上述狀態管理由程式邏輯負責，不由 agent 自行維護。

## C# Tool Shapes

```csharp
namespace CodeSnifferDog.Agent.Tools;

public sealed class CreateRuleReviewIssueArgs
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

public sealed class CreateRuleReviewIssueResult
{
    public required string RuleReviewIssueId { get; init; }
}

public sealed class GetRuleReviewIssueArgs
{
    public required string RuleReviewIssueId { get; init; }
}

public sealed class UpdateRuleReviewIssueArgs
{
    public required string RuleReviewIssueId { get; init; }
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

public sealed class DeleteRuleReviewIssueArgs
{
    public required string RuleReviewIssueId { get; init; }
}

public sealed class SubmitNoIssueConclusionArgs
{
    public required string ReviewStrategy { get; init; }
    public required string ScopeCoverage { get; init; }
    public required string CrossScopeAnalysis { get; init; }
    public required string WhyNoIssueWasFound { get; init; }
}
```
