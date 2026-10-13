# Codex Auto-Accept CLI — Windows 測試版 0.1.1

在 Windows Terminal 內透過 ConPTY 啟動 Codex，辨識完整英文核准選單後送出單次 `y`。與 VS Code 擴充套件分開運作，不需要 VS Code，也不修改 Codex config、sandbox 或企業 managed requirements。

## 最快測試方式

條件：Windows 11 或支援 ConPTY 的 Windows 10（1903／build 18362 以上），**原生 Windows Node.js 22／24，x64 或 ARM64**，以及已可執行的 Codex CLI。此套件不是獨立 EXE，不包含 Node.js 或 Codex。

1. 解壓縮 `codex-autoaccept-cli-windows-0.1.1.zip`。
2. Windows Terminal 開啟 PowerShell，進入解壓縮的 `codex-autoaccept-cli-0.1.1` 資料夾。
3. 檢查環境並跑自我測試：

```powershell
node --version
node -p "process.platform + ' ' + process.arch"
.\codex-auto.cmd --auto --self-test
```

應顯示 `SELF-TEST PASS: received y through PTY` 並結束。此測試只顯示假的核准選單，不會啟動 Codex、執行選單裡的命令或呼叫 AI。

ZIP 已附上 npm 的 `node-pty 1.1.0`、`@xterm/headless 5.5.0` 與依賴，以及 Windows x64／ARM64 預編譯模組，可先不執行 npm 安裝。

只有從原始碼取得套件或需要重裝依賴時，才在本目錄執行：

```powershell
npm.cmd ci
```

此步驟需連上 npm registry。使用 `.cmd` 可避免 npm PowerShell shim 的執行原則限制，無需更改 PowerShell ExecutionPolicy。

## 在自己的專案執行 Codex

先確認 `codex.cmd --version` 或 `codex --version` 可以執行。從**你的專案目錄**用包裝器的完整路徑啟動，工作目錄會維持在你的專案：

```powershell
cd C:\work\my-project
& 'C:\tools\codex-autoaccept-cli-0.1.1\codex-auto.cmd' --auto
```

請將上面工具路徑改為你的解壓縮位置。所有 Codex 參數放在 `--` 後面：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.1\codex-auto.cmd' --auto -- resume
```

預設 OFF；只有 `--auto` 或指定 config 裡的 `enabled: true` 會開啟。執行中按 **Ctrl+]** 切換 ON／OFF，終端機會顯示目前模式。`Ctrl+C`、方向鍵、貼上與一般輸入會轉送給 Codex；`Ctrl+]` 為包裝器保留熱鍵。如果鍵盤配置無法送出 Ctrl+]，可退出後用 `--auto` / `--off` 重啟。

建議首次真實 Codex 測試先使用 dry run：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.1\codex-auto.cmd' --auto --dry-run
```

dry run 只記錄可辨識的提示，不會自動送鍵；仍可手動確認。按 Ctrl+] 也不會解除 dry run。

## 設定與 log

