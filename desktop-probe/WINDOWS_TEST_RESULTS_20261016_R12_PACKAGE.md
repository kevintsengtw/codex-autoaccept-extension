# R12 掃描上限修正版驗證與封裝 — 2026-10-16

本輪重新建置及驗證 2026-10-15 已簽入的掃描上限修正，另製作桌面 ZIP。沒有操作或重啟 App，也沒有執行真實核准；程式回歸不代表公司長時間工作已驗收。

## 來源與環境

- 來源提交：`17f735ec7f4e92f249f498374d5cb2e0e44acf58`。
- Windows `10.0.26300.0`、PowerShell `7.6.7`、.NET SDK `10.0.402`、Python `3.15.0`。
- 測試根目錄：`C:\Temp\codex-desktop-node-recovery-package-20261016-01`；來源及建置輸出在其 `work` 子目錄，測試暫存檔在 `test-temp`。
- 正式 C#、專案、依賴鎖定檔及原有測試未修改。本輪僅補齊打包清單、拒絕覆寫既有 ZIP、更新說明並新增成品；既有 console-probe 未簽入修改另行保留。
- 保持組件與前後端版本 0.3.10，以完整 ZIP 名稱識別 R12；不混用原 R11 的 EXE 或 DLL。

## 本輪實際檢查

在隔離來源根目錄執行：

```powershell
.\scripts\check-desktop.ps1 -TestTempDirectory C:\Temp\codex-desktop-node-recovery-package-20261016-01\test-temp
```

臺北時間 **2026-10-16 17:49:15–17:49:49**，耗時 **33.89 秒**，退出碼 **0**。

| 項目 | 結果 |
| --- | --- |
| Probe、Helper、Tray 三個 net48 Release 建置，locked restore | 通過；建置零警告、零錯誤 |
| Scan、Background、Launch、Transport、Diagnostics、Desktop、Permission、Auto、Manual、Summary、Log 十一組測試 | 通過；PermissionTests 有既有 CS0649 警告 |
| 三個建置 EXE 的 API 稽核 | 通過；保留單一 COM Invoke、焦點設定與讀回、既有原生 API 範圍 |

Scan 直接使用正式 UIA／MSAA 掃描、節點上限恢復與雙掃描協調程式。涵蓋 2999／3000／3001、5999／6000／6001、9999／10000／10001，超限時不得採用部分候選，擴大後重新初掃與重驗，以及多候選、深度／循環／讀取錯誤、OFF 代次、未知結果不重送與紀錄失敗的限制。詳細案例沿用 `WINDOWS_TEST_RESULTS_20261015_NODE_LIMIT.md`，上述結果是本輪重新執行，並非引用前次 PASS。

## 新 ZIP

檔名：`codex-autoaccept-desktop-windows-0.3.10-nofocus-r12-node-recovery.zip`。

```powershell
python .\scripts\package-probe.py --name codex-autoaccept-desktop-windows-0.3.10-nofocus-r12-node-recovery
```

ZIP 內的 `SHA256SUMS` 記錄所有其他封裝檔案；外部雜湊記在交付目錄的 `SHA256SUMS`。封裝清單補入 `NodeScanBudget.cs`、`CompleteTreeScan.cs`、`AutoScanCycle.cs`、`AccessibleRefreshScan.cs`、`NodeRecoveryTests.cs` 及 10 月 15／16 日驗證文件，不加入本機 log、截圖或工作資料。內含 EXE／DLL 必須與本輪建置及稽核的檔案逐檔相符。

原 `codex-autoaccept-desktop-windows-0.3.10-nofocus-r11-verified.zip` 保留，SHA-256 維持：

```text
15c5acc336e85d3652dc4e3241b45e84a3052c9fd25d08135ed3597a0d34f174
```

原 R11 candidate 及其他歷史 ZIP 亦不覆寫。打包腳本遇到同名既有成品會拒絕寫入。

## 使用範圍與限制

- 完整解壓至新資料夾後使用 `CodexAutoAcceptDesktop.exe`。更換時只需退出舊版工具，App 保持開啟；啟動後預設 OFF，須人工開啟 ON。
- 預設系統匣自動模式遇到明確節點超限，依序使用 3000、6000、10000；每次從根元素重新完整掃描及重驗。10000 仍不完整時拒絕本輪核准，保持 ON 等待。
- 舊 `Run-Auto-Accept.cmd`、手動、唯讀或明訂 `--max-nodes` 的入口維持固定上限，不能將它們視為本次系統匣修正版的測法。
- 單次掃描維持 45 秒逾時；提高上限不解除 A05／A06 去重等待、未知結果或其他拒絕條件。
- 未執行目前 App 的背景／焦點矩陣、真實長時間多次核准、公司版本或鎖定情境。R11 在 2026-10-14 的 24 案為歷史證據，不能當作新 ZIP 的實機結果。
- 沒有建立 tag、Release、推送或發布；本機原始檢查紀錄保留在上述測試目錄的 `evidence`。
