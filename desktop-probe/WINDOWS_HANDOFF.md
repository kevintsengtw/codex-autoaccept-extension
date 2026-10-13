# Windows 11 驗證交接 — 2026-10-13

目標：在使用者另一台 Windows 11 測試電腦，以 Computer Use 操作真實 Codex／ChatGPT Desktop，重現並定位 auto-accept 在背景與最小化失敗的原因。公司電腦保持原有工作方式，不再要求使用者保留請求、記時間、操作診斷模式或外傳資料。

## 目前狀態與限制

公司為金融證券業，一般使用者權限，App 與版本由管理者控制。公司 ChatGPT Desktop App（Codex）版本是 `26.803.81509`。VS Code Codex extension、Codex CLI 和 Codex Desktop 均只能使用「請求核准」；`Approve for me`、`Full Access` 不能選用。不可把切換這些模式、提權或放寬公司政策當成解法。

| 功能／情境 | 公司使用者回報 |
| --- | --- |
| VS Code extension | 已完成一版可用的 auto-accept |
| Terminal／Codex CLI | 真實 Windows／公司端到端仍待驗證，不能視為完成 |
| Desktop 手動 Invoke | 「允許一次」單次驗證成功 |
| Desktop 0.2.1 | 一次 O 後連續三次核准、維持 ON；F 停止 |
| Desktop 0.3.2 | 一般使用者連接與基本自動核准成功 |
| Desktop 0.3.7 背景 | 使用者確認有核准請求，但不會 auto-accept |
| Desktop 0.3.7 最小化 | 使用者確認有核准請求，但不會 auto-accept |
| Desktop 0.3.7 鎖定 | **未測**；前兩項失敗，所以本次未繼續測 |

先前 0.3.5「完全遮蔽已成功」的廣泛結論已撤回；0.3.6 又確認最大化後完全蓋住仍失敗。更早曾回報鎖定時不核准、解鎖後自行恢復，但不能把這件事當成 0.3.7 的鎖定測試結果。0.3.7 的背景／最小化具體遮蔽比例、log 原因碼及 provider 根因沒有取得，不得補造。

使用者已明確不要畫面診斷功能。0.3.7 移除診斷區及唯讀按鈕，正常 ON 時寫中文本機 log 與短摘要。後續實作維持這個介面；需要增加觀測時寫到 log，清楚格式化，不要求使用者轉抄數字串。

## 取得與啟動基準

新目錄：

```powershell
git clone https://github.com/kevintsengtw/codex-autoaccept-extension.git
cd codex-autoaccept-extension
```

已有 checkout 時，先查看 `git status --short`，工作目錄乾淨才執行 `git pull --ff-only`，不要重設或清掉別人的改動。用 `git rev-parse HEAD` 記錄實際測試的 commit。

先測已交付的 **0.3.7 ZIP**，不要把重新編譯或個人修改當成原始基準：

```powershell
.\scripts\prepare-desktop-test.ps1
```

此腳本驗證 `artifacts/SHA256SUMS`、解壓到全新 `.desktop-test` 目錄，再驗證全部內部 SHA-256。它只準備檔案，不啟動 App、不開 ON、不提權，也不變更 execution policy。輸出 `EntryPoint` 即是待測 EXE。若腳本受本機政策限制，直接核對 checksum 並手動解壓即可，不改全域政策。

0.3.7 ZIP SHA-256：

```text
3fa6fc9a3e61c36b58716925146ceb73a6e7d4bf79d1fc51432191f5d5fbe51c
```

以一般使用者啟動 `CodexAutoAcceptDesktop.exe`，選正確 App 視窗，再手動 ON。每次重開工具都必須 OFF；關設定視窗只是縮到系統匣，退出需用系統匣選單。不要同時跑兩版工具或 console 版。

測 prebuilt EXE 需 Windows .NET Framework 4.8／4.8.1，不需開發 SDK。原始碼建置及可攜測試需 .NET SDK 與 .NET 8 runtime；用下列腳本執行三個 net48 建置、十一組測試及三項產物 API 檢查：

```powershell
.\scripts\check-desktop.ps1
```

