# Agent Design

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 Code Review Agent 的角色分工、互動模式、Prompt 組裝方式與多 Agent 協作設計
- 文件狀態：草稿
- 最後更新：2026-04-19

## 設計目標

- 建立一個面向大型專案的全自動化 Code Review Agent。
- 支援 CLI 與 Server Mode 兩種使用方式。
- 以固定的 Prompt 框架搭配可替換的 Markdown 規則文件驅動分析流程。
- 支援多規則並行執行，並在共享 worker budget 下產生各規則對應的獨立報告。
- 在高度自動化前提下保留足夠的可觀測性，讓使用者可追蹤 Agent 行為與執行進度。

## 與需求文件關聯

本文件以需求文件為基礎，對應內容請參考 [Z:\GitHub\CodeSnifferDog\docs\requirements.md](Z:\GitHub\CodeSnifferDog\docs\requirements.md)。

## 設計範圍

本文件將逐步定義以下內容：

- 主 Agent 與 Sub-Agent 的角色分工
- 規則文件如何載入並套用到固定 Prompt 框架
- 多 Agent 之間如何拆分任務、交換資訊與回報結果
- 各類 Agent 的輸入、輸出與生命週期
- 報告產出方式
- Agent 可觀測性與行為軌跡設計

本文件不處理以下主題，這些屬於其他設計文件：

- Server worker / queue / project lifecycle
- 資料庫 schema 與保存策略
- API 設計
- UI/UX 設計
- 部署與設定檔搜尋策略

## 目前已知 Agent 前提

- 系統存在固定的 Prompt 框架，使用者不直接修改核心 Prompt。
- 規則文件以 Markdown 為主，第一版優先採簡單條列式內容。
- 系統會掃描 `rules/` 目錄中的規則文件，自動決定要啟動哪些 Review 任務。
- 每個規則文件可對應一個獨立的 Sub-Agent 與一份獨立報告。
- CLI 輸入為專案目錄，Server Mode 輸入為 ZIP 專案。

## 參考資料

若需查看外部案例的設計哲學整理，請參考 [Z:\GitHub\CodeSnifferDog\docs\design\agent\references\reference-design-philosophy.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\references\reference-design-philosophy.md)。

## 初步設計方向

大型專案往往包含大量 projects、模組、檔案與跨檔案相依性。如果讓單一 Review Agent 直接面對整個 repo，容易出現上下文過大、注意力分散、重複閱讀與覆蓋不穩定等問題。

因此目前的方向是把前置準備拆成多個明確階段，而不是依賴單一大型 Planning Agent：

- 先由 `Scan Agent` 在 project 層級建立專案清單
- 再由 `Project Plan Agent` 針對單一 project 建立起始 scopes / plan items
- 最後由各規則的 `Rule Review Agent` 以 scope 為入口執行審查

在這個模型中：

- `scope` 是檢查入口，不是推理邊界
- `Project Plan Agent` 不負責預先看懂整個 solution 的所有語意關聯
- 跨 scope、跨檔案、跨模組的追蹤能力屬於後續 `Rule Review Agent` 的責任

這個方向的目的不是在前置階段一次切出完美的 review plan，而是：

- 建立足夠穩定的 coverage 起點
- 讓後續 rule review 有可分派的工作單位
- 避免單一 Agent 承擔過大的全域理解負擔

## 大架構

### 核心原則

- Agent 負責推理、分析、驗證與報告。
- 流程編排由程式邏輯負責，不額外引入 Coordinator Agent。
- 每個生成階段原則上都對應一個驗證階段，不直接信任單一 Agent 的輸出。
- `scope` 是檢查入口，不是推理邊界。
- `plan item` 的具體結構留待後續討論，本階段只先定義它是可被程式編排邏輯消費的結構化結果。
- 通用分析工具第一版只保留 `Shell` 與 `grep search` 兩類，避免過早擴張工具面。

### 架構優化原則

以下原則參考 Claude Code 的設計思路，但已依 CodeSnifferDog 的高度自動化場景調整：

#### 1. 把 Agent 做成強角色隔離

- 不同 Agent 應有非常明確的角色邊界，不盡量混用職責。
- `Scan Agent` 專注掃描。
- `Project Plan Agent` 專注規劃。
- `Rule Review Agent` 專注依規則審查。
- `Verifier` 專注驗證，不負責重寫整個前一階段成果。

#### 2. Scan Agent 可以更像 Explore Agent

