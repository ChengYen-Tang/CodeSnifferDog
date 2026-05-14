# Agent Status Page Execution Plan

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 Agent Status 頁面的資料載入、即時推送、補接與 UI merge 執行計畫
- 文件狀態：草稿
- 最後更新：2026-05-09

## 文件範圍

- 本文件聚焦 `AgentStatus.razor` 需要的資料流與前後端 contract。
- 本文件不定義最終 UI 視覺細節。
- 本文件不處理 rule/report 等 domain data page，只處理 agent status tree 與 agent timeline。
- 本文件以目前已定型的 `AgentStatus.razor` 模板為前提，討論如何把模板接上真實資料與渲染責任切分。

## 設計目標

- 已完成分析的 project，頁面可從 DB 正確載入完整歷史。
- 正在分析中的 project，頁面可先載入 DB snapshot，再接上即時推送。
- UI 不因晚進場、refresh、reconnect、server restart 而漏資料或重複顯示資料。
- tool timeline 在 DB 端維持單一 projection row，但仍保留 start / complete event 分離的設計彈性。
- 前端 merge 規則必須明確，避免之後用時間戳硬湊造成重複或順序錯亂。

## 核心原則

- DB 是 durable snapshot source，不是 ephemeral live source。
- 即時推送是 snapshot 的 tail，不是另一份獨立 truth。
- 不重新設計 `AgentStatus` UI 模板，只把既有模板接到 snapshot 與 live data。
- UI 必須先有 snapshot merge 規則，才能接 live events。
- 所有 live event 都必須可被 idempotent 套用。
- agent timeline 的穩定排序主鍵應以 `Sequence` 為主，不應依賴 `OccurredAtUtc`。
- tool entry 的關聯主鍵應以 `ToolCallId` 為主，不應依賴 message 文本比對。
- UI 對「分析完成」、「串流斷線」、「目前沒有新事件」三者必須能區分。

## 目前模板觀察

根據目前的 [AgentStatus.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server/Components/Pages/AgentStatus.razor)，頁面模板已固定為以下區塊：

### 1. Page Shell

- 頁面標題 `Agent Status`
- 外層 console layout

此區塊屬於靜態模板，不需要高頻更新。

### 2. Left Roster Pane

- 左側 `Agents` 區塊
- 一個 group 對應一張 `agent-group-card`
- group 內列出多個 agent node
- 每個 agent node 顯示：
  - `Name`
  - `CreatedAt`
  - `Status dot`

目前模板沒有直接把 group name / group status 顯示出來，但資料模型已有 group 層。

### 3. Right History Pane

- 上方 toolbar
- 標題 `History`
- 下方 `agent-chat-log`

目前模板假設右側正在顯示單一 agent 的歷史。

### 4. Timeline Entry Shapes

目前模板支援 4 種 entry shape：

- `Input`
- `Output`
- `Tool`
- `Compaction`

其中：

- `Input` / `Output` 走 chat bubble
- `Tool` 走可展開的 tool summary + detail
- `Compaction` 走單獨 notice row

### 5. Local UI State

目前模板唯一明確的 local UI state 是：

- `_expandedToolDetails`

這是純前端互動狀態，不應由 server live event 驅動。

## 模板區塊與資料責任對照

### A. 靜態模板區塊

以下區塊不需要從 live event 高頻重繪：

- Page title `Agent Status`
- 左右兩欄 layout
- `History` 標題
- tool detail 展開按鈕的基本視覺結構

### B. Snapshot Only Or Snapshot First 區塊

以下區塊應由 snapshot 建立初始畫面：

- group list
- agent list
- 當前 selected agent 的 timeline
- selected agent 已有的 tool expand state 初始 key 集合

### C. Snapshot Plus Live 區塊

以下區塊需要吃 live update：

- 新建立的 group
- 新建立的 agent
- agent status dot
- selected agent timeline 的新增 / 更新 entry
- 若 selected agent 尚未切換，右側 timeline 應持續追加或 upsert

