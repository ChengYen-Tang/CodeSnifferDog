# Review Verdict Model

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `Review Verifier Agent` 提交給程式編排邏輯的最小 verdict 結構
- 文件狀態：草稿
- 最後更新：2026-04-12

## 設計方向

- 只保留 agent 間交流需要的最小欄位
- 只使用 `bool` 與 `string`
- 不做複雜巢狀結構
- 驗證結果只需要表達「通過或退回」與「原因」

## C# Structure

```csharp
public sealed class ReviewVerdict
{
    public required bool Approved { get; init; }
    public required string Message { get; init; }
}
```

## 欄位意圖

- `Approved`
  驗證決定。`true` 代表通過，`false` 代表退回。

- `Message`
  給程式邏輯與上一個 agent 的文字說明。
  若 `Approved` 是 `true`，此欄位可簡短說明通過原因。
  若 `Approved` 是 `false`，此欄位應明確指出要補查、修正或重做的內容。

## 變更紀錄

- 2026-04-12：建立 `Review Verifier Agent` 的簡化 verdict 模型，採雙字串欄位。
- 2026-04-12：將 verdict 決定欄位改為 `bool`，避免字串枚舉。
