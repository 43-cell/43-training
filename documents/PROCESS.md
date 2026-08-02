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

### 第二階段 — 活動 2：自建 MCP Server

練習 3（註冊給 agent，做 before/after 對照）

1. Claude Code 輸入 `/mcp` 能看到 orderhub server 與三個工具
   - 有，`get_order`／`low_stock`／`customer_orders` 三個都能列出且能實際呼叫
2. 對照實驗完成且記錄
   - 見附錄片段 6：同一句「哪些商品庫存低於 5?」，沒 MCP 時要讀 `appsettings.json`、確認表名/欄位名、手寫 SQL、排查 `sqlcmd` 中文亂碼共四步；有 MCP 一次工具呼叫拿到乾淨的 UTF-8 結構化結果，兩邊查到的商品/庫存數字一致
3. `.mcp.json`（或 config 片段說明）進 git，一個獨立 commit
   - `10c4048` — 只含 `training-repo/.mcp.json` 這一個檔案

練習 4（會改資料的工具：cancel_order）

1. MCP Inspector 中 `cancel_order` 的 annotations 如所標，三個唯讀工具顯示 read-only
   - **後續補做**：一開始在這個 agent session 裡跑 Inspector 一直失敗（`dotnet run --project src/OrderHub.Mcp` 的 build 輸出被 Claude Code 自己維護的 orderhub 連線鎖住，`MSB3027`），後來換成我自己在本機終端機跑 `npx @modelcontextprotocol/inspector dotnet run --project src/OrderHub.Mcp`（跑之前先在 Claude Code `/mcp` 把 orderhub 斷線釋放檔案鎖，Inspector 連上後才把 orderhub 重新 reconnect 回來），Tools 分頁確認：`cancel_order` 顯示 destructive，`get_order`／`low_stock`／`customer_orders` 三個都顯示 read-only，跟原始碼標註的 `[McpServerTool(Destructive = true, Idempotent = false)]`／`[McpServerTool(ReadOnly = true)]` 一致
2. 對 agent 說「幫我取消訂單 X」：觀察權限確認提示——按允許之前資料不會被動到
   - 確認：呼叫 `cancel_order(206)` 和 `cancel_order(203)` 前，Claude Code 兩次都跳出權限確認對話框，按 allow 後才實際執行
3. 取消一筆待處理訂單成功，回 `/Products` 頁面確認庫存有回補
   - 訂單 203（Pending，客戶陳志明/Gold，品項 SKU-1008×1、SKU-1011×1）：取消前 `SKU-1008` 庫存 51、`SKU-1011` 庫存 90；呼叫 `cancel_order(203)` 回「訂單 203 已取消,庫存已回補」；直接查資料庫核對取消後庫存變 52／91（各 +1），訂單狀態變 `Cancelled`
4. 對同一筆訂單再取消一次、或挑一筆已出貨訂單取消：得到清楚的拒絕訊息而非 exception dump
   - 訂單 206 原本就是 `Cancelled` 狀態，呼叫 `cancel_order(206)` 回「取消失敗:狀態為 Cancelled 的訂單不可取消」，是清楚的文字訊息，不是 stack trace
5. 獨立 commit；PROCESS.md 記錄
   - `e9593ad` — 只含 `OrderHubTools.cs`（新增 `CancelOrder`，並補三個既有工具的 `ReadOnly = true`）

練習 5（MCP 不是只有 tools：Resources 與 Prompts）

1. MCP Inspector：Resources 分頁讀得到 `orderhub://discount-rules`；Prompts 分頁能帶 `threshold` 參數取得展開後的訊息
   - **後續補做**：延續練習 4 的做法，在自己的終端機跑 `npx @modelcontextprotocol/inspector dotnet run --project src/OrderHub.Mcp`（先在 Claude Code `/mcp` 斷開 orderhub 釋放檔案鎖，完成後再重新 reconnect）。Resources 分頁點進 `orderhub://discount-rules`，內容跟 `OrderHubResources.cs` 裡寫的一致（Standard 不打折／Silver 95 折／Gold 9 折）；Prompts 分頁用 `threshold=10` 執行 `low_stock_report`，展開出來的文字跟 `OrderHubPrompts.cs` 裡寫的那段提示一致（提到 low_stock 工具、threshold=10、採購建議表格式要求）——這次是真的走 Inspector 這條路驗證到的，不是像練習 4 一開始那樣繞道用其他工具代替
2. Claude Code：`@` 選 resource 後問折扣問題，agent 用 resource 內容作答
   - 問「Gold 會員買 1000 元商品應付多少?」，agent 讀 `orderhub://discount-rules`（Gold 9 折）算出 900 元，沒有讀 `OrderService.cs`
