# Windows Desktop 重新驗證結果 — 2026-10-13 R3

**本輪真實 App 矩陣全部受工具授權錯誤阻擋，背景／最小化新請求仍未解決。** 已重新驗證未修改 v0.3.7 成品，核對前次證據，重現 v0.3.8 的唯讀主 log 錯誤分類缺口並修正為 v0.3.9。建置、邏輯測試與 API 檢查通過，不代表真實 App 已成功核准，也不能替代公司版本驗收。

## 環境與成品

- 測試日期／時區：2026-10-13，Asia/Taipei（UTC+8）。
- Repository 起點：`da51ea564376ae535651b8318d371f8e1fbc9f07`。原工作目錄有前次修改，依保留既有變更的要求，未執行 `git pull`、重設或清除。本輪作業副本位於 `C:\Temp\codex-autoaccept-revalidation-20261013-02\work`；沒有推送、標籤或發布。
- 原版基準：再次以 `scripts/prepare-desktop-test.ps1` 驗證並解壓既有 v0.3.7 ZIP，62 筆內部雜湊通過。ZIP SHA-256 為 `3fa6fc9a3e61c36b58716925146ceb73a6e7d4bf79d1fc51432191f5d5fbe51c`。未改寫原 ZIP；原成品留於本輪 `baseline-037`。
- 修改前比較版：v0.3.8；僅用於相同本機檔案條件的錯誤分類重現，不計為本輪真實 App 核准測試。
- 新候選版：v0.3.9，本輪本機建置；最終候選 EXE 留於 `candidate-039-final`，未完成 App 實測。成品為 `artifacts/codex-autoaccept-desktop-windows-0.3.9.zip`；外部 ZIP SHA-256 記於 `artifacts/SHA256SUMS`，避免包內文件記自身 ZIP 雜湊形成循環。下表為最終建置檔案，不能解讀為真實 App 實測通過。
- Windows：Windows 11 專業版，build `26300.9457`，DisplayVersion `26H2`。
- App：本機 OpenAI.Codex 套件版本 `26.1007.2314.0`。公司固定版本為 `26.803.81509`，**不是同版本**。
- 語系／按鈕：前次已記錄本機繁體中文與精確「允許一次」Button。本輪未完成真實請求矩陣，因此不將前次按鈕紀錄當成本輪新請求確認。未放寬成所有 Button 或模糊文字。
- 語系查詢限制：本輪 PowerShell 5.1 程序回報 UI culture `en-US`，先前 PowerShell 7 查詢為 `zh-TW`；兩者為查詢程序資訊，未據此宣稱 Windows 或 App 語系已變更。App 的繁體中文按鈕以直接畫面觀察為準。
- 權限／模式：保留一般使用者與真實請求核准的驗證要求；未改為 Approve for me／Full Access，未使用 administrator、UIAccess 或修改公司政策。因矩陣受阻，本輪沒有新的 ON／OFF 與請求核准交叉確認結果。
- 啟動方式：App 已執行，未直接觀察原始捷徑。本輪查詢主程序仍未帶既有四個 Chromium 相容旗標，存在 Chromium 模組與 `app.asar` 引擎線索；未試驗相容啟動 profile，未疊加 Chromium／WebView2 旗標或修改安裝內容。
- Computer Use：本輪原生 Windows App 控制可用，並非只有瀏覽器控制；但讀取專用工具視窗時，即使使用者已允許，仍收到固定錯誤 `Computer Use was not approved to use codexautoacceptdesktop`。因此不能可靠操作工具前端、確認 ON／OFF、執行真實矩陣。這是本輪工具限制，不能推論提示詞可啟用不存在或被拒絕的能力。
- App 畫面一致性：原生 App 讀取有回傳，但該次可及性文字與截圖不一致；未據此確認本輪待核准請求或模式。專用聊天已結束的狀態另由聊天 API 核對，不以這次畫面當成真實核准證據。
- 專用測試資料夾：`C:\Temp\codex-autoaccept-revalidation-20261013-02`。本輪沒有以假 UI、CLI self-test、直接 Invoke 或建置結果冒充真實 App 完成證據。
- 真實請求／等待期限：本輪矩陣未建立可驗收的真實請求與有效工具狀態，因此沒有可列出的 20 秒／45 秒實測結果或成功次數。預定重測應各用獨立案例，確認真實請求後以單調時鐘記錄觀察期限。

| 最終 v0.3.9 檔案 | FileVersion | SHA-256 |
| --- | --- | --- |
| CodexAutoAcceptDesktop.exe | 0.3.9.0 | `b7e729324375d2bcab7547a26b43f435cba0b3bd1f54657966bc24c2e527a9b2` |
| CodexUiaManualInvoke.exe | 0.3.9.0 | `d1ecfa87c0d5b002f4b79e2ecc537a6723f96329d66d5f9b432b6e46618b6925` |
| CodexUiaProbe.exe | 0.3.9.0 | `1afee8f22b7c23b4bc07d1fb0fb9d06317c89961e11c0618414bbc782ecd9fae` |

