# PROCESS.md — 我的練習心得

> 一個原則：**寫「具體發生的事」，不寫感想文。**
> 貼上當時真實的 prompt、真實的數字、真實的錯誤訊息——三個月後的你（和你的同事）才用得上。

#### 使用的 agent 與模型：Claude Code（Sonnet 5）

---

## 通用四問

### 1. 我的任務拆解

（開工前你把任務拆成哪幾步？實際做的時候順序有變嗎？為什麼變？）

- 照 `activity-guideline.md` 的順序拆：練習 1（agent 初始設置：CLAUDE.md、settings.json 權限、hooks、subagents、fix-bug skill）→ 練習 2（3 個 bug，每個都走「重現→報數字→定位根因→確認→修復→回頁面驗證→補回歸測試→獨立 commit」）→ 練習 3（先計畫、Plan Mode 核准後才實作）→ 練習 4（重構前先講計畫、核准後才動手）
- 實際做的時候多了一個原本沒排進去的步驟：練習 2 的客訴 1、2 都發生「我報的數字」跟「agent 從程式碼推算的結果」對不上，兩次都靠直接用 `sqlcmd` 查資料庫核對數字才解開，這個「查資料庫核對」後來變成處理每個 bug 的固定動作，不是一開始就計畫好的

### 2. AI 幫上大忙的地方

（哪件事 agent 做得又快又好？**貼上當時的提問原文**，說明為什麼這樣問有效。）

- 練習 3 開場貼的 prompt：「我要新增『低庫存警示頁面』，規格如下（貼上上面整段規格）。先不要寫程式，請給我一份實作計畫，包含：要新增/修改哪些檔案，逐一列出路徑，並說明每個檔案的職責／每層怎麼分工／『近 30 天售出數量（排除 Cancelled）』打算在哪一層、用什麼查詢算，會不會有 N+1／threshold 驗證放哪一層、用什麼機制／打算補哪 3 個 service 單元測試，各驗證什麼。動手前先讀 ProductsController、ProductService/IProductService、Views/Products/Index.cshtml，沿用同一套慣例，不要自創寫法。」
- 有效的原因：把「要交付的答案要長什麼形狀」（逐檔案職責、分層分工、N+1 討論、驗證機制、3 個測試的驗證點）跟「動手前要先讀哪些參考檔案、沿用哪套慣例」都寫死，agent 產出的計畫每一條都能直接對照規格勾選，不用我自己再拆解規格

### 3. AI 誤導我的地方，與我如何發現

（agent 說錯／改錯／過度自信的時刻。你靠什麼抓到——對照程式碼？頁面實測？跑測試？）

- 比較常發生的其實是反過來：是「我」把重現觀察到的數字報錯，agent 沒有照單全收——客訴 1 我一開始說「翻到第 11 頁找到 #201」、客訴 2 我一開始說「原價 100/小計 100/應付 90」，兩次都跟 agent 從程式碼推算或資料庫查出來的數字對不上，agent 主動把落差攤出來問我，我才發現自己記錯或看錯（實際是第 10 頁看到 #178、商品原價其實是 160）
- 真要挑 agent 自己出的岔：練習 3 一開始規劃用一個 LEFT JOIN 查詢把近 30 天銷量一次撈完，套用後在 EF Core InMemory 測試環境直接炸掉（`Nullable object must have a value`）——這是 InMemory provider 對 GroupBy+LEFT JOIN 組合的已知限制，agent 事前沒預見到，是跑測試失敗才發現，發現後有主動講清楚原因並改成兩條查詢的替代方案，沒有含糊帶過

### 4. 我會帶回日常工作的一招

（一個具體、可複製的做法，不要寫「要多驗證」這種口號——寫出**操作步驟**。）

- 「回報的數字跟程式邏輯對不上時，直接查資料庫核對，不要在假設上面繼續來回猜」，操作步驟：
  1. 先從程式碼推算「照這個邏輯，結果應該是多少」
  2. 如果推算結果跟人工回報的觀察兜不攏，用 `sqlcmd`（或對應的 DB CLI）直接查該筆資料的實際欄位值
  3. 把查到的真實數字攤開來對照，而不是各自堅持自己記得的版本
- 這招在這次對話裡兩次直接抓出「原來是我看錯/記錯」，比反覆用文字互相猜測快很多，也比盲目相信任一方的口頭數字可靠

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
   - 35 個測試全部通過（含練習 2、3 補的回歸測試）；`dotnet build` 一開始因為 Visual Studio 開著網站鎖住 Web 專案的 dll/pdb 而失敗，停掉 VS 裡跑的網站後重新建置成功、0 警告 0 錯誤
2. 我能說出這次重構「改善了什麼、沒有改變什麼」
   - 改善：把 `CreateOrderAsync` 裡「請求本身合不合法」（客戶存在、明細非空、數量、重複商品）跟「逐行商品能否成立＋扣庫存」兩種不同性質的檢查，拆成 `ValidateRequestShape` 和 `ValidateLineAndReserveStock` 兩個獨立、可以各自看懂的私有方法，主體邏輯縮短很多
   - 沒有改變：錯誤訊息文字、檢查順序（客戶→明細非空→數量→重複→逐行商品/庫存）、多筆錯誤累積的方式、public 介面（`IOrderService`/`ICustomerService` 沒動），沒新增檔案或類別
3. 我有在 code review 的角度看過 diff（不是 agent 說好就好）
   - commit 前用 `git diff` 看過完整改動範圍，確認只動了 `OrderService.cs` 一個檔案，且兩個抽出來的方法邏輯跟原本逐行比對過是一致的

---

## 附錄：值得留下的對話片段

### 片段 1：客訴 1（訂單分頁）的除錯過程

**我怎麼問**：沒有直接複製客訴原文，先自己在頁面上重現、再報具體數字——「訂單編號是 #201，第 1 頁沒看到，翻到第 11 頁才找到」。

**它怎麼答**：從 `OrdersController` 追到 `OrderRepository.GetPagedAsync`，抓到 `Skip(page * pageSize)` 少了 `-1`；但依總筆數反推發現「第 11 頁理論上應該是空的，不可能撈到最新那筆」，主動指出跟我說的數字兜不攏、沒有照單全收，接著直接用 `sqlcmd` 查資料庫核對，發現是我記錯頁碼——實際是在第 10 頁看到全庫最舊的 #178，不是 #201。修復後也是先用 `git stash` 暫時還原成 bug 版本、跑一次確認新測試真的會失敗，才正式套用修復並要我回頁面驗證。

### 片段 2：練習 3 的「先計畫、再實作」prompt

**我怎麼問**：貼上完整規格後明講「先不要寫程式，請給我一份實作計畫」，並列出四個必須回答的問題（檔案清單與職責、每層怎麼分工、近 30 天銷量放哪一層會不會 N+1、threshold 驗證用什麼機制），還指定「動手前先讀 ProductsController/ProductService/Index.cshtml，沿用同一套慣例，不要自創寫法」。

**它怎麼答**：先讀完指定的檔案，再進 Plan Mode 寫出對應四點的完整計畫（含實際 LINQ 查詢草稿），核准後才開始寫程式；套用計畫時發現原本規劃的單一 LEFT JOIN 查詢在 EF Core InMemory 測試環境會炸掉，agent 主動講清楚這個落差、改成兩條查詢的替代方案（並解釋為什麼還是沒有 N+1），而不是悄悄改掉不講。
