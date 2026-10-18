# 控制台版 0.2.2 Windows 驗證結果 — 2026-10-15

## 簽入前核對與待驗證項目 — 2026-10-18

本次核對確認，程式差異僅為兩個 csproj 各新增一行 `PlatformTarget=AnyCPU`，未修改 C#、依賴版本、兩份 lock 或 `check.ps1`。目前 36 個來源輸入與 2026-10-15 通過完整檢查的隔離副本及其 SHA-256 清單逐檔相符；兩個建置、六組測試與兩項 API 稽核的結果均來自該日 14:16:08–14:16:27 的實際紀錄，本日未重跑完整回歸。

原交付 `codex-uia-probe-windows-0.2.2.zip` 的 SHA-256、CRC 及 44 個內部檔案雜湊重新核對通過。ZIP 仍是原始交付版本，未包含這兩行專案宣告；本次簽入不新增或重封裝成品。

**真實 Q 返回 PowerShell、至少兩小時持續 ON、自然核准與原長時間失效皆維持待驗證。** 下方 `BLOCKED` 是 10 月 15 日當時的執行狀態，沒有新增實測或執行期問題已修復的結論。

10 月 18 日依使用者指示清理歷史工作目錄後，最新成功回歸的原始紀錄與隔離來源仍保留在 `C:\Temp\codex-console-022-anycpu-fix-20261015-a206609e`；較早目錄已清除，失敗摘要保留於本文件。下方尚未 commit／push、保留目錄等敘述均為 10 月 15 日的歷史狀態。

## 2026-10-15 驗證摘要

當日程式回歸：兩個 csproj 明訂 `PlatformTarget=AnyCPU` 後，保留原 lock，未修改的 `check.ps1` 完整通過。修正前的 `NU1004` 失敗保留於下方歷史紀錄。真實 Q 返回 PowerShell 及長時間 ON 驗收仍為 **BLOCKED**，不能判定原本的執行期問題已修復。

## 成品與環境

- Repository：<https://github.com/kevintsengtw/codex-autoaccept-extension>
- 交付 commit 與驗證時 HEAD：`dda25f1ee7564f1f3e2c9da1eaab296bc9089313`。此次兩行專案修正尚未 commit，版本號維持 0.2.2。
- 初次驗證的 `git status --porcelain=v1` 為空；當時執行 `git pull --ff-only` 回報 Already up to date，HEAD 未變更。後續保留既有未追蹤報告，沒有再次 pull。原本另一個工作區的既有修改未處理。
- 待測 ZIP：`artifacts/codex-uia-probe-windows-0.2.2.zip`。
- ZIP SHA-256：`a3c96c871285236ec6f1d5ab73456543069b31bdc19e5781c4d18aa8af2a8ad8`，與指定值相符，**PASS**。
- ZIP 保持原始交付位元組，未重封裝，**不包含此次 csproj 修正**；本次回歸驗證的是修正後來源。
- ZIP 解壓到全新 `C:\Temp` 子目錄；45 個 ZIP entry，`SHA256SUMS` 列出 44 個檔案，全部相符、沒有漏列檔案，**PASS**。
- Windows 11 專業版，`10.0.26300`，64 位元；PowerShell `7.6.7` Core，一般使用者權限（IsAdministrator=false）。
- Codex 套件版本 `26.1007.9446.0`；其 `ChatGPT.exe` 檔案版本 `155.0.8059.40`。ChatGPT Desktop 套件版本 `1.2026.190.0`，檔案版本 `1.2026.190`。未執行 App 更新檢查或更新。
- .NET SDK `10.0.402`；.NET 8 runtime `8.0.32`；.NET Framework 註冊版本 `4.8.10008`、Release `534296`。
- 初次環境記錄起點：2026-10-15 13:53:16；該輪程序／檔案結束核對：2026-10-15 13:57:03，均為臺北時間。這段期間不是 ON 觀察時間。

## 修正前回歸（歷史紀錄）

已閱讀 `console-probe/README.md`、`VERIFICATION.md`、`check.ps1`、`Run-Auto-Accept.cmd`。從乾淨的交付 commit 複製 36 個受版本控制的來源檔到 `C:\Temp`，逐檔核對 SHA-256 後，在隔離副本原樣執行 `check.ps1`。未改專案、lock file、產品程式、全域設定或 execution policy。

