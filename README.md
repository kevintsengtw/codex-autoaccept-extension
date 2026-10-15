# Codex Auto-Accept

提供手動開關，讓 Codex 在提出可核准的操作時，自動送出**單次核准**。包含 VS Code 擴充套件，以及獨立的 Windows Terminal CLI 包裝器。預設關閉，可隨時停用。

適用於仍提供「允許一次」核准流程的環境。工具不修改 Codex 的 `config.toml`、sandbox、approval policy 或企業 managed requirements；政策直接禁止且沒有可核准提示的操作，不會因此變成可執行。

> **VS Code 的 Codex 側邊欄需要額外安裝 GUI 整合。安裝 VSIX、重開 VS Code、切到 ON 都不等於完成 GUI 整合。請務必執行 `Codex Auto-Accept: Install / Repair Codex GUI Integration`，然後重新載入視窗。**

## 成品下載與驗證狀態

[GitHub Releases](https://github.com/kevintsengtw/codex-autoaccept-extension/releases) 提供成品；repo 的 [`artifacts/`](artifacts/) 也保存以下檔案：

| 成品 | 版本 | 用途 |
| --- | --- | --- |
| [codex-autoaccept-0.1.1.vsix](artifacts/codex-autoaccept-0.1.1.vsix) | 0.1.1 | VS Code 擴充套件 |
| [codex-autoaccept-cli-windows-0.1.15.zip](artifacts/codex-autoaccept-cli-windows-0.1.15.zip) | 0.1.15 | Windows Terminal 獨立 CLI 測試版 |
| [codex-uia-probe-windows-0.2.1.zip](artifacts/codex-uia-probe-windows-0.2.1.zip) | 0.2.1 | Windows 桌面 App：UIA 探測、手動單次與控制台 ON／OFF 工具；[操作指南](docs/CODEX_UIA_PROBE_WINDOWS.md) |
| [SHA256SUMS](artifacts/SHA256SUMS) | — | 成品雜湊值 |

VS Code 版與 Windows CLI 包裝器分別版本化，兩個開關互不連動。本專案目前使用 VSIX 發佈，**尚未上架 VS Code Marketplace**。

| 情境 | 驗證狀態 |
| --- | --- |
| 公司 Windows：VS Code Codex 側邊欄 | 使用者已確認 0.1.1 可自動核准，官方 Codex 為 `26.1002.51308`；首次成功前需要安裝 GUI 整合 |
| 繁體中文「允許一次」核准 | 使用者已確認上述 GUI 情境可使用；GUI 不依賴按鈕文字 |
| VS Code 整合終端機 Codex CLI | 自動化測試通過，實際公司環境端到端測試待驗證 |
| Windows Terminal Codex CLI | 使用者確認 0.1.0 可自動核准但偶爾漏辨識；0.1.1 修正持續重繪與折行，Windows 實測待回報 |
| Codex 桌面 App | 公司一般權限、App `26.803.81509`：唯讀探測與手動 Invoke 已成功；使用者已確認 0.2.1 只按一次 O 即連續三次自動核准、保持 ON，按 F 停止 |

詳細測試紀錄見 [VS Code 驗證說明](docs/VERIFICATION.md) 與 [CLI 驗證說明](cli/VERIFICATION.md)。

## Windows 桌面 App：系統匣常駐工具

最新本機候選為 [0.3.10 R11 背景不搶焦點版](artifacts/codex-autoaccept-desktop-windows-0.3.10-nofocus-r11-verified.zip)。核准後端改用同一個 `CUIAutomation8` 用戶端完成掃描、重驗與 Invoke，設定並讀回 `AutoSetFocus=false`；設定或讀回失敗即停止，保留原有安全檢查與單次核准限制。

2026-10-14 已在本機 App `26.1007.2314.0` 完成 24 個真實背景／最小化案例：部分遮蔽、一般大小完全遮蔽、最大化完全遮蔽與最小化，各含既有／新請求各三案。全部在 20 秒內完成 marker，獨立 Windows 前景與焦點事件未記錄到 App 搶焦點，六個最小化案例全程保持最小化。前景與 OFF 回歸通過。本輪未重啟 App；公司 `26.803.81509` 與鎖定仍未驗證。詳見 [R11 結果與限制](desktop-probe/WINDOWS_TEST_RESULTS_20261014_R11.md)。

以下 R4／R5 與舊 ZIP 為歷史結果，保留失敗證據，不代表 R11 的實測結果。

另有 [0.3.10 候選測試版](artifacts/codex-autoaccept-desktop-windows-0.3.10.zip)：修正核准後觀察誤將根元素或 WindowPattern 讀取失效視為原核准按鈕已離開的問題。這類讀取錯誤現在保留結果未知與去重狀態，不重送核准；只有原候選本身讀取失效才可使用元素失效的週期判定。沿用 0.3.9 的本機 log 修正與紀錄節流，沒有新增診斷面板或放寬按鈕條件。三個建置、十一組測試及三項 API 檢查通過，另以真正 UIA 例外完成 21 項共用程式回歸檢查；這些檢查沒有操作 App，不能作為真實背景驗收。**0.3.10 的一般大小完全遮蔽、最大化完全遮蔽及最小化後新請求，各三案仍失敗；背景／最小化問題未修復。** 普通使用者工作階段矩陣與 OFF／重啟已完成稽核。相容冷啟動通過唯讀 preflight 並 armed 等待退出，但 GUI Quit 使控制會話中斷，沒有 launch stage／相容 profile 真實矩陣，列 BLOCKED；沒有猜測退出碼或新 App 啟動來源。

2026-10-13 的 R4 已重新確認原生 Computer Use 可操作專用工具。先使用未修改的 0.3.9：前景只開 ON 一次，三筆不同真實請求均完成獨立 marker；App 完全遮蔽後才產生的新請求，三案在各自 20 秒觀察期內均未完成。主 log 的完整背景掃描沒有精確「允許一次」Button，還原前景後才完成；這是候選建立前的失敗，不能據此斷言 renderer 停止，也不能用 0.3.10 的觀察分類修正宣稱已解決。R4 矩陣與限制整理於 [本輪結果](desktop-probe/WINDOWS_TEST_RESULTS_20261013_R4.md)。本機 App 為 `26.1007.2314.0`，與公司 `26.803.81509` 不同，個人機結果不能替代公司驗收。

0.3.10 的 R5 已確認前景一次 ON 三個真實 marker、部分遮蔽兩種順序各三案，以及一般大小／最大化完全遮蔽既有請求各三案完成；遮蔽完成案返回後均轉為前景，快速完成案保留計時限制。最小化既有請求三案完成，呼叫返回後仍最小化，UIA 觀察未知時保持去重且沒有重送。一般大小／最大化完全遮蔽及最小化後的新請求各三案，正式 20 秒均沒有精確 Button／核准嘗試或 marker，還原前景後才完成。初始 OFF、切 OFF 停止新嘗試與重啟仍 OFF 亦通過；重啟案是重啟前已存在的請求，不提升為重啟後新請求驗收。控制中斷與候選排除仍保留，詳見本輪結果。

本輪相容嘗試的 App 關閉是代理執行的操作，使用者回報與代理承認均保留，沒有歸咎使用者。中斷後目前 App 保持開啟，不再關閉／重試；單次 ledger 保留。最後已確認工具 OFF、停止本輪前後端並恢復原設定，核對原設定位元組與雜湊相符。main log／摘要／journal／marker／producer 時序保留本機；歷史矩陣畫面來自本會話 Computer Use 工具輸出，未另存本機截圖檔，不宣稱已備份。原始 runtime 資料與影像不提交或加入 ZIP；成品封裝與 checksum 另行核對。

[0.3.9 成品](artifacts/codex-autoaccept-desktop-windows-0.3.9.zip) 與 [R3 結果](desktop-probe/WINDOWS_TEST_RESULTS_20261013_R3.md) 保留；R3 當時的工具授權拒絕屬於該輪歷史狀態。[0.3.8 成品](artifacts/codex-autoaccept-desktop-windows-0.3.8.zip)、[更早結果](desktop-probe/WINDOWS_TEST_RESULTS_20261013.md) 與原始 0.3.7 ZIP 也均保留。

[下載 Windows 系統匣版 0.3.7](artifacts/codex-autoaccept-desktop-windows-0.3.7.zip)。完整解壓後，雙擊 **`CodexAutoAcceptDesktop.exe`**，不需 Windows Terminal。第一次選取 ChatGPT App 視窗，再用滑鼠開啟 ON；關閉設定視窗仍會留在系統匣，右鍵圖示可切 ON／OFF 或退出。

每次啟動都 OFF，由使用者手動開啟。可選擇登入 Windows 時啟動，只設定目前使用者的 Run 值；公司政策阻擋時會顯示錯誤。需要 Windows .NET Framework 4.8／4.8.1，以及公司允許前後端 EXE 執行，不要求管理員權限。

公司 App `26.803.81509` 已確認 0.2.1 持續 ON／OFF，以及 0.3.2 一般使用者連接與基本核准。2026-10-13 的 0.3.7 公司回報：背景與最小化都有核准請求但不會自動處理，鎖定未測；先前將完全遮蔽視為已驗收的結論已撤回。0.3.7 移除畫面診斷區，改為正常 ON 時自動寫中文 log 與短摘要，可在本機稍後查看，不要求手動轉抄或外傳檔案。公司 extension／CLI／Desktop 均只能使用「請求核准」，不能依賴 Approve for me／Full Access。此版沒有宣稱修好上述背景情境，也尚無命令規則或聊天 ID 綁定。

使用與更新見 [桌面版說明](desktop-probe/tray/README.md)，紀錄格式見 [log 說明](desktop-probe/LOG_FORMAT.md)，歷史與驗證見 [桌面工具驗證](desktop-probe/VERIFICATION.md)。成品仍附舊探測 CLI 原始碼與工具，日常操作不需要使用。

另一台 Windows 11 可依 [Windows 驗證交接](desktop-probe/WINDOWS_HANDOFF.md) 取得 0.3.7 基準；[起始提示詞](desktop-probe/WINDOWS_CODEX_PROMPT.md) 及 [結果模板](desktop-probe/WINDOWS_TEST_RESULTS_TEMPLATE.md) 仍保留。2026-10-13 已在個人 Windows 使用實際原生 Computer Use 操作，結果與版本差異記於上方測試文件；不能替代公司版本驗收。

## Windows UIA Probe：控制台操作工具

[下載 codex-uia-probe-windows 0.2.1](artifacts/codex-uia-probe-windows-0.2.1.zip)，完整解壓後，從一般權限 PowerShell 進入該目錄：

| 模式 | 執行命令 | 用途 |
| --- | --- | --- |
| 唯讀探測 | `.\Run-Probe.cmd` | 檢查 App 的 UIA 控制項，不核准 |
| 手動單次 | `.\Run-Manual-Once.cmd` | 輸入 INVOKE ONCE 後才核准一次 |
| 持續 ON／OFF | `.\Run-Auto-Accept.cmd` | 選取 App 視窗；按 O 開啟、F 停用、Q 退出，不需 Enter |

需要 Windows 10／11 與 .NET Framework 4.8／4.8.1，不需 Node.js。自動模式每次啟動預設 OFF；控制台必須保持執行。這不是 VS Code／Codex CLI 工具，也不是上方的系統匣 GUI 版。

完整安裝、選取視窗、參數、報告路徑、摘要、結果判讀及移除方式見 **[codex-uia-probe-windows 操作指南](docs/CODEX_UIA_PROBE_WINDOWS.md)**；進階行為見 [Probe 詳細說明](desktop-probe/README.md)。完整 UIA 報告可能含公司資訊，留在本機即可。

## VS Code 安裝：Codex 側邊欄

### 環境需求

- VS Code `1.96.2` 以上。
- 已安裝並可正常使用官方 Codex 擴充套件，ID：`openai.chatgpt`。
- 開啟信任的本機 workspace；Restricted Mode 不會自動核准。
- 本套件與官方 Codex 必須位於相同 extension host。Remote SSH、WSL、Dev Containers 需特別確認安裝位置，尚未完成端到端驗證。
- GUI 修補目前核對的官方版本是 `26.1002.51308`；未知程式結構會拒絕修補。

### 首次安裝：每個 VS Code 設定檔都要確認整合

1. 下載 `codex-autoaccept-0.1.1.vsix`。
2. 開啟 Extensions／擴充功能，按 `…` → **Install from VSIX…／從 VSIX 安裝…**，選取下載檔案。
3. 如 VS Code 要求，重新載入視窗。
4. 開啟專案；確認為信任的 workspace。
5. 按 **Ctrl+Shift+P**（macOS：**Cmd+Shift+P**），執行：

   ```text
   Codex Auto-Accept: Install / Repair Codex GUI Integration
   ```

6. 整合安裝成功後，按 **Reload Window**，或執行 `Developer: Reload Window`。
7. 點右下角 **Codex Auto-Accept: OFF**，切換為 **ON**。也可執行 `Codex Auto-Accept: Toggle`。
8. 在 Codex 側邊欄觸發一個需要核准、且未被規則排除的操作。整合會直接回應單次核准，不需要按「允許一次」。

**狀態列 ON 僅表示使用者設定已啟用，不代表 GUI 整合已安裝或載入。** 拿到另一台電腦安裝時，仍須完成第 5、6 步。0.1.1 曾出現「ON 但仍需按允許一次」的實測案例，原因就是漏做 GUI 整合。

### 日常操作與更新

- 點右下角開關切換 ON／OFF；設定儲存後立即刷新。
- `OFF`：保留人工核准。`ON`：依規則自動核准。`DRY RUN`：只記錄決策。`SETTINGS ERROR`：設定格式錯誤，自動核准暫停。
- 狀態列括號中的數字是本次 extension host 執行期間的 **CLI** 自動送鍵次數；GUI 核准請看 GUI log。
- 更新本套件後重新載入視窗；GUI runtime 的更新也需要重新載入才能生效。
- 官方 Codex 更新後，曾安裝整合的設定檔會嘗試重新修補。請查看 log；必要時執行 Install / Repair 並重新載入。未來版本不保證相容。
- 開關寫入失敗時會顯示原因並保留實際儲存狀態，快速連點會依序處理。

## VS Code 設定

開啟 Settings，搜尋 `codexAutoAccept`，或執行 **Preferences: Open User Settings (JSON)**。本套件僅讀取目前 VS Code 設定檔的**使用者設定**，不採用專案 `.vscode/settings.json` 的啟用值或規則。

| 設定 | 預設值 | 說明 |
| --- | --- | --- |
| `codexAutoAccept.enabled` | `false` | 總開關；右下角按鈕會修改此值 |
| `codexAutoAccept.guiEnabled` | `true` | 啟用已安裝整合的 GUI 核准處理 |
| `codexAutoAccept.terminalEnabled` | `true` | 監聽新啟動的整合終端機 Codex CLI |
| `codexAutoAccept.approveFileChanges` | `true` | 允許辨識到的檔案變更核准 |
| `codexAutoAccept.dryRun` | `false` | 只記錄，不回應核准或送鍵 |
| `codexAutoAccept.allowPatterns` | `[]` | JavaScript regex 白名單；空值不額外限制，非空需匹配至少一條 |
| `codexAutoAccept.denyPatterns` | `[]` | 額外 JavaScript regex 拒絕規則，優先於白名單 |
| `codexAutoAccept.terminalColumns` | `120` | CLI shadow terminal 欄數；20–1000 |
| `codexAutoAccept.terminalRows` | `40` | CLI shadow terminal 列數；10–300 |

範例：

```json
{
  "codexAutoAccept.enabled": true,
  "codexAutoAccept.guiEnabled": true,
  "codexAutoAccept.terminalEnabled": true,
  "codexAutoAccept.approveFileChanges": true,
  "codexAutoAccept.dryRun": false,
  "codexAutoAccept.allowPatterns": [],
  "codexAutoAccept.denyPatterns": ["\\bsudo\\b"],
  "codexAutoAccept.terminalColumns": 120,
  "codexAutoAccept.terminalRows": 40,
  "terminal.integrated.shellIntegration.enabled": true
}
```

規則不是 glob。GUI 命令比對命令文字，GUI 檔案比對核准 metadata，CLI 比對畫面中的核准區塊。無效設定或 regex 會保留人工核准。內建拒絕規則涵蓋部分遞迴刪除、Git 強制推送/reset/clean、破壞性 SQL、遠端腳本直接執行等；額外規則不能移除內建限制。

規則只是文字比對，無法辨識所有 shell 語意、間接腳本或隱藏副作用。GUI 檔案核准請求可能沒有 diff；若需要逐一查看 diff，請設 `approveFileChanges: false`。

## VS Code 命令與 log

| 命令 | 用途 |
| --- | --- |
| `Codex Auto-Accept: Toggle` | 切換總開關 |
| `Codex Auto-Accept: Install / Repair Codex GUI Integration` | 首次安裝或修復 GUI 整合；完成後重新載入 |
| `Codex Auto-Accept: Restore Original Codex Extension` | 還原官方 bundle；完成後重新載入 |
| `Codex Auto-Accept: Start Codex CLI` | 建立新 terminal，等 shell integration 後啟動 `codex` |
| `Codex Auto-Accept: Show Log` | 開啟本套件 Output channel |

開啟 **View → Output／檢視 → 輸出**，從下拉選單選擇：

- **Codex Auto-Accept**：開關、GUI 整合安裝結果與 CLI 決策。
- **Codex Auto-Accept GUI**：辨識到的 GUI 核准決策，顯示 `ACCEPT` 或 `MANUAL`；此 channel 在有決策紀錄時才建立。

紀錄不包含完整命令、檔案路徑、提示或工具參數。

## VS Code 整合終端機 Codex CLI

此情境仍待公司環境實測。

1. 確認 terminal 中可執行 `codex --version`。
2. 啟用 `terminal.integrated.shellIntegration.enabled`，使用支援的 PowerShell、bash、zsh 或 fish，必要時重開 terminal。
3. 確認本套件已啟動並切到 ON。
4. 執行 `Codex Auto-Accept: Start Codex CLI`，或在有 shell integration 的新執行階段直接執行 `codex`。
5. Output 應有 `Watching Codex CLI execution.`；辨識成功後出現 `CLI ACCEPT once`。

首次測試建議設 `dryRun: true`，觀察 log 後再關閉 dry run。

此版只監聽直接執行的 `codex`、絕對路徑 `codex` 或 `codex.exe`。不監聽 `npx`、npm、alias、wrapper，也不能接上已在執行中的 Codex。**Windows npm shim `codex.cmd` 不在目前直接監聽的辨識範圍內**，若未出現 Watching 紀錄，需確認 shell 回報的命令；可改用獨立 Windows CLI 包裝器測試。

CLI 目前只辨識官方**英文完整核准選單**，包含 `Yes, proceed (y)` 等選項，送出單次 `y`，不送 Enter。這個限制與 VS Code 介面語言無關；GUI 不比對文字，CLI 則比對 terminal 畫面。

穩定 VS Code API 沒有一般 terminal 尺寸通知；shadow terminal 預設 120 欄 × 40 列。請讓設定與實際 terminal 尺寸相符。尺寸不同、翻譯、截斷、重繪或提示改版可能無法辨識，改變尺寸後建議重啟 Codex。輸出刻意模仿完整核准選單仍可能被辨識。

## Windows Terminal 獨立 CLI 包裝器

0.1.15 修正 B005 把報告中的 Git 操作說明與後面的檔名串成危險指令的誤判，真正的 hard reset、force clean／push 仍保留人工確認。包含既有分頁改善、變數誤判修正與 log 輪替；公司 Windows 最新案例待確認，完整變更與已知限制見 [CLI README](cli/README.md)。

此成品為 **Windows 測試版**，不需要 VS Code，不必安裝 GUI 整合。需要支援 ConPTY 的 Windows 10 1903／build 18362 以上或 Windows 11、原生 Windows Node.js 22／24（x64／ARM64），以及已可使用的 Codex CLI。ZIP 含 Windows 原生依賴，**不包含 Node.js 或 Codex**。

### 安裝與自我測試

1. 解壓縮 `codex-autoaccept-cli-windows-0.1.15.zip`。
2. Windows Terminal 開啟 PowerShell，進入解壓縮後的 `codex-autoaccept-cli-0.1.15` 目錄。
3. 執行：

   ```powershell
   node --version
   node -p "process.platform + ' ' + process.arch"
   .\codex-auto.cmd --auto --self-test
   ```

4. 預期顯示 `SELF-TEST PASS: received y through PTY`。此測試使用模擬選單，不啟動 Codex、不呼叫 AI、不執行選單中的命令。

ZIP 已含 npm 提供的 Windows x64／ARM64 `node-pty` 依賴，正常解壓後不需 npm 安裝。只有從原始碼安裝或重建依賴時，才在 `cli/` 執行 `npm.cmd ci`。

### 啟動與操作

從自己的專案目錄，用包裝器完整路徑啟動，保持 Codex 的工作目錄在專案內：

```powershell
cd C:\work\my-project
& 'C:\tools\codex-autoaccept-cli-0.1.15\codex-auto.cmd' --auto
```

請將路徑換成你的解壓縮位置。預設 OFF，`--auto` 開啟，`--off` 關閉；執行中 **Ctrl+]** 切換，狀態不寫回 config。若鍵盤無法送出熱鍵，退出後以啟動參數切換。