### D. Purely Local UI State

以下 state 應維持在 client local，不進 DB、不走 live event：

- 當前選取哪個 agent
- 哪些 tool card 展開
- 捲動位置
- roster pane 的 hover / focus / selected 樣式

## Rendering Strategy 決策

### 結論

- 目前 `AgentStatus.razor` 使用 `@rendermode InteractiveServer`，這不適合直接承接高頻 agent message live updates。
- 此頁模板可以保留，但高頻資料更新不應依賴 server-side interactive rerender。
- 此頁應改成 client-heavy render model：
  - server 提供 snapshot
  - server 推 live event DTO
  - client 持有 page state 並套用 reducer

### 原因

- agent timeline 是高頻更新區。
- 若每次 input / output / tool / compaction event 都觸發 server-side component diff，server 需要為每個連線維護 render circuit。
- 當同時有數十到數百使用者觀看進行中的 project 時，server render queue、memory 與 SignalR 負載都會被放大。
- 這頁本質更接近 log viewer / dashboard，不適合以 server 持續主導 UI diff。

### 第一版建議切法

- 保留模板結構不變。
- 首次進頁可接受 server 回 snapshot。
- 後續 live updates 改由 client state merge 後更新模板。
- 若未來仍使用 Blazor hybrid render，至少應避免讓 timeline 高頻更新依賴 `InteractiveServer` circuit。

## AgentStatus 模板資料綁定對照

### Left Roster Pane

建議對應資料：

- group card
  - `ProjectAgentGroup`
- agent node
  - `ProjectAgent`

建議綁定欄位：

- `agent.Name`
- `agent.CreatedAtUtc`
- `agent.Status`

備註：

- 模板目前顯示 `CreatedAt` 字串。
- 串真資料時應由 DTO 提供可直接顯示的時間字串，或由 client 做 formatting。

### Right History Pane

建議對應資料：

- `SelectedAgent`
- `SelectedAgentTimelineEntries`

建議綁定欄位：

- toolbar / title 第一版可維持固定文字
- timeline 以 `SelectedAgentTimelineEntries` render

### Input / Output Entry

建議對應：

- `ProjectAgentTimelineEntryType.Input`
- `ProjectAgentTimelineEntryType.Output`

建議綁定欄位：

- `Message`
- `OccurredAtUtc` 可作為次要顯示資訊

### Tool Entry

建議對應：

- `ProjectAgentTimelineEntryType.Tool`

建議綁定欄位：

- `ToolName`
- `ToolArguments`
- `ToolResult`
- `ToolCallId`

備註：

- 模板可維持現有 expand/collapse 方式。
- 若 `ToolName` 為空但 `ToolResult` 已存在，仍需能渲染 placeholder tool card。
- `ToolCallId` 主要供 merge 與 debug，不一定需要直接顯示。

### Compaction Entry

建議對應：

- `ProjectAgentTimelineEntryType.Compaction`

建議綁定欄位：

- 第一版可顯示固定文案或沿用 `Message`

## 區塊更新頻率分類

### Low Frequency

- page shell
- group 建立
- agent 建立

### Medium Frequency

- agent status dot
- selected agent 切換

### High Frequency

- selected agent timeline
- tool row upsert
- input / output 連續追加

結論：

- 右側 timeline 是此頁最需要避免 server-side high-frequency rerender 的區塊。

## 第一版 UI 接線順序

### Step 1. 保留模板，只替換假資料

- 先把 `_agentGroups` 與 `_history` mock data 換成 snapshot DTO。
- 不改版面結構。

### Step 2. 加入 selected agent state

- 左側點擊 agent 後，右側切換到對應 agent timeline。
- 這屬於 local UI state，不需要 server 持久化。

### Step 3. 加入 live reducer

- live event 進來後：
  - upsert group
  - upsert agent
  - upsert timeline entry
- 若目前顯示的正是該 agent，右側 timeline 立即更新。