- `Scan Agent` 應偏向快速探索型 Agent。
- 它的目標是快速建立專案清單與結構 inventory，而不是深入理解程式語意。
- 這個階段應追求快、便宜、可預期。

#### 3. Project Plan Agent 應輸出很精簡

- `Project Plan Agent` 不應輸出冗長的自然語言計畫。
- 它應透過工具插入精簡且結構化的 `plan items`，供程式邏輯直接消費。
- 它的目標是建立後續 review 的起始工作單位，而不是產出完整設計報告。

#### 4. Verifier 要有非常明確的 verdict 格式

- 各類 `Verifier Agent` 不應只輸出模糊評論。
- 驗證結果應透過明確 verdict 提交，方便程式邏輯決定下一步。
- 具體 verdict 欄位與格式留待後續定義，但方向上應支援明確流轉，例如通過、補查、退回或重做。

#### 5. Verifier 不一定每層都做同強度

- 所有驗證階段都存在，但不代表所有 verifier 的檢查深度都相同。
- `Scan Verifier` 可偏輕量。
- `Project Verifier` 可中等強度。
- `Review Verifier` 與 `Report Verifier` 可較高強度。
- 驗證強度的具體定義留待後續的 Prompt / Behavior Design。

#### 6. 可以考慮引入快 Agent / 深 Agent 分層

- 不同階段的 Agent 不必使用相同深度與成本設定。
- `Scan Agent` 適合偏快、偏便宜的設定。
- `Project Plan Agent` 可採中等深度。
- `Rule Review Agent`、`Review Verifier`、`Report Verifier` 可採較深的分析模式。
- 這種分層有助於降低整體成本，同時保留關鍵階段的分析品質。

### Agent 階段

#### 1. Scan 階段

- `Scan Agent`
- `Scan Verifier Agent`

職責：

- 掃描 solution / repo，找出有哪些 projects
- 驗證掃描結果是否足以覆蓋專案的 project 層級結構
- `Scan Agent` 使用固定的 system prompt
- 每次執行時，由程式邏輯以 system-controlled user input 注入固定 prefix 與 repo path
- 固定 prefix 第一版為：
  `The following path is the repository root to scan for projects. Identify the project units that should enter the next planning stage.`
- `Scan Verifier Agent` 使用固定的 system prompt
- 透過 prompt 佔位符接收固定的 `Repository root path`
- 每次驗證時，由程式邏輯以 system-controlled user input 注入固定 prefix 與目前的 `ListScanProjects` 結果
- 固定 prefix 第一版為：
  `The following content is the current scan result from the Scan Agent. Approve it if acceptable. Reject it if more work is required, and explain why.`

### 2. Project Planning 階段

- `Project Plan Agent`
- `Project Verifier Agent`

職責：

- 每個 `ScanProject` 由一個 `Project Plan Agent` 負責
- 針對單一 project 建立 review 用的起始 scopes / plan items
- 驗證 project plan 是否具備足夠覆蓋性與合理性
- `Project Plan Agent` 由 scan 結果 foreach 啟動
- 多個 `Project Plan Agent` 可以平行啟動
- 實際平行數量受系統設定限制
- `Project Plan Agent` 使用固定的 system prompt
- 透過 prompt 佔位符接收固定的 `Repository root path`
- 每次執行時，由程式邏輯以 system-controlled user input 注入固定 prefix 與當前 `ScanProject`
- 固定 prefix 第一版為：
  `The following content is the scan project to plan. Create task items that should enter the next review stage.`
- `Project Plan Agent` 應避免切出過大的 `task item`
- 第一版建議同時控制：
  - 單一 `task item` 的檔案數上限
  - 單一 `task item` 的總行數上限
- 建議第一版預設值為：
  - `MaxFilesPerTaskItem = 10`
  - `MaxTotalLinesPerTaskItem = 2000`
- 若單一檔案本身已超過總行數上限，則允許該檔案單獨成為一個 `task item`
- 對 C/C++ 類型專案，若 header 與 implementation 檔案明顯成對，應優先保留在同一個 `task item`
- 這條成對規則優先於一般的檔案數與總行數上限
- `Project Verifier Agent` 使用固定的 system prompt
- 透過 prompt 佔位符接收固定的 `Repository root path` 與 `ScanProject`
- 每次驗證時，由程式邏輯以 system-controlled user input 注入固定 prefix 與目前的 `ListProjectPlanTaskItems` 結果
- 固定 prefix 第一版為：
  `The following content is the current project plan result from the Project Plan Agent. Approve it if acceptable. Reject it if more work is required, and explain why.`

