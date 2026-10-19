# R13 摘要時間、log 寫入與本機診斷重新驗證及封裝 — 2026-10-20

## 來源與範圍

基準提交為 `14b46e3f206c5b0bb6f86304af7f3c12e9327025`，包含 `d226af1` 的摘要日期時間及 `14b46e3` 的 log 寫入復原、本機診斷、測試與驗證文件。本輪重新建置及測試，不將 10 月 19 日結果當成本次 PASS；正式 C#、專案、依賴鎖定檔、原有測試及啟動批次檔均保持基準位元組。本輪只調整封裝清單、說明與交付成品。

隔離目錄為 `C:\Temp\codex-desktop-r13-package-20261020-01`；來源副本與建置在 `work`，測試暫存在 `test-temp`，原始結果在 `evidence`。本機 Windows `10.0.26300`、PowerShell `7.6.7`、.NET SDK `10.0.402`、Python `3.15.0`。產品目標 net48，既有測試目標 net8.0。使用獨立的三個既有公開套件快取、空的本機來源及 `NuGetAudit=false`；鎖定檔不變，沒有執行套件弱點資料查詢。

沒有執行產品入口、連接 App、執行核准、重啟 App、建立 tag、Release 或推送。公司資料不加入來源或 ZIP。

## 本輪重新驗證

在 `work` 根目錄執行：

```powershell
$taskRoot = 'C:\Temp\codex-desktop-r13-package-20261020-01'
$env:NUGET_PACKAGES = "$taskRoot\nuget"
$env:RestoreSources = "$taskRoot\offline-feed"
$env:NuGetAudit = 'false'
.\scripts\check-desktop.ps1 -TestTempDirectory "$taskRoot\test-temp"
.\scripts\verify-desktop-log-runtime.ps1 -EvidenceDirectory "$taskRoot\evidence\net48-runtime"
```

臺北時間 **2026-10-20 05:46:14–05:46:49**，完整檢查耗時 **35.15 秒**，退出碼 **0**：

| 項目 | 本輪結果 |
| --- | --- |
| Probe、Helper、Tray 三個 net48 Release 建置，locked restore | 通過，三個建置均零警告／錯誤 |
| Scan、Background、Launch、Transport、Diagnostics、Desktop、Permission、Auto、Manual、Summary、Log 十一組測試 | 通過；PermissionTests 保留既有 CS0649 警告 |
| 三個建置 EXE 的 API 稽核 | 通過，保留單一 COM Invoke、焦點設定與讀回、既有原生 API 範圍 |

摘要測試重新檢查同筆主檔與摘要時間一致、歷史錯誤／核准時間固定、L 序號、跨午夜、重讀不刷新、系統時間回退、文化及時區格式。log 測試重新檢查短暫主檔開啟衝突復原、持續占用、唯讀檔、部分寫入／flush 不重播、摘要與主檔雙重失敗、診斷另存失敗後介面資料保留、重連隔離、固定協定及隱私資料拒絕。合成錯誤碼不是公司根因的證明。

編譯後 net48 驗證於 **05:47:26–05:47:28** 通過，退出碼 **0**。只透過建置後組件的紀錄方法重現 Windows 檔案占用，確認暫時衝突解除後只追加一筆；持續占用時，實際失敗時間、MAIN_OPEN、Win32 32 及五次嘗試可經後端協定保存到前端獨立檔，主 log 仍鎖定時也可保存。沒有執行 `Main`、初始化 UIA 或核准操作。無視窗文字量測為 108 像素、低於 140 像素，只支持本機字型／DPI 的文字容量，不代表實際 GUI 視覺驗收。

封裝內容解壓復驗於 **05:51:51–05:52:04** 通過，耗時 **13.33 秒**，退出碼 **0**：三個封裝來源專案可重新建置，Log／Diagnostics 測試、三個隨附 EXE 的 API 稽核，以及使用隨附 Helper／Tray 的 net48 診斷流程均通過。第一次解壓驗證在編譯 net48 驗證程式時，因驗證腳本傳入來源的路徑解析問題出現 CS1504、退出碼 1；封裝內來源已存在且雜湊相符，改用絕對路徑後通過。原始失敗輸出與退出碼保留為 `extracted-verification-01.txt`／`.json`，沒有改寫成 PASS。