### Step 4. 處理 reconnect

- reconnect 後先重抓 snapshot，再重新建立 live tail。
- `_expandedToolDetails` 與 selected agent 是否保留，可作為 UI 體驗優化項。

## 主要情境

### 1. Snapshot Load

- 前端進頁時，先從 DB 載入 project 的 agent status snapshot。
- snapshot 負責建立目前完整畫面：
  - group tree
  - agent tree
  - selected agent timeline

### 2. Resume Subscription

- snapshot 載入完成後，前端送一個 resume / subscribe 訊號給 server。
- 該訊號至少包含：
  - `ProjectId`
  - 前端目前持有的 agent cursor 集合
- 每個 agent cursor 至少包含：
  - `AgentId` 或等價 runtime identity
  - 該 agent 目前已收到的最新 `Sequence`
- server 之後只推各 agent 在該 `Sequence` 之後的差異。

### 3. Idempotent Upsert

- 即使 snapshot 與 live stream 邊界重疊，前端仍須可安全 upsert。
- UI 不可假設 live event 永遠不重送，也不可假設重連後不會收到已存在資料。
- 所有 group / agent / timeline event 都必須可重複套用而不產生重複渲染。

## 情境收斂說明

以下看似不同的情境，實作本質上都屬於同一套流程：

- 已完成 project 開頁
- 正在分析中的 project 開頁
- 晚進場
- F5 refresh
- SignalR reconnect
- server 短暫重啟後重新訂閱

其差異主要只在：

- 是否需要 live tail
- resume request 內的 agent cursor 是空的，還是已有值

因此第一版不需要把上述情境拆成多套獨立機制，而是統一收斂成：

1. 取 snapshot
2. 帶著 per-agent latest sequence resume subscription
3. 對所有 live updates 做 idempotent upsert

## 補充情境

### Analysis Completion

- analysis 結束後，live stream 可能停止。
- UI 必須能知道這是正常完成，而不是異常斷線。

### Incomplete But Valid History

- analysis 可能中斷、失敗、降級、部分 agent 停在中間。
- UI 必須接受不完整但合法的 timeline。
- 例如只有 tool result、沒有 tool start 的資料，也必須能顯示。

### Multi Client Viewing

- 同一個 project 可能同時有多個 client 開著。
- event push 設計必須以 project 為 topic / channel，而不是綁死單一頁面實例。

## 非目標

- 第一版不處理跨裝置已讀狀態。
- 第一版不處理 timeline entry 編輯。
- 第一版不處理全文搜尋 timeline。
- 第一版不處理虛擬滾動最佳化。

## 記憶體控制方案

### 問題定義

- 目前 `AgentStatus` 的主要風險不是單次 render，而是 client browser 端持有過多 agent timeline 歷史。
- 若 snapshot 持續包含所有 agent 的完整 timeline，且 live push 持續把所有 agent 的 timeline event 都送到同一個 viewer，則：
  - 晚進場使用者會一次載入過大資料
  - 長時間觀看時 browser memory 會持續上升
  - 多 agent 並行分析時，大量與目前畫面無關的 timeline event 仍會被送到 client

### 解法原則

- 左側 roster 與右側 selected agent history 必須切開責任。
- project-level snapshot 與 live push 只負責常駐 summary。
- selected agent 的 timeline 改成按需載入、按需訂閱。
- 前端不保留多個 agent 的 history cache，第一版只保留目前 selected agent。

### Snapshot 調整

- `project-level snapshot` 第一版改為只提供：
  - project execution status
  - agent groups
  - agent summaries
  - 預設 selected agent 的 timeline
- snapshot 不再預設包含所有 agent 的完整 timeline。
- 預設 selected agent 規則沿用既有 page state 選取規則。

### Agent History 載入

- 右側 history pane 改成 selected agent detail model。
- 使用者進頁時：
  1. 先取 project snapshot
  2. 依 snapshot 決定 selected agent
  3. 只顯示該 agent 的 timeline