| 項目 | 結果 | 實際範圍與限制 |
| --- | --- | --- |
| 原樣 `check.ps1` | **FAIL** | 第一個 build 的鎖定還原回報 `NU1004`，腳本立即停止，沒有執行後續項目。 |
| Probe net48 Release build | **FAIL** | 本環境要求的 RuntimeIdentifier `win-x86` 與交付 lock 不一致；建置輸出為 0 警告。 |
| Helper net48 Release build | **FAIL** | 另以腳本的相同建置命令獨立執行，同樣 `NU1004`、exit 1、0 警告。 |
| Auto suite | **PASS** | 補充獨立執行，exit 0；模擬生命週期、20 次連續核准、去重及併行。 |
| Manual suite | **PASS** | 補充獨立執行，exit 0；六類手動閘門情境。 |
| Summary suite | **PASS** | 補充獨立執行，exit 0；十項摘要與隱私檢查。 |
| Recovery suite | **PASS** | 補充獨立執行，exit 0；合成樹、預算擴大、完整重掃、部分掃描拒絕與去重。 |
| Observation suite | **PASS** | 補充獨立執行，exit 0；根元素／候選例外隔離、未知結果保留去重。 |
| Launcher suite | **PASS** | 補充獨立執行，exit 0；Windows cmd.exe 執行立即返回 17 的無害 stub，未送出 Q、未操作 UIA。 |
| Probe API audit | **PASS** | 補充執行，exit 0；唯讀檢查已核對 ZIP 的實際 Probe EXE metadata。 |
| Helper API audit | **PASS** | 補充執行，exit 0；唯讀檢查已核對 ZIP 的實際 Helper EXE metadata。 |

六組補充測試與兩項稽核均有 PASS 標記，未見警告。原樣腳本所要求的「新建置 EXE 稽核」未能執行；交付 ZIP EXE 的稽核是另外的結果，不代表建置成功。兩份 lock 都未包含 `win-x86` target，而 csproj 未明訂 RID；本輪只確認本環境的 locked restore 不一致，不推論是 .NET 10 專屬缺陷、缺套件或網路失敗，也未解除鎖定來繞過錯誤。

Launcher stub 不讀 Q，不能支持五秒返回、退出碼 0、`CONSOLE_RETURNED` 或 `PS_READY` 的真實驗收。API 稽核只檢查組件參考，不證明 Windows provider 行為。

## AnyCPU 最小修正後回歸

經使用者授權，只在 `console-probe/CodexUiaProbe.csproj` 與 `console-probe/manual/CodexUiaManualInvoke.csproj` 的既有 PropertyGroup 各新增一行 `<PlatformTarget>AnyCPU</PlatformTarget>`。保留 `Prefer32Bit=false`、`RestorePackagesWithLockFile=true`、兩份原 lock 與原樣 `check.ps1`。

根因是 .NET SDK 在 Windows 上為未明訂 PlatformTarget 的 net48 EXE 推導 `win-x86`，與交付 lock 的空 RID 集合不一致。明訂 AnyCPU 使這個隱含 x86 RID 推導條件不成立；本環境不需解除 locked mode 或重新產生 lock。

將修正後 36 個來源輸入（含兩份 lock）複製至全新 `C:\Temp\codex-console-022-anycpu-fix-20261015-a206609e\patched-source`，逐檔核對 SHA-256 後，在隔離副本執行未修改的 `console-probe/check.ps1`，未額外加入 MSBuild 屬性。執行時間為 **2026-10-15 14:16:08 至 14:16:27（臺北）**，約 **18.33 秒**。

| 項目 | 結果 | 實際範圍與限制 |
| --- | --- | --- |
| 完整 `check.ps1` | **PASS** | 腳本成功返回，最終退出碼 0，完整成功標記存在。 |
| Probe net48 Release build | **PASS** | 原 locked build 命令成功，0 警告。 |
| Helper net48 Release build | **PASS** | 原 locked build 命令成功，0 警告。 |
| Auto suite | **PASS** | 由完整腳本執行，模擬生命週期與去重。 |
| Manual suite | **PASS** | 由完整腳本執行，手動閘門邏輯。 |
| Summary suite | **PASS** | 由完整腳本執行，摘要與隱私檢查。 |
| Recovery suite | **PASS** | 由完整腳本執行，合成樹與預算擴大；覆蓋限制仍如下表。 |
| Observation suite | **PASS** | 由完整腳本執行，例外隔離與未知結果去重。 |
| Launcher suite | **PASS** | 由完整腳本執行；仍是立即返回 17 的無害 stub，不讀 Q、不操作 UIA。 |
| Probe API audit | **PASS** | 稽核本輪隔離副本新建置的 Probe EXE metadata。 |
| Helper API audit | **PASS** | 稽核本輪隔離副本新建置的 Helper EXE metadata。 |

六組測試與兩項稽核均有各自成功標記，所有步驟均通過腳本退出碼檢查；未見錯誤或警告。測後隔離副本的 36 個來源輸入未改變；repository 兩份 lock 的 SHA-256 與修正前完全一致，`check.ps1` 亦未變更。

本次結束保全核對為 **2026-10-15 14:17:05（臺北）**：`desktop-probe/` 與 `artifacts/` 的 465 個檔案、R11 設定及交付 ZIP 均未變更；Codex／ChatGPT 兩個原主程序的 PID、啟動時間與路徑相同；未啟動產品 EXE，沒有殘留以本輪測試目錄執行的程序。原始 log、程序清單與雜湊證據只保存在本機 `C:\Temp\codex-console-022-anycpu-fix-20261015-a206609e`，未加入 repository。

