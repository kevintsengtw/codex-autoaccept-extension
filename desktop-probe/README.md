# Codex Windows UIA Probe 0.2.1

下載 `codex-uia-probe-windows` ZIP 後，請先看 **[獨立操作指南](../docs/CODEX_UIA_PROBE_WINDOWS.md)**：包含安裝、三種模式、O／F／Q 操作、報告位置、參數與疑難排解。下文保留進階與歷史驗證細節。

目前本機候選為 0.3.10 R11，使用與下載請先閱讀根目錄 README 及 `tray/README.md`。本機 App 的 24 個背景／最小化案例已通過，且未觀察到搶焦點；公司版本與鎖定未驗證，詳見 `WINDOWS_TEST_RESULTS_20261014_R11.md`。下列保留舊診斷 CLI 說明，日常使用不需操作。共享核准核心使用 `CUIAutomation8` 與 `AutoSetFocus=false`，沿用唯讀 provider 更新；不需重啟 App。正常 ON 時自動寫中文 log 與短摘要，參閱 `LOG_FORMAT.md`。

公司 Windows 環境用的唯讀探測工具，目標是確認 ChatGPT Desktop App（Codex）`26.803.81509` 的核准介面是否提供可用的 Windows UI Automation 資訊。這個版本號由使用者提供，不代表工具已在該版本實測。

**一般探測模式不點擊、不送鍵、不呼叫 Invoke、不核准。另提供需手動確認的單次 Invoke 模式，以及 0.2.1 的 ON／OFF 自動模式。所有模式均不提權、不修改 App／Codex 設定，也不連網。** 只讀取你指定視窗的控制項樹、程序版本和權限層級，在本機寫入報告。探測時會啟用 UIA 查詢；目標 App 的 accessibility provider 可能因此開始產生控制項資料，但工具不更改其 accessibility 啟動設定。

公司實測（2026-10-12）：唯讀探測及手動單次 Invoke 已成功。使用者後續澄清 0.2.0 在核准一次後會切 OFF，不符合持續自動核准需求；0.2.1 已由使用者確認只按一次 O 後，連續三次「允許一次」皆自動核准且保持 ON，按 F 後停止自動核准。

## 0.2.1：ON／OFF 自動核准測試版

先解壓完整 ZIP，從**一般權限** PowerShell 執行：

```powershell
.\Run-Auto-Accept.cmd
```

人工選取 ChatGPT 視窗後，工具預設 **OFF**。保持 Terminal 開啟，切回這個 Terminal 按下以下按鍵即可操作（不用 Enter）：

| 按鍵 | 操作 |
| --- | --- |
| `O` | ON，允許自動核准 |
| `F` | OFF，停止新核准 |
| 空白鍵 | 切換 ON／OFF |
| `Q` | OFF 並退出 |

這些是 Terminal 內的按鍵，不是全域熱鍵，也沒有系統匣圖示。每次啟動皆為 OFF，不保存 ON 到下次執行；OFF 期間不做 UIA 掃描。ON 會處理目前已顯示的核准及後續核准，不需逐次輸入 `INVOKE ONCE`。不依賴 Node.js、不更改 Codex／App 權限設定，預設深度 60。

**核准範圍：你選取的視窗內，唯一的「允許一次」Button（精確 Name、enabled、onscreen、Invoke 可用）。** 切換該 App 視窗裡的聊天，也會改變監聽對象。這版尚無聊天 ID 綁定、命令白名單／拒絕規則，也不能區分命令、檔案或其他同名核准按鈕的請求種類；UIA 能按下按鈕並不提供完整請求的語意資訊。它不具有 VS Code 版 RPC 規則的判斷能力。

工具每輪完整掃描所選視窗，確認後再重掃，並於 Invoke 前重新檢查目標及局部內容。O／F 都會更新啟用代次，先前掃描結果不能跨過 OFF → ON 繼續使用。**OFF 可撤銷尚未承諾開始的操作；已進入 ATTEMPT 的一次 Invoke 可能仍會完成，無法撤回。**

**ON 是持續模式：成功核准後仍保持 ON，繼續等待下一次核准；不因 5 秒內沒有觀察到元素移除就切 OFF。** 若原按鈕仍可見，保持 ON 並等待，不連點同一個請求。讀取／畫面重繪錯誤同樣保留開關，等待重掃；不完整的掃描不會作為請求已結束的證據。

原元素失效，或在 App 視窗可見且未最小化時觀察到按鈕隱藏，可開啟下一個核准週期；兩次完整、有效代次的掃描均沒有任何可見的同名 Button 也可開始下一週期。停用但仍可見的按鈕仍視為舊請求，不能只因 disabled 就重試。最小化／整個 App 視窗隱藏不會被當成請求結束。