- 使用者切換 agent 時：
  1. 清掉目前 history pane 的 timeline data
  2. 向 server 取新 selected agent 的 history
  3. history 載入完成後再 render 右側 timeline

### Frontend Cache 原則

- 第一版只保留當前 selected agent 的 history cache。
- 切換到另一個 agent 後，前一個 agent 的 timeline data 直接釋放。
- 左側 roster summary 常駐保留，不受此規則影響。
- `_expandedToolDetails` 仍屬 local UI state，但其 key 集合應只跟目前 selected agent 相關。

### Live Push 分層

- live push 改成兩層 channel：

#### 1. Project Summary Channel

- subscription key 以 `projectId` 為主。
- 只推：
  - `ProjectStatusChanged`
  - `AgentGroupUpserted`
  - `AgentUpserted`
  - `AgentStatusChanged`

#### 2. Selected Agent Timeline Channel

- subscription key 以 `projectId + agentId` 為主。
- 只推目前正在看的那個 agent 的 timeline live updates：
  - `TimelineEntryUpserted`

### SignalR Group 設計

- project summary group：
  - `project:{projectId}`
- agent timeline group：
  - `project:{projectId}:agent:{agentId}`

- 前端進頁後：
  1. 先加入 `project:{projectId}`
  2. 決定 selected agent
  3. 再加入 `project:{projectId}:agent:{selectedAgentId}`

- 切換 agent 時：
  1. 離開舊的 agent timeline group
  2. 加入新的 agent timeline group
  3. 重新取得新 agent 的 history
  4. 之後只接收新 agent 的 timeline live updates

### Live Backfill Contract 調整

- 目前 per-agent cursor 集合模型不再適合作為 history live tail 的主模型。
- selected agent timeline channel 第一版改成只帶單一 agent cursor：
  - `ProjectId`
  - `AgentId`
  - `LatestSequence`

- project summary channel 不需要 timeline cursor。
- reconnect / refresh 時：
  - project summary 重新訂閱即可
  - selected agent timeline 再帶單一 `LatestSequence` 做 backfill

### 實作順序

#### Step 1

- 調整 snapshot DTO，移除「所有 agent 全量 timeline」的預設載入方式。

#### Step 2

- 建立 selected agent history read API。

#### Step 3

- 調整 client page state：
  - roster summary state
  - selected agent history state
  - selected agent live connection state

#### Step 4

- 調整 SignalR contract 與 group join / leave 流程。

#### Step 5

- 補切換 agent、refresh、reconnect 的測試。

## 資料模型原則

### Group / Agent Tree

- 左側樹狀清單的資料單位是：
  - `ProjectAgentGroup`
  - `ProjectAgent`
- group 與 agent 的顯示狀態，應能由 snapshot 直接建立左側樹。
- 若 live event 之後才建立 agent，UI 要能增量插入。

### Timeline

- 右側 timeline 是單一 agent 的歷史。
- timeline entry 類型第一版至少包含：
  - `Input`
  - `Output`
  - `Tool`
  - `Compaction`
- `Sequence` 是單一 agent timeline 的穩定順序欄位。
- `OccurredAtUtc` 用於顯示，不用於決定主要排序。

### Tool Entry

- DB projection 維持單一 `Tool` row。
- `ToolCallStarted` 與 `ToolCallCompleted` 仍是分離 event。
- 若 complete 先到，DB 允許先建立只含 `ToolCallId` + `ToolResult` 的 row。
- 若後續 start 再到，再補 `ToolName` / `ToolArguments`。
- 因此 UI 不可假設每筆 tool entry 一開始就所有欄位完整。

## 建議的前後端 contract

### Snapshot DTO

第一版建議 server 提供 project-level snapshot DTO，至少包含：

- Project execution status
- Agent groups
- Agents
- 每個 agent 的 timeline entries
- 每個 agent 目前最新 status
- snapshot generation time

第一版建議每筆 timeline entry DTO 至少包含：

