# Project Plan Agent Tools

## 輸入原則

- `Project Plan Agent` 的固定上下文由 prompt 佔位符提供。
- `Repository root path` 是固定上下文，應由 prompt 佔位符提供。
- 每次執行 project planning 時，程式邏輯應以固定 prefix 加上當前 foreach 的 `ScanProject`，透過 system-controlled user input 啟動該 flow。
- 固定 prefix 第一版定義為：
  `The following content is the scan project to plan. Create task items that should enter the next review stage.`

## 切分原則

- `Project Plan Agent` 應避免切出過大的 `task item`。
- 第一版建議同時控制：
  - 單一 `task item` 的檔案數上限
  - 單一 `task item` 的總行數上限
- 建議第一版預設值為：
  - `MaxFilesPerTaskItem = 10`
  - `MaxTotalLinesPerTaskItem = 2000`
- 若任一上限超過，應優先拆成多個 `task item`。
- 若單一檔案本身已超過總行數上限，則允許該檔案單獨成為一個 `task item`。
- 對 C/C++ 類型專案，若 header 與 implementation 檔案明顯成對，應優先保留在同一個 `task item`。
- 這條成對規則優先於一般的檔案數與總行數上限。
- 第一版原則是寧可切小，不要切太胖。

## AddProjectPlanTaskItem

新增一筆 `ProjectPlanTaskItem`。

- 使用時機：`Project Plan Agent` 建立一筆新的 task item 時。
- 參數：
  - `files`：此 task item 的 `ProjectPlanFile` 集合。
- 回傳：系統自動產生的 `ProjectPlanTaskItemId`。

## AddProjectPlanTaskItems

一次新增多筆 `ProjectPlanTaskItem`。

- 使用時機：`Project Plan Agent` 想一次提交多個 task item 時。
- 參數：
  - `taskItems`：多筆 `ProjectPlanTaskItem` 內容。
- 回傳：每筆新增後對應的 `ProjectPlanTaskItemId` 清單。

## DeleteProjectPlanTaskItem

刪除既有的 `ProjectPlanTaskItem`。

- 使用時機：`Project Plan Agent` 判斷某筆 task item 不應保留時。
- 參數：
  - `projectPlanTaskItemId`：要刪除的 task item 識別值。
- 回傳：通常只需回傳刪除成功狀態。

## ListProjectPlanTaskItems

列出目前 project planning 階段已建立的所有 `ProjectPlanTaskItem`。

- 使用時機：`Project Plan Agent` 或 `Project Verifier Agent` 需要查看目前 project plan 結果時。
- 參數：無。
- 回傳：目前所有 `ProjectPlanTaskItem` 的清單，每筆都應包含 `ProjectPlanTaskItemId`。

## C# Tool Shapes

```csharp
namespace CodeSnifferDog.Agent.Tools;

public sealed class AddProjectPlanTaskItemArgs
{
    public required IReadOnlyList<ProjectPlanFile> Files { get; init; }
}

public sealed class AddProjectPlanTaskItemResult
{
    public required string ProjectPlanTaskItemId { get; init; }
}

public sealed class AddProjectPlanTaskItemsArgs
{
    public required IReadOnlyList<AddProjectPlanTaskItemArgs> TaskItems { get; init; }
}

public sealed class AddProjectPlanTaskItemsResult
{
    public required IReadOnlyList<string> ProjectPlanTaskItemIds { get; init; }
}

public sealed class DeleteProjectPlanTaskItemArgs
{
    public required string ProjectPlanTaskItemId { get; init; }
}
```

## 變更紀錄

- 2026-04-13：建立 `Project Plan Agent` 第一版工具設計。