同一個核准週期內最多嘗試一次，OFF → ON 不能清除該次嘗試。確認舊核准區塊離開後，才清除該週期的去重紀錄，讓 App 重用相同 RuntimeId／按鈕容器時，下一次核准仍可自動處理。UIA 仍沒有真實請求 ID；如果兩次請求間沒有可觀察的離開過程，工具會保持 ON 並等待，不把文字變動直接當成新請求。

本機輸出與紀錄：

- `STATUS: OFF`／`STATUS: ON`：開關狀態。
- `ATTEMPT: AUTO_INVOKE_ONCE`：一次核准已承諾開始。
- `INVOKE: RETURNED`：呼叫返回，仍需後續觀察。
- `OBSERVATION: ORIGINAL_ELEMENT_REMOVED`／`ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW`：舊區塊離開，可以處理下一次核准。
- `AUTO: CONTINUING`：開關保持 ON。
- `AUTO: WAITING FOR PREVIOUS APPROVAL TO LEAVE`：保持開關、等待舊請求離開，不連點。
- `AUTO: READ/ACTION ERROR`：開關保持原狀態，等待重掃，不重試已承諾的 Invoke。

自 0.3.7 起，中文階段紀錄與 `-summary.txt` 短摘要留在 `%LOCALAPPDATA%\CodexUiaProbe\auto-*.txt`，不寫入命令、UI 文字或指紋，不需送出公司。Q 或關閉 Terminal 會停止 helper；已送出的動作仍可能完成。Invoke 逾時會結束 helper，console 可顯示上一次 ON，但 helper 已退出；請查看最後的 TIMEOUT 和 exit code，重新執行時預設 OFF。

建議驗收：先在 OFF 觸發測試核准，確認仍等待人工處理；按 O 確認它自動解除並繼續；**保持 ON 不再按 O，連續觸發至少三次核准並確認開關沒有自動切 OFF**；再按 F，確認後續核准留給人工處理。不要把完整報告送出公司，若允許描述診斷資訊，只需回覆這幾個動作是否符合預期。

## 0.1.2：手動單次 Invoke 驗證

一般探測使用 `Run-Probe.cmd`，仍然唯讀。手動測試使用新的 `Run-Manual-Once.cmd`：

```powershell
.\Run-Manual-Once.cmd
```

1. 在公司一般權限的 ChatGPT App 留住「允許一次」核准畫面，用原本就願意核准的測試操作驗證。
2. 從一般權限 PowerShell 執行上面的指令，選取 ChatGPT 視窗。請將整個 ZIP 解壓，兩支 EXE 和其 config 都放在同一目錄。
3. 手動模式預設深度 60、3000 節點。兩支程序與目標 App 均以一般權限執行；helper 會檢查自身及目標完整性層級為 medium，未知或不符時不操作。
4. 完整掃描後，只接受一個 Name 精確為「允許一次」、Button 類型、已啟用、未 offscreen、宣告可 Invoke 的候選。沒有、超過一個、讀取失敗或超出掃描範圍時，均停止。
5. 顯示所選視窗及按鈕 immediate parent／同層控制項的局部文字，最多 200 字元／控制項，只在公司電腦 console 顯示。鄰近文字可能不包含命令，**請同時在 ChatGPT 畫面確認待核准操作**。這不是完整命令的規則檢查。
6. 想取消可直接 Enter。要核准才輸入精確的 `INVOKE ONCE`，再 Enter。沒有命令列 yes 參數或自動確認。
7. 確認後完整重掃，核對 HWND／PID／程序啟動時間／視窗 RuntimeId，以及按鈕 RuntimeId、父層和局部文字指紋。呼叫前再次檢查。目標改變時取消本次操作，不自動挑選新按鈕。
8. 最多呼叫一次 Invoke。錯誤、逾時、重新出現按鈕都不重試。Invoke 返回後觀察原按鈕最多 5 秒，顯示本機狀態。

這是你輸入確認後會**實際按下單次允許**的測試模式。UIA 不提供原子化的「比對核准請求並核准」交易，重新檢查仍不能完全排除畫面在呼叫瞬間變動；確認後請不要切換聊天或手動處理另一個核准。

本機結果：

| 輸出／exit code | 意義 |
| --- | --- |
| `CANCELLED`／0 | 未呼叫 Invoke |
| `INVOKE: RETURNED` | Invoke 已返回，單憑返回不代表核准成功 |
| `BUTTON_UNAVAILABLE_OR_HIDDEN`／0 | 原按鈕已失效、隱藏、停用或改名；仍請人工確認操作有繼續 |
| `NOT_INVOKED` 或 `TARGET_CHANGED`／4 | 沒有操作，請檢查候選數及目標狀態 |
| `BUTTON_STILL_AVAILABLE` 或 `UNKNOWN_AFTER_ATTEMPT`／5 | 結果不確定，先看 ChatGPT，再決定是否重新測試 |
| `TIMEOUT`／3 | 該階段逾時；若在 single invocation 階段，即使沒有返回也可能已生效，不要直接重試 |

