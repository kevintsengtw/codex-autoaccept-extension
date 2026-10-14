# Codex Auto-Accept CLI — Windows 測試版 0.1.12

在 Windows Terminal 內透過 ConPTY 啟動 Codex，辨識完整英文核准選單後送出單次 `y`。與 VS Code 擴充套件分開運作，不需要 VS Code，也不修改 Codex config、sandbox 或企業 managed requirements。

## 最快測試方式

條件：Windows 11 或支援 ConPTY 的 Windows 10（1903／build 18362 以上），**原生 Windows Node.js 22／24，x64 或 ARM64**，以及已可執行的 Codex CLI。此套件不是獨立 EXE，不包含 Node.js 或 Codex。

1. 解壓縮 `codex-autoaccept-cli-windows-0.1.12.zip`。
2. Windows Terminal 開啟 PowerShell，進入解壓縮的 `codex-autoaccept-cli-0.1.12` 資料夾。
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
& 'C:\tools\codex-autoaccept-cli-0.1.12\codex-auto.cmd' --auto
```

請將上面工具路徑改為你的解壓縮位置。所有 Codex 參數放在 `--` 後面：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.12\codex-auto.cmd' --auto -- resume
```

預設 OFF；只有 `--auto` 或指定 config 裡的 `enabled: true` 會開啟。執行中按 **Ctrl+]** 切換 ON／OFF，終端機會顯示目前模式。`Ctrl+C`、方向鍵、貼上與一般輸入會轉送給 Codex；`Ctrl+]` 為包裝器保留熱鍵。如果鍵盤配置無法送出 Ctrl+]，可退出後用 `--auto` / `--off` 重啟。

建議首次真實 Codex 測試先使用 dry run：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.12\codex-auto.cmd' --auto --dry-run
```

dry run 只記錄可辨識的提示，不會自動送鍵；仍可手動確認。按 Ctrl+] 也不會解除 dry run。

## 設定與 log

設定只從你明確傳入的檔案載入，不會從專案自動搜尋：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.12\codex-auto.cmd' --config 'C:\tools\codex-autoaccept-cli-0.1.12\config.example.json' --auto
```

JSON 支援 `enabled`、`dryRun`、`approveFileChanges`、`expandLongCommands`、`logMaxBytes`、`logBackupCount`、`allowPatterns`、`denyPatterns`。規則是 JavaScript regex，不是 glob；無效 JSON、型別或 regex 會在啟動 Codex 前拒絕啟動。變更檔案後需重新啟動；Ctrl+] 的狀態不會寫回檔案。

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

## 0.1.2：Reason 與實際命令分開檢查

命令核准只對 `$` 執行區塊套用截斷與命令規則，避免前面的 `Reason:` 說明重複命令、含省略號或描述破壞性操作時，誤判完整的 pwsh 指令。仍要求完整 Yes/No/footer，實際執行區塊截斷仍人工確認。檔案核准維持原內容檢查。18 項測試通過；此改版在公司 Windows 的結果待實測。

## 0.1.3：診斷版

未辨識的選單細分為缺少完整標題、Yes、No、結尾 footer 或 `$` 命令起點，log 附 terminal 欄列；START 記錄包裝器版本。0.1.3／0.1.4 曾把 MANUAL／UNRECOGNIZED 顯示在 terminal；0.1.5 已移除這個輸出，診斷只寫入 log。20 項測試通過，包含完整長多行 pwsh fixture。此版本沒有放寬未知選單的自動核准，尚未確定使用者 0.1.2 停住的精確原因；請以新版訊息回報。

## 0.1.4：修正舊折行標記造成核准標題合併

游標定位覆寫曾經折行的行時，xterm 可能保留 `isWrapped` 標記。重建時把完整核准標題接到前一行，便會回報 missing complete approval heading。此版保留核准標題、metadata、命令起點、選項與 footer 的行界線，其他命令 soft wrap 仍正常接回。回歸測試在舊邏輯會失敗並產生相同診斷，新版可送出一次 y。22 項測試通過；公司 Windows 結果仍待驗證。

## 0.1.5：移除互動畫面的自動診斷輸出

不再把 MANUAL／UNRECOGNIZED 寫入正在使用的 Codex terminal，以免診斷占用畫面、干擾 TUI 或讓使用者誤判操作狀態。所有自動決策與辨識診斷只寫入 cli.log；啟動資訊、使用者 Ctrl+] 切換回饋及終止錯誤仍會顯示。保留 0.1.4 的標題重繪修正。

## 0.1.12：正常字體下，核准標題超出可見區域時的展開修復

長 `Reason` 說明可能把核准標題與命令起點擠出可見區域。舊版必須看到完整標題才啟動 Ctrl+A，會因此卡住；縮小字體增加行列數後才可能恢復。

