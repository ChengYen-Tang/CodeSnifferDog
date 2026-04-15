# Memory And Compaction

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 Agent 的記憶與 context compaction 機制
- 文件狀態：草稿
- 最後更新：2026-04-14

## 設計目標

- 第一版完整採用 Claude Code 類型的 context compaction 機制。
- 在 context 接近上限時，自動將當前對話歷史壓縮成可續跑的摘要。
- 壓縮後以摘要取代舊歷史，讓 Agent 繼續工作，而不是保留無限制增長的完整歷史。
- 不同 Agent 可使用不同的 summary prompt。
- 若多個 Agent 的工作型態足夠接近，也允許共用同一份 summary prompt。

## 核心原則

- 本設計優先處理 in-run context compaction，而不是長期記憶。
- compaction 的目的是讓 Agent 可以繼續完成工作，不是保留完整聊天紀錄。
- compaction 後必須保留足夠上下文，避免：
  - 重複做已做過的事情
  - 遺失 verifier feedback
  - 遺失已看過 / 未看過的範圍資訊
  - 遺失下一步應做的事情
- compaction 機制本身應統一，不因 Agent 類型而改變。
- summary prompt 可依 Agent 類型調整，但應盡量共用，以降低維護成本。

## 採用方向

- 完整採用 Claude Code 類型機制：
  - 持續追蹤 context usage
  - 超過門檻時觸發 compaction
  - 用專門的 summary prompt 產生摘要
  - 以摘要取代舊歷史
  - 在壓縮後繼續同一個 Agent run
- 不採第一版就導入複雜的多層 long-term memory provider。
- 若未來需要長期記憶，可在此機制之外額外擴充。

## 機制總覽

### 1. Context usage 監控

- 系統應持續追蹤當前 Agent run 的 context usage。
- 至少應能取得或估算：
  - context window size
  - current input tokens
  - current output tokens
  - used percentage
  - remaining percentage

### 2. Compaction trigger

- 當 context 使用量超過設定門檻時，系統應觸發 compaction。
- 第一版應比照 Claude Code 類型機制，以 token usage 為主要 trigger。
- 第一版應至少支援：
  - `Enabled`
  - `ContextTokenThreshold`
  - `Model`
  - `SummaryPrompt`
- `ContextTokenThreshold` 代表當前 context token 使用量超過此值時，應進入 compaction。
- token 使用量的估算應優先採用模型或 SDK 實際回傳的 usage 資訊。
- 若供應商無法提供完整 usage，系統可使用統一估算器，但此屬實作細節。
- 第一版不要求每個 Agent 有不同 trigger 機制。
- 第一版允許每個 Agent 使用相同機制，但門檻值可依 Agent 類型或模型能力設定。

### 3. Summary generation

- 觸發 compaction 後，系統應發起一次專門的 summary model call。
- 這次 model call 的目的不是解原任務，而是生成可續跑的摘要。
- 該摘要必須遵守對應 Agent 的 summary prompt。
- 這次 summary model call 應使用當前 Agent 的既有歷史作為輸入基礎。
- 但它的最後一個使用者意圖不再是原任務，而是 summary prompt。
- 若當前歷史尾端只有純工具呼叫且沒有有效 assistant 內容，系統應先做必要清理，再進入 summary call。

### 4. History replacement

- 當摘要生成成功後，系統應以摘要取代既有對話歷史。
- 壓縮後的歷史應成為新的工作起點。
- 之後同一個 Agent run 繼續在此基礎上運作。
- 第一版應比照 Claude Code 類型機制，直接以單一 summary checkpoint 取代既有歷史，而不是保留大量舊訊息。
- 第一版建議壓縮後歷史形式為一筆 summary message。
- 該 summary message 應視為新的工作基底，供後續同一個 Agent run 繼續累積新歷史。

### 5. Continue execution

- compaction 完成後，Agent 不應被視為完成。
- Agent 應繼續原本任務，並沿用：
  - 原本的 system prompt
  - 原本的固定輸入
  - 壓縮後的新摘要歷史
- compaction 不應重設工具狀態。
- compaction 不應清除程式層維護的結構化狀態。
- compaction 只壓縮模型對話歷史，不改變 workflow 狀態本身。

## Claude Code 對齊規則

- 第一版應完整採用 Claude Code 類型流程：
  1. 持續追蹤 context usage
  2. 超過 token threshold
  3. 發起 summary model call
  4. 生成 operational summary
  5. 用 summary 取代既有歷史
  6. 在同一個 Agent run 中繼續執行
- 第一版不採 pipeline compaction strategy。
- 第一版也不採多階段 message reduction 組合。
- 第一版優先追求與 Claude Code 類型機制一致，而不是與 framework sample 的多策略 pipeline 一致。

## 失敗處理與 Fallback

### Summary generation 失敗

- 若 summary model call 失敗，系統不應直接丟失既有歷史。
- 若 summary 失敗，應保留原始歷史與原始狀態。
- 第一版應比照 Claude Code 類型機制：
  - 不在同一個 Agent run 內啟動第二套 compaction fallback 流程
  - 不在同一個 Agent run 內改用另一份 summary prompt
  - 不在同一個 Agent run 內切換成別種 compaction strategy
- 若 summary generation 發生例外，該次 compaction 視為失敗，當前 Agent run 應直接進入 error 狀態。

### Compaction failed 後的處理

