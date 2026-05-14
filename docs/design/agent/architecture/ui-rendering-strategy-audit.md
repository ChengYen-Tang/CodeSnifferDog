# UI Rendering Strategy Audit

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：盤點目前主要 UI 頁面與共用元件的渲染責任，明確定義哪些內容適合 SSR、哪些適合 client-side state、哪些適合 snapshot + live hybrid
- 文件狀態：草稿
- 最後更新：2026-05-14

## 文件範圍

- 本文件聚焦目前 Blazor UI 的 render strategy 分類與後續調整順序。
- 本文件不重新定義 UI 視覺模板。
- 本文件不重複展開 `agent-status-page-execution-plan.md` 已定義的 snapshot / live handshake 細節，只在需要時引用其結論。
- 本文件以目前已存在的頁面與共用元件為主，先做第一輪 render audit。

## 相關文件

- [agent-status-page-execution-plan.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/agent-status-page-execution-plan.md)
- [agent-design.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/agent-design.md)
- [execution-pipeline.md](/Z:/GitHub/CodeSnifferDog/docs/design/agent/architecture/execution-pipeline.md)

## 核心原則

- 先分清楚資料頻率，再決定 render strategy，不反過來做。
- 初始畫面優先可由 server 提供 shell 與 snapshot。
- 高頻區塊不應依賴 server-side interactive circuit 持續做 UI diff。
- 中低頻區塊可接受 SSR + 輕量互動，前提是狀態與成本合理。
- transport state 與 local UI state 必須分離，避免 reconnect、refresh、prerender 時互相污染。
- live update 一律視為 snapshot tail，不應與 snapshot 並列成兩套 truth。

## 分類標準

### 1. Static / SSR First

適用條件：

- 內容幾乎不變，或只在進頁時需要一次性載入
- 不需要高頻互動
- SEO 不是主因，但可從較快首屏與較低 client state 成本受益

建議做法：

- 可由 server prerender shell
- 若需要互動，再補 client hydration

### 2. Snapshot First + Low/Medium Frequency Live

適用條件：

- 進頁先有完整 snapshot
- 後續偶爾有更新，但頻率不高
- 需要避免 server-side interactive 持續維護大量 diff 狀態

建議做法：

- 以 snapshot 當初始資料
- client 端維護 page state
- live event 只做增量 merge

### 3. Client Heavy / High Frequency Live

適用條件：

- 更新頻率高
- 使用者會長時間停留觀看
- diff 次數多，server-side interactive 成本會被放大

建議做法：

- shell 可 prerender
- 主資料區交給 client reducer / local state
- server 專注提供 snapshot 與 live DTO，不負責高頻 UI diff

## 目前頁面與元件盤點

### A. `AgentStatus`

參考：

- [AgentStatus.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Pages/AgentStatus.razor)

目前特性：

- project 進頁先讀 snapshot
- 後續接 SignalR live subscription
- timeline 與 agent status 屬高頻更新區
- page state 已切開 snapshot、live connection、selection、completion

建議分類：

- `Client Heavy / High Frequency Live`

結論：

- 這頁可作為其他高頻頁面的參考樣板。
- server 提供 snapshot 與 live tail。
- 高頻 timeline diff 留在 client。

### B. `Reports`

參考：

- [Reports.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Pages/Reports.razor)

目前特性：

- report 內容可能偏大
- 使用者切換左側 report 檔案時，主要變動是右側 markdown 內容與少量選取狀態
- 不需要 live push
- 真正風險不是高頻更新，而是一次把太多 markdown 內容常駐在瀏覽器記憶體

建議分類：

- `Static / SSR First + On-Demand Content Fetch`

結論：

- 這頁不需要比照 AgentStatus 走 live architecture。
- 首次進頁時，由 server 先輸出：
  - page shell
  - report list metadata
  - 預設選中的單一 report metadata
  - 預設選中的單一 raw markdown
- markdown 渲染統一由 client 端處理。
- 使用者切換其他 report 時，client 只向 server 取該份 report 的 raw markdown 與必要 metadata。
- client 不應常駐所有 report 內容，第一版以「只保留目前選中的 report」為原則。
- 若未來需要體感優化，再考慮很小的最近使用快取，不應先做全量 preload。
- 本頁目前優先目標是功能完成；markdown 內容安全性策略不納入這一輪 scope，但後續必須補文件與實作決策。

