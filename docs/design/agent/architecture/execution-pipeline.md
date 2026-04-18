# Execution Pipeline

## 文件資訊

- 專案名稱：CodeSnifferDog
- 文件目的：定義 Agent 階段之間的執行順序與跳轉規則
- 文件狀態：草稿
- 最後更新：2026-04-19

## Scan Agent 到 Scan Verifier Agent 的跳轉規則

### 基本原則

- `Scan Agent` 不自行決定是否進入下一階段。
- 模型的一輪自動對話 / tool loop 結束後，由程式邏輯檢查目前狀態。
- 是否跳轉到 `Scan Verifier Agent`，由程式邏輯依據目前 `ScanProject` 狀態決定。

### 一輪 Scan Agent 結束後的狀態檢查

當 `Scan Agent` 的當前一輪自動對話與工具呼叫循環結束後，系統應檢查：

1. 目前是否已建立任何 `ScanProject`

### 跳轉條件

#### 情況 1：已存在 scan result

- 條件：`ScanProject` 數量大於 0
- 動作：直接進入 `Scan Verifier Agent`

#### 情況 2：不存在任何 scan result

- 條件：`ScanProject` 數量等於 0
- 動作：不進入 verifier，退回同一個 `Scan Agent`

### 退回 Scan Agent 時的固定提示

若發生情況 2，系統應以固定的 user input 重新提示 `Scan Agent`，明確告知：

- 本輪結束時沒有建立任何 `ScanProject`
- 因此目前工作未完成
- 你必須建立至少一筆 `ScanProject`

此提示應由程式邏輯產生，而不是由模型自行推斷。

### 重試上限與重置策略

若發生情況 2，系統不應無限次以固定提示反覆推進同一個 `Scan Agent`。

建議策略如下：

- 當固定提示連續觸發達到上限次數時，視為目前 agent run 已陷入無效循環。
- 建議的第一版上限可先設為 3 次。

### 達到上限後的處理

當固定提示連續觸發 3 次後仍無改善，系統應：

1. 終止當前 `Scan Agent` run
2. 重置該 agent run 的對話歷史與暫態推理上下文
3. 保留由工具寫入的結構化狀態
4. 以相同輸入重新啟動新的 `Scan Agent` run

### Scan Verifier Agent 的輸入方式

`Scan Verifier Agent` 的 system prompt 是固定的。
它會透過 prompt 佔位符接收穩定不變的上下文，並透過 system-controlled user input 接收每次循環變動的 scan result。

固定 prompt 佔位符為：

- `Repository root path`

每次進入 verifier 時，不論是第一次驗證，或是 scan agent 被退回後重新提交的結果，程式邏輯都應以 system-controlled user input 提供：

- 固定 prefix
- 當前 `ListScanProjects` 結果

固定 prefix 第一版定義為：

```text
The following content is the current scan result from the Scan Agent.
Approve it if acceptable.
Reject it if more work is required, and explain why.
```

### Scan Verifier Agent 的跳轉條件

- 如果 `Approved = true`，scan 階段完成，進入 `Project Plan Agent` loop。
- 如果 `Approved = false`，程式邏輯會把 `Message` 轉成 system-controlled user input，送回原本的 `Scan Agent`，要求它依原因補強。

### Scan / Verifier 回退上限

`Scan Agent` 與 `Scan Verifier Agent` 之間的回退循環也應有上限。

- 建議第一版上限可先設為 3 次。
- 每當 `Scan Verifier Agent` 提交 `Approved = false`，即算一次回退。
- 程式邏輯應記錄目前 scan 階段已發生幾次 verifier rejection。

### 達到回退上限後的處理

第一版先不引入額外 adjudicator。

因此當 scan verifier rejection 達到上限後，系統不再繼續讓 scan / verifier 無限循環，而是採用降級前進：

1. 停止目前 scan / verifier 回退循環
2. 保留最後一次 verifier 的 `Message`
3. 將目前 scan 階段標記為「scan verifier not approved」
4. 仍然進入 `Project Plan Agent` loop，但保留 verifier 未通過狀態

## Project Plan Agent 到 Project Verifier Agent 的跳轉規則

### 基本原則

- `Project Plan Agent` 不自行決定是否進入下一階段。
- 模型的一輪自動對話 / tool loop 結束後，由程式邏輯檢查目前狀態。
- 是否跳轉到 `Project Verifier Agent`，由程式邏輯依據目前 `ProjectPlanTaskItem` 狀態決定。

### 一輪 Project Plan Agent 結束後的狀態檢查

