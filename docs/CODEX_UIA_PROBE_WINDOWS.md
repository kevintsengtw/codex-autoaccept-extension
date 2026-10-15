# codex-uia-probe-windows 操作指南

此指南對應 [codex-uia-probe-windows-0.2.2.zip](../artifacts/codex-uia-probe-windows-0.2.2.zip)，用於 Windows Codex／ChatGPT 桌面 App。它是控制台工具，不是 VS Code extension，也不是 Windows Terminal Codex CLI 包裝器。

若要滑鼠操作、系統匣常駐，請使用另一個 [Windows 桌面系統匣版](../desktop-probe/tray/README.md)。本指南的 O／F／Q 按鍵只在 Probe 的控制台內有效。

0.2.2 是從原始 0.2.1 修正的控制台支線，原始碼在 [console-probe](../console-probe/README.md)，與 R11 常駐版分開。修正長時間失效時原因被通用例外蓋掉、預設掃描超限後無法擴大，以及 Q 退出後仍停在 `pause` 的問題。公司 Windows 長時間恢復與 Q 返回 PowerShell 尚待實測，不能以可攜測試代替。

## 選擇執行模式

| 目的 | 啟動檔 | 是否會核准 |
| --- | --- | --- |
| 查看 App 是否提供 UI Automation 控制項／按鈕資訊 | `Run-Probe.cmd` | 唯讀，不核准 |
| 人工確認後測試一次「允許一次」 | `Run-Manual-Once.cmd` | 輸入 `INVOKE ONCE` 才操作一次 |
| 持續等待並自動按「允許一次」 | `Run-Auto-Accept.cmd` | 預設 OFF；使用者按 O 後持續自動處理 |

只想日常自動核准，不必先產生探測報告；直接使用自動模式即可。這些模式不改 Full Access、sandbox 或企業權限設定。

## 下載、解壓縮與執行條件

1. 下載上述 ZIP。GitHub 檔案頁可按 Download raw file；亦可從 repo 的 `artifacts/` 取得。
2. 完整解壓到允許執行的使用者目錄，例如 `C:\tools\codex-uia-probe-windows-0.2.2`。不要在壓縮檔預覽內直接執行。
3. 保留 `CodexUiaProbe.exe`、`CodexUiaManualInvoke.exe`、兩個 `.exe.config` 及三個 `.cmd` 在同一目錄。
4. 使用 Windows 10／11，以及 .NET Framework 4.8／4.8.1；不需 Node.js、Python 或另裝 .NET 8／9／10 runtime。從原始碼建置才需要 .NET SDK。
5. 開啟一般權限的 Codex／ChatGPT App，工具也使用一般權限，位於同一個互動式 Windows 桌面。不要從管理員 Terminal 啟動。

可用 PowerShell 核對 ZIP 的 SHA-256，與 [artifacts/SHA256SUMS](../artifacts/SHA256SUMS) 的同名項目比較：

```powershell
Get-FileHash .\codex-uia-probe-windows-0.2.2.zip -Algorithm SHA256
```

EXE 未經 Authenticode 簽署；公司若阻擋執行，請保留錯誤依公司流程處理，不透過提權解決。

## 持續自動核准：最快操作方式

在一般權限 PowerShell 進入解壓縮目錄：

```powershell
cd C:\tools\codex-uia-probe-windows-0.2.2
.\Run-Auto-Accept.cmd
```

1. 工具列出候選視窗。輸入 Codex／ChatGPT **桌面 App** 對應的編號，再 Enter。候選可能包含 VS Code 或 Terminal，請依視窗標題與 PID 確認。直接 Enter 可取消。
2. 啟動後是 OFF。回到這個控制台按 **O**，不需 Enter。
3. 回到 App 繼續工作；控制台必須保持執行。App 顯示唯一、可操作的「允許一次」按鈕時，工具會嘗試核准一次。
4. 核准後仍保持 ON，不必每次重新按 O。等待原核准區塊離開後才處理下一次，不連點同一請求。
5. 要停用，切回控制台按 **F**；按 **Q** 停用並退出。0.2.2 的自動模式不再於退出後執行 `pause`，應直接返回 PowerShell，無需 Ctrl+C；Codex Desktop 可以保持開啟。

