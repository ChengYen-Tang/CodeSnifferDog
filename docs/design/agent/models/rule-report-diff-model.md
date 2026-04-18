# Rule Report Diff Model

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `Report Verifier` 用來驗證本次聚合結果的差異模型
- 文件狀態：草稿
- 最後更新：2026-04-12

## 設計方向

- `Report Verifier` 以「當前 flow issues + 本次聚合差異」為主要輸入
- 差異模型不追求 patch 級別精細度
- 差異模型應描述某條 `rule` 的 latest snapshot 與當前 `plan item + rule flow` working report 之間的新增、更新、刪除
- repo-level report issue 與 review-stage issue 分開建模

## C# Structure

```csharp
public sealed class RuleReportDiff
{
    public required IReadOnlyList<StoredRuleReportIssue> CreatedIssues { get; init; }
    public required IReadOnlyList<StoredRuleReportIssue> UpdatedIssues { get; init; }
    public required IReadOnlyList<StoredRuleReportIssue> DeletedIssues { get; init; }
}
```

## 欄位意圖

- `CreatedIssues`
  本次聚合相對於 latest snapshot，新建立到 working report 的 repo-level issues。

- `UpdatedIssues`
  本次聚合相對於 latest snapshot，被修改過的 repo-level issues。

- `DeletedIssues`
  本次聚合相對於 latest snapshot，被從 working report 刪除的 repo-level issues。

## 變更紀錄

- 2026-04-12：建立 `RuleReportDiff` 第一版模型。