當 `Project Plan Agent` 的當前一輪自動對話與工具呼叫循環結束後，系統應檢查：

1. 目前是否已建立任何 `ProjectPlanTaskItem`

### 跳轉條件

#### 情況 1：已存在 project plan result

- 條件：`ProjectPlanTaskItem` 數量大於 0
- 動作：直接進入 `Project Verifier Agent`

#### 情況 2：不存在任何 project plan result

- 條件：`ProjectPlanTaskItem` 數量等於 0
- 動作：不進入 verifier，退回同一個 `Project Plan Agent`

### 退回 Project Plan Agent 時的固定提示

若發生情況 2，系統應以固定的 user input 重新提示 `Project Plan Agent`，明確告知：

- 本輪結束時沒有建立任何 `ProjectPlanTaskItem`
- 因此目前工作未完成
- 你必須建立至少一筆 `ProjectPlanTaskItem`

此提示應由程式邏輯產生，而不是由模型自行推斷。

### 重試上限與重置策略

若發生情況 2，系統不應無限次以固定提示反覆推進同一個 `Project Plan Agent`。

建議策略如下：

- 當固定提示連續觸發達到上限次數時，視為目前 agent run 已陷入無效循環。
- 建議的第一版上限可先設為 3 次。

### 達到上限後的處理

當固定提示連續觸發 3 次後仍無改善，系統應：

1. 終止當前 `Project Plan Agent` run
2. 重置該 agent run 的對話歷史與暫態推理上下文
3. 保留由工具寫入的結構化狀態
4. 以相同輸入重新啟動新的 `Project Plan Agent` run

### Project Verifier Agent 的輸入方式

`Project Verifier Agent` 的 system prompt 是固定的。
它會透過 prompt 佔位符接收穩定不變的上下文，並透過 system-controlled user input 接收每次循環變動的 project plan result。

固定 prompt 佔位符為：

- `Repository root path`
- `ScanProject`

每次進入 verifier 時，不論是第一次驗證，或是 `Project Plan Agent` 被退回後重新提交的結果，程式邏輯都應以 system-controlled user input 提供：

- 固定 prefix
- 當前 `ListProjectPlanTaskItems` 結果

固定 prefix 第一版定義為：

```text
The following content is the current project plan result from the Project Plan Agent.
Approve it if acceptable.
Reject it if more work is required, and explain why.
```

### Project Verifier Agent 的跳轉條件

- 如果 `Approved = true`，該 `ScanProject` 的 project planning flow 完成，進入後續 review loop。
- 如果 `Approved = false`，程式邏輯會把 `Message` 轉成 system-controlled user input，送回原本的 `Project Plan Agent`，要求它依原因補強。

### Project Planning 到 Review 階段的分派原則

當某個 project planning flow 完成並進入 review 階段後，程式邏輯不應直接把所有 `taskItems x rules` 展開成同時建立的等待 tasks。

第一版應採用以下分派模型：

- 每個 `task item` 先轉成多個 rule-specific work items
- 每個 work item 依 `rule` 進入對應 execution lane 的 queue
- scheduler 只在 worker budget 可用時才啟動新的 flow
- 同一條 `rule` 在任一時間最多只允許一條 running flow
- scheduler 第一版優先挑選：
  1. 目前沒有 running flow 的 `rule`
  2. 在可啟動 rules 中 queue 剩餘數量最多者

這代表：

- 可等待的是 queue 中的 work item，而不是已建立但尚未取得 budget 的 agent tasks
- 不同 `rule` 可以並行
- 同一 `rule` 在不同 `task item` 間不得同時進入 review / report flow
- flow 對應的 agents 應在 scheduler 真正選中該 work item 時才建立
- 外部不應直接組裝 review-stage 內部 workflow，而應透過 `Review Agent Team` 建立單一 `Worker` 來持有共享 budget 與調度狀態
- `Worker` 應在建立時綁定 `repository root path` 與 `rule markdowns`，執行時只透過單一 `AnalyzeAsync` 啟動整段流程
- `Worker` 在生命週期結束時，也應透過明確的 cleanup contract 釋放 team surface 持有的 runtime 資源

### Project Plan / Verifier 回退上限

`Project Plan Agent` 與 `Project Verifier Agent` 之間的回退循環也應有上限。

- 建議第一版上限可先設為 3 次。
- 每當 `Project Verifier Agent` 提交 `Approved = false`，即算一次回退。
- 程式邏輯應記錄目前 project planning flow 已發生幾次 verifier rejection。