設定只從你明確傳入的檔案載入，不會從專案自動搜尋：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.1\codex-auto.cmd' --config 'C:\tools\codex-autoaccept-cli-0.1.1\config.example.json' --auto
```

JSON 支援 `enabled`、`dryRun`、`approveFileChanges`、`allowPatterns`、`denyPatterns`。規則是 JavaScript regex，不是 glob；無效 JSON、型別或 regex 會在啟動 Codex 前拒絕啟動。變更檔案後需重新啟動；Ctrl+] 的狀態不會寫回檔案。

內建拒絕規則包括原 VS Code 版的破壞性命令，另加 Windows 的 `Remove-Item`／`ri`／`del`／`erase`／`rmdir`／`rd`、磁碟格式化與 `Invoke-Expression`／`iex`。命中時停留在人工核准，不自動拒絕 Codex 的操作。白名單非空時核准區塊必須匹配至少一條；拒絕規則永遠優先。這是文字比對，不是完整 shell 語意分析。

Log 預設在 `%LOCALAPPDATA%\CodexAutoAccept\cli.log`，只記錄 `START`、`MODE`、`ACCEPT`、`MANUAL`、`DRY_RUN` 與簡短理由，不記錄完整命令或畫面。可以另開 Windows Terminal 分頁：

```powershell
Get-Content "$env:LOCALAPPDATA\CodexAutoAccept\cli.log" -Tail 20 -Wait
```

用 `--log 'C:\temp\codex-auto.log'` 可指定其他位置。Log 無法寫入時會停止包裝器。

## 執行檔解析與排除問題

- npm 安裝的 Codex：尋找 PATH 上的 `codex.cmd`／`codex.ps1`，再直接使用 Node 執行同目錄 `node_modules\@openai\codex\bin\codex.js`。不經過 `cmd.exe /c` 拼接命令字串。
- 直接安裝的 `codex.exe`：從 PATH 尋找，也可使用 `--codex 'C:\tools\codex.exe'`。
- 自訂 shim 找不到官方 npm entry 時保留錯誤，不猜測 shell 命令。可用 `--codex` 指向官方 `bin\codex.js` 或原生 `.exe`。
- 不支援 PowerShell alias/function 名稱解析；PATH 或明確執行檔路徑必須存在。
- 從 Windows Terminal 的原生 PowerShell/cmd 使用。本版未驗證 WSL、Git Bash、SSH，也不支援輸入／輸出重新導向。
- 本版使用 Microsoft node-pty 的 ConPTY；輸出與輸入透明轉送，PTY 與 xterm shadow screen 同步終端機行列數，包含 resize。
- 提示必須是完整的官方英文選單，包含 `Yes, proceed (y)`、`No, and tell Codex... (esc)` 與確認 footer。若選單有翻譯、截斷、不同版本格式或特殊重繪，會保留人工確認。畫面文字不是可驗證的核准事件，刻意模仿完整提示的輸出仍可能被辨識。
- 企業政策直接禁止的操作不會因送出確認鍵而變成允許。此版不調整 approval policy 或申請 Full Access。

## 驗收項目

1. `--auto --self-test` 應自動 PASS。
2. `--off --self-test` 應停在選單；按 Ctrl+] 後自動 PASS，或手動輸入 y。
3. `--auto --dry-run --self-test` 應停在選單，log 顯示 DRY_RUN；手動 y 可結束。
4. 真實 Codex 出現允許的命令核准時，應自動確認且 log 顯示 ACCEPT。
5. 在等待核准前切 OFF，應保留人工確認。
6. 調整 Windows Terminal 大小，檢查畫面與核准流程；退出 Codex 後應恢復一般鍵盤輸入。

回報問題時提供 Node 版本/架構、Codex 版本、Windows 版本、使用的啟動參數與 metadata log。核准選單文字需自行遮去公司路徑或敏感資料。

## 驗證範圍與移除

已通過 10 項自動化測試（參數、Windows PATH/含空白路徑解析、規則、ANSI 畫面、重複核准、開關、結束、resize、dry run、log 錯誤），並在 macOS 用真實 PTY 跑過 self-test 和 OFF→ON。macOS npm 的 spawn-helper 缺少 executable bit，僅本機測試依賴補上執行權限；不包含在 Windows 包裡。**使用者已確認 0.1.0 在 Windows 真實 Codex 可自動核准，但有間歇漏辨識；0.1.1 的 Windows 實測仍待回報**，這份交付是 Windows 測試版。

移除時退出包裝器，刪除解壓縮目錄即可。需要清除 log 時另外刪除 `%LOCALAPPDATA%\CodexAutoAccept`；不需要還原 Codex 安裝。此版與 VS Code 版的開關互不連動。

參考：[Microsoft node-pty](https://github.com/microsoft/node-pty)。套件依賴的 LICENSE 保留在各自的 node_modules 目錄內。

## 0.1.1 修正與紀錄

- 檢查採固定期限，不再等待輸出停止，避免持續重繪讓核准一直延後。
- 依 xterm 的 soft-wrap metadata 接回折行文字，改善窄視窗選單辨識。
- 同一提示不因單純 resize 重複送鍵；觀察到提示消失後，再次出現的相同操作仍可核准。
- `UNRECOGNIZED` 表示已看到核准標題，但選單不完整或格式不支援；只記錄原因，不記錄畫面內容。
- `MANUAL deny rule`、`MANUAL clipped content` 等仍需人工確認；這些不代表偵測卡住。

0.1.1 通過 16 項測試，含持續重繪、窄視窗、快速連續相同核准與缺少選單的診斷。保留完整英文選單要求，不以定時重複 y/Enter 處理未知提示。
