# Project Plan Task Item Model

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `Project Plan Agent` 輸出給後續 review 階段使用的 task item 結構
- 文件狀態：草稿
- 最後更新：2026-04-13

## 設計方向

- `Project Plan Agent` 的輸出是一批 `task item`
- 每個 `task item` 本質上是一組作為 scope 入口的程式碼檔案
- 第一版不加入多餘 metadata
- 但保留每個檔案的總行數，幫助後續 agent 與 verifier 判斷 scope 大小與 coverage

## C# Structure

```csharp
namespace CodeSnifferDog.Agent.Messages;

public sealed class ProjectPlanTaskItem
{
    public required IReadOnlyList<ProjectPlanFile> Files { get; init; }
}

public sealed class ProjectPlanFile
{
    public required string FilePath { get; init; }
    public required int TotalLines { get; init; }
}
```

## 欄位意圖

- `Files`
  此 `task item` 的 scope entry files。

- `FilePath`
  檔案路徑。

- `TotalLines`
  該檔案總行數。

## 設計補充

- `scope` 仍然只是檢查入口，不是推理邊界。
- 後續 `Rule Review Agent` 仍可跨 scope 追查相依性。
- `Project Plan Agent` 應避免切出過大的 `task item`，以降低後續 review 遺漏風險。
- 第一版建議同時控制單一 `task item` 的檔案數與總行數。

## 變更紀錄

- 2026-04-13：建立 `Project Plan Agent` 第一版 task item 結構。