### 達到回退上限後的處理

第一版先不引入額外 adjudicator。

因此當 project verifier rejection 達到上限後，系統不再繼續讓 planner / verifier 無限循環，而是採用降級前進：

1. 停止目前 planner / verifier 回退循環
2. 保留最後一次 verifier 的 `Message`
3. 將目前 project planning flow 標記為「project verifier not approved」
4. 仍然進入後續 review loop，但保留 verifier 未通過狀態

## Rule Review Agent 到 Review Verifier Agent 的跳轉規則

### 基本原則

- `Rule Review Agent` 不自行決定是否進入下一階段。
- 模型的一輪自動對話 / tool loop 結束後，由程式邏輯檢查目前狀態。
- 是否跳轉到 `Review Verifier Agent`，由程式邏輯依據 issue / no-issue 狀態決定。

### 一輪 Rule Review Agent 結束後的狀態檢查

當 `Rule Review Agent` 的當前一輪自動對話與工具呼叫循環結束後，系統應檢查：

1. 目前是否存在任何 `RuleReviewIssue`
2. 目前是否存在 `SubmitNoIssueConclusion`

### 跳轉條件

#### 情況 1：存在任何 issue

- 條件：`RuleReviewIssue` 數量大於 0
- 動作：直接進入 `Review Verifier Agent`

#### 情況 2：不存在 issue，但存在 no-issue conclusion

- 條件：`RuleReviewIssue` 數量等於 0，且已成功呼叫 `SubmitNoIssueConclusion`
- 動作：直接進入 `Review Verifier Agent`

#### 情況 3：不存在 issue，也不存在 no-issue conclusion

- 條件：`RuleReviewIssue` 數量等於 0，且未呼叫 `SubmitNoIssueConclusion`
- 動作：不進入 verifier，退回同一個 `Rule Review Agent`

### 退回 Rule Review Agent 時的固定提示

若發生情況 3，系統應以固定的 user input 重新提示 `Rule Review Agent`，明確告知：

- 本輪結束時沒有建立任何 issue
- 也沒有提交 `SubmitNoIssueConclusion`
- 因此目前工作未完成
- 你必須：
  - 建立至少一筆 issue，或
  - 明確提交 no-issue conclusion

此提示應由程式邏輯產生，而不是由模型自行推斷。

### 重試上限與重置策略

若發生情況 3，系統不應無限次以固定提示反覆推進同一個 `Rule Review Agent`。

建議策略如下：

- 當固定提示連續觸發達到上限次數時，視為目前 agent run 已陷入無效循環。
- 建議的第一版上限可先設為 3 次。

### 達到上限後的處理

當固定提示連續觸發 3 次後仍無改善，系統應：

1. 終止當前 `Rule Review Agent` run
2. 重置該 agent run 的對話歷史與暫態推理上下文
3. 保留由工具寫入的結構化狀態，例如既有 issues 與 no-issue 狀態
4. 以相同輸入重新啟動新的 `Rule Review Agent` run

若 repeated missing submission 持續發生，直到超過允許的 agent reset 上限，第一版不應讓整個外層流程直接失敗。

此時應將目前這條 `rule flow` 視為：

- 沒有產出可驗證的 review result
- 以 degraded state 結束
- 保留原因到 workflow state 與可觀測性紀錄

也就是說，這種情況下：

1. 不進入 `Review Verifier Agent`
2. 不進入 `Report Aggregator`
3. 不影響其他 `rule flow` 或其他 `task item` 繼續執行
4. 由外層 orchestration 在容器收斂時，將此 flow 視為一條已結束但未成功提交 review result 的 flow
5. 清理這條 flow 內建立的 agents 與暫態執行狀態，但保留 degraded reason 與可觀測性紀錄

### 為什麼要重置

這個機制的目的，是避免 agent 陷入重複性的推理循環，例如：

- 不斷重述已做過的事情
- 不建立 issue，也不提交 no-issue conclusion
- 被自身前文困住而無法修正行為

在這種情況下，保留工具層的結構化狀態，但重置模型對話歷史，通常比無限追加提示更有機會讓 agent 重新分析。

### 重置後的輸入

重置後重新啟動 `Rule Review Agent` 時，仍應提供相同的：

- repository root path
- rule definition
- scope entry files

另外，系統可選擇補充一段簡短的 system-controlled user input，說明：

- 前一輪未能完成必要提交
- 既有 issue 狀態已保留
- 你需要重新審查並完成必要的 issue 或 no-issue 提交