| 按鍵 | 行為 |
| --- | --- |
| O | ON |
| F | OFF |
| 空白鍵 | 切換 ON／OFF |
| Q | OFF 並退出 |

每次重新啟動皆為 OFF。按鍵不是全域熱鍵，也沒有系統匣圖示；在 App 或其他 Terminal 按 O 不會切換本工具。

0.2.1 的持續模式已由使用者在公司環境確認：只按一次 O，連續三次核准都完成且保持 ON，按 F 後停止。這不等同所有 App 版本或背景狀態都相容。測試時先保持 App 展開可見；遮蔽、最小化、鎖定的情境不能據此保證。

核准依據是所選視窗內精確名稱「允許一次」的唯一 Button、啟用／可見狀態與 Invoke 支援。這版沒有完整命令白名單／拒絕規則，也沒有聊天 ID 綁定；切換 App 內的聊天會改變目標。它與 CLI 版的 B001 等拒絕規則是不同工具。

## 唯讀探測

用於查找 UIA 控制項資訊，不會按「允許一次」：

```powershell
.\Run-Probe.cmd
```

選取 App 視窗後，預設有 5 秒準備時間。切回 App，保持核准區塊可見，先不手動處理。完成後工具顯示報告路徑；之後才由你人工核准。

預設輸出目錄為 `%LOCALAPPDATA%\CodexUiaProbe`：

| 檔案 | 內容 |
| --- | --- |
| `probe-*.jsonl` | 控制項與程序資料 |
| `probe-*.jsonl.txt` | 可讀控制項樹 |
| `probe-*.jsonl.summary.txt` | 不含 UI 文字的本機摘要 |

完整報告可能包含對話、命令、帳號與公司路徑，不會自動匿名化。保留在公司本機；回報問題只需摘要中的數字、狀態或 exit code，不需要上傳全文。

常用參數：

```powershell
# 只列候選視窗，不掃描 UIA
.\CodexUiaProbe.exe --list

# 候選沒看到 App：列出所有可見頂層視窗
.\CodexUiaProbe.exe --list --list-all

# HWND 請換成清單裡的實際數字；輸出必須用新檔名
.\CodexUiaProbe.exe --hwnd 0x1234 --output .\approval.jsonl

# 多讀 TextPattern；只在確實需要文字資訊時使用
.\CodexUiaProbe.exe --include-text

# 比較另一種 UIA 樹視圖
.\CodexUiaProbe.exe --view control

# 已有報告時只產生／顯示摘要，不重新掃描
.\CodexUiaProbe.exe --summarize .\approval.jsonl

.\CodexUiaProbe.exe --help
```

| 參數 | 預設／範圍 |
| --- | --- |
| `--delay` | 5 秒；0–60 |
| `--max-nodes` | 3000；1–10000；自動模式未指定時遇超限逐級提高至 10000 |
| `--max-depth` | 探測 30、手動／自動 60；1–100；自動模式未指定時遇超限逐級提高至 100 |
| `--timeout` | 45 秒；5–120 |
| `--view` | raw；可選 raw／control（唯讀探測） |

達到節點／深度／時間限制會產生部分結果。視窗重開後 HWND 可能改變，必須重新列出。

## 手動單次核准驗證

```powershell
.\Run-Manual-Once.cmd
```

1. 先讓 App 留在你願意核准的測試操作畫面，再選取該視窗。
2. 工具檢查唯一可 Invoke 的「允許一次」按鈕，並在公司電腦控制台顯示有限的相鄰文字。
3. 在 App 確認待核准操作；按 Enter 可取消。
4. 只有輸入精確的 **`INVOKE ONCE`** 再 Enter，工具才重掃並嘗試一次 Invoke。
5. 確認 App 是否繼續工作。Invoke 返回本身不能證明操作已成功。