### 3. Rule Review 階段

此階段的主要調度單位不是 `task item` 本身，也不是把單一 `task item` 直接展開成一批同時啟動的 flows，而是：

- 每條 `rule` 一個獨立的 execution lane
- 每條 lane 各自維護自己的 work queue

當 `Project Plan Agent` 產生 `task items` 後，程式邏輯會把每個 `task item` 轉成多個 rule-specific work items，並依 `rule` 分派到對應 lane 的 queue。

例如目前存在 4 個規則檔，則單一 `task item` 會被拆成 4 個 work items，分別進入 4 條規則 lane，而不是直接在同一時間建立 4 條 flow tasks 等待執行。

Review 階段的平行原則如下：

- 不同 `rule` 的 lanes 可以平行執行
- 同一條 `rule` 在任一時間最多只允許一條 flow 執行
- scheduler 會在 worker 的可用 budget 內，從可執行的 lanes 中挑選下一條 flow 啟動
- 第一版優先策略為：
  - 該 `rule` 目前沒有 running flow
  - 在可啟動 lanes 中優先挑 queue 剩餘數量最多者
- queue 中等待的是 work item，不是已建立但尚未取得 budget 的 agent tasks

這種設計的目的，是在維持高吞吐量的同時，避免同一條 `rule` 在不同 `task item` 間同時進行 report merge，導致 snapshot 與 repo-level report state 發生同步衝突。

每一條 flow 都包含：

- `Rule Review Agent`
- `Review Verifier Agent`
- `Report Aggregator`
- `Report Verifier`

不過若某條 flow 的最終結果是 `NoIssueConclusion`，且已通過 `Review Verifier Agent`，則該 flow 可直接結束，不進入 `Report Aggregator` 與 `Report Verifier`。

每條 flow 的職責：

- `Rule Review Agent`
  以 plan item / scope 為入口，依單一規則執行審查
- `Review Verifier Agent`
  驗證該規則的 review 結論是否可信，必要時要求補查或退回
- `Report Aggregator`
  將當前 flow 的 verified issues 合併進整個 repo 範圍下同一條 `rule` 的總 report issue 集合，必要時也收納 verifier 未完全認可的降級結果
- `Report Verifier`
  驗證本次聚合差異是否合理，且沒有扭曲、遺漏或過度合併當前 flow 的結果

另外，若 `Rule Review Agent` 在 repeated missing submission 與 reset 後，仍然無法形成任何 issue 或 `NoIssueConclusion`，則這條 flow 不應讓整個外層流程直接失敗。
此時應以 degraded state 結束該 flow，保留原因與執行紀錄，並由外層 orchestration 繼續調度其他 lanes 的 work items。

### Task Item 與 Review Group 關係

- `task item` 由 `Project Plan Agent` 產生。
- 每個 `task item` 以一組 files 作為 scope 的進入點。
- `review group` 保留作為與單一 `task item` 對應的邏輯分組與可觀測性容器。
- `review group` 的責任是追蹤該 `task item` 在所有規則 lanes 下對應的 rule flows 與最終結果。
- `review group` 不是主要的平行調度容器；實際調度由 rule lanes 與 scheduler 負責。
- 容器結束的條件，是該 `task item` 在所有規則 lanes 下對應的 rule flows 都已結束其生命週期。
- flow 的結束可分為 approved completion 或 degraded completion，不要求每條 flow 都通過最終驗證。
- 每一條 rule flow 都有自己獨立的 issue state、no-issue state 與 verdict state。
- 可平行的是不同 `rule` lanes 下的 flow 執行，不是同一條 `rule` 的多條 flow 共用同一份 review state。
- 同一條 flow 內的 reviewer 與 verifier 會沿用同一份垂直狀態。
- flow 完成後，該 flow 內建立的 agents 與暫態 state 都應被清理；只保留 repo-level report snapshot、report issue state 與必要的可觀測性紀錄。

### Rule Lane 與 Scheduler