這次完整回歸只確認來源建置、程式測試與 API 稽核通過；下列真實 Q、持續 ON 與自然核准項目未新增執行證據，維持原狀。

## 真實互動與長時間觀察

本會話的 computer-use 技能所要求遵守的操作規範明文禁止：`Do not automate terminal applications such as Windows Terminal, Command Prompt, or Windows PowerShell.` 因此未啟動產品 EXE，未代按 O／Q，也未使用 stdin、管線、重新導向或其他方法替代互動式按鍵。

| 驗收項目 | 結果 | 實際資料 |
| --- | --- | --- |
| OFF 狀態按一次 Q，五秒內返回原 PowerShell | **BLOCKED** | 未取得允許的互動終端操作方式；延遲、退出碼 0、返回標記與 PS_READY 均未驗證。 |
| 同一視窗是否已有另一套監聽工具 | **未覆蓋** | 前後程序清單未發現已知 Probe／Helper／AutoAccept／R11 程序；這項盤點不足以排除未知工具，也未確認特定 HWND 的監聽狀態。 |
| 重新啟動預設 OFF、按 O 後持續 ON 至少兩小時 | **BLOCKED** | 產品未啟動，實際 ON 觀察時間 **0 小時**。 |
| 自然核准與後續工作進度 | **未覆蓋** | 0.2.2 實測核准次數為 0（未執行）；未觀察自然請求數，不能解讀為沒有請求。未產生本輪產品 auto log。 |
| 原長時間失效是否重現 | **未覆蓋** | 沒有真實 ON 長測，沒有可歸屬於本輪的 AUTO_REASON。不能判定已排除或修好。 |
| 長測後按一次 Q 返回 PowerShell | **BLOCKED** | 未進行長測及互動 Q；所有返回判準與延遲均未驗證。 |

沒有插入工作指令、測試訊息或 marker，也沒有切換聊天、人工核准、反覆切 ON 或重啟 helper。

## 分支覆蓋邊界

| 分支 | 程式測試 | 真實 App 自然觸發 |
| --- | --- | --- |
| NODE_LIMIT：3000 → 6000 → 10000；完整重掃／重驗 | Recovery **PASS**（7,501 節點合成樹） | **未覆蓋** |
| DEPTH_LIMIT：60 → 80 → 100；明訂上限不擴大、最大值不再增長 | Recovery **PASS**（合成樹） | **未覆蓋** |
| ROOT_CHANGED／TARGET_CHANGED | 分類與提示檢查 **PASS**；實際切 OFF 未驗證 | **未覆蓋** |
| 未知核准結果不重送，OFF／ON 不清除去重 | Auto／Recovery／Observation **PASS**（模擬） | **未覆蓋** |
| 超過 10,000 節點／100 層時的最高上限拒絕 | 僅驗證最大預算不再增長，未以超出硬上限的樹執行拒絕案例 | **未覆蓋** |
| 其他 AUTO_REASON 與 provider 時序 | 僅上述 suite 的明列範圍 | **未覆蓋** |

未為觸發分支而修改 App、聊天或任何工作。

## 初次驗證的保全與本機證據（歷史紀錄）

- **PASS**：Codex 與 ChatGPT Desktop 原來兩個主程序在結束核對時均保持相同 PID、啟動時間及執行路徑。原始 PID／啟動時間只記錄於本機證據；未關閉、重啟、終止或更新 App。
- 未發送取消或新增工作指令；未另行讀取工作內容或驗證工作進度。程序身分一致不當成工作完成的證明。
- **PASS**：`desktop-probe/` 與 `artifacts/` 共 465 個檔案，前後 SHA-256 全部一致；既有 R11 設定亦一致。已知工具程序盤點為 0 → 0，本輪未啟停 R11；未知程序與視窗綁定仍未覆蓋。
- **PASS**：隔離回歸的 36 個來源輸入前後雜湊未變；產品 ZIP 未重封裝。本輪未 commit、push、提權或變更企業政策。
- 本輪測試子程序已全部結束，未啟動兩個產品 EXE，無需終止或清理其他程序。保留全部本輪證據與測試副本。
- 原始 log、程序清單及設定／檔案雜湊只存於 Windows 本機：`C:\Temp\codex-console-022-validation-20261015-f81c77bc`。Repository 僅新增這份必要資料摘要，未加入原始 log、畫面或工作內容。

2026-10-15 結論（歷史）：保留原 lock，兩個專案各新增 AnyCPU 後，完整 `check.ps1` 已通過。修正前 NU1004 失敗與本次通過分別保留；ZIP 仍為原始交付版，真正的 Q 返回與長時間自動核准仍為 **BLOCKED**。本輪未重封裝、commit 或 push，也未操作 R11 或 App。