- `TimelineEntryId`
- `AgentId`
- `Sequence`
- `EntryType`
- `OccurredAtUtc`
- `Message`
- `ToolCallId`
- `ToolName`
- `ToolArguments`
- `ToolResult`

### Live Event DTO

第一版建議 live event 不直接暴露 raw domain event，而是暴露 UI-friendly event。

至少應有：

- `AgentGroupCreated`
- `AgentCreated`
- `AgentStatusChanged`
- `TimelineEntryUpserted`

理由：

- UI 不需要知道內部 raw event 類型細節。
- tool start / complete 在 server 已合併成 DB projection 時，推到 UI 也應以 projection 觀點為主。
- `TimelineEntryUpserted` 比 `ToolCallStarted` / `ToolCallCompleted` 更適合前端處理。

### Resume Subscription Request

第一版建議前端在 snapshot 載入完成後，明確送出 resume request。

request 至少包含：

- `ProjectId`
- `AgentCursors`

`AgentCursors` 至少包含：

- `AgentId` 或等價 runtime identity
- `LatestSequence`

概念示意：

```text
ProjectId = <project-id>
AgentCursors =
  - AgentId = <agent-a>, LatestSequence = 42
  - AgentId = <agent-b>, LatestSequence = 17
  - AgentId = <agent-c>, LatestSequence = 8
```

server 收到後，應只推各 agent 在 `LatestSequence` 之後的差異。

### Why Not Single Global Sequence

- 目前 `Sequence` 的語意是單一 agent timeline 內排序。
- 它不是 project-level global ordering。
- 因此前端不可只回傳單一 `latestSequence`，否則 server 無法判斷不同 agent 各自推到哪裡。
- 第一版應以 per-agent cursor 為準。

### Identity And Merge Key

- agent tree merge：
  - group 以 `GroupId` 或 `GroupRuntimeKey` 作為 identity
  - agent 以 `AgentId` 或 `AgentRuntimeKey` 作為 identity
- timeline merge：
  - 一般 entry 以 `TimelineEntryId` 為主鍵
  - tool entry 不應只靠 `ToolCallId` merge，仍應以 `TimelineEntryId` 為主，`ToolCallId` 為輔
- timeline 排序：
  - 同一 agent 內以 `Sequence` 排序

## UI Merge 規則

### Snapshot Merge

- snapshot 載入時，直接覆蓋本地 page state。
- snapshot 是初始 truth，不需要逐筆 diff。

### Live Merge

- `AgentGroupCreated`
  - 若不存在則新增
  - 若已存在則 upsert 顯示資料
- `AgentCreated`
  - 若不存在則插入對應 group
  - 若已存在則 upsert 顯示資料與 status
- `AgentStatusChanged`
  - 只更新該 agent 的目前狀態
- `TimelineEntryUpserted`
  - 以 `TimelineEntryId` 為 key
  - 若不存在則新增
  - 若已存在則覆寫欄位
  - 插入後依 `Sequence` 重新排序該 agent timeline

### Boundary Deduplication

- snapshot 完成後接 live stream 時，live 的第一批事件可能與 snapshot 尾端重疊。
- 因此 UI 必須可對 `TimelineEntryId` / `AgentId` / `GroupId` 做 idempotent upsert。
- 不可使用 append-only 心智模型處理 live event。

## Server 執行計畫

### Phase 1. Snapshot Read API

- 建立 project-level agent status snapshot query service。
- 由 EF 模型組出 UI 專用 DTO。
- 明確決定排序規則：
  - group 排序
  - agent 排序
  - timeline 以 `Sequence` 排序

完成條件：

- 已完成分析的 project 可正確載入完整 agent tree 與 timeline。

### Phase 2. Live Push Contract

- 定義 project-level live event DTO。
- 定義 push channel：
  - 建議以 project 為 subscription key
- 決定傳輸層：
  - SignalR 或等價 server push 機制

完成條件：