此補充訊息應保持簡短，不應把前一輪完整對話歷史重新灌回。

### 設計理由

這個跳轉規則的目的，是將「是否完成」從自然語言判斷改成狀態判斷。

也就是說，系統不依賴模型口頭宣稱 review 已完成，而是依據實際工具狀態來判斷：

- 是否已有 issue 被建立
- 是否已有 no-issue conclusion 被提交

只有當上述狀態明確存在時，才允許流轉到 verifier。

若在 repeated missing submission 與 reset 後仍無法形成上述狀態，系統仍應保留這條 flow 的執行結果與失敗事實，而不是讓整個大流程因單一 flow 沒有提交而中止。

## 與其他文件關聯

- Agent design: [Z:\GitHub\CodeSnifferDog\docs\design\agent\architecture\agent-design.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\architecture\agent-design.md)
- Rule review tools: [Z:\GitHub\CodeSnifferDog\docs\design\agent\tools\rule-review-tools.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\tools\rule-review-tools.md)
- Review verifier tools: [Z:\GitHub\CodeSnifferDog\docs\design\agent\tools\review-verifier-tools.md](Z:\GitHub\CodeSnifferDog\docs\design\agent\tools\review-verifier-tools.md)

## Review Verifier Agent 的跳轉規則

`Review Verifier Agent` 完成檢查後，應透過 `SubmitReviewVerdict` 提交結果。
`SubmitReviewVerdict` 只表達驗證是否通過，不負責描述下一步路由。
下一步由程式邏輯根據 `Approved` 與當前 review result 類型決定。

### Review Verifier Agent 的輸入方式

`Review Verifier Agent` 的 system prompt 是固定的。
它會透過 prompt 佔位符接收穩定不變的上下文，並透過 system-controlled user input 接收每次循環變動的 review result。

固定 prompt 佔位符為：

- `Repository root path`
- `Rule definition`
- `Scope entry files`

每次進入 verifier 時，不論是第一次驗證，或是 review agent 被退回後重新提交的結果，程式邏輯都應以 system-controlled user input 提供：

- 固定 prefix
- 當前 review result

固定 prefix 第一版定義為：

```text
The following content is the current review result from the Rule Review Agent.
Approve it if acceptable.
Reject it if more work is required, and explain why.
```

其中當前 review result 應為以下二選一：

- `ListRuleReviewIssues` 列出的所有 `RuleReviewIssue`
- `NoIssueConclusion`

也就是說，verifier 的穩定上下文放在 prompt 佔位符中，而每次循環變動的內容只透過固定 prefix 加上目前最新的 issues 或 no-issue 結論注入。

- 如果 `Approved = true`，且目前 review result 為 `RuleReviewIssue` 清單，程式邏輯將目前 rule flow 推進到 `Report Aggregator`。
- 如果 `Approved = true`，且目前 review result 為 `NoIssueConclusion`，該 rule flow 直接結束，不進入 merge 階段。
- 如果 `Approved = false`，程式邏輯會把 `Message` 轉成 system-controlled user input，送回原本的 `Rule Review Agent`，要求它依原因補強。

### Review / Verifier 回退上限

`Rule Review Agent` 與 `Review Verifier Agent` 之間的回退循環也應有上限。

- 建議第一版上限可先設為 3 次。
- 每當 `Review Verifier Agent` 提交 `Approved = false`，即算一次回退。
- 程式邏輯應記錄目前 rule flow 已發生幾次 verifier rejection。

### 達到回退上限後的處理

第一版先不引入 adjudicator。

因此當 verifier rejection 達到上限後，系統不再繼續讓 review / verifier 無限循環，而是採用降級前進：

1. 停止目前 review / verifier 回退循環
2. 保留最後一次 verifier 的 `Message`
3. 將目前 rule flow 標記為「verifier not approved」
4. 若目前 review result 為 issue 清單，仍然推進到 `Report Aggregator`
5. 若目前 review result 為 `NoIssueConclusion`，直接結束該 rule flow，但保留 verifier 未通過狀態

### 為什麼允許降級前進

第一版的目標是先建立完整自動化鏈路，而不是一開始就完美處理所有 agent 分歧。

在此策略下：

- 有結果比完全沒有結果更有價值
- verifier 不同意這件事不會被隱藏
- 後續彙整、UI 與可觀測性層仍可明確呈現這個 flow 曾經未通過 verifier
- 未來可再升級為更嚴格的停止策略或 adjudicator 機制

這裡的重點是：

