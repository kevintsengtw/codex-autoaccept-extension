# 給 Windows 11 Codex Desktop 的起始提示詞

將下方區塊整段貼到 Windows 11 上、已開啟此 repository 的 Codex 聊天。

```text
請接手 codex-autoaccept-extension 的 Windows Desktop auto-accept 驗證與修正。
Repository：https://github.com/kevintsengtw/codex-autoaccept-extension

這是我另一台 Windows 11 測試電腦。我授權你在這台電腦使用目前實際可用的 Computer Use 工具，操作專用 Codex／ChatGPT Desktop 測試聊天、CodexAutoAcceptDesktop、一般測試視窗和測試資料夾，重現與驗證自動核准。可建立無害的 marker 檔案作為完成證據，並修改此專案、建置與測試。此授權不包括存取公司電腦、公司資料、帳密、傳送訊息給他人或替我推送後续改動。

先查看 git status；保留既有變更。乾淨時 git pull --ff-only，記錄實際 commit，閱讀 desktop-probe/WINDOWS_HANDOFF.md、desktop-probe/VERIFICATION.md、desktop-probe/tray/README.md 和 desktop-probe/LOG_FORMAT.md。完整原始碼、測試與 0.3.7 ZIP 已在 repo；以 scripts/prepare-desktop-test.ps1 驗證並解壓既有 0.3.7 基準，先不要修改後直接宣稱修復。

公司是金融證券業、一般使用者，App 版本固定為 26.803.81509。公司 VS Code Codex extension、CLI、Desktop 只能使用「請求核准」，Approve for me／Full Access 都不可用。解法不能依賴這些模式、administrator、UIAccess 或修改公司政策。個人測試機也保持一般使用者與真實請求核准模式。

最新公司結果（2026-10-13）：0.3.7 在背景和最小化都已有核准請求卻不會 auto-accept；因而鎖定尚未測。舊的「完全遮蔽成功」結論已撤回，不能引用為已通過。已確認的舊功能是前景基本核准、0.2.1 持續 ON／OFF、0.3.2 一般使用者連接。0.3.7 主要改的是中文 log，沒有修好背景問題。

我不要畫面診斷區或唯讀診斷流程，不要要求我停住工作、記時間點、抄數字串或外傳公司檔案。這台測試機由你主動操作與觀察，需要的階段資訊寫成清楚的本機 log。不要恢復診斷面板。

先確認目前工具是否真的能控制原生 Windows 視窗；僅有瀏覽器控制不算。如果沒有原生能力，清楚說明此會話限制並完成仍可做的建置／檢查，不能假稱 Computer Use 已測。記錄 Windows build、App 實際版本、按鈕語言及啟動方式；測試機版本不同時，不把通過當成公司版通過。目前自動匹配精確「允許一次」Button，語言不同先記錄相容性差異，不隨意放寬到所有按鈕。

請用專用聊天產生只讀寫測試資料夾的真實核准請求。先確認 OFF 留給人工，一次 ON 後前景連續三次成功；再按交接矩陣測部分遮蔽、一般大小完全遮蔽、最大化完全遮蔽、最小化已有請求、最小化後新請求、OFF 停止與恢復。背景測試的等待期間不要點允許、切回前景、還原視窗或直接 Invoke 代替工具；以獨立 marker、主 log 與後續工作證明工具真的核准。沒有產生真實請求就記 BLOCKED，Invoke 返回或候選存在不能算 PASS。每次有限等待並自行記錄，不要讓我對時鐘。

背景／最小化先完成；鎖定延後，只有這兩類通過且和我安排好人工解鎖時再測。不要嘗試取得帳密或靠 Computer Use 自動解鎖。

先重現 0.3.7，分辨 provider 沒按鈕、候選不可用、幾何或讀取拒絕、重掃不通過、Invoke 結果未知及去重等待。依據證據修正，保留啟動 OFF、持續 ON、OFF 代次、唯一精確候選、完整重掃、目標／祖先／內容確認與每週期最多一次嘗試。不要靠搶前景、放寬沙箱、重送不確定核准或猜測啟動旗標來冒充成功。

修改後執行 scripts/check-desktop.ps1（三個建置、十一組測試、三項 API audit），再用 Computer Use 重跑真實矩陣。有新版成品需一致升版並另外打包，不覆蓋 0.3.7。依 WINDOWS_TEST_RESULTS_TEMPLATE.md 整理版本、案例與證據；runtime log／截圖保留本機，repo 只放整理過的測試資料。持續做完可做的工作；若 provider 或工具能力確實阻擋，交付明確的重現條件與限制，不宣稱已解決。
```
