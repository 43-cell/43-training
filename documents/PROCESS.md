# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

-

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

-

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

-

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

-

## 自我驗證（做到哪個階段答哪題）

### 第一階段 — Agentic Coding

練習 1

1. 我能不看筆記說出三個專案（Web/Core/Infrastructure）各自的職責
   - Web：Controller / ViewModel / View，只做轉接與顯示
   - Core：Domain model、service 介面與商業邏輯（折扣、庫存、狀態轉移）
   - Infrastructure：EF Core DbContext、repository、migration、種子資料
2. 我核對過 agent 描述的建單流程，且**至少找出一處不精確或過度簡化的說法**
   - 產生 CLAUDE.md 時，agent 寫「折扣集中在 `OrderService.CalculateTotal`，不要在別處重算」。
     對照 `OrderService.CreateOrderAsync` 後發現這句話過度簡化：Gold 會員的 `unitPrice`
     在建單當下就先打了一次折（`if (customer.Tier == CustomerTier.Gold) unitPrice = ...`），
     `CalculateTotal` 又對 subtotal 打第二次折——折扣邏輯其實出現在兩處，不是只有 `CalculateTotal`。
3. 我知道商業邏輯應該放在哪一層、新增頁面要動哪些地方
   - 商業邏輯放 `OrderHub.Core/Services`，透過 interface 注入給 Controller
   - 新增頁面要動：Controller（轉接）、Service（邏輯）、Repository（如需新查詢）、ViewModel、View、對應測試

練習 2

1. 三個 bug 我都先在頁面上重現過，才開始找程式
   - 客訴 1：建立訂單 #201，第 1 頁看不到，一開始誤記翻到第 11 頁看到，後來核對資料庫發現看錯，實際是第 10 頁看到 #178（最舊的一筆）
   - 客訴 2：訂單 #202（商品原價 160，Gold 會員），一開始口誤講成「原價100/小計100/應付90」，後來重新核對明細頁才確認正確數字是小計 144、應付 129.6
   - 客訴 3：SKU-1001 原始庫存 25，訂單 #205 建立後變 24，取消後仍是 24
2. 我給 agent 的資訊包含具體觀察（頁碼／金額數字／庫存數字），而不是只貼客訴原文
   - 每個 bug 都附了訂單編號、頁碼、金額或庫存的實際數字，agent 遇到數字兜不攏時（客訴 1、2 都發生過）會回頭用 `sqlcmd` 查資料庫核對，而不是照單全收
3. 每個修復都回到頁面驗證過症狀消失
   - 客訴 1：確認第 1 頁看得到 #201、最後一頁不再空白
   - 客訴 2：另建一筆 Gold 新訂單（原價 1420），確認小計 1420、應付 1278（只打一次折）
   - 客訴 3：另建一筆新訂單並取消，確認庫存正確加回
   - 三個都有留意到：已存在的舊訂單/舊資料（#202 的價格快照、#205 卡住的庫存）不會被程式修復回溯更正，需要用新資料驗證
4. 每個 bug 都補了一個回歸測試，`dotnet test` 全綠
   - 三個回歸測試都先在修復前（用 `git stash` 暫時還原成 bug 版本）跑過一次確認會失敗，再套用修復確認轉綠，不是憑感覺寫斷言
   - 最終 `dotnet test`：32 個測試全部通過
5. 三個獨立 commit，message 說明症狀與根因
   - `d0b9ba1` 訂單分頁 off-by-one（`Skip(page * pageSize)` 少了 `-1`）
   - `4390a84` Gold 會員折扣算兩次（建單時 unitPrice 先打一次折，`CalculateTotal` 又打一次）
   - `7526d17` 取消訂單庫存沒回補（`order.Status` 先改成 Cancelled 才檢查狀態，判斷式永遠是 false）
6. （思考題）為什麼原本的測試沒抓到這三個 bug？
   - 客訴 1：`GetOrders_ReportsTotalCountAndTotalPages` 只斷言 `TotalCount`／`TotalPages` 的數字對不對，從沒斷言 `Items` 裡實際回傳的是「哪幾筆」，所以分頁位移算錯也測不出來
   - 客訴 2：折扣相關測試（`CalculateTotal_AppliesTierDiscountOnSubtotal` 等）都是直接建構 `Order`／`OrderItem` 呼叫 `CalculateTotal`，繞過了 `CreateOrderAsync`；唯一走 `CreateOrderAsync` 的快照測試（`CreateOrder_SnapshotsCurrentUnitPrice`）又只用預設 Standard 會員测，Gold 分支從沒被端到端測過
   - 客訴 3：取消訂單的測試只斷言 `order.Status` 變成 Cancelled，沒有任何測試斷言取消後商品的 `StockQuantity`
   - 共同點：每個 bug 都躲在「單元測試各自獨立驗證沒錯，但兩段邏輯兜在一起／副作用沒斷言」的縫隙裡

練習 3

1. `/Products/LowStock` 不帶參數 → 門檻 10 的結果；帶 `?threshold=3` → 結果隨之改變
   - agent 先用 curl 驗證過一輪（不帶參數時 input 顯示 `value="10"`；`?threshold=3` 回 200 且結果不同），我自己在瀏覽器上又確認了一次
2. `?threshold=0`、`?threshold=-1` → 頁面顯示驗證錯誤，不是 500
   - curl 檢查兩者都是 HTTP 200，表單顯示「門檻必須大於 0」，input 保留原本輸入的值（0 / -1），不是 500 錯誤頁
3. 售出數量欄位排除了 Cancelled 訂單（可用一筆已取消的訂單驗證）
   - 對應測試 `GetLowStock_RecentSoldQuantity_ExcludesCancelledOrders`：同商品建一筆 Confirmed（賣 3）、一筆 Cancelled（賣 5），驗證近 30 天售出數量只算 3
4. 停售（已停售 badge）商品不出現在列表
   - 對應測試 `GetLowStock_ExcludesInactiveProducts`：庫存 5、`IsActive=false` 的商品即使低於門檻也不會出現
5. 程式分層與命名跟既有的 Products 功能一致（請 agent 自我 review 一次，並自己確認）
   - agent 自我 review：Controller 只轉接＋映射 ViewModel，「近 30 天」的日期換算放在 service，EF Core 查詢在 repository，View 綁 ViewModel，跟既有 `ProductsController`/`ProductService` 同一套寫法
   - 過程中有一個計畫外的調整：原計畫要用一個 LEFT JOIN 查詢一次撈完，但在 EF Core InMemory 測試環境跑不過（`Nullable object must have a value`，InMemory provider 對 GroupBy+LEFT JOIN 的已知限制），改成兩條查詢（低庫存商品 + 近 30 天銷量分組加總）在記憶體組合，查詢次數固定是 2 次、不是 N+1，agent 有主動講清楚這個落差
6. 至少 3 個新測試，`dotnet test` 全綠
   - 新增 3 個：`GetLowStock_FiltersByThresholdAndSortsByStockAscending`、`GetLowStock_ExcludesInactiveProducts`、`GetLowStock_RecentSoldQuantity_ExcludesCancelledOrders`
   - `dotnet test`：35 個測試全部通過

練習 4

1. 重構後 `dotnet test` 全綠
2. 我能說出這次重構「改善了什麼、沒有改變什麼」
3. 我有在 code review 的角度看過 diff（不是 agent 說好就好）

---

## 附錄：值得留下的對話片段

（貼 1–2 段最有代表性的 prompt 與回應**摘要**——不用貼全文，重點是「我怎麼問」和「它怎麼答」。）