3. Claude Code：`/mcp__orderhub__low_stock_report` 一鍵產出採購建議表
   - 觸發後展開成 `OrderHubPrompts.cs` 寫的那段指示，agent 呼叫 `low_stock(threshold=10)` 撈出 5 個低庫存商品，並產出 SKU／名稱／現有庫存／建議補貨量／理由的表格（見附錄片段 8，過程中暴露一個工具缺口）
4. PROCESS.md 記錄 5c 第 3 點的思考；獨立 commit
   - commit：`afa0685` — 只含 `Program.cs`（註冊 `WithResources`／`WithPrompts`）、`OrderHubResources.cs`、`OrderHubPrompts.cs`
   - 思考題——折扣規則用 Resource 給，和讓 agent 自己去讀 `OrderService.cs`，差在哪？
     Resource 是團隊共用、版本控制過的單一事實來源：規則只寫一次在 `DiscountRules()`，改版時只要改這一個地方，不管接上這個 MCP server 的是 Claude Code 還是 Codex，下次讀到都是最新版；讓 agent 自己讀程式碼則每次都要重新解析，商業邏輯一旦散落多處（像活動 1 客訴 2 發現的「Gold 折扣算兩次」那樣），不同次問同樣問題可能因為讀到的程式碼片段或理解角度不同而給出不一致答案
   - prompt 範本放在 server，和每個人自己打一段話，差在哪？
     範本放 server 上等於把「這句話該怎麼問」也版本控制、團隊共用——採購同事不用自己記得或重新想那段「請用 low_stock 工具…」的咒語，直接打 `/mcp__orderhub__low_stock_report` 就好；範本要調整（例如門檻改 15、報告欄位改格式）時只要改一個地方，所有人下次呼叫就是新版本。每個人自己打一段話則問法不一致、品質參差，也沒人知道「標準問法」是什麼

---

## 附錄：值得留下的對話片段

### 片段 1：客訴 1（訂單分頁）的除錯過程

**我怎麼問**：沒有直接複製客訴原文，先自己在頁面上重現、再報具體數字——「訂單編號是 #201，第 1 頁沒看到，翻到第 11 頁才找到」。

**它怎麼答**：從 `OrdersController` 追到 `OrderRepository.GetPagedAsync`，抓到 `Skip(page * pageSize)` 少了 `-1`；但依總筆數反推發現「第 11 頁理論上應該是空的，不可能撈到最新那筆」，主動指出跟我說的數字兜不攏、沒有照單全收，接著直接用 `sqlcmd` 查資料庫核對，發現是我記錯頁碼——實際是在第 10 頁看到全庫最舊的 #178，不是 #201。修復後也是先用 `git stash` 暫時還原成 bug 版本、跑一次確認新測試真的會失敗，才正式套用修復並要我回頁面驗證。

### 片段 2：練習 3 的「先計畫、再實作」prompt

**我怎麼問**：貼上完整規格後明講「先不要寫程式，請給我一份實作計畫」，並列出四個必須回答的問題（檔案清單與職責、每層怎麼分工、近 30 天銷量放哪一層會不會 N+1、threshold 驗證用什麼機制），還指定「動手前先讀 ProductsController/ProductService/Index.cshtml，沿用同一套慣例，不要自創寫法」。

**它怎麼答**：先讀完指定的檔案，再進 Plan Mode 寫出對應四點的完整計畫（含實際 LINQ 查詢草稿），核准後才開始寫程式；套用計畫時發現原本規劃的單一 LEFT JOIN 查詢在 EF Core InMemory 測試環境會炸掉，agent 主動講清楚這個落差、改成兩條查詢的替代方案（並解釋為什麼還是沒有 N+1），而不是悄悄改掉不講。

### 片段 3：接上 Playwright MCP 後，「親手重現」不再是人專屬的步驟

**練習 2 當時的做法**：`activity-guideline.md` 的建議流程第一步是「①在頁面上親手重現」——三個客訴都是我自己在瀏覽器點 `/Orders`、翻頁、建單、取消訂單，把看到的頁碼／金額／庫存數字記下來，再打字報給 agent（例如客訴1報「翻到第11頁看到#201」）。當時 agent 沒有瀏覽器能力，這一步只能由人做。

**這次的做法**：裝了 `claude mcp add playwright -- npx @playwright/mcp@latest` 之後，直接請 agent 用 Playwright MCP 打開 `localhost:5150`，agent 自己跑 `browser_navigate` 開站（跳轉到 `/Orders`）、`browser_snapshot`／`browser_take_screenshot` 看畫面、`browser_click` 點進 `#206` 訂單明細、再點「查看此客戶的所有訂單」連結——整條瀏覽器操作路徑不需要我動手點。

**對比**：練習2的「①在頁面上親手重現」當時是人專屬的步驟，因為 agent 沒有瀏覽器；現在這一步 agent 自己就能做（開頁、點連結、截圖）。但「該重現哪個症狀」「畫面顯示的數字對不對」這種判斷仍然要人把關——agent 接手的是「動手操作瀏覽器」這個體力活，不是判斷本身。