## 本輪真實 App 矩陣

下列 BLOCKED 是本輪未能執行的固定狀態，不沿用 R2 的 marker、P 編號、主 log 或恢復紀錄列為本輪 PASS。

| 案例 | 狀態 | 真實請求已確認 | 自動處理數／請求數 | 操作與工作完成證據 | 固定 log 碼／結果 | 前景是否被改變 |
| --- | --- | --- | --- | --- | --- | --- |
| C01 新啟動 OFF | BLOCKED | 本輪未確認 | — | 工具視窗控制受阻，無本輪 marker 證據 | Computer Use 授權錯誤 | 未測 |
| C02 前景一次 ON，連續三次 | BLOCKED | 本輪未確認 | — | 未建立有效 ON／持續運作證據 | 同上 | 未測 |
| C03-A 一般大小部分遮蔽，請求先存在 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C03-B 一般大小部分遮蔽後新請求 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C04-A 一般大小完整覆蓋，請求先存在 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C04-B 一般大小完整覆蓋後新請求 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C05-A 最大化完整覆蓋，請求先存在 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C05-B 最大化完整覆蓋後新請求 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C06 最小化既有請求 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C07 最小化後新請求 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C08 OFF 停止新嘗試 | BLOCKED | 本輪未確認 | — | 未重跑 | 同上 | 未測 |
| C09-RECOVER 恢復後是否完成 | BLOCKED | 本輪未確認 | — | 無本輪失敗請求與恢復完成證據 | 同上 | 未測 |
| C09-RESTART 重開是否仍為 OFF | BLOCKED | 本輪未確認 | — | 未完成前端重開驗收 | 同上 | 未測 |
| C10 鎖定／解鎖 | NOT RUN | — | — | 背景／最小化未通過，沒有進入鎖定測試 | 未測 | 未測 |

## 本機錯誤重現與修正

**直接觀察：** 在專用試驗目錄把主 log 檔案設為唯讀，修改前 v0.3.8 回報 `UNCLASSIFIED:UnauthorizedAccessException:5`；新 v0.3.9 在相同條件回報 `CLASSIFIED:MAIN_APPEND:5`。證據為本輪 `evidence/readonly-baseline.txt` 與 `evidence/readonly-fixed.txt`。這是本機實際檔案操作，不是真實 App 核准案例，也不是公司環境重現。

修正 `LocalLogWriter.cs` 的檔案錯誤分類，納入 `UnauthorizedAccessException` 與 `SecurityException`，分別保留主檔附加、摘要暫存寫入、摘要原子取代的階段。主 log 附加失敗停止新嘗試；不重複主檔附加、不重送已開始或結果未知的核准。摘要錯誤保留主 log 的固定原因；只有符合既有條件的 32／33 分享衝突有限次重試摘要取代。非 Win32 HRESULT 不把低位元誤標為 Win32 錯誤代碼。

新增 Windows 實際唯讀檔案測試，涵蓋 `MAIN_APPEND:5`、`SUMMARY_WRITE:5`、摘要取代失敗、原檔屬性恢復、舊原子摘要保留、摘要失敗後保留完整主檔與沒有重送。檔案試驗全部位於 `C:\Temp` 下。

因前次相同狀態合併使逐案掃描證據不足，本輪亦調整 `ReadableLog.cs`：上一筆滿十秒後，在下一次完成的掃描／等待事件保留新紀錄。**十秒是重複紀錄的節流間隔，不保證 provider 緩慢或卡住時每十秒寫入，也不取代外部案例計時。** 沒有候選的紀錄仍不宣稱存在真實請求，P 編號仍是本機候選序號，不是 App 請求 ID。

## 前次 R2 證據核對與更正

本輪核對前次結果與原始紀錄，保留歷史檔案，不覆寫舊 JSON。整理的本機稽核文件為 `evidence/prior-evidence-audit.md`。

- 前次 `gui-038-r2-journal.json` 只有觀察結束時間與 `after20` 標籤，缺少開始／確認請求時間、實際經過時間、每案幾何與後端即時狀態。不能單靠這份檔案重新驗算每案「確認請求後 20 秒」；也不能由缺欄位反推前次未等滿期限。
- 前次主 log 合併重複 S00。R2-C04-B02、R2-C05-B02/B03 沿用較早背景掃描，R2-C07-03 最後新增 S00 是 FN。獨立 marker 不存在的工作失敗，與「本次完整背景掃描沒有 Button」須分別判定，不能把歷史 S00 當成每次請求的新掃描。
- R2-C05-B02/B03 的遮蔽截圖只證明遮蔽視窗最大化，未單獨證明底下 App 當時最大化。下一輪各案應重新核對目標原生狀態與範圍，不沿用前次幾何。
- 舊 `gui-r2-cleanup-corrections.json` 對 R2-C04-B01 的「重新 ON 才完成」仍不符合時間順序：marker 寫入早於下一個 G01，期間沒有新的 A01。前次結果文件將它列為人工點擊後完成並排除自動成功，較符合現有證據。B02 有重新 ON 後的 A01 與後續 marker。兩案均不能計為背景期間成功或已確認人工拒絕。
- 前次重啟首筆引用舊 log 已另留更正；新後端 OFF 判定應引用實際新的 G00／G02。前次摘要錯誤後，主 log 有後續 A01 與 marker，可支持當時同一後端繼續運作，不能延伸成所有錯誤或公司版本均已修好。