如果得到逾時或不確定結果，先看 App 是否已生效，再決定是否重新測試；不要直接重複核准。

## 0.2.2 長時間失效的原因紀錄

錯誤時，控制台與 `%LOCALAPPDATA%\CodexUiaProbe\auto-*.txt` 會保存 `AUTO_REASON` 與中文結果；不用改跑其他診斷模式、不用記住核准時間或外傳公司檔案。

- `NODE_LIMIT`／`DEPTH_LIMIT`：預設自動模式提高上限後重做完整掃描。節點為 3000 → 6000 → 10000，深度為 60 → 80 → 100；指定的參數固定不變，達最高值仍拒絕不完整掃描。
- `ROOT_CHANGED`／`TARGET_CHANGED`：切 OFF。先確認 App 的核准結果，再按 Q 只退出工具，重開工具並重新選視窗，不需關閉 App。
- `TREE_REPEAT`：App 回傳重複控制項，取消本輪並等待重掃。
- `CONTEXT_LIMIT`／`CONTEXT_MISSING`：按鈕的局部內容超限或缺少父層，無法完整驗證，本輪不核准。

0.2.1 的照片無法確認具體拒絕原因。重開 PowerShell 不會縮短 App 內仍保留的控制項樹；修正版用明確原因碼區分這些情況。已開始的核准嘗試不因重掃或 OFF／ON 而重送。

## 結果與疑難排解

| 現象 | 處理 |
| --- | --- |
| 找不到 App 候選 | 確認 App 已開啟；用 `--list --list-all` 查看 |
| 找不到「允許一次」 | 保持真正核准區塊可見；唯讀 probe 檢查 Button／Invoke 資訊 |
| 有多個同名按鈕 | 工具不自動選一個；確認所選視窗與聊天 |
| 權限層級不符或未知 | App 與工具用一般使用者執行；不能把 unknown 當成 medium |
| 自動模式等待舊核准離開 | 仍保持 ON，不重複 Invoke；確認 App 是否仍顯示原區塊 |
| 關閉控制台後沒再核准 | helper 已停止，重新啟動仍為 OFF |
| EXE／helper 缺少或無法載入 | 完整解壓，檢查 .NET Framework 與公司允許執行的範圍 |
| Probe exit code 2 | 部分探測結果，查看摘要；不表示 UIA 不可用 |
| Probe exit code 3 | 探測逾時，已有資料仍可能保留 |

唯讀探測 exit code：0 完成／列出／取消，1 啟動或參數錯誤，2 不完整，3 逾時。手動模式另有 4 未操作／目標改變、5 操作結果不確定；完整狀態與自動模式去重限制見 [控制台詳細說明](../console-probe/README.md)。

自動／手動 helper 的本機紀錄也在 `%LOCALAPPDATA%\CodexUiaProbe`。0.2.2 的控制台狀態可看到 STATUS、ATTEMPT、INVOKE、OBSERVATION、AUTO_REASON 等；新版系統匣工具的中文 log／摘要格式另見 [LOG_FORMAT.md](../desktop-probe/LOG_FORMAT.md)，不要混用不同版本的回報名稱。

## 停用與移除

自動模式先按 F 再 Q。退出後可刪除解壓縮目錄；要清理本機報告與紀錄，再處理 `%LOCALAPPDATA%\CodexUiaProbe`。此控制台工具不設定 Windows 開機啟動、不修改官方 App bundle，不需要執行 VS Code 的 Restore GUI Integration。

原始碼建置與完整驗證紀錄見 [console-probe/README.md](../console-probe/README.md) 及 [VERIFICATION.md](../console-probe/VERIFICATION.md)。