Codex 自己的參數必須放在 `--` 後：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.15\codex-auto.cmd' --auto -- resume
```

首次真實測試建議 dry run：

```powershell
& 'C:\tools\codex-autoaccept-cli-0.1.15\codex-auto.cmd' --auto --dry-run
```

`--dry-run` 只記錄，不送鍵；Ctrl+] 不會解除 dry run。一般輸入、Ctrl+C、貼上和方向鍵轉送給 Codex。

### CLI 設定與排除問題

- `--config 'C:\tools\config.json'`：明確指定 JSON；不自動讀專案檔案。
- 支援 `enabled`、`dryRun`、`approveFileChanges`、`allowPatterns`、`denyPatterns`；修改檔案後重啟。
- `--codex 'C:\tools\codex.exe'`：指定執行檔；也可指定官方 npm `bin\codex.js`。
- `--log 'C:\temp\codex-auto.log'`：指定 log；預設 `%LOCALAPPDATA%\CodexAutoAccept\cli.log`。
- npm 安裝的 Codex 會由 PATH 的 shim 定位官方 JS entry，再直接使用 Node 啟動；不以 shell 字串拼接參數。不支援 PowerShell alias/function。
- 與 VS Code 版相同，目前只辨識完整英文核准選單。包裝器額外包含 Windows 刪除、格式化及 `Invoke-Expression` 等拒絕規則。
- 未驗證 WSL、Git Bash、SSH；不支援輸入／輸出重新導向。

完整參數、設定範例、log 和驗收步驟見 [CLI README](cli/README.md)。

0.1.8 修正官方 `[… 數字 lines] ctrl+a view all` 展開提示的漏辨識。CLI 設定 `expandLongCommands` 預設啟用：遇到明確展開提示時，嘗試讀取官方完整命令檢視器、檢查規則，再返回同一核准提示。只支援可辨識的預設介面；不完整或未知狀態保留人工確認。可先用 `--auto --self-test-expanded` 測試。完整限制見 [CLI README](cli/README.md)。公司 Windows 實測待回報。

## GUI 原理、相容性與排除問題

VS Code 未提供本套件可用的跨擴充套件 Webview DOM 核准控制 API。本版在本機修補官方 `openai.chatgpt/out/extension.js`：在 app-server 核准請求分派給 UI 前套用規則，回傳一次性的核准結果。**不靠「Allow once／允許一次」按鈕文字，因此 GUI 不限定英文。**

支援 `item/commandExecution/requestApproval`、`item/fileChange/requestApproval`，以及舊版 `execCommandApproval`、`applyPatchApproval`。不自動回答使用者問題、MCP elicitation 或其他額外 permissions。即使 ON，未支援的請求與被規則排除的操作仍會出現人工核准。

安裝會保留原檔備份、SHA-256 metadata，確認唯一程式特徵及 JS 語法後才寫入。更新或外部修改造成 hash 不一致時會拒絕覆寫。

| 現象 | 檢查方式 |
| --- | --- |
| ON 但仍顯示「允許一次」 | 先執行 Install / Repair GUI Integration，再 Reload Window；ON 不表示整合就緒 |
| `Unsupported ... bundle/layout` | 提供官方 Codex 版本及整合 log；需重新核對程式結構 |
| GUI 安裝權限錯誤 | 查看實際錯誤；本套件需要能寫入該官方擴充套件目錄 |
| `SETTINGS ERROR` | 修正使用者設定中的型別、尺寸或 regex |
| Log 顯示 MANUAL | 檢查 dry run、拒絕規則、白名單及檔案核准設定 |
| 無 GUI 決策紀錄 | 檢查整合載入、相同 extension host、workspace trust、`guiEnabled` 及請求是否支援 |
| CLI 無 Watching 紀錄 | 確認新啟動階段、shell integration 及直接命令格式 |
| CLI 有 Watching 但沒核准 | 確認完整英文選單、shadow terminal 尺寸及規則 |

回報問題請提供 OS、VS Code／官方 Codex／本套件版本、使用情境、操作步驟及 metadata log；CLI 再附 Node 版本與架構。請避免貼上公司敏感命令或路徑。

## 停用、還原與移除

VS Code：先切 OFF → 執行 **Codex Auto-Accept: Restore Original Codex Extension** → 重新載入 → 解除安裝。還原前會核對 hash；若被其他工具修改而拒絕還原，可重新安裝官方 Codex 取得原檔。只停用或解除安裝本套件會停止自動核准，但修補和備份仍留在磁碟，完整清除需先還原。

Windows CLI：退出包裝器後刪除解壓縮目錄；如需清 log，另刪 `%LOCALAPPDATA%\CodexAutoAccept`。不需還原 Codex 安裝。

## 原始碼、建置與版本

```sh
git clone https://github.com/kevintsengtw/codex-autoaccept-extension.git
cd codex-autoaccept-extension
npm ci
npm run check
npm test
npm run package
```

`npm run package` 會先 build 再產出 VSIX；Windows PowerShell 可使用 `npm.cmd`。建置 VSIX 不會修改本機官方 Codex bundle，GUI 修補由使用者執行整合命令觸發。

獨立 CLI：

```sh
npm --prefix cli ci
npm run test:cli
npm run package:cli
```

CLI ZIP 打包腳本需要 Python 3，成品會包含 npm 提供的 Windows 原生依賴，排除 macOS binaries 與 debug symbols。repo 的 `artifacts/` 保存已交付成品；修改來源後需重新建置、測試並更新 checksum。

| 目錄 | 內容 |
| --- | --- |
| `src/` | VS Code 開關、CLI detector、GUI 決策與 bundle 修補 |
| `test/` | 單元、lifecycle 及隔離 Extension Host 測試 |
| `cli/` | 獨立 CLI 包裝器、設定範例與說明 |
| `scripts/` | Windows CLI ZIP 打包工具 |
| `docs/` | 驗證紀錄 |
| `artifacts/` | 已打包 VSIX、Windows ZIP 及 checksum |

0.1.1 修正開關寫入與刷新、快速連點、失敗回報，並保留錯誤設定下的使用者開關意圖；自動核准仍暫停至設定修正。Windows CLI 測試版為 0.1.15，修正 B001 把 `$rd` 等 PowerShell 變數當成刪除命令的誤判，真正的刪除命令仍保留人工確認。包含小可見區域展開修復與 B004 工具清單誤判修正，公司 Windows 最新案例實測待確認。log 預設每檔 5 MiB、3 份備份，總量約 20 MiB，舊大檔會保留近期完整紀錄後縮減。拒絕紀錄包含規則 ID／類別，回報時不需要命令全文；設定與分類表見 [CLI README](cli/README.md)。

## 參考來源與授權

主要參考為使用者指定的 **Claude Code Auto-Accept**：

- 原始碼 repo：[varfmx/claude-code-auto-accept](https://github.com/varfmx/claude-code-auto-accept)。讀取 `src/extension.ts`，參考 commit：[`9e38ba1a974db9754f6519e42e6e5e7c7d46800b`](https://github.com/varfmx/claude-code-auto-accept/tree/9e38ba1a974db9754f6519e42e6e5e7c7d46800b)。
- 該參考套件的 VS Code Marketplace：[Claude Code Auto-Accept — varfmx](https://marketplace.visualstudio.com/items?itemName=varfmx.claude-code-auto-accept)。**這是 Claude 參考套件的頁面，不是本專案的安裝頁面。**

參考其狀態列開關、Terminal Shell Integration 監聽和辨識後送鍵的設計。本專案另行實作 Codex detector、設定與規則、GUI RPC 整合，以及 Windows CLI 包裝器；不是原專案的官方 Codex 發行版。原參考套件以 terminal 為單位啟用，本套件使用目前 VS Code 設定檔的總開關。

其他技術參考：

- [DevCloudOps1/claude-auto-accept](https://github.com/DevCloudOps1/claude-auto-accept)：提示辨識、規則與 Codex 單次 `y` 核准設計。
- [OpenAI Codex 原始碼](https://github.com/openai/codex/tree/main/codex-rs/tui/src/bottom_pane)：核准選單與 snapshot。
- [Microsoft node-pty](https://github.com/microsoft/node-pty)：PTY／Windows ConPTY。
- [xterm.js](https://github.com/xtermjs/xterm.js)：ANSI terminal 畫面重建。

本專案採 [MIT License](LICENSE)。VSIX 的第三方元件授權見 [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md)；CLI ZIP 保留依賴各自的 LICENSE。本專案與 OpenAI、VS Code 及參考專案作者無隸屬關係。