前次 R2 記錄九個完整覆蓋／最小化新請求在觀察結束沒有 marker，仍是前次證據，**不提升為本輪已重現**。前次遮蔽既有請求核准後轉前景、既有最小化請求核准後仍最小化，亦不能把「工作自動完成」與「全程留在背景」混為同一判定。

## 原因、限制與未測範圍

- **直接觀察：** 本輪原生 App 控制可用，但專用工具視窗仍回報上述 Computer Use 授權錯誤；本輪矩陣 BLOCKED。唯讀主檔的 v0.3.8／v0.3.9 分類差異已由相同檔案條件重現。
- **使用者回報：** 公司 v0.3.7 在背景與最小化有真實請求但不自動核准，鎖定未測；更早完全遮蔽成功結論已撤回。本輪未存取公司電腦、資料或 log。
- **推論／仍需確認：** 新的錯誤分類可讓前端辨識主檔不可寫並安全停止；新的重複紀錄規則可補強完成掃描的活性證據。這些修改是否在真實 App 的長時間運作中符合預期，仍需可操作工具前端後重測。不從背景無 Button 推論 renderer 停止，不把 Invoke 返回當成工作成功。
- **無法執行／未測：** 本輪全部真實矩陣、公司 App `26.803.81509`、相容啟動 profile、Windows 鎖定、其他語系與長時間穩定性。未取得帳密或嘗試自動解鎖。

## 修改與回歸

- 新版版本：前端、後端版本協定、三個組件、manifest、版本比對與打包名稱統一為 v0.3.9。另建成品，保留 v0.3.7／v0.3.8；包內 EXE 與上表一致，成品與內部 SHA-256 另行驗證。
- 主要修改：`manual/LocalLogWriter.cs`、`manual/ReadableLog.cs`、`manual/ManualProgram.cs`、`tray/WorkerDiagnostics.cs`、相應測試、log 文件及版本欄位。未新增診斷面板，未放寬精確候選或加入搶前景／還原視窗來核准的操作。
- `scripts/check-desktop.ps1`：三個建置、十一組測試、三項 API 檢查通過，結束碼 0；初次紀錄為 `evidence/check-desktop-039.txt`，最終紀錄為 `evidence/check-desktop-039-final.txt`。初次有 NU1900（無法取得 NuGet 弱點資料來源）及既有 CS0649 警告；最終仍有 NU1900，**不是零警告**，也不能宣稱線上弱點資料檢查完成。
- 最終檢查使用 Windows PowerShell 5.1。沙箱內原命令先回報 `AuthorizationManager check failed`、結束碼 1；保留於 `evidence/check-desktop-039-final-sandbox-failure.txt`。依執行限制獲准在沙箱外用同一一般使用者重跑同腳本後通過，未加 ExecutionPolicy 參數或修改執行原則。
- 一般權限、每次啟動 OFF、持續 ON、OFF 阻止新嘗試、唯一精確候選、完整重掃及目標／祖先／局部內容確認、每週期最多一次 Invoke 的既有規則保留；本輪邏輯測試不替代真實矩陣重跑。
- 仍未解決：背景／最小化後新請求的真實自動核准；本輪工具限制亦未消失。沒有本輪真實 App PASS，更沒有公司同版本 PASS。

## 本機證據與後續驗收

Computer Use 拒絕訊息、建置輸出、檔案重現與稽核證據留於本機 `C:\Temp\codex-autoaccept-revalidation-20261013-02`，不加入 repository 或 ZIP。本輪未建立 App 核准案例，所以沒有本輪成功 marker 或可引用的核准 runtime log。本文件只引用整理過的固定狀態與證據檔名。前次原始 log、截圖與試驗資料仍保留於 `C:\Temp\codex-autoaccept-validation-20261013`，未刪除或改寫。

專用工具已停止，先前工具設定由本輪開始前的備份還原，SHA-256 與備份一致。專用測試聊天 API 狀態為 idle，最後一個前次請求已拒絕且未重送；本輪沒有傳送新測試請求。沒有修改登入啟動、App 安裝、政策或相容參數。

工具前端可正常控制後，優先以同一套成品重跑：前景一次 ON 連續三次、一般大小完整覆蓋後新請求三次、最大化完整覆蓋後新請求三次、最小化後新請求三次。每案固定案例物件、後端代次及主 log 路徑；保存真實請求確認、當次幾何、ON ack、單調時鐘期限、獨立 marker 與聊天簡短固定狀態。清理先 OFF，確認請求已拒絕且 marker 不存在後才開始下一案。恢復前景後的完成另記，尚未成立的條件記 BLOCKED。