`TransportTests` 只會建立自己的測試子程序，不控制 App。測試通過不能當成真實 UIA／背景核准成功。修改後的三個 EXE 分別在 `desktop-probe/bin/Release/net48`、`desktop-probe/manual/bin/Release/net48`、`desktop-probe/tray/bin/Release/net48`；前後端放在同一新測試目錄，保留 EXE.config，不覆蓋原 0.3.7 基準包。

## Computer Use 與環境辨識

先檢查這個 Windows 會話實際提供的工具是否能讀取、操作原生 Windows 視窗。只有瀏覽器控制不等於可以控制 Desktop App；未提供原生控制能力時明確列為工具能力阻礙，可繼續原始碼建置與檢查，但不得宣稱已做 Computer Use 實測。不要因提示詞提到 Computer Use 就假定功能存在。

記錄 Windows build、App 實際版本／名稱、按鈕實際文字、語系、一般權限、工具版本及一般／相容啟動模式。測試機 App 若不是公司版 `26.803.81509`，結果屬於版本相容性實驗；通過也不能代替公司驗收。不要為取得公司版而繞過 Store／管理政策。

目前只自動接受精確文字「允許一次」的 Button。英文或其他標籤先記錄為相容性差異，不把「所有 Button」或模糊名稱變成 auto-accept 範圍。從實際 UI 確認是否可在個人測試機用繁體中文，不能假設某個語系選項一定存在。

## 真實測試方法

在這台個人測試機建立專用測試資料夾和專用測試聊天；同一 App 視窗也可以，不需假定 App 能另開視窗。與 Computer Use 的控制工作分開產生測試請求，避免工具核准控制者自己的一般工作。測試請求只讀寫該測試資料夾中的無害 marker 檔，逐次有獨立案例編號，不涉及公司檔案、憑證、外部傳送或系統設定。

使用實際「請求核准」模式，先找到可重複產生真實核准的操作。一般檔案操作可能不會要求核准；**若沒有產生真實請求，案例是 BLOCKED，不是 PASS**。不得用假 UI、CLI self-test 或直接呼叫 Invoke 的結果冒充真實 App 成功。也不要切 Approve for me／Full Access 讓提示消失。

每個案例開始前記錄工具 ON／OFF 和目標，操作者確認真實請求存在。等待背景／最小化核准期間不要人工點允許、直接 Invoke、還原 App 或切回前景；Computer Use 把目標帶回前景也可能改變被測條件。用獨立 marker 和已保存 log 觀察工作是否真的繼續。到觀察期限後再恢復視窗確認結果，將「觀察期間自行完成」與「恢復前景才完成」分開記錄。建議一般案例每次觀察 20 秒；provider 卡住時依 45 秒後端期限處理，分段等待並更新進度，不無限等待。

| 案例 | 操作與驗收 |
| --- | --- |
| C01 啟動 OFF | 新啟動工具，不開 ON；真實請求保留，沒有 marker 完成 |
| C02 前景 ON | 一次 ON 後連續三個不同真實請求自動完成，保持 ON，沒有人工核准 |
| C03 一般大小部分遮蔽 | 分別測已出現的請求與進入背景後新產生請求，記錄實際覆蓋狀態 |
| C04 一般大小完全遮蔽 | 同上；整個目標被覆蓋，不能靠露出一角或切前景通過 |
| C05 最大化完全遮蔽 | App 最大化後完整蓋住，涵蓋公司 0.3.6 失敗情境 |
| C06 最小化既有請求 | 前景確認請求後最小化；不還原也會完成才算通過 |
| C07 最小化後的新請求 | 先最小化，再由獨立測試流程產生請求；確定請求確實產生 |
| C08 OFF 停止 | 切 OFF，後續請求保留人工核准；不撤回已開始的操作 |
| C09 恢復／重啟 | 記錄失敗案例切回前景或還原後是否恢復；工具重啟仍 OFF |
| C10 鎖定 | **延後**：背景／最小化通過後，再與使用者安排可人工解鎖的測試 |

核心案例各測三個請求；失敗不要反覆重送同一候選。新舊請求沒有可觀察離開週期時記錄去重限制。要清理失敗請求時先 OFF，再在 App 人工處理，清楚標示不屬於 auto-accept 成功。

