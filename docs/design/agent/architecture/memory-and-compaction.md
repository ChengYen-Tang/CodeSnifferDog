# Memory And Compaction

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 Agent 的記憶與 context compaction 機制
- 文件狀態：草稿
- 最後更新：2026-04-14

## 設計目標

- 第一版以 Claude Code 的 context compaction 設計為對齊目標，但會在 Microsoft Agent Framework 提供的抽象介面內實作對應機制。
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
- `ContextCollapse` 應作為獨立 controller 存在，不應把 collapse orchestration 直接塞進 generic reducer。
- generic reducer 的責任應收斂為：根據輸入 messages 產生 compact result。
- provider / workflow middleware 的責任應收斂為：組 pre-call pipeline、決定何時呼叫對應 controller、以及在呼叫成功或失敗後交由 controller 決定 staged span 的命運。

## 採用方向

- 第一版的整體方向仍對齊 Claude Code 類型機制：
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
- 自動 compaction trigger 應比照 Claude Code，對「當前 outbound messages」做 pre-call token estimation，再決定是否壓縮。
- provider / framework 回傳的 usage 仍應保留，用於觀測與執行紀錄，但 pre-call compaction trigger 不依賴它。
- `ModelContextWindowTokens` 不屬於 provider runtime usage 的一部分，而是由外部執行設定提供。

### 1.1 Message shrinking

- 在進入 full compaction 前，系統應先支援 Claude Code 設計意圖對應的 message shrinking。
- 第一版至少包含兩層：
  - `microcompact`
  - `snip`
- 這兩層的責任不是生成 summary，而是先縮小大體積工具訊息造成的 context 壓力。
- `microcompact` 的第一版定位為 framework-level shrinking，不直接宣稱等價於 Claude Code 在 provider / API layer 上的 prompt cache edit 路徑。
- 第一版只針對高體積通用工具結果啟用：
  - `RunShellCommand`
  - `RunRipgrepCommand`
- 第一版不對 review / report CRUD 類工具結果做 shrinking，避免破壞 verifier 與後續 flow 需要的結構化上下文。

### 1.2 Compaction modes

- 第一版應明確區分 context management mode，而不是只靠零散布林值。
- 至少支援以下 modes：
  - `Standard`
  - `ReactiveOnly`
  - `ContextCollapse`
- `Standard`
  - 允許 proactive automatic compaction
  - 允許 microcompact
  - 允許 snip
- `ReactiveOnly`
  - 禁止 proactive automatic compaction
  - 保留 reactive compaction retry
  - 允許 microcompact 與 snip
- `ContextCollapse`
  - 禁止 proactive automatic compaction
  - 保留 outer-layer session state，用來記錄 staged span、commit log 與 projection snapshot
  - pre-call pipeline 仍先套用 `snip` 與 `microcompact`，再進入 collapse controller
  - `ContextCollapse` 應有自己的 proactive collapse trigger 與 retry chain，不應只是一般 reactive compaction 的附屬行為
  - collapse recovery 仍沿用 full compaction summary，但結果不再只是一筆摘要紀錄，而是會先進 staged span，再在 retry 成功後轉成 committed span

### 2. Compaction trigger

- 當 context 使用量超過設定門檻時，系統應觸發 compaction。
- 第一版應比照 Claude Code 類型機制，以 pre-call token estimation 為主要 trigger。
- 第一版的 worker 顯式輸入應只保留：
  - `ModelContextWindowTokens`
- 第一版不應要求使用者直接設定 `ContextTokenThreshold`。
- `ModelContextWindowTokens` 應由外部設定或 worker 初始化參數明確提供。
- 第一版不依賴 provider runtime metadata 動態提供 model context window。
- 第一版使用 Claude Code 類型常數：
  - `SummaryReservedOutputTokens = 20_000`
  - `AutoCompactBufferTokens = 13_000`
- `ContextCollapse` 另有自己的百分比門檻：
  - `CollapseProactiveThresholdPercentage = 90`
  - `CollapseBlockingThresholdPercentage = 95`
- 自動 compaction 需支援 suppression：
  - reactive-only 模式時禁止 proactive automatic compaction
  - context-collapse 類型模式啟用時禁止 proactive automatic compaction
  - 連續自動 compaction 失敗達上限時，需打開 circuit breaker，停止後續 automatic attempt
- `microcompact` 應在 automatic compaction trigger 判斷前先執行
- `snip` 應優先作為 reactive retry 前的訊息縮減手段，而不是取代 full compaction summary
- `ContextCollapse` 的 proactive 判斷也應基於 pre-call token estimation，而不是等 provider 回報 overflow 後才開始做第一步處理

### 2.1 Worker 顯式輸入與內部推導