- Agent 與 Agent 之間只透過簡單字串交流
- 跳轉、回退、重試次數與狀態管理都由程式邏輯負責
- `Review Verifier Agent` 不直接控制其他 agent，也不直接修改 review issues
- 第一版允許在 verifier 多次未通過後，以降級狀態繼續往下

## Report Aggregator 到 Report Verifier 的跳轉規則

`Report Aggregator` 完成目前這輪聚合後，應結束其當前一輪自動對話 / tool loop。
之後由程式邏輯啟動 `Report Verifier`。

`Report Verifier` 完成檢查後，應透過 `SubmitReviewVerdict` 提交結果。
`SubmitReviewVerdict` 只表達驗證是否通過，不負責描述下一步路由。
下一步由程式邏輯根據 `Approved` 決定。

### Report Verifier 的輸入方式

`Report Verifier` 的 system prompt 是固定的。
它會透過 prompt 佔位符接收穩定不變的上下文，並透過 system-controlled user input 接收每次循環變動的 diff。

固定 prompt 佔位符為：

- `Repository root path`
- `Rule definition`
- `Current flow issues`

每次進入 `Report Verifier` 時，不論是第一次驗證，或是 `Report Aggregator` 被退回後重新提交的結果，程式邏輯都應以 system-controlled user input 提供：

- 固定 prefix
- 當前 `RuleReportDiff`

固定 prefix 第一版定義為：

```text
The following content is the current report diff from the Report Aggregator.
Approve it if acceptable.
Reject it if more work is required, and explain why.
```

也就是說，`Report Verifier` 的穩定上下文放在 prompt 佔位符中，而每次循環變動的內容只透過固定 prefix 加上目前最新的 `RuleReportDiff` 注入。

### Report Verifier 的跳轉條件

- 如果 `Approved = true`，該 rule flow 直接完成。
- 如果 `Approved = false`，程式邏輯會把 `Message` 轉成 system-controlled user input，送回原本的 `Report Aggregator`，要求它依原因補強。

### Report / Verifier 回退上限

`Report Aggregator` 與 `Report Verifier` 之間的回退循環也應有上限。

- 建議第一版上限可先設為 3 次。
- 每當 `Report Verifier` 提交 `Approved = false`，即算一次回退。
- 程式邏輯應記錄目前 rule flow 已發生幾次 report verifier rejection。

### 達到回退上限後的處理

第一版先不引入額外 adjudicator。

因此當 report verifier rejection 達到上限後，系統不再繼續讓 aggregator / verifier 無限循環，而是採用降級完成：

1. 停止目前 aggregator / verifier 回退循環
2. 保留最後一次 verifier 的 `Message`
3. 將目前 rule flow 標記為「report verifier not approved」
4. 仍然將此 rule flow 視為完成，但保留 verifier 未通過狀態

### Flow 完成後的 snapshot 與 cleanup

每次 `Report Aggregator` 開始前，系統應先用該 `rule` 的 latest snapshot 初始化這條 `plan item + rule flow` 自己的 working report。
不同 flow 不可共用同一份 working report 或 diff。
`Report Aggregator` 整個回退循環都應持續操作這條 flow 自己的 working report。
`RuleReportDiff` 應由程式邏輯以「latest snapshot -> 這條 flow 的 working report」計算，而不是拿當前 flow issues 當 baseline。

當 `Report Verifier` 通過，或在達到回退上限後以降級完成方式結束時，系統應：

1. 將當前 working report 升版成該 `rule` 的最新快照
2. 清理本次 `plan item + rule flow` 的 working report、diff 與其他暫態執行狀態
3. 清理這條 flow 內建立的 agents
4. 保留最新快照、repo-level report issue state，以及可觀測性 / 審計所需的執行紀錄

這裡的重點是：

- 刪除暫態 state
- 保留最終結果 state
- 保留行為紀錄與快照，供後續 diff 計算與外部檢視使用

## 變更紀錄

- 2026-04-12：建立 `Rule Review Agent` 到 `Review Verifier Agent` 的執行跳轉規則。
- 2026-04-12：補充 `Review Verifier Agent` 的 verdict-based 跳轉規則。
- 2026-04-12：補充 review / verifier 回退上限與第一版降級前進策略。
- 2026-04-12：補充 `Report Aggregator` 到 `Report Verifier` 的執行規則與 flow 完成後的 snapshot / cleanup 原則。
- 2026-04-19：補充 rule-lane queue scheduling、shared worker budget 與 flow 完成後的 agent cleanup 原則。
