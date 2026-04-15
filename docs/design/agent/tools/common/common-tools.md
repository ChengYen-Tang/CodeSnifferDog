# Common Tools

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義所有 Agent 共用的通用分析工具
- 文件狀態：草稿
- 最後更新：2026-04-14

## 第一版範圍

- 第一版通用工具只保留兩類：
  - `Shell`
  - `Ripgrep search`
- 先不要再拆更多通用工具。
- 若後續發現有明確重複需求，再考慮新增其他工具。

## 設計原則

- 工具數量應保持精簡，避免 Agent 在多個相似工具之間混亂選擇。
- 能由 `Shell` 補足的能力，第一版不額外拆成獨立工具。
- `Ripgrep search` 應作為大規模 codebase 搜尋的主要工具。
- 所有 Agent 都共用同一組通用工具定義，不因單一 provider 的能力差異改變抽象名稱。

## Shell

- 用途：
  - 執行平台原生命令
  - 補足檔案探索、目錄探索、環境查詢與必要的輕量驗證
- 平台策略：
  - Windows 使用 `PowerShell`
  - Linux/macOS 使用 `bash`
- 預設工作目錄：
  - `Repository root path`
- 第一版不額外細分成多個 shell 工具。
- 第一版先不要設計過多限制，後續依實作需要再補安全策略與命令白名單。

## Ripgrep search

- 用途：
  - 搜尋 repo 內的文字、符號、模式與關鍵字
  - 作為大型專案分析時的主要搜尋入口
- 工具形式：
  - 提供單一 `RunRipgrepCommand` 工具
  - Agent 傳入的 `Command` 只包含 `rg` 後面的參數
  - Agent 不應在 `Command` 內重複包含 `rg`
- 預設工作目錄：
  - `Repository root path`
- 執行策略：
  - 所有平台都使用系統隨附的 `ripgrep (rg)` 編譯資產
  - 不依賴使用者環境中的 PATH
- 資產策略：
  - 應用程式應可依目前執行位置推導資產目錄
  - 實作層從資產目錄中的 `rg` 可執行檔啟動搜尋
  - Windows 使用 `rg.exe`
  - Linux/macOS 使用 `rg`
- 目前專案內的資產位置：
  - `CodeSnifferDog/assets/ripgrep/win-x64/rg.exe`
  - `CodeSnifferDog/assets/ripgrep/win-arm64/rg.exe`
  - `CodeSnifferDog/assets/ripgrep/linux-x64/rg`
  - `CodeSnifferDog/assets/ripgrep/linux-arm64/rg`
  - `CodeSnifferDog/assets/ripgrep/osx-x64/rg`
  - `CodeSnifferDog/assets/ripgrep/osx-arm64/rg`
- 目前專案已設定在編譯時複製 `CodeSnifferDog/assets/ripgrep/**` 到輸出目錄。
- 理由：
  - `rg` 為跨平台工具
  - 速度快
  - 適合大型 codebase
  - 可統一各平台的行為與回傳風格
- 因為 `rg` 由系統隨附，第一版不需要設計使用者環境缺少 `rg` 的 fallback。

## 與 Provider 的關係

- `Shell` 與 `Ripgrep search` 是系統的通用抽象，不直接綁定特定模型 provider。
- 若底層 provider 剛好有相近能力，例如 Claude 的 `Bash` 或 `Glob`，那屬於實作映射問題，不改變通用工具設計。
- 第一版仍以系統自行提供這兩個工具為主。

## 變更紀錄

- 2026-04-14：建立通用工具文件，第一版只保留 `Shell` 與 `Ripgrep search`。
- 2026-04-14：補充 `RunRipgrepCommand` 使用系統隨附的 `rg` 編譯資產，不依賴使用者環境 PATH。
- 2026-04-14：補充專案內 `rg` 資產的實際放置路徑與編譯複製行為。
