# Codex UIA Probe 控制台版 0.2.2

這是 `codex-uia-probe-windows-0.2.1` 的獨立修正版。原始碼直接取自原始 0.2.1 ZIP，來源雜湊記於 `BASELINE.json`；不使用或改動 0.3.10 R11 常駐版的程式碼與成品。

## 本次修正

- 長時間運作後的通用 `ManualRefusalException` 改成具體 `AUTO_REASON`，同時保存中文處理說明及掃描計數到本機 log。相同錯誤十秒內合併，之後仍會留下紀錄。沒有加入診斷面板，也不需要記錄核准時間。
- 預設自動掃描遇到節點上限，會依序用 3,000、6,000、10,000 節點重做完整掃描；深度依序為 60、80、100。每次都完整重掃與核准前重驗，不能在不完整樹上核准。明確指定的上限及手動／唯讀模式不自動擴大。
- 根元素或目標視窗改變時切 OFF，顯示先確認 App 結果、退出工具並重新選取視窗的指示，不一直印通用錯誤，也不自動改綁目標。
- 修正核准後根元素讀取失敗可能被誤當成原按鈕已移除的問題。結果不明時保留去重，不重送 Invoke；擴大掃描上限及 OFF／ON 都不會清除已嘗試的紀錄。
- 移除 `Run-Auto-Accept.cmd` 最後的 `pause`。按 Q 退出工具後直接返回 PowerShell，不再要求第二次按鍵或 Ctrl+C；退出碼保留。

照片不能證明原本是哪一個拒絕原因。節點／深度超限是可重現並已處理的條件，不宣稱已在公司 Windows 重現原問題。達最高上限仍不接受不完整掃描；新原因碼會明確留在本機 log。此版保留 0.2.1 的可見視窗條件，沒有加入背景／最小化／鎖定功能。

## 更新與使用

**Codex Desktop 可以保持開啟並繼續工作，不需重啟 App、切換聊天或變更權限。**

1. 退出舊的輔助工具。舊版 Q 後若停在批次檔的 `pause`，再按任意鍵結束即可。
2. 將 0.2.2 完整解壓到新的資料夾，兩個 EXE、兩個 config 和三個 cmd 放在同一目錄；不要覆蓋 R11 或其他版本。
3. 從一般權限 PowerShell 進入該目錄，執行：

```powershell
.\Run-Auto-Accept.cmd
```

4. 選取仍在執行的 Codex／ChatGPT 視窗。每次啟動均為 OFF；按 O 開啟、F 停用、空白鍵切換、Q 退出，不需 Enter。按鍵只在這個控制台有焦點時生效。
5. 按 Q 後應直接回到 `PS ...>`。只退出輔助工具，不會關閉 Codex Desktop；已開始的核准可能仍會完成。

自動模式持續 ON；唯一精確「允許一次」Button、enabled／onscreen／Invoke、完整重掃、目標／內容確認及每個週期最多一次 Invoke 都保留。不分析命令種類，也沒有聊天 ID 綁定。一般權限、.NET Framework 4.8／4.8.1、公司允許執行即可使用；不需要 Node.js、Python 或另裝 .NET 8 runtime。

其他模式：`Run-Probe.cmd` 唯讀探測；`Run-Manual-Once.cmd` 必須輸入 `INVOKE ONCE` 才核准一次。這兩個舊啟動檔保留原本結束時的暫停行為。

## 原因碼與 log

本機紀錄位於 `%LOCALAPPDATA%\CodexUiaProbe\auto-*.txt`。不需傳出公司檔案，也不用停住工作等回覆；稍後只需查看 `AUTO_REASON` 與中文「結果」。自動模式紀錄不含 UI 文字、命令、RuntimeId 或指紋；唯讀完整 probe 報告仍可能包含 UI 內容。

| 原因碼 | 意義 |
| --- | --- |
| `NODE_LIMIT`／`DEPTH_LIMIT` | 完整掃描超過節點／深度上限；預設自動模式將在上限內擴大並重掃 |
| `TREE_REPEAT` | 控制項樹回傳重複元素，不當成單純節點不足而無限提高上限 |
| `ROOT_CHANGED`／`TARGET_CHANGED` | 視窗根元素或目標已變更，切 OFF；先確認 App 結果，再只重開本工具選取視窗 |
| `CONTEXT_LIMIT`／`CONTEXT_MISSING` | 按鈕同層內容超限／缺少父層，無法完成局部驗證 |
| `MULTIPLE_CANDIDATES` | 不只一個可操作的同名按鈕，本輪不核准 |
| `READ` | 其他 UIA 讀取／驗證未完成；已開始的嘗試不重送 |

明確指定上限的例子：

```powershell
.\Run-Auto-Accept.cmd --max-nodes 10000 --max-depth 100
```

範圍仍是節點 1–10000、深度 1–100、逾時 5–120 秒（預設 45）。指定參數不會取消完整掃描要求或重送結果不明的核准。

## 原始碼與驗證

Repository 的 `console-probe/`、ZIP 的 `source/` 是這條控制台支線。以 .NET SDK 執行 `check.ps1`，會建置兩個 EXE、執行六組測試與兩項 API audit；不操作 Codex App。Windows launcher 測試使用自己建立的無害子程序，驗證批次檔不等待額外按鍵及退出碼傳遞；非 Windows 只檢查啟動檔結構並明確標示未跑該程序測試。

建置與可攜測試不等同公司 Windows 的長時間驗收。詳細限制見 `VERIFICATION.md`。