此版在完整的 `[… N lines] ctrl+a view all` 提示、Yes／No 選項與確認 footer 都可見時，也能啟動展開。必須識別官方 EXEC 檢視器、讀完全部頁面、通過規則，並返回相同的核准畫面才送出一次 `y`。不直接核准畫面外的命令；不完整選單、未知檢視器與 deny rule 仍保留人工確認。

固定 80×24、60×18、120×30 的終端測試，全程不 resize，已驗證長說明擠出標題後仍能展開、返回與核准，並保留遞迴刪除拒絕。55 項本機測試通過；macOS 真實 PTY 的長說明與多頁展開自測通過。公司 Windows 正常字體實測仍待確認，尚不能保證使用者回報的每次卡住都屬於此原因。若仍卡住，請提供同一時間的 log；新增的 `heading outside viewport` 診斷只寫入 log，不占用 Codex 畫面。

此版包含 0.1.11 的 B004 修正與 0.1.10 的 log 輪替。`--self-test-expanded` 現在也會模擬長說明擠出標題的情境。

## 0.1.11：修正 B004 工具清單誤判

PowerShell 用 `Get-Command $_ -ErrorAction SilentlyContinue` 查詢工具時，清單中的 `'rm.exe'` 曾被 B004 和後面的 `-ErrorAction` 連在一起，誤判為遞迴刪除。此版要求 `rm`／`rm.exe` 後接執行參數，並比對完整的刪除選項，避免這個誤判。

`rm -r`、`rm -rf`、`rm --recursive` 等仍保留人工確認；使用者自訂拒絕規則仍生效。規則仍是文字啟發式判斷，不是完整 shell 語法分析器。52 項本機測試通過，包含工具清單、自訂規則、遞迴刪除及核准按鍵流程；公司 Windows 實測待確認。

此版包含下列 0.1.10 的 log 大小上限與輪替功能。

## 0.1.10：log 大小上限與輪替

CLI log 不再無限追加。預設每檔最多 **5 MiB**，保留 **3 份備份**：

| 檔案 | 用途 |
| --- | --- |
| `cli.log` | 目前紀錄，仍可用原本方式查看 |
| `cli.log.1` | 最近一次輪替的紀錄 |
| `cli.log.2` | 較舊紀錄 |
| `cli.log.3` | 最舊的保留紀錄 |

包含目前檔案共最多約 **20 MiB**。下一筆紀錄將超過單檔上限時先輪替，刪除最舊備份。首次寫入也會縮減舊版過大的 log，只保留近期完整紀錄行；中文 UTF-8 不從中間截斷。這代表超出保留範圍的歷史紀錄會移除，如需留存，更新前自行備份。

只管理所選 log 檔、`.1`、`.2` 等數字備份，以及本工具縮減大檔時產生的特定臨時檔；不清除資料夾內其他檔案。`--log` 自訂路徑適用同一輪替方式；UIA／桌面工具的紀錄與原始探測報告使用不同路徑，本 CLI 更新不處理它們。

可在明確指定的 JSON config 調整：

```json
{
  "logMaxBytes": 5242880,
  "logBackupCount": 3
}
```

`logMaxBytes` 範圍 65,536–104,857,600 bytes；`logBackupCount` 為 0–20。設為 0 時只保留目前檔案。更改設定後重新啟動；縮減備份數量會清除超出的數字備份。不需要設定檔也會使用預設上限。

新版用短暫檔案鎖協調多個 CLI 同時寫入，避免輪替互相覆寫；鎖狀態無法確認或寫入失敗時仍停止自動處理，避免沒有決策紀錄。請退出舊版再啟動，舊版不遵守此鎖。正常寫完會移除 `.lock`；能確認原程序已退出的鎖可復原，未知鎖保留並顯示錯誤。

49 項測試包含輪替上限、舊大檔處理、中文完整性、備份減量、零備份、保留其他檔案、平行程序寫入、鎖復原與中斷後臨時檔清理。Windows 實際檔案操作仍待確認。核准、展開與規則編號功能維持不變。

## 0.1.9：只記錄拒絕規則編號與類別

拒絕規則與展開流程不變。普通核准與展開核准的 log 現在會記錄所有命中的規則 ID，例如：

```text
MANUAL expanded approval: deny rule; rules=B005:git-destructive
```

不記錄命令、路徑、匹配片段或自訂 regex。類別表示命中哪條文字規則，不能單憑它斷定命令真的有破壞性操作。回報 ID／類別即可，不需要提供公司命令。