鎖定可能讓 Computer Use 本身失去桌面控制，不能依靠螢幕自動化解鎖或要求帳密。鎖定與解鎖是之後獨立安排的測試，目前公司 0.3.7 是未測。

## 定位與程式入口

先重現基準再改碼。0.3.7 的 log 在 `%LOCALAPPDATA%\CodexUiaProbe\auto-*.txt`，同名 `-summary.txt` 是短摘要。關注完整主檔中失敗條件下的完整／不完整掃描、候選有無、拒絕原因及 A01/A02/A03 的階段；不能只看摘要最後一行或後續前景成功。若新 log 不足以判定，增加固定中文階段紀錄；不要再加診斷控制面板。

| 原始碼 | 主要責任 |
| --- | --- |
| `desktop-probe/tray/Program.cs` | 系統匣、選視窗、ON/OFF、hidden helper、重連、開 log |
| `desktop-probe/tray/DesktopState.cs` | 使用者意圖、後端 ack、預設 OFF、故障狀態 |
| `desktop-probe/tray/BackgroundLaunch.cs` | 既有選用 Chromium／WebView2 啟動實驗；未證明公司有效 |
| `desktop-probe/manual/ManualProgram.cs` | native snapshot、完整 raw UIA 掃描、兩次確認、Invoke、結果觀察 |
| `desktop-probe/manual/BackgroundPolicy.cs` | 前景／背景幾何與最小化條件，不能製造 provider 未提供的按鈕 |
| `desktop-probe/manual/AutoGate.cs` | OFF 代次撤銷、去重、持續 ON 與週期界線 |
| `desktop-probe/manual/ReadableLog.cs` | 中文紀錄、候選 P 序號、短摘要與固定回報碼 |
| `desktop-probe/shared/ScanMetrics.cs` | 掃描 metadata、legacy pipe 固定格式 |
| `desktop-probe/verify/` | 十一組可攜測試及產物 API audit |
| `scripts/package-probe.py` | 將建好的三個 EXE、原始碼與文件打包；需 Python |

需要區分：App 未提供 Button、Button 不可用、WindowPattern／父層讀取失敗、幾何拒絕、核准前目標變動、Invoke 已嘗試但結果未知、去重仍等待。UIA `IsOffscreen` 不是遮蔽百分比，`ok`／候選數不是核准成功，P 是本機候選序號而非 App 原生請求 ID。沒有 foreground HWND 也不是明確鎖定事件。

可在個人測試機完整退出專用 App 後分開試既有相容啟動，記錄是哪一種 profile、有何引擎證據、是否真的用新參數啟動；先完成一般啟動基準。不疊加猜測旗標，不覆蓋既有 WebView2 參數、不改安裝、沙箱或公司政策。不能用工具主動還原／搶前景來宣稱背景成功。

## 修改與交付

保留：一般權限、啟動 OFF、持續 ON、OFF 阻止新嘗試、唯一精確 Button、完整重掃與目標／祖先／內容確認、一個週期最多一次 Invoke、不重送不確定操作。App 自己被 Invoke 帶到前景時也要記錄，不能省略。

有證據的根因再做修正，加能辨識該失敗的測試，執行 `check-desktop.ps1`，然後重跑真實矩陣。需要發新成品時升前後端／manifest／版本檢查／打包版本，不覆蓋 0.3.7 ZIP；原始碼建置通過仍不能宣稱公司背景成功。沒有可行修正也要清楚交付可重現條件與限制，不製造 PASS。

使用 `WINDOWS_TEST_RESULTS_TEMPLATE.md` 建立結果文件，區分直接觀察、使用者回報、推論、未測。`windows-test-results/`、`.desktop-test/`、bin／obj 和 graphify 快取不簽入；原始 log／截圖保留本機。可把整理過、只有測試資料的結果 Markdown 放在 `desktop-probe/`，review 後再交付。不要帶公司資料或讓使用者外傳公司檔案。

完整歷史見 `VERIFICATION.md`，操作見 `tray/README.md`，固定碼見 `LOG_FORMAT.md`。Windows agent 的起始提示詞在 `WINDOWS_CODEX_PROMPT.md`。
