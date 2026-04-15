# Rule Report Diff Model

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `Report Verifier` 用來驗證本次聚合結果的差異模型
- 文件狀態：草稿
- 最後更新：2026-04-12

## 設計方向

- `Report Verifier` 以「當前 flow issues + 本次聚合差異」為主要輸入
- 差異模型不追求 patch 級別精細度
- 第一版直接用 `RuleReviewIssue` 集合描述新增、更新、刪除

## C# Structure

```csharp
namespace CodeSnifferDog.Agent.Messages;

public sealed class RuleReportDiff
{
    public required IReadOnlyList<RuleReviewIssue> CreatedIssues { get; init; }
    public required IReadOnlyList<RuleReviewIssue> UpdatedIssues { get; init; }
    public required IReadOnlyList<RuleReviewIssue> DeletedIssues { get; init; }
}
```

## 欄位意圖

- `CreatedIssues`
  本次聚合新增到 repo-level rule report 的 issues。

- `UpdatedIssues`
  本次聚合修改過的 repo-level issues。

- `DeletedIssues`
  本次聚合從 repo-level rule report 刪除的 issues。

## 變更紀錄

- 2026-04-12：建立 `RuleReportDiff` 第一版模型。
