# Reference Design Philosophy

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：整理外部參考案例的設計哲學，作為 Agent Design 的前置思考材料
- 文件狀態：草稿
- 最後更新：2026-04-12

## 參考來源

- 主要參考文件：[Z:\GitHub\claude-code-harness-blog](Z:\GitHub\claude-code-harness-blog)
- 輔助參考程式碼：[Z:\GitHub\claude-code-sourcemap](Z:\GitHub\claude-code-sourcemap)

## 參考設計哲學

在正式展開 Agent 細節前，本專案先參考 Claude Code 在大型專案情境下的設計哲學，重點不是直接複製其產品形態，而是抽取適合 CodeSnifferDog 的架構原則。

### 從 Claude Code 抽出的核心原則

#### 1. 不把大型任務交給單一 Agent 硬吃

Claude Code 的方向不是讓一個 Agent 持有所有上下文、一次完成所有工作，而是把任務拆成可控的子任務，由多個子代理協作完成。

對 CodeSnifferDog 的啟發：

- 不應讓單一 Review Agent 同時負責所有規則類型的分析。
- 較合理的方向是以「每個規則檔對應一個 Sub-Agent」作為基本拆分單位。
- 大型專案的可擴展性來自任務切分與協調，而不是單次 Prompt 無限制擴大。

#### 2. 協調者與工作者應明確分工

在參考資料中，Coordinator 與 Worker 的工具能力與責任被刻意區分。Coordinator 偏向調度、監看與整合；Worker 偏向實際執行。

對 CodeSnifferDog 的啟發：

- 應存在一個主 Agent 或協調層，負責讀取規則、建立 Sub-Agent、追蹤進度與收斂結果。
- 規則型 Review Sub-Agent 應專注在單一主題分析，例如記憶體洩漏或效能。
- 協調者不應直接承擔所有分析細節，否則會重新變回單體 Agent。

#### 3. 多 Agent 協作必須可控，而不是越多越好

參考程式碼中可看到明確的代理角色切分、工具限制與保守的執行策略，核心精神是讓多 Agent 協作維持可控。

對 CodeSnifferDog 的啟發：

- 規則很多時不能無上限地讓 Agent 彼此再派生更多 Agent。
- 多 Agent 的互動結構應盡量保持單純，例如以主 Agent 調度規則型 Sub-Agent 為主。
- 協作應建立在可控與可觀測之上，而不是只追求更多平行度。

#### 4. 預設保守，明確聲明後才放寬

Claude Code 在工具併發與代理工具權限上偏向 fail-closed：不安全或未知的情況預設不放行，只有明確聲明後才允許。

對 CodeSnifferDog 的啟發：

- 新的 Review 規則不應自動獲得過多執行能力。
- 後續若有規則層級的額外能力，例如允許特定工具或額外上下文，應採白名單或明確配置。
- 系統應優先保證結果穩定與可追蹤，再逐步增加靈活性。

#### 5. 成本控制是 Agent 設計問題，不只是系統問題

參考資料多次強調 fork、prompt cache、上下文共享與限制遞迴代理，背後核心是控制 token 成本與延遲成本。

對 CodeSnifferDog 的啟發：

- 大型專案 review 的主要成本不只在模型呼叫次數，也在重複傳遞上下文。
- 後續設計應思考主 Agent 與 Sub-Agent 是否共享部分前置分析結果、摘要或索引，而不是每個 Agent 都從頭讀完整專案。
- 規則拆分雖然提升模組化，但若沒有共享機制，也會帶來重複分析成本。

#### 6. 可觀測性不是附加功能，而是核心能力

Claude Code 的設計明顯不是黑盒：有 query loop、tool execution、agent transcript、progress state 等概念，代表系統從一開始就把可觀測性納入核心。

對 CodeSnifferDog 的啟發：

- 「看到哪些 Agent 被啟動、做了什麼、說了什麼、呼叫了什麼工具」不應被視為除錯附加功能，而應被視為產品基本能力。
- CLI 與 Server UI 都需要建立在一致的事件與軌跡模型之上。
- 可觀測性資料模型將直接影響 UI/UX、資料庫設計與除錯能力。

## 對 CodeSnifferDog 的初步設計結論

根據上述參考，本專案在 Agent Design 上可先採用以下高層方向：

- 採用「協調者 + 多個規則型 Sub-Agent」而非單一大型 Agent。
- 每個 Sub-Agent 聚焦單一規則檔與單一報告輸出。
- 從一開始就把 agent 行為軌跡、工具呼叫與進度事件視為一級資料。
- 後續設計需特別處理共享上下文與成本控制，避免多個 Sub-Agent 重複掃描大型專案。

## 不直接照抄的部分

雖然 Claude Code 提供了很多值得借鏡的設計，但 CodeSnifferDog 不應直接照抄以下內容：

- 它是通用型 coding agent，而我們是專注在 Code Review 的垂直場景。
- 它的工具系統極廣，而我們需要的是較可控、較聚焦的 review workflow。
- 它的互動模式包含大量即時人機互動，而我們的目標更偏向低互動或無互動。
- 它的 worktree、editing、task orchestration 設計，有些適合參考，但不一定全部需要進入第一版。

## 變更紀錄

- 2026-04-12：從 Agent Design 拆出參考設計哲學。