| ID | 類別 | 規則範圍 |
| --- | --- | --- |
| B001 | windows-file-delete | Remove-Item／ri／del／erase／rmdir／rd |
| B002 | disk-format | 磁碟格式化／清除 |
| B003 | dynamic-execution | Invoke-Expression／iex |
| B004 | recursive-delete | rm 遞迴刪除 |
| B005 | git-destructive | Git reset --hard／clean／強制 push |
| B006 | destructive-sql | DROP／TRUNCATE／DELETE FROM |
| B007 | system-operation | mkfs／shutdown／reboot |
| B008 | raw-disk-write | dd 寫入 |
| B009 | world-writable-permissions | chmod 777 |
| B010 | remote-script-execution | curl／wget 管線執行腳本 |
| B011 | infrastructure-delete | terraform／kubectl／docker 刪除操作 |
| D001、D002… | user-defined | 使用者 denyPatterns 第 1、2…條（重排設定時編號隨之改變） |

40 項測試包含規則分類、多重匹配、自訂 regex 與展開後 log 的隱私檢查。Windows 實際回報仍待確認。

## 0.1.8：修正官方展開提示格式

官方提示為 `[… 38 lines] ctrl+a view all`，而 0.1.7 僅匹配 `+38 lines`，造成沒有 EXPAND 紀錄、直接停在 MANUAL clipped content。0.1.8 支援官方中括號／省略號格式，以及 ASCII `[...]` 變體；仍要求完整核准選單和明確 Ctrl+A 快捷鍵。自我測試已改用官方格式，不再只測錯誤的合成樣板。

36 項測試與本機真實 PTY 展開測試通過。舊 0.1.7 在官方格式回歸測試會失敗；新版能啟動展開並完成雙頁讀取、返回及單次核准。公司 Windows 的後續檢視器相容性仍待驗證。使用 `--auto --self-test-expanded` 可先確認傳輸流程；實際操作時應開始出現 EXPAND 紀錄。

## 0.1.7：自動展開長命令並讀取內容（Windows 測試版）

`expandLongCommands` 預設 `true`，只處理完整英文命令核准選單中明確顯示的展開提示。0.1.7 原先要求 `+數字 lines`，0.1.8 已修正為支援官方 `[… 數字 lines] ctrl+a view all` 格式。只有 ON 且非 dry run 才會送出展開／翻頁鍵。設定為 `false` 可回到 0.1.6 的人工處理方式。

流程是 Ctrl+A → 辨識官方 `/ E X E C` 檢視器與預設 PageDown／q 提示 → 讀取命令分頁 → 檢查最後一頁的連續重疊 → 套用完整命令的拒絕／白名單 → q 返回 → 確認仍為同一個核准提示 → 送出一次 y。檢視器顯示完整命令，而非長 Reason 摘要。掃描期間畫面會切入官方檢視器並翻頁，不會額外印出診斷訊息。

未知介面、客製快捷鍵、模糊頁面重疊、內容不完整、提示改變、超過 64 頁／500,000 字元／20 秒或回應超過 3 秒，都保留人工確認。100% 是四捨五入值，不能單獨視為完整性證據。完整內容命中拒絕規則時，也會返回後留給人工確認。

使用者輸入、Ctrl+] 切 OFF、resize 或結束會停止掃描。介入／resize 時不再發送自動按鍵，可能留在檢視器，可依官方提示按 q 返回。這版不修改貼上轉送、不隱藏 queued message、不解決 Ctrl+V 拆分。

log 包含 EXPAND（展開、頁數／百分比、返回）；成功時為 `ACCEPT once; full expanded command reviewed`，失敗時為 `MANUAL expanded approval: ...`。不記錄命令全文、路徑或畫面。

先在解壓縮目錄跑無害的完整流程測試：

```powershell
.\codex-auto.cmd --auto --self-test-expanded
```

預期顯示 `SELF-TEST PASS: expanded full command, returned and received y`。測試模擬官方檢視器，不啟動 Codex、不執行顯示命令、不呼叫 AI。它能驗證 PTY 傳輸，不能證明特定 Codex 版本介面相容。34 項自動化測試與 macOS 真實 PTY 普通／展開測試通過；公司 Windows 結果待回報。

## 0.1.6：畫面逐行結構的備援辨識測試版

- 保留接回 soft wrap 的主要辨識；若選單無法辨識，再對保留 physical row 界線的畫面辨識。
- 允許獨立一行的 `$` 提示符接著完整命令，仍要求完整標題、Yes／No、footer 和實際執行區塊。
- 主辨識已判為拒絕或截斷時，不使用備援繞過它；兩種表示切換時不重複送鍵。
- 備援成功的 log 為 `ACCEPT once; physical-row fallback`，失敗時列出 logical／physical 的各別原因；不包含完整命令或畫面。
- 針對 `+數字 lines … ctrl+a … view all` 的明確隱藏行提示保留人工確認，這版不自動展開。
- 輸入與貼上轉送維持 0.1.5，沒有加入 prompt-file 或新貼上捷徑。診斷仍只寫 log。

26 項測試通過。用舊 0.1.5 重跑回歸 fixture 會得到使用者回報的 missing command start；新版可辨識。這是局部重現，尚不能保證公司畫面的原因完全相同。公司 Windows 實測待回報。