手動 helper 的讀取／操作階段有逾時；人工確認等待沒有逾時。逾時會結束 helper 自己的程序，不結束 ChatGPT。已送到 provider 的動作無法保證撤回，因此確認是否生效必須看 App。

手動狀態紀錄存於 `%LOCALAPPDATA%\CodexUiaProbe\manual-*.txt`，只含時間、固定中文階段與結果，不保存標題、鄰近文字、命令或指紋。整份報告不必帶出公司；若公司允許描述診斷資訊，只需告知 `INVOKE`／`OBSERVATION` 的狀態，以及你是否看到核准解除、操作繼續。

## 0.1.1：本機摘要，不需送出報告

完成掃描後，畫面及 `.summary.txt` 會顯示權限層級、節點數、可能核准按鈕數、Invoke 宣告數、相鄰可讀文字數、讀取錯誤、失效控制項及掃描限制。摘要不包含控制項 Name、命令、路徑、對話或原始錯誤訊息。完整報告仍留在本機，不需要帶出公司。

若你已有 0.1.0 的報告，不必重新觸發核准，可用新 EXE 在公司電腦讀取既有報告：

```powershell
.\CodexUiaProbe.exe --summarize "既有報告的完整路徑.jsonl"
```

此模式只讀取本機報告、顯示摘要，不使用 UIA、不重寫舊檔。Exit code 0 表示摘要成功顯示，不表示舊的探測完整。

`Probe exit code: 2` 是 **部分結果**，不代表 EXE 被阻擋或 UIA 不可用。摘要裡的 `Read errors`、`Depth boundaries reached`、`Node budget reached` 可區分原因；`stale-element exceptions` 可能來自畫面重繪。只顯示 `COMException` 等一般錯誤時，無法單憑錯誤類別判定為權限不足。

`Approval label BUTTON candidates` 只計算 Button 類型的文字匹配；`Candidates declaring Invoke` 只記錄 provider 宣告，未實際操作。`readable adjacent non-button labels/text` 只表示同父層有可讀文字，不保證那些文字是命令內容。如果需要討論結果，在公司允許的前提下，只需描述摘要中的數字與狀態，不必傳送完整報告。

## 執行條件與成品

- Windows 10／11，已安裝 .NET Framework 4.8 或 4.8.1。這是 Windows 的 .NET Framework，與 .NET 8／9／10 runtime 不同。
- 使用一般使用者身分，與 Codex App 位於同一個互動式桌面。manifest 為 `asInvoker`、`uiAccess=false`，不要求 administrator。
- 解壓縮後直接執行 EXE，無需 Node.js、Python、PowerShell 腳本、VS2022 或下載 NuGet。建置才需要 SDK／NuGet。
- 公司應允許這支自製 EXE 及其唯讀 worker 子程序啟動。免安裝不代表公司一定允許執行；若端點防護阻擋，保留錯誤交由公司處理。
- EXE 未經 Authenticode 簽署；SHA-256 僅用於核對檔案。缺少 runtime 或公司阻擋時，不要改走提權流程，先記錄錯誤。

ZIP 裡的 EXE 是在 macOS 用 .NET SDK 交叉建置的 Windows .NET Framework 成品。**唯讀探測與手動 Invoke 已由使用者在公司版本驗證；0.2.1 持續 ON、連續三次核准及 F 停止也已由使用者確認成功。**

## 建議的兩次探測

先開啟一般權限的 Codex App，再執行工具。不要用管理員 VS2022 或管理員 Windows Terminal 開啟探測工具。

### 1. 一般畫面

1. 將 ZIP 解壓到公司允許執行的使用者目錄，保留 EXE 和 `.exe.config` 在同一資料夾。
2. 雙擊 `Run-Probe.cmd`，或在一般權限 PowerShell 執行：

   ```powershell
   .\CodexUiaProbe.exe
   ```

3. 工具列出候選視窗，包括 HWND、PID、程序名稱、標題。輸入對應 **Codex App** 的編號；候選只是名稱篩選，也可能包含 VS Code 或 Terminal，請人工確認。Enter 可取消。
4. 工具有 5 秒準備時間。可以切回 Codex App，保持視窗展開。
5. 結束後會顯示 `.jsonl` 和 `.jsonl.txt` 報告的完整路徑。預設保存在 `%LOCALAPPDATA%\CodexUiaProbe`，每次產生新檔。