### 片段 4：用 Playwright MCP 重跑練習2客訴3的驗收步驟（庫存回補回歸測試）

**做法**：延續片段3的能力，這次直接請 agent 用瀏覽器把練習2客訴3當時的驗收流程「另建一筆新訂單並取消，確認庫存正確加回」自己走一遍：`browser_select_option` 選客戶＋商品（SKU-1001，建單前庫存 25）、`browser_click` 送出訂單（產生 #207）→ 開 `/Products` 截圖確認庫存變 24 → 回 `#207` 明細頁點「取消訂單」、`browser_handle_dialog` 接受確認對話框 → 再開 `/Products` 截圖確認庫存回補到 25。

**結果**：25 → 24（建單後）→ 25（取消後），跟 `7526d17`（取消訂單庫存沒回補的修復）當時的回歸測試邏輯一致，只是這次驗收動作全由 agent 用瀏覽器完成，不是我自己點。

**跟片段3的差異**：片段3是「重現症狀」自動化，這次是「驗收修復」自動化——同一套 Playwright MCP 能力，用在練習2流程的最後一步（⑤回到頁面確認），而不是第一步。

### 片段 5：每次 commit 後，agent 自己跑一輪全站 smoke test

**做法**：這次 session 每 commit 一次 `PROCESS.md`／`.gitignore` 變動，就請 agent 用 Playwright MCP 依序打開 `/Orders`、`/Products`、`/Products/LowStock`、`/Customers`、`/Orders/Create`、`/Orders/Details/206`、`/Customers/9/Orders` 這幾條主要路徑，逐頁截圖確認畫面正常，最後再跑一次 `dotnet test` 當最終回歸關卡。

**意外收穫**：中途我自己記錯／亂猜了兩條不存在的巢狀路由 `/Products/9/Orders`、`/Customers/9/Orders/207`（正確應該是 `/Customers/9/Orders`、`/Orders/Details/207`），agent 兩次都直接回報 HTTP 錯誤（`net::ERR_HTTP_RESPONSE_CODE_FAILURE`），沒有含糊地說「頁面正常」，也沒有自己亂猜路由硬套——等於順便驗證了不存在的路由會乾淨地回 404，不是 500 或意外行為。

**跟片段3、4的關係**：片段3是重現症狀自動化、片段4是驗收單一修復自動化，這次是把「commit 後全站巡一輪＋跑測試」這個原本要人手動點過一輪的收尾動作也交給 agent，而且過程中 agent 對「路由到底存不存在」這件事沒有照單全收我的猜測，是直接讓瀏覽器的真實回應說話。

### 片段 6：接上 OrderHub MCP 後，同一句問題的 before/after 對照

**我怎麼問**：兩次都是同一句「哪些商品庫存低於 5?」，沒帶任何額外提示。

**沒有 MCP 時（模擬關掉 `.mcp.json` 之後 agent 只能怎麼答）**：agent 得先讀 `appsettings.json` 找連線字串（`Server=localhost;Database=OrderHubTraining;...`），再回頭確認 `Products` 表跟 `StockQuantity`／`IsActive` 欄位名稱，才能手刻一句 SQL：
```
sqlcmd -S localhost -d OrderHubTraining -E -Q "SELECT Sku, Name, StockQuantity FROM Products WHERE StockQuantity < 5 AND IsActive = 1 ORDER BY StockQuantity ASC;"
```
中途多踩了一個坑：`sqlcmd` 預設輸出編碼把商品中文名稱印成亂碼（SKU 跟庫存數字正常，`Name` 欄位變成看不懂的符號），得另外處理編碼才看得懂完整結果。前後共走了「讀設定檔 → 讀程式碼確認表名/欄位名 → 手寫 SQL → 排查中文編碼」四步。

**開啟 MCP 後**：直接呼叫 `mcp__orderhub__low_stock({ threshold: 5 })`，一次工具呼叫拿到結構化 JSON，五筆商品的 Sku／正確 UTF-8 中文名稱／庫存量一次到位，不用猜表名或欄位名，也沒有編碼問題。

**結果比對**：兩邊查到的商品與庫存數字完全一致——`SKU-1048`/2、`SKU-1005`/3、`SKU-1023`/3、`SKU-1014`/4、`SKU-1032`/4，只是沒 MCP 那邊要多繞四步而且中文名稱是亂碼，有 MCP 一次工具呼叫就乾淨拿到全部欄位。

### 片段 7：cancel_order 這個「會改資料的工具」——連線卡住的除錯過程，以及成功/失敗兩條路徑的實測

