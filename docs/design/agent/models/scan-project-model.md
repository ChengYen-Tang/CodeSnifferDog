# Scan Project Model

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `Scan Agent` 輸出給後續 `Project Plan Agent` 使用的 project 結構
- 文件狀態：草稿
- 最後更新：2026-04-12

## 設計方向

- 第一版只保留支撐後續 project planning loop 的最低必要資訊
- `Scan Agent` 不負責深入理解專案語意
- `Scan Agent` 的目標是找出有哪些 project 單位應進入後續 planning 階段

## C# Structure

```csharp
namespace CodeSnifferDog.Agent.Messages;

public sealed class ScanProject
{
    public required string ProjectName { get; init; }
    public required string ProjectPath { get; init; }
    public required string ProjectType { get; init; }
    public required string Reason { get; init; }
}
```

## 欄位意圖

- `ProjectName`
  專案或模組名稱。

- `ProjectPath`
  專案或模組在 repo 中的路徑。

- `ProjectType`
  專案類型。第一版可接受檔案型別或技術類型，例如 `.csproj`、`package.json`、`pyproject.toml`、`go.mod`、`Cargo.toml`，或 `dotnet`、`node`、`python`、`go`、`rust`。

- `Reason`
  `Scan Agent` 為什麼把它視為值得進入後續 planning 的 project 單位。

## 變更紀錄

- 2026-04-12：建立 `Scan Agent` 第一版 project 結構。