- 第一版應比照 Claude Code 類型行為：
  - 標記當前 Agent run 為 `compaction failed`
  - 停止該次 Agent run
  - 將失敗原因保留到可觀測性紀錄
  - 將控制權交回外層 workflow
- 外層 workflow 可再決定是否：
  - 重啟新的 Agent run
  - 進入人工檢查
  - 採用未來版本才支援的降級策略
- 但這些都不屬於同一個 Agent run 內的 compaction fallback。

### Summary validation

- 第一版建議在 summary 成功後做最基本驗證：
  - 是否為非空內容
  - 是否符合要求的 summary 包裝格式
  - 是否明顯包含 next steps / current objective / critical context
- 若未通過基本驗證，視為 summary 失敗。

## 狀態邊界

- 以下內容不應被 compaction 替換：
  - 工具層儲存的 issue / verdict / task item / report state
  - workflow node 狀態
  - retry / rejection 計數
  - 可觀測性與審計紀錄
- 以下內容應由 compaction 壓縮：
  - 模型自然語言對話歷史
  - 工具呼叫前後產生的大量推理文字
  - 已不需要逐字保留的舊輪次內容

## Workflow Routing

- 若 compaction 沒有被觸發，當前 Agent run 照常繼續。
- 若 compaction 被觸發且成功，系統以 summary 取代歷史，當前 Agent run 照常繼續。
- 若 compaction 被觸發但失敗，當前 Agent run 直接失敗並結束。
- 第一版不在 Agent 內部設計 compaction failure recovery loop。
- 第一版的 recovery 應由外層 workflow 處理，而不是由同一個 Agent run 自行補救。

## Summary 的角色

- summary 不是給使用者看的報告。
- summary 是給同一個 Agent 自己續跑用的 operational checkpoint。
- 它必須保留：
  - 已完成的工作
  - 已嘗試但無效的方法
  - 目前已知的重要事實
  - verifier 或 system-controlled user input 的修正要求
  - 下一步應該執行的動作
  - 不能遺失的上下文

## Summary prompt 設計原則

- summary prompt 應明確要求保留可恢復工作所需資訊。
- summary prompt 應偏向 operational，而不是敘事型摘要。
- summary prompt 不應產生大量無用自然語言。
- summary prompt 應優先輸出：
  - 已做過什麼
  - 尚未做什麼
  - 接下來要做什麼
  - 哪些上下文不能丟

## Prompt 共用策略

- 不同 Agent 可以有不同 summary prompt。
- 但如果多個 Agent 的工作型態相近，應優先共用同一份 summary prompt。
- 第一版建議至少考慮以下共用群組：
  - 掃描 / 規劃型 Agent
  - review / verifier 型 Agent
  - aggregation / report verifier 型 Agent
- 若共用 prompt 會讓保留資訊變得模糊，再拆成獨立 prompt。

## 第一版分組

### 1. Scan / Scan Verifier

- 適用：
  - `Scan Agent`
  - `Scan Verifier Agent`
- 應保留：
  - 已掃描到的 project units
  - 已檢查過的 repo 區域
  - 尚未確認但可疑的區域
  - verifier 的修正要求
  - 下一步應補掃的位置

### 2. Project Plan / Project Verifier

- 適用：
  - `Project Plan Agent`
  - `Project Verifier Agent`
- 應保留：
  - 當前 `ScanProject`
  - 已建立的 task item 方向
  - 已納入的檔案
  - 尚未納入但可能需要納入的檔案
  - task item 切分原則
  - verifier 的調整要求

### 3. Rule Review / Review Verifier

- 適用：
  - `Rule Review Agent`
  - `Review Verifier Agent`
- 第一版建議共用同一份 summary prompt。
- 應保留：
  - 當前 rule
  - scope entry files
  - 已檢查的檔案
  - follow-up files
  - 已建立的 issues 或 no-issue 狀態
  - verifier feedback
  - 尚缺的證據、覆蓋或交叉檢查

### 4. Report Aggregator / Report Verifier

- 適用：
  - `Report Aggregator`
  - `Report Verifier`
- 應保留：
  - current flow issues
  - repo-level issue set 的整合方向
  - 本輪 merge / dedupe 的主要判斷
  - verifier 打回原因
  - 下一步應補的整合或修正

## 與現有 Agent 設計的關係

- 此機制適用於所有會長時間運作、可能累積大量歷史的 Agent。
- 對 CodeSnifferDog 而言，較可能需要 compaction 的包含：
  - `Project Plan Agent`
  - `Rule Review Agent`
  - `Review Verifier Agent`
  - `Report Aggregator`
  - `Report Verifier`
- `Scan Agent` 與 `Scan Verifier Agent` 也可支援此機制，但通常壓力較低。

## 第一版先不展開的內容

- 長期記憶 provider 的實作選型
- 多模型 compaction routing
- compaction prompt 的實際 wording
- 是否保留 compaction 前完整 transcript 的外部審計儲存方式
- 不同模型供應商下 token usage 的統一估算實作

## 後續文件

- 後續應再補一份 summary prompt 設計文件。
- 該文件應定義：
  - 哪些 Agent 共用 prompt
  - 哪些 Agent 使用獨立 prompt
  - 每份 prompt 的 placeholder
  - 每份 prompt 必須保留的資訊欄位

## 變更紀錄

- 2026-04-14：建立記憶與 context compaction 設計初稿，採用 Claude Code 類型的完整 compaction 機制。
- 2026-04-14：補充第一版 summary prompt 分組，先收斂為 4 份共用 prompt。