## R13 ZIP

檔名：`codex-autoaccept-desktop-windows-0.3.10-nofocus-r13-log-diagnostics.zip`。

```powershell
python -B .\scripts\package-probe.py --name codex-autoaccept-desktop-windows-0.3.10-nofocus-r13-log-diagnostics
```

封裝清單補上 `source/shared/LogWriteDiagnostic.cs`、`source/tray/LocalLogDiagnostics.cs`、`source/verify/LogRuntimeVerification.cs` 及 10 月 19／20 日驗證文件。保留既有來源、測試、授權與工具，不加入本機原始 log、截圖、工作資料或套件快取。組件與前後端版本仍為 0.3.10，R13 由完整 ZIP／資料夾名稱辨識。

封裝腳本拒絕覆寫同名既有 ZIP，並檢查內容、CRC 及逐檔位元組。交付前另以獨立稽核核對 ZIP 內全部 SHA-256、檔案清單、正式來源與本輪七個執行檔／設定／DLL，確認所有專案明訂的來源及完整 C#／專案／鎖定檔清單均已封裝；再從解壓來源重新建置並檢查 Log／Diagnostics，以及解壓後的 net48 紀錄流程。任何檢查未通過都不交付。最終 ZIP 的 SHA-256 記在外部 `artifacts/SHA256SUMS`；原始稽核結果留在本輪 `evidence`，不為加入自我雜湊而改寫 ZIP。

封裝清單包含 **87 筆內部雜湊**、合計 **88 個 ZIP 項目**；62 個正式／測試來源資產與基準逐檔相符，45 個專案明訂的 Compile 輸入齊全，七個 EXE／設定／DLL 與本輪受測建置一致。同名 ZIP 再次執行打包時明確拒絕，預期退出碼 1，ZIP 與外部雜湊清單未變。解壓復驗後只補入這些驗證文件，再以相同來源與受測成品產生交付檔並重新核對內容；第一份驗證用封裝留在本機證據，沒有覆寫 R11／R12。

原 R11、R12 保留，原始 SHA-256 分別為：

```text
15c5acc336e85d3652dc4e3241b45e84a3052c9fd25d08135ed3597a0d34f174  codex-autoaccept-desktop-windows-0.3.10-nofocus-r11-verified.zip
921f690c071273ae863473c0a21863badd06d8a05f584523aaffcb88f8c1f14c  codex-autoaccept-desktop-windows-0.3.10-nofocus-r12-node-recovery.zip
```

## 使用與待驗證項目

完整解壓至新的獨立資料夾，保留 R11／R12 ZIP。更換時由使用者將舊工具 OFF 並退出工具，App 保持開啟；再開啟新資料夾的 `CodexAutoAcceptDesktop.exe`，選擇目標並人工開啟 ON。本輪沒有替使用者執行切換。勿混用新舊 EXE 或 DLL，也不要以舊 `Run-Auto-Accept.cmd` 代替系統匣模式的節點上限測法。

R13 摘要包含本機日期時間、毫秒、UTC offset 及紀錄序號；主 log 寫入前遇到 Win32 32／33 時最多嘗試五次、間隔 100 毫秒。開始寫入後或其他持續錯誤仍停止新核准，不重寫不確定紀錄、不重送核准。失敗診斷另存 `%LOCALAPPDATA%\CodexAutoAcceptDesktop\diagnostics`，保留時間、階段、Windows 碼與嘗試次數；若診斷本身也無法保存，介面明示未另存成功。

**公司環境實測維持待驗證。** R12 原始寫入失敗的 Windows 碼與確切原因仍未知；公司版本長時間持續 ON、多次自然核准、實際 GUI 的不同 DPI、背景／焦點及鎖定情境沒有本輪新驗收結果。R11 的 2026-10-14 真實矩陣是歷史證據，不能當作 R13 公司實測。此次交付只是新的本機 ZIP，沒有發布或重啟 App。
