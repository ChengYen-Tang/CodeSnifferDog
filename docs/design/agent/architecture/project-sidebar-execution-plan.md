# Project Sidebar Execution Plan

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 `NavMenu / Project Sidebar` 的 snapshot、client state、refresh 與 reconnect 執行計畫
- 文件狀態：草稿
- 最後更新：2026-05-15

## 文件範圍

- 本文件聚焦：
  - [NavMenu.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Layout/NavMenu.razor)
  - `Project Sidebar` 相關 state/service
- 本文件只處理 sidebar summary 與導覽互動。
- 本文件不處理 project detail 頁面的重內容。
- 本文件不重新設計 sidebar UI 樣式。

## 相關文件

- [project-sidebar-rendering-strategy.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/project-sidebar-rendering-strategy.md)
- [ui-rendering-strategy-audit.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/ui-rendering-strategy-audit.md)
- [agent-status-page-execution-plan.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/agent-status-page-execution-plan.md)

## 設計目標

- 使用者第一次開頁時，可直接看到可用的 project sidebar。
- sidebar 初始資料由 server 提供，不等 client 完整啟動才出現。
- hydration 後 sidebar 互動由 client 接手。
- sidebar 只常駐 summary data，不持有 detail heavy payload。
- refresh / reconnect 後，sidebar summary 能恢復一致。
- local UI state 與 transport state 明確分離。

## 核心原則

- sidebar 是 `snapshot first + low/medium frequency refresh/live assist`，不是高頻 stream viewer。
- server 提供初始 snapshot 與必要 refresh signal。
- client 持有 sidebar state，後續互動與更新由 client merge。
- live event 若存在，視為 snapshot reload trigger 或低成本增量，不是另一套 truth。
- 不把 report、agent history、project detail 常駐在 sidebar state。

## Sidebar 資料模型原則

### Sidebar Summary

第一版 sidebar snapshot 只包含 summary：

- group summary
- project summary
- project status
- 排序欄位
- initial selected project

### 不屬於 Sidebar Snapshot 的資料

- agent timeline
- report markdown
- project detail content
- 大型分析結果

## State 切分

### 1. Snapshot State

- groups
- projects
- current snapshot timestamp / version

### 2. UI State

- selected project id
- expanded groups
- active nav item

### 3. Transport State

- isLoading
- isRefreshing
- liveConnected
- liveError
- pollingFallbackActive

## 主要情境

### 1. First Page Load

- server render layout shell
- server render initial sidebar snapshot
- client hydration 後接手互動

### 2. Project Summary Changed

- client 收到 refresh signal 或 polling trigger
- 重新抓 sidebar snapshot
- 覆蓋 summary state
- 保留合法的 local UI state

### 3. Refresh

- 使用者 refresh 頁面
- server 再次輸出初始 snapshot
- client 再次接手 sidebar state

### 4. Reconnect

- 若 live connection 中斷後恢復
- client 直接重抓 sidebar snapshot
- 不做複雜 resume merge

## Snapshot Contract

### 第一版建議 DTO

- `SidebarSnapshotDto`
  - `GeneratedAtUtc`
  - `SelectedProjectId`
  - `Groups`

- `SidebarGroupDto`
  - `GroupId`
  - `DisplayName`
  - `SortOrder`
  - `Projects`

- `SidebarProjectDto`
  - `ProjectId`
  - `DisplayName`
  - `Status`
  - `CreatedAtUtc`
  - `SortOrder`

### 排序原則

- group 排序規則必須固定
- project 排序規則必須固定
- 不要把排序責任交給 client 臨時猜

## Refresh / Live Contract

### 第一版保守做法

- live channel 不直接推完整 sidebar data
- live 只推：
  - sidebar needs refresh
  - 或 very small summary delta

### 建議第一版訊號

- `ProjectCreated`
- `ProjectStatusChanged`
- `ProjectDeleted`
- `SidebarRefreshRequested`

### 第一版 client 行為

- 收到上述訊號後：
  1. 設為 refreshing
  2. 重抓 sidebar snapshot
  3. 覆蓋 snapshot state

## UI State 保留規則

### Selected Project

- 若 snapshot reload 後 selected project 仍存在，保留
- 若已不存在，改選第一個合法 project

### Expanded Groups

- 若 group 仍存在，保留展開狀態
- 若 group 已不存在，移除其 expand state

### Error / Loading State

- refresh 完成後清掉過期錯誤
- reconnect 成功後清掉過期 live error

## Reconnect Strategy

### 第一版

- reconnecting：
  - 顯示 transport state
  - 不清空目前 sidebar summary

- reconnected：
  - 直接重抓 snapshot
  - snapshot 覆蓋 summary state
  - local UI state 依規則保留

## Rendering Strategy 落地

### Server Responsibility

- render layout shell
- render initial sidebar snapshot
- 提供 sidebar snapshot read API
- 提供低頻 refresh signal 或等價通知

### Client Responsibility

- 維護 selected / expanded / active UI state
- 維護 loading / refreshing / reconnecting state
- 套用 refresh 後的新 snapshot
- 處理 polling fallback

## Phase 1. Snapshot Contract

- 定義 sidebar summary DTO
- 明確定義 group / project 排序規則
- server 能輸出初始 sidebar snapshot

完成條件：

- 首次進頁可直接看到 sidebar，不需要等 client 再補資料

## Phase 2. Client Sidebar State Model

- 建立 sidebar page/service state
- 明確分離：
  - snapshot state
  - ui state
  - transport state

完成條件：

- selected、expanded、loading、liveError 不再混在同一層資料物件

## Phase 3. Snapshot Loader

- client hydration 後接手 snapshot state
- 定義 selected project 保留規則
- 定義 expanded group 保留規則

完成條件：

- refresh / reload 後 sidebar selection 行為一致可預測

## Phase 4. Refresh Trigger Flow

- 定義 sidebar refresh trigger
- 第一版可接受：
  - SignalR 通知 refresh
  - polling fallback

完成條件：

- project summary 變更時，sidebar 可重新同步，不需要持續 server interactive diff

## Phase 5. Reconnect Strategy

- reconnecting 時保留現有 sidebar summary
- reconnect success 後重抓 snapshot

完成條件：

- reconnect 後 sidebar 不重複、不缺資料，且 UI 不閃成空白

## Phase 6. Tests

### Client Tests

- initial snapshot render 正常
- selected project 保留規則正確
- selected project 消失時 fallback 正確
- expanded group 保留 / 清理規則正確
- refresh trigger 後會重新抓 snapshot
- reconnect success 後會重抓 snapshot

### Server Tests

- sidebar snapshot 可正確輸出 group / project summary
- 排序規則正確

### End To End

- 首次開頁直接看到 sidebar
- status 變更後 sidebar 可同步
- reconnect 後 sidebar 可恢復一致

## 第一版建議落地順序

1. 先做 sidebar snapshot DTO 與讀取入口。
2. 再做 client sidebar state model。
3. 再接 refresh trigger 與 polling fallback。
4. 最後補 reconnect 與測試。

## 非目標

- 第一版不做 sidebar 的複雜 incremental reducer。
- 第一版不做 detail payload preload。
- 第一版不做跨分頁共享 UI 展開狀態。