**背景**：`.mcp.json` 裡 `orderhub` 是 stdio server，Claude Code 會在連線/reconnect 時自己 `dotnet run --project src/OrderHub.Mcp` 把它 spawn 起來——這件事我原本不知道，所以在 `CancelOrder` 加進 `OrderHubTools.cs` 之後，我自己也手動在另一個終端機跑了同一行指令想確認能不能編譯。

**卡住的過程**：新工具遲遲不出現在 agent 的工具清單裡，反覆 `/mcp` reconnect 都沒用。查下去發現 `bin/Debug/net8.0` 底下的 build 輸出被一個叫 `OrderHub.Mcp` 的 process 鎖住（`MSB3026` 錯誤，retry 9 次後編譯失敗）——agent 一開始誤判那個 process 是自己之前測試留下的殘留，直接把它 kill 掉，結果反而斷開了當下真正在用的 orderhub 連線（連 `get_order`／`low_stock` 都跟著斷線）。後來才確認：問題根源是我自己手動跑的 `dotnet run` 跟 Claude Code 自動 spawn 的那個 process 同時搶同一份 build 輸出的檔案鎖。

**修好的方法**：把我手動跑的那個終端機 process 停掉，只留 Claude Code 自己管理的那一個，`/mcp` 重新 reconnect 後 `cancel_order` 才正常出現在工具清單。

**成功路徑實測**：訂單 203（Pending，客戶陳志明/Gold，品項 SKU-1008×1、SKU-1011×1）。取消前查資料庫：`SKU-1008` 庫存 51、`SKU-1011` 庫存 90。呼叫 `cancel_order(203)` 前 Claude Code 跳出權限確認對話框，按 allow 後才實際執行，回傳「訂單 203 已取消,庫存已回補」；再查資料庫核對，兩個品項庫存分別變 52／91（各 +1），訂單狀態變成 `Cancelled`。

**失敗路徑實測**：訂單 206 原本就是 `Cancelled`。呼叫 `cancel_order(206)` 同樣先跳權限確認，按 allow 後執行，回傳「取消失敗:狀態為 Cancelled 的訂單不可取消」——是一句清楚的文字訊息，不是 exception dump 或 stack trace。

**跟片段 6 的差異**：片段 6 的三個工具都是唯讀的，agent 頂多「答錯」；`cancel_order` 是第一個會改資料庫的工具，這次實測到的重點不是「答案對不對」，而是「執行前有沒有人工確認、失敗時錯誤訊息夠不夠讓 agent 停手而不是瞎猜重試」——兩者都有做到。

### 片段 8：Resource 與 Prompt 實測——折扣問答免讀程式碼，採購建議表卻暴露一個工具缺口

**Resource 實測**：`/mcp` reconnect 後，用 `@` 附加 `orderhub` 的「會員折扣規則」resource，問「Gold 會員買 1000 元商品應付多少?」——agent 直接讀 `orderhub://discount-rules` 的內容（Gold 9 折）算出 900 元，全程沒有去讀 `OrderService.cs`。

**Prompt 實測的小插曲**：第一次觸發時打成 `/orderhub:low_stock_report low_stock`（多帶了一個參數），回傳 `McpError -32603`；改用正確的 `/mcp__orderhub__low_stock_report`（不帶參數，走 threshold 預設值 10）才成功展開成 `OrderHubPrompts.cs` 裡寫的那段指示。

**執行過程中暴露的工具缺口**：展開的指示要 agent「用 low_stock 工具撈出低庫存商品，再用其他工具了解這些商品的近期訂單狀況」——撈出 5 個低庫存商品（SKU-1048/1005/1023/1014/1032）後，發現 orderhub 目前**沒有任何一個工具能回答「哪些訂單包含這個商品」**：`get_order` 只能查單筆訂單、`customer_orders` 只能查某個客戶的訂單，都無法反查商品。最後是直接查資料庫、比照 `/Products/LowStock` 頁面同一套「近 30 天售出量、排除 Cancelled」邏輯，才算出每個商品的銷售速度並產出補貨建議（例如曜石機械鍵盤庫存 4、近 30 天賣 18 件，庫存撐不到一週，優先度最高；極光筆電支架庫存 3 但近 30 天賣 0 件，標記「先查原因」而非照公式硬補）。

**跟片段 6、7 的關係**：片段 6 是「有沒有工具」的差異、片段 7 是「工具會不會改資料」的差異，這次是「prompt 範本寫的指示，超出了現有工具的能力範圍」——`low_stock_report` 這個 prompt 假設有辦法查到商品的近期訂單，但對應的 tool 從沒被造出來，等於提前寫好了一個工具還沒跟上的範本。這正好呼應練習 5 自己要想的思考題：如果「低庫存採購建議」是團隊常態需求，下一步該補的可能不是更好的 prompt 文字，而是一個真正的 `GetRecentOrdersByProduct` 之類的 tool。