- `RepositoryRootPath` 與 review rule definitions 屬於分析目標，應在 worker 建立時綁定。
- `ModelContextWindowTokens` 屬於 compaction 與執行策略相關輸入。
- 第一版正式對外暴露的 compaction 相關設定，應透過 worker 的 `ExecutionOptions` DTO 提供，而不是獨立散落。
- 第一版 `ExecutionOptions` 至少應包含：
  - `MaxParallelAgents`
  - `ModelContextWindowTokens`
  - `ContextCompactionMode`
- `ModelContextWindowTokens` 第一版預設值為 `128_000`。
- 其他 compaction 參數應維持內部預設值或由程式邏輯推導，不直接暴露給一般使用者。
- 第一版的 compaction 相關內部推導值應至少包含：
  - `SummaryReservedOutputTokens`
  - `AutoCompactBufferTokens`
  - `CompactionTriggerThreshold`
- 共同公式為：
  - `CompactionTriggerThreshold = (ModelContextWindowTokens - SummaryReservedOutputTokens) - AutoCompactBufferTokens`

### 3. Summary generation

- 觸發 compaction 後，系統應發起一次專門的 summary model call。
- 這次 model call 的目的不是解原任務，而是生成可續跑的摘要。
- 該摘要必須遵守對應 Agent 的 summary prompt。
- 這次 summary model call 應使用當前 Agent 的既有歷史作為輸入基礎。
- 但它的最後一個使用者意圖不再是原任務，而是 summary prompt。
- 若當前歷史尾端只有純工具呼叫且沒有有效 assistant 內容，系統應先做必要清理，再進入 summary call。
- 若 reactive retry 前已先套用 `snip`，則 summary generation 應以 snipped 後的 messages 為輸入。

### 3.1 Microcompact

- `microcompact` 應優先處理高體積工具結果，而不是自然語言訊息。
- Claude Code 的 `microcompact` 主路徑會優先利用 provider / API layer 的 cache-edit 類能力；第一版在 Microsoft Agent Framework 抽象層內無法假設這類能力存在。
- 因此第一版的 `microcompact` 採 framework-level 替代實作：
  - 保留最近少量工具結果
  - 保留 tool call、message identity 與關鍵 metadata
  - 將較舊的 compactable tool result 轉成可追溯的 compact artifact，而不是假裝仍是原始結果
- `microcompact` 的目的是減少 tool result 內容壓力，同時保留後續 continuity、collapse 與 agent 推理仍需要的追蹤資訊，而不是刪除整段工作歷史。
- 第一版不宣稱 `microcompact` 與 Claude Code 的 API-layer prompt cache edit 路徑完全等價；這是受 Microsoft Agent Framework 抽象介面限制下的對應設計。

### 3.2 Snip

- `snip` 應比 `microcompact` 更強。
- 第一版採保守策略：
  - 只刪除較舊的 compactable tool call / tool result 訊息
  - 保留最近少量工具結果
  - 插入一筆 snip boundary message，說明曾發生訊息裁切
- `snip` 不應直接移除一般使用者 / assistant 自然語言歷史。
- 第一版 `snip` 先只作為 reactive retry 前的縮減手段。

### 3.3 Context collapse outer state

- `context collapse` 不能只是一個布林開關，還必須留下 session 層可觀測狀態。
- session state 至少應記錄：
  - `StagedSpans`
  - `LastCollapseReason`
  - `Commits`
- `Snapshot` 至少應記錄：
  - `ProjectedCollapseIds`
  - `LastCommittedCollapseId`
  - `LastStagedCollapseId`
- 每一筆 committed collapse span 應至少保留：
  - `CollapseId`
  - `Summary`
  - `Reason`
  - `FirstArchivedMessageIndex`
  - `FirstArchivedMessageId`
  - `LastArchivedMessageIndex`
  - `LastArchivedMessageId`
  - `ArchivedMessagesCount`
- 每一筆 staged collapse span 應保留和 committed span 相同的核心邊界資訊，差別只在於它還沒有正式進入 commit log。

### 4. History replacement

- 當摘要生成成功後，系統應以摘要取代既有對話歷史。
- 壓縮後的歷史應成為新的工作起點。
- 之後同一個 Agent run 繼續在此基礎上運作。
- 第一版應比照 Claude Code 類型機制，使用：
  - compact boundary message
  - summary checkpoint message
  - preserved recent tail
  - post-compact continuity artifacts
- post-compact continuity artifacts 第一版至少應支援：
  - attachment 類型訊息 reinjection
  - hook-result 類型訊息 reinjection
  - 結構化 continuity-state artifact
- reinjection 應受 token budget 限制，避免壓縮後立刻重新塞爆 context。
- continuity-state artifact 不應只是重複 summary 原文，而應至少重建：
  - `CurrentObjective`
  - `CompletedWork`
  - `NextSteps`
  - `CriticalContext`