建議前端接手更新的範圍：

- 右側 markdown 內容
- 目前選中的檔名
- 目前選中的規則名
- 左側 report list 的 active 樣式

不應因切換 report 而重建的範圍：

- 整個 page shell
- 整個 sidebar 結構
- 整包 report list 資料
- 其他與目前 report 內容切換無關的 UI 區塊

建議 state 切分：

- server-first state
  - report list metadata
  - 初始 selected report id
- client-local state
  - current selected report id
  - current selected report markdown
  - current selected report display fields
  - current content request 的 loading / error state

### C. `Home`

參考：

- [Home.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Pages/Home.razor)

目前特性：

- 主要是 project upload form
- 使用者互動集中在表單輸入與檔案選取
- 不存在高頻 server push

建議分類：

- `Static / SSR First`

結論：

- 這頁不需要 client-heavy state architecture。
- 維持簡單表單頁即可。
- 後續若要補 upload progress，再單獨把 progress 區塊做成中頻互動，不需要整頁升級。

### D. `NavMenu` / Project Sidebar

參考：

- [NavMenu.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Layout/NavMenu.razor)
- [ProjectSidebarSyncService.cs](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Services/Projects/ProjectSidebarSyncService.cs)

目前特性：

- 會顯示 project list 與 status group
- 目前同時使用 snapshot reload、SignalR hub 通知、15 秒 polling fallback
- 更新頻率通常低於 agent timeline，但高於靜態頁
- 有 local UI state：
  - group 展開
  - selected project
- 有 transport state：
  - loading
  - hub error

建議分類：

- `Snapshot First + Low/Medium Frequency Live`

結論：

- 這類 sidebar 不應走 server-side high-frequency diff。
- 但也不需要像 AgentStatus 一樣做重型 reducer。
- 適合維持 client-side service 持有 state，server 只推「project list 需要刷新」這類低頻訊號，或直接推簡化的增量 DTO。

### E. `Counter`

參考：

- [Counter.razor](/Z:/GitHub/CodeSnifferDog/CodeSnifferDog.Server/CodeSnifferDog.Server.Client/Pages/Counter.razor)

目前特性：

- 樣板頁

建議分類：

- 不列入產品 render strategy 決策

## 共用模式建議

### 1. 高頻觀測頁

適用：

- agent timeline
- 未來若有 execution stream、worker log、tool stream

建議：

- 採 `snapshot + live tail + client reducer`
- page state 拆開：
  - snapshot state
  - live connection state
  - selection / expansion / scroll 等 local UI state
- server 不負責高頻 UI diff

### 2. 中頻作業列表 / 導覽元件

適用：

- project sidebar
- 未來若有 queue board、project overview card list

建議：

- client service 集中管理狀態
- 先取 snapshot
- live 只負責提醒 reload 或送低成本增量
- 保留 polling fallback

### 3. 低頻內容頁

適用：

- reports
- upload / settings / metadata 類頁面

建議：

- 優先簡單資料流
- 不要為了架構一致性硬套 live subscription

## 第一輪結論

目前主要頁面可先分成以下三類：

- `AgentStatus`：高頻，已採 client-heavy hybrid，方向正確
- `NavMenu / Project Sidebar`：中頻，維持 snapshot-first + live assist
- `Reports`、`Home`：低頻，維持 SSR first / local interactive 即可

這表示後續 render strategy 不應做成全站單一模式，而應做成「按頁面頻率分層」。

## 建議的後續工作順序

### Priority 1

- 盤點其餘產品頁面或 upcoming 頁面，先標記頻率等級
- 明確決定哪些頁面需要沿用 `AgentStatus` 的 page-state / reducer 模式

### Priority 2

- 補一份 `project sidebar` 專用設計文件
- 釐清它是否要從「reload driven」升級成「incremental live DTO driven」

### Priority 3

- 若未來新增 execution progress、worker stream、tool diagnostics 頁面
- 一律先套用本文件分類標準，再決定 render mode

## 不應做的事

- 不要因為已經有 Blazor hybrid，就把所有頁面都改成 server interactive。
- 不要因為 `AgentStatus` 已走 client-heavy，就把所有頁面都硬改成同一模式。
- 不要把 local UI state 與 live transport state 混在同一層資料物件。
- 不要跳過資料頻率判斷，直接從框架偏好反推 render strategy。