- 每條 `rule` 都有自己的 execution lane。
- 每條 lane 具有獨立 queue，用來收納不同 `task item` 對應到該 `rule` 的待執行 work items。
- 同一條 lane 在任一時間最多只允許一條 running flow。
- 不同 lanes 可在 worker budget 允許下同時執行。
- scheduler 負責：
  - 將 `task item` 轉成多個 rule-specific work items
  - 將 work items 放入對應 lane 的 queue
  - 在 worker 的 `max parallel agents` 限制內挑選下一條可執行 flow
  - 只在真正要開始執行時才建立該 flow 對應的 agents
  - 避免同一條 `rule` 在不同 `task item` 間同時進入 report merge
- 第一版 `Review Agent Team` 應持有唯一的共享 concurrency budget，所有 lanes 都必須共用這個 budget，而不是各 workflow 自己維護獨立平行參數。

### Team / Worker 封裝邊界

- 外部組裝程式不應直接 new `RepositoryPreparationWorkflow`、`ReviewStageWorkflow`、`ReviewGroupWorkflow` 或 scheduler。
- 外部應先建立 `ReviewAgentTeam` 的組裝入口，提供 `Scan`、`Project Plan`、`Rule Flow` 對應的 workflow runners。
- 每次 project / repo 分析開始時，再由該 team 建立一個 `Worker` 實例。
- `Worker` 建立時就應綁定這次分析的 `repository root path` 與 `rule markdowns`。
- `Worker` 也應接收一個 `ExecutionOptions` DTO，作為少量明確的執行策略輸入。
- 第一版 `ExecutionOptions` 至少應包含：
  - `MaxParallelAgents`
  - `ModelContextWindowTokens`
  - `ContextCompactionMode`
- `Worker` 持有該次分析專屬的共享 concurrency budget，並透過單一 `AnalyzeAsync` 入口負責串起 preparation 與 review stage。
- `Worker` 應提供明確的 cleanup 邊界，用來釋放 team surface 持有的 runtime 資源，而不只依賴底層 workflow 的間接清理。
- `Worker` 結束後，flow 相關的暫態狀態與 agents 應隨之清理；repo-level report snapshot 則依既有規則保留。

### Rule Flow 生命週期

單一 rule flow 的高層生命週期如下：

1. `Rule Review Agent`
2. `Review Verifier Agent`
3. `Report Aggregator`
4. `Report Verifier`

#### 1. Rule Review Agent

- 以 `task item` 的 scope files 作為入口進行審查
- 必要時可跨 scope 延伸檢查相依性
- 完成後透過 submit 類型工具提交 review 結果

#### 2. Review Verifier Agent

- 檢查 review 結果的證據鏈是否完整
- 檢查結論是否合理
- 檢查 scope 內應檢查內容是否已有覆蓋
- 檢查必要的跨 scope 相依性是否已被看清楚
- 檢查提交格式是否正確
- 使用固定的 system prompt
- 透過 prompt 佔位符接收固定的 `Repository root path`、`Rule definition` 與 `Scope entry files`
- 每次驗證時，由程式邏輯以 system-controlled user input 注入固定 prefix 與當前 review result
- 固定 prefix 第一版為：
  `The following content is the current review result from the Rule Review Agent. Approve it if acceptable. Reject it if more work is required, and explain why.`

Review Verifier Agent 應透過單一 verdict 工具明確決定：

- `Approved = true`
- `Approved = false`

若為 `Approved = false`，必須附帶可直接回送給 `Rule Review Agent` 的明確原因字串。

第一版不引入額外的 adjudicator。
因此 `Rule Review Agent` 與 `Review Verifier Agent` 的回退循環應有上限。
若達到上限仍未通過，flow 不直接卡死，而是以「verifier 未通過」的狀態進入下一階段，由後續彙整與外部觀察機制保留這個事實。

#### 3. Report Aggregator

- 接收通過 review verification 的 issue-based 結果
- 由系統以 `system-controlled user input` 提供當前 `plan item + rule flow` 的局部 `RuleReviewIssue` 集合
- 維護整個 repo 範圍下同一條 `rule` 的 repo-level `RuleReportIssue` 集合
- 每次 `plan item + rule flow` 開始時，系統都要為這條 flow 建立自己獨立的 working report
- 該 working report 由該 `rule` 的 latest snapshot 複製而來
- `Report Aggregator` 只在這條 flow 自己的 working report 上執行新增、更新、刪除與去重整合
- verifier 檢查的 `RuleReportDiff`，是該 `rule` 的 latest snapshot 與這條 flow 當前 working report 之間的差異

#### 4. Report Verifier