- 正在分析中的 project 可收到 group / agent / status / timeline 的增量更新。

### Phase 3. Snapshot Plus Live Handshake

- 前端進頁流程固定為：
  1. 取 snapshot
  2. render 初始畫面
  3. 建立 live subscription
  4. 接收增量 upsert
- 若 server 端需要 cursor，應在此階段定義：
  - `LastKnownSequencePerAgent`
  - 或 snapshot version / watermark

完成條件：

- 使用者晚進場時不會漏掉分析中已存在的資料。

### Phase 4. Reconnect And Resume

- 定義斷線後重連策略。
- 第一版可採保守設計：
  - reconnect 後直接重抓 snapshot，再重新訂閱 live
- 第二版若需要優化，再考慮 cursor-based resume。

完成條件：

- refresh / reconnect 後，UI 不會重複顯示資料，也不會缺資料。

### Phase 5. Completion State

- server 必須提供 project execution status。
- UI 必須能辨識：
  - analysis running
  - analysis completed
  - analysis failed
  - live disconnected but project still running

完成條件：

- 使用者能分辨「正常結束」與「連線異常」。

## Frontend 執行計畫

### Phase A. Page State Model

- 建立 page-level state model，明確分離：
  - snapshot data
  - live connection state
  - selected agent
- 不要把 UI state 與 transport state 混成同一層 object。

### Phase B. Snapshot Loader

- 頁面進入時先拉 snapshot。
- 根據 snapshot 建立左側 group / agent tree。
- 預設選取規則需明確：
  - 無 selected agent 時要選誰
  - 原本 selected agent 消失時怎麼處理

### Phase C. Live Event Reducer

- 建立單一 reducer / apply function 處理所有 live event。
- 所有 event 都要 idempotent。
- tool entry update 必須是 upsert，不是 append。

### Phase D. Reconnect Strategy

- 第一版：斷線後顯示 reconnecting 狀態。
- 重連成功後重抓 snapshot。
- 重新掛上 live tail。

## 測試計畫

### Server Tests

- snapshot query 可正確輸出 group / agent / timeline tree
- timeline 依 `Sequence` 排序
- tool incomplete row 可正確輸出
- 正在進行中的 project 可輸出目前狀態與部分 timeline

### Frontend Tests

- snapshot render 正常
- live event 可新增 group / agent
- live event 可更新 agent status
- `TimelineEntryUpserted` 對同一 `TimelineEntryId` 不會重複插入
- tool entry 先 result 後 start，UI 最終呈現正確
- refresh / reconnect 後資料不重複

### End To End Tests

- 已完成 project 開頁只靠 snapshot 即可顯示
- 進行中 project 開頁可先看到既有資料，再看到後續推送
- 中途開頁不漏資料
- 斷線重連後可恢復一致狀態

## 目前已完成項目

- EF 已具備 agent group / agent / timeline 基礎模型
- timeline 已可保存：
  - input
  - output
  - tool
  - compaction
- tool timeline 已支援單筆 projection 與 out-of-order merge
- `Sequence` 已作為單一 agent timeline 順序欄位

## 尚未完成項目

- project-level snapshot read API
- project-level live push DTO
- UI state reducer
- snapshot 與 live handoff 機制
- reconnect / resume 策略落地
- analysis completion 與 connection state 的 UI 區分

## 第一版建議落地順序

1. 先做 snapshot read API，讓已完成 project 可顯示。
2. 再做 live push DTO 與 subscription，讓進行中 project 可 tail。
3. 再補 reconnect 策略，先採重抓 snapshot 的保守版本。
4. 最後再討論 UI 表現細節，例如 tool row、compaction row、狀態 badge。

## 決策備忘

- `Sequence` 是 timeline 主排序依據。
- DB projection 允許 tool incomplete row。
- live event 應以 UI-friendly projection event 為主，不直接暴露 raw internal event。
- 第一版 reconnect 可接受重抓 snapshot，不必一開始就做 cursor resume。
