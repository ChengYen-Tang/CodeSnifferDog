# Scan Agent Tools

## 輸入原則

- `Scan Agent` 的 repo path 不直接寫進固定 prompt。
- 每次執行 scan 時，程式邏輯應以固定 prefix 加上 repo path 的方式，透過 system-controlled user input 提供當前輸入。
- 固定 prefix 第一版定義為：
  `The following path is the repository root to scan for projects. Identify the project units that should enter the next planning stage.`

## AddScanProject

新增一筆 `ScanProject`。

- 使用時機：`Scan Agent` 發現一個應進入後續 project planning 階段的 project 單位時。
- 參數：
  - `projectName`：專案或模組名稱。
  - `projectPath`：專案或模組在 repo 中的路徑。
  - `projectType`：專案類型。
  - `reason`：為什麼將它視為值得進入後續 planning 的 project 單位。
- 回傳：系統自動產生的 `ScanProjectId`。

## AddScanProjects

一次新增多筆 `ScanProject`。

- 使用時機：`Scan Agent` 想一次提交多個 project 單位時。
- 參數：
  - `projects`：多筆 `ScanProject` 內容。
- 回傳：每筆新增後對應的 `ScanProjectId` 清單。

## DeleteScanProject

刪除既有的 `ScanProject`。

- 使用時機：`Scan Agent` 判斷某筆 scan 結果不應保留時。
- 參數：
  - `scanProjectId`：要刪除的 project 識別值。
- 回傳：通常只需回傳刪除成功狀態。

## ListScanProjects

列出目前 scan 階段已建立的所有 `ScanProject`。

- 使用時機：`Scan Agent` 或 `Scan Verifier Agent` 需要查看目前 scan 結果時。
- 參數：無。
- 回傳：目前所有 `ScanProject` 的清單，每筆都應包含 `ScanProjectId`。

## C# Tool Shapes

```csharp
namespace CodeSnifferDog.Agent.Tools;

public sealed class AddScanProjectArgs
{
    public required string ProjectName { get; init; }
    public required string ProjectPath { get; init; }
    public required string ProjectType { get; init; }
    public required string Reason { get; init; }
}

public sealed class AddScanProjectResult
{
    public required string ScanProjectId { get; init; }
}

public sealed class AddScanProjectsArgs
{
    public required IReadOnlyList<AddScanProjectArgs> Projects { get; init; }
}

public sealed class AddScanProjectsResult
{
    public required IReadOnlyList<string> ScanProjectIds { get; init; }
}

public sealed class DeleteScanProjectArgs
{
    public required string ScanProjectId { get; init; }
}
```

## 變更紀錄

- 2026-04-12：建立 `Scan Agent` 第一版工具設計。