- 檢查本次報告整合後的差異是否合理
- 對照當前 flow 的 `RuleReviewIssue` 集合，確認本次聚合差異沒有遺漏、扭曲或過度推論
- 使用固定的 system prompt
- 透過 prompt 佔位符接收固定的 `Repository root path`、`Rule definition` 與 `Current flow issues`
- 每次驗證時，由程式邏輯以 system-controlled user input 注入固定 prefix 與當前 `RuleReportDiff`
- 固定 prefix 第一版為：
  `The following content is the current report diff from the Report Aggregator. Approve it if acceptable. Reject it if more work is required, and explain why.`

Report Verifier 應透過單一 verdict 工具明確決定：

- `Approved = true`
- `Approved = false`

若回退，必須附帶明確原因。

若 `Report Verifier` 通過，該 rule flow 視為完成。
若達到回退上限後採降級完成，該 flow 也視為完成。
完成時，系統應將目前 working report 升版為該 `rule` 的 latest snapshot，並清理本次 `plan item + rule flow` 的 working report 與 diff 等暫態執行狀態。
清理時應保留 latest snapshot、repo-level report issue state，以及可觀測性或審計所需的執行紀錄。

### 程式編排邏輯

本系統仍然存在流程編排，但這個角色由程式邏輯承擔，而不是由 Coordinator Agent 承擔。

初步上，程式邏輯負責：

- 接收 Scan / Plan 的結構化輸出
- 將 project 與 plan items 轉成可執行任務
- 將 task items 轉成多個 rule-specific work items，並分派進對應的 rule lanes
- 透過 scheduler 在共享 worker budget 內挑選下一條可執行 flow
- 根據 verifier 結果決定 pass、補查、退回或重做
- 管理 review / verifier 回退次數上限，必要時觸發降級前進
- 在每個 task item 的 review group 完成後收斂結果，再進入後續輸出階段
- 在 flow 完成後清理該 flow 內的 agents 與暫態執行狀態

關於 `Rule Review Agent` 到 `Review Verifier Agent` 的具體跳轉規則，請參考 [Z:\GitHub\CodeSnifferDog\docs\design\agent\architecture\execution-pipeline.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\architecture\execution-pipeline.md)。

若 `Rule Review Agent` 在固定補提示下仍反覆無法完成必要提交，系統應採用有限次重試後的 reset / rerun 策略；詳細規則亦定義於 `execution-pipeline.md`。

通用工具設計請參考 [Z:\GitHub\CodeSnifferDog\docs\design\agent\tools\common\common-tools.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\tools\common\common-tools.md)。
記憶與 context compaction 設計請參考 [Z:\GitHub\CodeSnifferDog\docs\design\agent\architecture\memory-and-compaction.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\architecture\memory-and-compaction.md)。

## 待設計主題

- Scan Agent 與 Scan Verifier Agent 的輸出邊界
- Agent 之間的互動拓樸是否固定為單層，或允許多層派生
- Project Plan Agent 與 Project Verifier Agent 的輸出格式
- Prompt 框架組裝流程
- 規則文件解析方式
- 各類 Agent 的輸入、輸出與生命週期
- 報告格式與彙整策略
- Review group 與 rule lane 的生命週期與完成條件
- task item schema 與 scope files 的表達方式
- 各類 verifier 的 verdict schema 與回退規則
- 各階段 agent 的分析深度與成本配置
- Agent 可觀測性資料模型
- 失敗處理、重試與取消在 Agent 層的表現

## 變更紀錄

- 2026-04-11：建立 Agent Design 初稿。
- 2026-04-12：將參考設計哲學拆分到獨立文件。
- 2026-04-12：收斂前置 planning 與多階段 Agent 架構，移除已過時的單一 Planning Agent 表述。
- 2026-04-12：補充 6 項 Agent 架構優化原則，包含角色隔離、Explore 型 Scan、精簡 Plan、明確 Verdict 與快慢 Agent 分層。
- 2026-04-12：補充 task item / review group 關係與單一 rule flow 的生命週期。
- 2026-04-19：將 review 階段的主要調度模型收斂為 rule execution lanes 與 scheduler，移除 review group 作為主要平行調度容器的舊描述。
- 2026-04-19：補充 queue-based scheduling 與 flow 完成後的 agent / state cleanup 原則。
- 2026-04-14：補充通用工具第一版只保留 `Shell` 與 `grep search` 的原則。
- 2026-04-14：補充記憶與 context compaction 以 Claude Code 類型機制作為主要對齊方向的設計連結。