- 若進入 `ContextCollapse` projection，除了 collapsed summary message，也應同步投影 continuity-state artifact，讓下一輪仍有結構化續跑資訊。
- `ContextCollapse` projection 應 replay 全部 committed collapse log，而不是只投影最後幾筆 commit。

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

- 第一版應盡量對齊 Claude Code 類型流程：
  1. 持續追蹤 context usage
  2. 超過 token threshold
  3. 發起 summary model call
  4. 生成 operational summary
  5. 用 summary 取代既有歷史
  6. 在同一個 Agent run 中繼續執行
- 第一版不採 pipeline compaction strategy。
- 第一版也不採多階段 message reduction 組合。
- 第一版優先追求與 Claude Code 類型機制一致，而不是與 framework sample 的多策略 pipeline 一致。
- 但 `microcompact` 層只要求對齊 Claude Code 的設計意圖，不要求複製 Anthropic / Claude 自家 provider contract 上的 API-layer 行為。

## 已知不對齊

- 第一版已明確接受一項和 Claude Code 的已知差異：
  - `microcompact` 不是 API-layer cache edit 路徑
  - 它是在 Microsoft Agent Framework 抽象層內，直接對 local messages 做 framework-level shrinking
- 這個差異是目前技術邊界下的刻意設計，不視為實作遺漏。

## 失敗處理與 Fallback

### Summary generation 失敗

- 若 summary model call 失敗，系統不應直接丟失既有歷史。
- 若 summary 失敗，應保留原始歷史與原始狀態。
- automatic compaction failure 應比照 Claude Code：
  - 保留原始歷史
  - 不讓當前 turn 直接失敗
  - 累計 consecutive automatic failures
  - 達到上限後打開 circuit breaker，停止後續 automatic attempt
- reactive compaction failure 可讓原本的 invocation failure 照常往外拋出。

### Compaction failed 後的處理

- 自動 compaction 失敗不應在同一 turn 內切換到第二套壓縮策略。
- 第一版已在 summary compaction 之外導入 `microcompact` 與 `snip`，但它們的角色仍是前置 shrinking，而不是第二套 summary strategy。
- 若未來 Microsoft Agent Framework 暴露更低階的 request 擴充點，或系統改為直接控制 provider request payload，再評估是否補齊更接近 Claude Code 的 `microcompact` 主路徑。

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
- 若 automatic compaction 被觸發但失敗，當前 Agent run 應保留原始歷史並繼續，且更新 automatic failure state。
- 若 reactive compaction 被觸發但失敗，則沿用原本 invocation failure routing。
- 若 `microcompact` 有效，則以 microcompacted messages 繼續後續 automatic compaction 判斷。
- 若 reactive retry 前 `snip` 有效，則以 snipped messages 進入 reactive full compaction。
- 若 mode 為 `ContextCollapse`，則 reactive recovery 成功後應同步更新 collapse session snapshot。
- `ContextCollapse` 的一般進入路由應為：
  1. 先套用既有 committed span 的 projected view
  2. 若 pre-call estimation 超過 collapse proactive threshold，先產生一筆 staged collapse span
  3. 若仍低於 collapse blocking threshold，當前呼叫仍沿用 projected view；新 staged span 先不進入 live request
  4. 若已達 collapse blocking threshold，先提交本輪 staged span，再用 committed projection 作為當前呼叫輸入
- `ContextCollapse` 的 reactive retry 路由應為：
  1. 產生 collapse compaction result
  2. 將 archived span 先寫入 staged state
  3. 在 retry 前先提交本輪 staged span
  4. 用 committed projection 進行 retry
  5. 下一個 turn 由 projected collapsed view 取代對應的舊 span
- 若 proactive collapse 後的第一次送出仍然遇到 blocking 類型 overflow，應先提交本輪新 staged span，再進入更深一層的 collapse-owned reactive retry，而不是直接退回標準模式

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
- 不同模型供應商下 context window metadata 的統一取得方式

## 後續文件

- 後續應再補一份 summary prompt 設計文件。
- 該文件應定義：
  - 哪些 Agent 共用 prompt
  - 哪些 Agent 使用獨立 prompt
  - 每份 prompt 的 placeholder
  - 每份 prompt 必須保留的資訊欄位

## 變更紀錄

- 2026-04-14：建立記憶與 context compaction 設計初稿，以 Claude Code 類型機制作為主要對齊方向。
- 2026-04-14：補充第一版 summary prompt 分組，先收斂為 4 份共用 prompt。
- 2026-04-21：明確收斂 `microcompact` 為 Microsoft Agent Framework 抽象層內的替代設計，不再宣稱完全複製 Claude Code 的 API-layer 路徑。
