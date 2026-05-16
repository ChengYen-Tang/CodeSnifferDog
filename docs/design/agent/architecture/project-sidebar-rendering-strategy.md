# Project Sidebar Rendering Strategy

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `NavMenu / Project Sidebar` 的渲染責任切分與後續實作方向
- 文件狀態：草稿
- 最後更新：2026-05-15

## 文件範圍

- 本文件聚焦：
  - [NavMenu.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Layout/NavMenu.razor)
  - `Project Sidebar` 類型的共用導覽區塊
- 本文件只討論資料載入、rendering strategy、state ownership。
- 本文件不重新設計 sidebar UI 視覺模板。
- 本文件不展開 `AgentStatus` 那種高頻 timeline live reducer 細節。

## 相關文件

- [ui-rendering-strategy-audit.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/ui-rendering-strategy-audit.md)
- [agent-status-page-execution-plan.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/agent-status-page-execution-plan.md)

## 問題定義

- `NavMenu / Project Sidebar` 是使用者開頁後最早看到的框架之一。
- 它需要在首屏就提供可用導覽，不適合等 client 完整啟動後才顯示。
- 但它的後續互動，例如：
  - active project 高亮
  - group 展開收合
  - project status 小幅更新
  - project list refresh
  不需要依賴 server 持續做高頻 UI diff。

結論：

- 初始顯示應由 server 提供。
- 進頁後的狀態變化應交給 client。

## 核心原則

- 初始 shell 與初始 sidebar snapshot 由 server render。
- 後續互動與局部更新由 client state 接手。
- sidebar 不是高頻觀測頁，不應走 server-side interactive 持續 rerender。
- local UI state 與 transport / loading state 必須分開。
- 若後續有低頻 live 更新，應視為 snapshot 的補充，不是另一套 truth。

## Rendering Strategy 結論

### 第一版建議

- `Server render first`
  - 使用者第一次開啟網頁時：
    - server render layout shell
    - server render 初始 project sidebar snapshot
- `Client owns updates after hydration`
  - client 啟動後接手：
    - active item
    - group expand/collapse
    - selected project 切換
    - 後續 snapshot refresh
    - 低頻 live merge 或 refresh trigger

### 不建議的方向

- 不要讓 sidebar 之後的更新都持續依賴 server interactive circuit。
- 不要把 `AgentStatus` 的高頻 client reducer 模式整份硬套到 sidebar。
- 不要為了統一而把所有 project 內容預先長駐在 browser memory。

## State Ownership

### 1. Layout Snapshot State

這層是 server 初始輸出，client 後續可覆蓋更新：

- project list summary
- group list summary
- 每個 project 的基本 status
- 初始 selected project

這層是 sidebar 的資料 truth。

### 2. Client UI State

這層只屬於前端互動，不應由 server push 驅動：

- 哪些 group 目前展開
- 哪個 project 目前 active
- hover / focus / selected 樣式
- client 端暫時 loading 指示

### 3. Transport State

這層描述資料同步狀態，不應與 UI state 混在同一個 object：

- isLoadingSnapshot
- isRefreshing
- liveConnected
- liveError
- fallbackPollingActive

## 資料頻率判斷

### 低頻

- page 初始載入
- project 新增或刪除
- 使用者切換頁面

### 中低頻

- project status 更新
- sidebar summary refresh
- project analysis 開始 / 完成 / 失敗

### 非高頻

- sidebar 不應承接像 agent timeline 那種連續訊息流

因此這塊適合：

- `SSR snapshot + client refresh/live assist`

不適合：

- `server interactive high-frequency diff`

## 初始進頁流程

### Step 1

- server render layout shell

### Step 2

- server render 初始 sidebar snapshot

初始 snapshot 至少包含：

- project summary list
- group summary
- project status
- initial selected project

### Step 3

- client hydration 後接手 sidebar state

### Step 4

- client 再決定是否需要：
  - 訂閱低頻更新
  - 啟動 polling fallback
  - 主動 refresh

## 後續更新責任

### Client 自行更新的範圍

- 左側 active 樣式
- group 展開收合
- selected project
- loading / refreshing 視覺狀態
- 小幅 summary 資料變更

### 需要重新向 server 取資料的情況

- project list 結構變更
- project status summary 需要整包刷新
- reconnect 後需要重新同步
- polling fallback 觸發 refresh

### 可接受的第一版做法

- server live 只送「sidebar 需要刷新」訊號
- client 收到後重新抓一份 sidebar snapshot

這樣比一開始就做完整 incremental DTO 更穩。

## 建議的資料切分

### Sidebar Summary DTO

第一版建議只提供 summary，不夾帶重內容：

- group id
- group name
- project id
- project name
- project status
- 排序所需欄位

### 不應放進 Sidebar Snapshot 的資料

- agent timeline
- report markdown
- project detail 頁的重內容
- 與 sidebar 顯示無關的大型 payload

## 記憶體原則

- sidebar 只常駐 summary data。
- 不因為 sidebar 存在，就把 project detail data 常駐在 browser。
- 切換 selected project 時，只更新 active state 與必要 summary。
- project 的重內容由各自頁面按需載入。

## 第一版實作方向

### Phase 1. Server Initial Snapshot

- server 可在 layout / nav render 時提供初始 sidebar snapshot
- 確保首屏可直接看到導覽結構

### Phase 2. Client Sidebar State

- 建立 sidebar 專用 client state model
- 至少分成：
  - snapshot state
  - ui state
  - transport state

### Phase 3. Refresh Strategy

- 先保留目前的 reload / polling fallback 思路
- 明確定義 refresh trigger

### Phase 4. Optional Low-Frequency Live

- 若現有 SignalR 已足夠穩定，可保留低頻通知
- 第一版可以只推 refresh signal，不必急著做複雜 reducer

## Refresh Trigger 建議

第一版可接受以下 trigger：

- project analysis started
- project analysis completed
- project analysis failed
- project created
- reconnect recovered
- polling interval reached

client 收到後：

1. 標記 sidebar 進入 refreshing state
2. 重新抓 snapshot
3. 用新 snapshot 覆蓋 summary state
4. 保留可保留的 local UI state

## Local UI State 保留規則

### 建議保留

- group 展開狀態
- 若 selected project 仍存在，保留 selected project

### 建議重置

- 已不存在的 selected project
- 已不存在 group 的 expand state
- 過期的 loading / error 提示

## Reconnect Strategy

- sidebar reconnect 後不做複雜 resume merge。
- 第一版採保守策略：
  - reconnect success
  - client 直接重抓 sidebar snapshot
  - snapshot 覆蓋 summary state
  - local UI state 依規則保留或清理

## 與 AgentStatus 的差異

- `AgentStatus` 是高頻 timeline viewer。
- `Project Sidebar` 是中低頻導覽 summary。
- 因此 sidebar 不需要：
  - per-agent timeline cursor
  - selected timeline live tail
  - timeline reducer

sidebar 應保持更簡單的同步模型。

## 驗收條件

- 使用者第一次開頁時，可直接看到完整 sidebar。
- client hydration 後，sidebar 互動由前端接手。
- project summary 更新時，不需要整個 layout 重新由 server 持續互動渲染。
- reconnect / refresh 後，sidebar summary 能恢復一致。
- sidebar 不持有與導覽無關的大型資料。

## 後續建議

- 下一步可補一份 `Project Sidebar Execution Plan`，把：
  - snapshot contract
  - client state model
  - refresh/live trigger
  - reconnect strategy
  寫成可直接實作的任務清單。