### 2. 等待「允許一次」的畫面

1. 在測試專案觸發一個你本來就會測試的核准操作，讓核准區塊留在畫面上。
2. 再執行上述工具並選取相同 App 視窗，先不要按「允許一次」。
3. 探測結束後，核准仍交由你手動處理。
4. 如果報告的 Name 看不到命令內容，可以加 `--include-text` 再探測一次：

   ```powershell
   .\CodexUiaProbe.exe --include-text
   ```

`--include-text` 額外讀取 TextPattern，最多每個控制項 2048 字元；已標示密碼的控制項及無法確認密碼狀態的控制項不讀取文字。不讀 ValuePattern 的值、不擷取圖片、不讀磁碟上的對話資料。

## 指定視窗及排除問題

```powershell
# 只列出候選，不使用 UIA 探測
.\CodexUiaProbe.exe --list

# 候選中沒有 App 時，列出所有可見的頂層視窗（可能列出其他 App 的標題）
.\CodexUiaProbe.exe --list --list-all

# 換成清單裡的實際 HWND；output 請使用新檔名
.\CodexUiaProbe.exe --hwnd 0x1234 --output .\approval.jsonl

# Raw view 太多內容或太慢時，另存一份 Control view 比較
.\CodexUiaProbe.exe --view control
```

視窗關閉重開後 HWND 可能改變，需重新列出。工具預設用 Raw view，保留父子關係、AutomationId、ClassName、FrameworkId、ControlType、RuntimeId、Enabled、Offscreen、Bounds 及支援的 Pattern；控制項 Name 最多 1024 字元，截斷會標記。

最多 3000 個控制項、深度 30、worker 執行 45 秒。可用 `--max-nodes`、`--max-depth`、`--timeout` 調整；完整範圍見 `--help`。達到限制、控制項失效或錯誤時會標記不完整。UIA 卡住時，只終止工具自己的 worker，保留先前已寫入的報告，不終止 Codex。逾時中斷的最後一行可能不是完整 JSON，文字報告會標記。

Exit code：`0` 完成／列出／取消、`1` 啟動或參數錯誤、`2` 不完整、`3` 逾時。若 worker 被公司阻擋，可能只產生空的或不完整報告；請一併記錄畫面錯誤。視窗正在重繪時，報告是逐項讀取的快照，不保證所有節點來自同一瞬間。

## 如何判讀

- `header.probeIntegrity` 和 `targetIntegrity` 應為 `medium`（一般使用者）。`unavailable` 表示無法查詢，不能當成 medium。
- `targetProcess.fileVersion` 是視窗所屬 EXE 的版本，可能與 App 設定顯示的產品版本不同；請一併提供 App 裡的 `26.803.81509`。
- `approvalLabelHint=true` 只表示 Name 含已知文字，不證明它是真正的核准按鈕。
- `patterns` 含 `InvokePattern` 表示 provider 宣告支援；**工具不呼叫 Invoke，因此不能證明單次允許實際可操作。**
- 根據 `index`／`parent` 看按鈕與命令是否同屬核准區塊。文字報告會用縮排呈現。
- 沒有節點、沒有 Pattern、或沒有找到核准文字，可能是樹視圖、虛擬化、權限、重繪、逾時或 provider 的問題，不能直接判定 App 不支援 UIA。

## 報告資料與回傳

**預設 Name 就可能含對話、命令、帳號、公司路徑與專案名稱；`--include-text` 會增加內容。報告不自動匿名化。** 不要直接把整份報告上傳 GitHub，也不要在未檢查前帶離公司環境。

完整報告與 node 資料保留在公司本機，不要求回傳、截圖或匯出。桌面版回報使用中文 log 摘要的「回報碼」及「結果」，不需整理舊探測資料或核准時間。

## 原始碼與建置

Windows VS2022／.NET SDK 或可交叉建置 net48 的 .NET SDK：

```powershell
dotnet build .\CodexUiaProbe.csproj -c Release
```

只需要 `Microsoft.NETFramework.ReferenceAssemblies` 作為建置用套件，執行時依賴 Windows 已安裝的 Framework。手動 helper 另以 `dotnet build .\manual\CodexUiaManualInvoke.csproj -c Release` 建置。原始碼及套件鎖定檔隨 ZIP 附上；無需在公司重新建置即可測試 EXE。驗證範圍見 `VERIFICATION.md`。

技術來源：[Microsoft UIA FindAll 與搜尋範圍說明](https://learn.microsoft.com/en-us/dotnet/api/system.windows.automation.automationelement.findall?view=windowsdesktop-10.0)、[UIA 安全模型](https://learn.microsoft.com/en-us/windows/win32/winauto/uiauto-securityoverview)。
