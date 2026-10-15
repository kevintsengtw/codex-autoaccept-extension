# Windows Desktop 真實驗證結果 — 2026-10-13 R4／R5

**0.3.9 基準與 0.3.10 候選版均重現：App 一般大小、完全遮蔽後才產生的新請求，三案正式 20 秒內沒有自動完成，還原前景後才完成。背景問題尚未修好。** 0.3.10 修正的是核准後觀察的例外分類，不能把這項修正當成背景核准 Button 已可取得。兩版前景一次開 ON 連續三個真實 marker 已完成；遮蔽既有請求完成時，核准呼叫返回後 App 轉為前景，不能宣稱全程背景成功。

本文件依結果模板合併 R4 的未修改 0.3.9 基準與 R5 的 0.3.10 回歸。R4／R5 普通使用者工作階段矩陣均已結束；R5 核對截至 `2026-10-13 15:19:53 UTC`（臺灣時間 23:19:53）的停止主檔與重啟新主檔快照，G08／G09 亦已完成稽核。相容冷啟動已嘗試準備，但關閉 App 的 GUI 操作使控制會話中斷，狀態為 BLOCKED，沒有相容 profile 真實矩陣。鎖定仍未測，背景／最小化新請求仍未修復。最後清理已停止本輪工具、恢復原設定並保持 App 開啟；封裝資料另行核對，不加入 ZIP 的自我雜湊。

## 環境、來源與權限

- 日期／時區：2026-10-13，Asia/Taipei（UTC+8）。案例 journal 使用 UTC 與單調時鐘，工具主 log 使用本機時間；二者分開核對。
- Repository 起點：`da51ea564376ae535651b8318d371f8e1fbc9f07`。開始時已有前次未提交修改，因此沒有 pull、重設或清除。修正與建置在 `C:\Temp\codex-autoaccept-revalidation-20261013-03\work` 副本進行；本報告不表示已提交、推送、標籤或發布。
- R4 工具來源：以 `scripts/prepare-desktop-test.ps1` 驗證、解壓既有未修改 0.3.9 ZIP，67 筆內部雜湊通過。ZIP SHA-256 為 `182c255a394d2e2591d5dba65040a69a916965fb86a00091b62f3f487ad39b9c`。實測 log header 為 0.3.9，沒有用修改後版本替代原問題重現。
- R5 工具來源：由已通過建置的 0.3.10 二進位檔準備獨立 runtime，File／Assembly version 為 `0.3.10.0`、Product version 為 `0.3.10`。三個 EXE 逐一核對雜湊與通過檢查的成品相同；config 與外部 manifest 以複製位元組、既有建置及 API audit 核對。本段尚未列為最終 ZIP 封裝驗收，不以 0.3.9 ZIP SHA 代替新版身分。
- Windows：Windows 11 專業版，build `26300.9457`，DisplayVersion `26H2`。
- App：OpenAI.Codex 套件實際版本 `26.1007.2314.0`。公司固定版本是 `26.803.81509`，**不是同版本，本機結果不能替代公司驗收**。
- 語系／核准文字：程序 UI culture 為 `zh-TW`，原生畫面為繁體中文，模式顯示「要求核准」，精確核准 Button 為「允許一次」。測試維持使用者要求的「請求核准」流程，沒有切換 Approve for me／Full Access，沒有擴大按鈕文字範圍。
- 權限：本輪程序與 App 為一般使用者；沒有 administrator、UIAccess、改動公司政策、沙箱或 App 安裝內容。
- 啟動方式：普通矩陣沿用測試開始前已執行的 App，原始捷徑未直接觀察，程序沒有既有相容 profile 的四個旗標。之後冷啟動 one-shot 通過唯讀 preflight 並 armed，但 GUI「結束 ChatGPT」使控制會話中斷；只有等待退出紀錄，沒有 launch stage 或相容 profile 實測。後續同路徑的新 App 程序四個旗標均未出現，啟動來源未確認。
- Computer Use：重新授權後，實際原生 `@oai/sky` 能讀取與操作 App、專用 auto-accept 工具及遮蔽視窗，並非只有瀏覽器控制。R3 的工具授權拒絕保留為歷史，不能代表本輪能力。控制中斷、過期索引或文字／截圖不一致的案例另列限制。
- 專用測試資料：只使用本輪 `markers` 資料夾內的獨立無害 marker 與專用測試聊天。沒有公司資料、憑證、帳密或對外傳送。

| 0.3.10 實測二進位檔 | SHA-256 |
| --- | --- |
| CodexUiaProbe.exe | `572d8aa6e4b2741f1f09253e5433b1e3d648bf89ac338a632742f7ddadca3ad0` |
| CodexUiaManualInvoke.exe | `0afd3727b5167fb6f4b988aa14752520f8d0f91a6d15b1c9c231fcf35ca49570` |
| CodexAutoAcceptDesktop.exe | `76095b4a6ddffb030216632f6f5760466d14447441ca4b361d786a59bd0ca707` |

原始 0.3.7、0.3.8、0.3.9 ZIP 與歷史文件保留，不覆寫基準。0.3.7 基準 ZIP 驗證、公司使用者回報與 R3 授權限制見歷史文件，不把 ZIP 驗證寫成個人機真實 App 矩陣。此輪新增結果以明列版本與案例為準。

## 真實請求、等待與判定方法

專用測試聊天以一次 `require_escalated` 的 exec 產生真實核准請求，不設 prefix rule，只建立並讀回該案 marker。producer 與控制工作分開；一般由 GUI 提交，無法提交時在工具 OFF 下使用獨立 peer API，再選測試聊天確認真實核准卡後才啟用。R5 的原批次 producer 排程在控制中斷後停止，後續由新流程繼續，不重送同一候選。

背景 A 類先在前景確認真實請求，才建立遮蔽或最小化條件及 ON；B 類先建立條件並保持 ON，producer 之後才產生新請求。實際 exec 時間另由專用聊天補證，不以提示中的 sleep 長度、預定 BEGIN 或較晚 API waiting 截面反推順序。

一般正式觀察在真實 pending 與條件成立後開始，分段等待 20 秒；provider 卡住則依既有 45 秒後端期限處理。背景期間沒有人工允許、直接 Invoke、還原或切回 App。期限後的還原與工作恢復另列，不能倒填為背景期間完成。清理時先 OFF 或停止本輪專用工具，人工拒絕不算 auto-accept；停止程序不冒充已取得前端 OFF ack，切 OFF 也不能撤回已開始的一次操作。

每案使用當次 main log 與 offset，核對第一期限的原始 byte 前綴、獨立 marker、新候選的單次 A01、producer 的 exec／後續工作結果及 A08。完整主 log 用 `FileShare.ReadWrite | FileShare.Delete` 共享讀取並保留 BOM；後來的成功不能取代第一期限前綴。P 編號是同一 helper 內的候選序號，不是 App 請求 ID，跨 helper 同號不合併。S01、A02 返回或 A03 元素失效均不能單獨證明工作完成。

一般 App 幾何為 1280×820@(0,0)，部分遮蔽視窗為 602×462@(507,100)；完整遮蔽視窗與最大化 App 各為 1920×1152@(0,0)。每案重新確認幾何，最大化動畫結束後再確認，避免把 Computer Use 啟用視窗時的還原或中途動畫當成既定條件。UIA IsOffscreen 不代表遮蔽百分比，沒有前景 HWND 也不代表已確認鎖定。

快速完成且未捕捉正式 pending 起點的案件保留成功工作證據，但明列計時限制。R4 C01-R01 OFF 重測原計畫 20 秒、實際約 118974ms；R4 G05-B03 原 timer 是 `19999.2001ms`，略少於 20 秒。這些原值不改寫為滿 20 秒。R5 前景與部分遮蔽新請求有 marker 早於後來 timer 的案例，不以較晚 timer 當成仍 pending 的背景觀察。

## R4：未修改 0.3.9 基準矩陣

PASS 只表示該列明列的工作或控制行為成立。A08 顯示返回後轉前景者，不算全程留在背景成功。被污染、條件失效或不足三個的類別不概括為完整驗收。

| 案例 | 狀態 | 真實請求已確認 | 自動處理數／有效請求數 | 操作與工作完成證據 | 固定 log 碼／結果 | 前景是否被改變 |
| --- | --- | --- | --- | --- | --- | --- |
| C01 初次 OFF | BLOCKED | 有 | 不計 | 使用者確認早期 marker 曾人工允許，排除自動完成判定 | 觀察期沒有可歸屬該案的 A01 | 人工處理分開 |
| C01-R01 OFF 重測 | PASS（OFF 保留人工處理） | API pending | 0/1 | 實際約 118974ms 無 marker，後續完成不列自動 | G00／G02，期間無自動 A01 | 無自動操作 |
| C02 首次 MCP 派送 | FAIL；候選關聯未確認 | 是 | 0/1 | 正式 20 秒無 marker；OFF ack 後人工拒絕，peer 確認未執行 | P1 S01 早於對應 producer turn，不能歸屬本案成功 | 不據此歸因 provider |
| G02-01–03 前景一次 ON | PASS（自動完成） | 三案實際 exec 與 marker | 3/3 | 同一 helper，P2／P3／P4 各一次 A01，期間無 OFF、結束仍 ON | 039-FN-S01／A01／A02／A08／A03 | 仍前景 |
| G03-A01／A02／A04 部分遮蔽既有請求 | PASS（自動完成） | 原生真卡與 pending | 3/3 | 每案正式期限前 marker；唯一 P5／P6／P25 A01 | 039-BN-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G03-A03 部分遮蔽既有請求 | BLOCKED | 是 | 不計背景 | 控制中斷後實際顯示控制聊天；選回測試聊天才完成 | CONDITION_INVALIDATED | 僅恢復階段完成 |
| G03-B01／B03／B04 部分遮蔽後新請求 | PASS（自動完成；計時限制） | 實際單次 exec 補證 | 3/3 | producer 在本案遮蔽後；P8／P10／P26 各一次 A01、獨立 marker | 039-BN-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G03-B02 部分遮蔽後新請求 | BLOCKED | 真實單次 exec | 不計背景 | 前案轉前景後未及重建遮蔽，P9 在 FN 完成 | 039-FN-S01／A01／A02／A08／A03 | 呼叫前已前景 |
| G04-A01–03 一般大小完全遮蔽既有請求 | PASS（自動完成） | 三案 | 3/3 | 三案正式 20 秒內，各新候選一次 A01 與 marker | 039-BN-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G04-B01–03 一般大小完全遮蔽後新請求 | FAIL | producer 均晚於本案遮蔽 | 0/3 | 每案正式 20 秒無 marker、零 A01、新完整 S00 | 039-BN-S00，精確 Button 零 | 還原前景後才完成 |
| G05-A01–03 最大化完全遮蔽既有請求 | PASS（自動完成） | 三案 | 3/3 | 正式 20 秒內 P15／P29／P30 各一次 BX-A01 與 marker | 039-BX-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G05-B01 最大化完全遮蔽後新請求 | BLOCKED（USER_REPORT） | 真實單次 exec | 不計背景 | 使用者回覆「有切換視窗」；P16 在 S01／A01 前已 FX | 039-FX-S01／A01／A02／A08／A03 | 人工切窗使背景條件失效 |
| G05-B02／B03／B04 最大化完全遮蔽後新請求 | FAIL（B03 計時限制） | 三案 | 0/3 | 各無 marker、零 A01、新完整 BX-S00；B02／B04 滿 20 秒，B03 略短 | 039-BX-S00，精確 Button 零 | 期限後還原才完成 |
| G06-A01–03 最小化既有請求 | PASS（自動完成） | 三案 | 3/3 | 各第一 20 秒期限前 marker，P19／P20／P21 各一次 A01，producer 完成 | 039-M-S01／A01／A02／A08；A06／A05 不重送 | 返回後仍最小化 |
| G07-B01–03 最小化後新請求 | FAIL | 實際 exec 均晚於本案最小化 | 0/3 | 第一正式 20 秒無 marker、零 A01、新完整 M-S00 | 039-M-S00，精確 Button 零 | 還原前景後才完成 |
| G08-C01 OFF 停止後續新請求 | PASS（OFF 保留人工處理） | producer 晚於 OFF | 0/1 | 正式 20 秒無 marker、零 A01；OFF 下期限後人工拒絕 | U-G02；FN-B08 是掃描切換，非核准 | 無自動操作 |
| C09 還原恢復 | PASS（恢復，非背景） | G04／G05／G07 失敗案 | 分開記錄 | 第一期限後還原，才有 FN-A01 與 marker | 恢復階段另列 | 人工還原後完成 |
| G09-C01 重啟仍 OFF | PASS（預設 OFF／不嘗試；條件限制） | API pending，期限後原生真卡 | 0/1 | pending 早於重啟；重啟與連接仍 OFF，20 秒新 main 1383 bytes 不增加、零 A01 | 新 main G00／G02；ORDER_CORRECTION | 觀察期顯示控制聊天，非完整前景 pending 驗收 |
| C10 鎖定／解鎖 | NOT RUN | — | — | 背景與最小化新請求未通過，未安排人工解鎖 | 未測 | 未測 |

R4 計時與條件限制：G02-01／02 屬快速完成，沒有正式 pending 後 20 秒截面；G02-03 有正式截面。G03-B01／B04 快速完成；G03-B03 的 API waiting 比 marker 晚約 25.54 秒，不能當成起點仍 pending。G03-A02 的 ON_ACK 標籤與前端文字不一致，啟用另由同案 G01、BN 動作與 marker 支持。G02-02 的首筆 peer 狀態屬上一 turn，已以 STATUS_CORRECTION 排除。G03-A01 寫完 marker 後遇到 model capacity，續接只確認結果，不把中斷抹掉。

R4 G09 的 producer 為 `10:11:00.146 UTC`，重啟為 `10:14:39.789 UTC`，是重啟前已存在的 pending；不是重啟後才產生的新請求。原 journal 已追加 ORDER_CORRECTION。第一期限後才選專用聊天看見原生真卡，因此只支持重啟預設 OFF、連接後不嘗試與 pending 保留，完整前景待核准驗收仍有限制。G08／G09 期限後在 OFF 下人工拒絕，marker 未建立，不計自動完成。

## R5：0.3.10 候選版已稽核矩陣

本表包含前景、遮蔽、最小化與 OFF／重啟的已稽核案例，控制中斷與相容冷啟動受阻列 BLOCKED。鎖定沒有執行，列 NOT RUN；沒有借用舊版結果。

| 案例 | 狀態 | 真實請求已確認 | 自動處理數／請求數 | 操作與工作完成證據 | 固定 log 碼／結果 | 前景是否被改變 |
| --- | --- | --- | --- | --- | --- | --- |
| G01-C01 新啟動 OFF | PASS（OFF 保留人工處理） | API pending，期限後原生真卡 | 0/1 | 正式 20002.198ms 無 marker、零 A01；期限後 OFF 下人工拒絕 | 0310-U-G00／G02，main 1386 bytes 不增加 | 無自動操作 |
| G02-A01–03 前景一次 ON | PASS（自動完成；計時限制） | 實際單次 exec、新候選、marker | 3/3 | 一次 ON，P1／P2／P3 各一次 A01，期間無 OFF、結束仍 ON | 0310-FN-S01／A01／A02／A08／A03 | 仍前景 |
| G03-A01／A02／A04 部分遮蔽既有請求 | PASS（自動完成） | 原生真卡與 pending | 3/3 | 正式 20 秒起點無 marker、期限有 marker；初次 helper P4／P5，恢復 helper P1 各一次 A01 | 0310-BN-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G03-A03 部分遮蔽既有請求 | BLOCKED（控制失敗） | producer 已呼叫，停止後仍有真卡 | 不計背景 | 工具 activation 兩次失敗，條件未建立；停止後人工拒絕，marker 不存在 | CONTROL_FAILURE；中斷前 P6／P7 排除 | 無有效背景條件 |
| G03-B01–03 部分遮蔽後新請求 | PASS（自動完成；計時限制） | 單次 producer、新候選、marker | 3/3 | 每案重建遮蔽後才 producer；恢復 helper P2／P3／P4 各一次 A01 | 0310-BN-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G04-A01–03 一般大小完全遮蔽既有請求 | PASS（自動完成） | 原生真卡與 pending | 3/3 | 完整遮蔽動畫結束後才 ON，正式 20 秒內每案一次 A01 與 marker | 0310-BN-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G04-B01–03 一般大小完全遮蔽後新請求 | FAIL | producer 均晚於本案遮蔽 | 0/3 | 每案第一正式 20 秒無 marker／A01，新完整 BN-S00；還原後才完成 | 0310-BN-S00，精確 Button 零 | 期限後人工還原才完成 |
| G05-A01–03 最大化完全遮蔽既有請求 | PASS（自動完成） | 原生真卡、producer 各一次 exec | 3/3 | 起點無 marker、各第一正式 20 秒期限有 marker，P11／P12／P13 各一次 BX-A01 | 0310-BX-S01／A01／A02／A08／A03 | 返回後轉前景 |
| G05-B01–03 最大化完全遮蔽後新請求 | FAIL | producer 各晚於本案 settled cover | 0/3 | 第一正式 20 秒無 marker／A01，新完整 BX-S00；還原後才完成 | 0310-BX-S00，精確 Button 零 | 期限後人工還原才完成 |
| G06-A01–03 最小化既有請求 | PASS（自動完成） | 前景真卡，producer 各早於最小化 | 3/3 | 第一期限各唯一 M-A01 與 marker，A08 仍最小化；期限後補讀仍最小化，未提前還原 | 0310-M-S01／A01／A02／A08；A06／A05 保留未知 gate、不重送 | 返回後與期限後補讀仍最小化 |
| G07-B01–03 最小化後新請求 | FAIL | producer 各晚於本案最小化 | 0/3 | 第一正式 20 秒無 marker／A01，新完整 M-S00；還原後才完成 | 0310-M-S00，精確 Button 零；root 可見性未讀取 | 期限後人工還原才完成 |
| G08-C01 OFF 停止新嘗試 | PASS（OFF 保留人工處理） | 原生真卡、API pending，producer 晚於 OFF | 0/1 | 正式 20013.764ms 無 marker／A01；期限後 OFF 下人工拒絕，producer rejected | 0310-U-G02，main 238196 bytes 不增加 | 無自動操作 |
| C09 還原恢復 | PASS（恢復，非背景） | G04／G05／G07 各三案 | 分開記錄 | 第一期限後才各一次 FN-A01 與 marker；後來十秒觀察起點已完成 | 各一次 FN-A01 | 人工還原後完成 |
| G09-C01 重啟 OFF | PASS（預設 OFF／保留既有 pending） | 正式觀察前原生真卡、API pending | 0/1 | 新啟動與連接仍 OFF，正式 20011.146ms 無 marker／A01；期限後 OFF 下人工拒絕，producer rejected | 新 main 0310-U-G00／G02，1386 bytes 不增加；ORDER_CORRECTION | 無核准操作；請求早於重啟 |
| C10 鎖定／解鎖 | NOT RUN | — | — | 背景新請求仍失敗，未安排人工解鎖 | 未測 | 未測 |
| 相容冷啟動 | BLOCKED（控制會話中斷） | 未進入相容 profile 真實矩陣 | — | 唯讀 preflight 通過、one-shot armed 等待退出；GUI Quit 呼叫中斷 | 只有 WAITING_FOR_EXISTING_APP_EXIT，沒有 launch stage；不猜 exit code | 使用者確認代理關閉 App；目前 App 開啟，不再關閉／重試 |

R5 G02 的三個 marker 分別早於後來 API waiting／timer 約 19.648、15.934、14.520 秒，三筆觀察起點均已存在 marker。後續 timer 是較晚的完成截面，不能宣稱當時仍 pending 或三案是在該二十秒內才核准。它們的實際單次 exec、新候選、唯一 A01、獨立 marker 與同一 helper 持續 ON 支持工作完成，計時限制仍保留。

R5 恢復後的 G03-A04 正式 20001.695ms 起點無 marker、期限有 marker，補足第三個有效既有請求；不改寫被中斷的 A03。G03-B01 快速完成；B02／B03 timer 起點的 marker 已存在約 25.757／27.883 秒，不當成完整 pending 背景觀察。B02 實際 producer 晚於遮蔽僅 1.319 秒，由專用 producer 時間補證；不能只依 sleep 完成時點判定順序。三案每次另建遮蔽條件與各自唯一 A01，A08 全轉前景。

R5 G04 失敗與恢復的固定證據如下，UTC 與臺灣時間相差八小時。這些第一期限前綴沒有使用後來成功紀錄倒填。

| 新請求 | producer UTC | 完整遮蔽確認 UTC | 第一觀察 elapsed／截止 UTC | 第一期限原始 byte | 期限後還原 UTC／marker UTC |
| --- | --- | --- | --- | --- | --- |
| G04-B01 | 13:32:30.911 | 13:32:02.177 | 20012.149ms／13:33:53.424 | 69966 | 13:34:34.663／13:34:39.574 |
| G04-B02 | 13:36:26.075 | 13:36:17.834 | 20013.620ms／13:37:13.488 | 85374 | 13:37:31.765／13:37:36.143 |
| G04-B03 | 13:39:15.315 | 13:39:03.356 | 20016.194ms／13:40:06.991 | 98176 | 13:40:30.129／13:40:34.581 |

三案第一觀察起點與期限都無 marker，前綴零 A01；當次完整 BN-S00 的精確 Button 為零，根元素 UIA 可見性提示可見。還原後才各一次 FN-A01，marker 與後續工作確認相符。十秒恢復觀察開始時 marker 已存在，只記還原後恢復，不宣稱在該十秒內才核准，也不提升原背景 FAIL。

R5 最大化既有請求三案各有新 App／遮蔽視窗 1920×1152@(0,0)，正式觀察為 `20006.131ms`／`20001.189ms`／`20009.894ms`；第一期限 UTC 為 `13:45:11.810`／`13:56:35.738`／`13:59:35.551`，原始 byte 為 `111496`／`121486`／`128436`。marker 建立於 `13:44:54.786`／`13:56:18.547`／`13:59:18.491`，各第一期限前只有一次 BX-A01，A08 全轉前景。A02 producer 略早於 OFF_COMMAND 落盤，不能宣稱該請求在 OFF 後才提出；原生真卡、遮蔽確認在 OFF 後，此前沒有 A01。遮蔽至 ON 的較長設定等待不計入正式 20 秒。三案工作完成仍不等於全程背景通過。

R5 最大化後新請求三案的 settled cover 均早於實際 producer，正式觀察各滿 20 秒；第一期限前只有新完整 BX-S00、精確 Button 零，沒有 A01 或 marker。root UIA 可見性提示可見，不可解讀成遮蔽比例或 App 正在供應核准 Button。

| 最大化新請求 | producer UTC | settled cover UTC | 第一觀察 elapsed／截止 UTC | 第一期限原始 byte | 期限後還原 UTC／marker UTC |
| --- | --- | --- | --- | --- | --- |
| G05-B01 | 14:00:54.152 | 14:00:24.228 | 20007.484ms／14:01:33.011 | 136995 | 14:01:46.944／14:01:53.176 |
| G05-B02 | 14:03:43.498 | 14:03:10.242 | 20017.357ms／14:04:29.889 | 151196 | 14:04:43.161／14:04:47.863 |
| G05-B03 | 14:06:23.411 | 14:06:08.130 | 20006.912ms／14:06:56.983 | 163255 | 14:07:10.740／14:07:16.387 |

R5 最小化既有請求三案在前景確認真卡、producer 早於最小化；正式觀察各第一起點無 marker、第一期限有 marker，只有一次 M-A01。A08 返回後仍最小化，後續 A06 是 UIA 結果觀察未知、A05 保留舊候選去重，沒有第二次 A01，亦沒有 A07。marker 與 producer 支持工作已完成，不因 A06 抹去工作完成，也不虛構 root 例外。

| 最小化既有請求 | 第一觀察 elapsed／截止 UTC | 第一期限原始 byte | marker UTC | 期限後仍最小化補讀 UTC／還原 UTC |
| --- | --- | --- | --- | --- |
| G06-A01 | 20002.373ms／14:10:21.233 | 174558 | 14:10:04.459 | 14:10:34.506／14:10:45.674 |
| G06-A02 | 20005.511ms／14:12:52.412 | 183605 | 14:12:35.133 | 14:13:08.606／14:13:19.309 |
| G06-A03 | 20015.890ms／14:15:25.767 | 191848 | 14:15:08.784 | 14:15:38.844／14:16:12.046 |

`MINIMIZED_AT_DEADLINE` 名稱的狀態確認實際在期限後補讀，不是與 DEADLINE 同一毫秒的原生截面。還原均在第一期限與補讀之後；G06-A03 的 API RESULT 稍晚，不取代期限前 marker 與單次 M-A01 證據。

R5 最小化後新請求三案各在最小化確認後才由 producer 提出，第一正式 20 秒沒有 marker／A01，新完整 M-S00 的精確 Button 為零。最小化時原始碼略過 root IsOffscreen，root UIA 可見性提示為「未讀取」，**不能寫成可見或讀取失敗**。

| 最小化新請求 | 最小化確認 UTC | producer UTC | 第一觀察 elapsed／截止 UTC | 第一期限原始 byte | 期限後還原 UTC／marker UTC |
| --- | --- | --- | --- | --- | --- |
| G07-B01 | 14:16:28.809 | 14:16:46.416 | 20006.785ms／14:17:22.456 | 201484 | 14:17:43.810／14:17:50.017 |
| G07-B02 | 14:18:38.065 | 14:19:19.609 | 20009.563ms／14:20:12.477 | 216775 | 14:20:23.001／14:20:26.768 |
| G07-B03 | 14:21:13.021 | 14:21:53.739 | 20004.582ms／14:23:05.087 | 230364 | 14:23:20.264／14:23:24.098 |

去重另核對：G07-B01 前舊 P19 沒有 A03，但有兩筆完整前景、root 可見、零 Button 的 S00；依既有 AutoGate 的兩次完整可見掃描規則，支持舊等待已結束。B02／B03 前各有上案恢復候選的 FN-A03。三個新請求第一前綴都沒有 A05，不把本次候選建立前 S00 誤歸為舊候選一直未離開。三案只在期限後還原才各一次 FN-A01 與 marker，後來十秒恢復起點都已存在 marker，不列最小化期間 PASS。

R5 G08 的 OFF 為 `14:24:13.305 UTC`，producer `14:24:54.651 UTC` 才提出單次請求，原生卡與 API pending 在 `14:25:16.241 UTC` 確認。正式觀察 `14:25:16.242–14:25:36.256 UTC`、elapsed `20013.764ms`，marker 始終不存在；第一前綴只有 BN-S00 與 U-G02，沒有 A01。期限後 `14:25:50.972 UTC` 在 OFF 下人工拒絕，producer rejected，marker 仍不存在。這是 OFF 控制行為成功，人工拒絕不列自動完成。

R5 G09 producer 為 `14:27:23.811 UTC`，早於 `15:14:30 UTC` 的本輪工具停止／重啟，ORDER_CORRECTION 明列請求先存在。中間較長設定等待不算正式 timer。新工具初始未連接且 OFF，連接後仍 OFF；`15:16:37.806 UTC` 的 user input notice 發生在量測前，主代理重讀狀態、選回專用聊天後，`15:17:21.019 UTC` 再確認原生真卡與 API pending。這次沒有沿用 R4 G09 的觀察期畫面限制，仍不宣稱測過重啟後才產生新請求。

G09 正式觀察 `15:17:21.020–15:17:41.031 UTC`、elapsed `20011.146ms`，新 main 起點與截止均為 1386 bytes，只有 G00／G02，零 A01，marker 起點與期限均不存在。期限後 `15:17:55.018 UTC` 在 OFF 下人工拒絕，producer rejected，兩個 OFF 清理案的 marker 都未建立。專用 producer turn 於 `15:18:33.706 UTC` 完成；重啟預設 OFF／連接後保留既有 pending 成功，不當成重啟後新請求的驗收。

R5 整理共 31 筆獨立 producer exec，每案恰好一次、無 prefix rule：18 個相應測試條件下自動完成，9 個新背景／最小化請求第一期限失敗後還原完成，3 個 OFF 控制行為保留人工處理，另有一個控制中斷 BLOCKED。這些數字不包含排除的原 helper P7、人工拒絕、未測鎖定或公司版本驗收。

## 相容冷啟動嘗試與最後清理

完成普通矩陣後，為分開驗證既有 Chromium 相容 profile，先做唯讀 preflight，確認本輪身分、設定、目標與引擎線索；驗證紀錄以 VALIDATION_COMPLETE 結束。one-shot 保留既有四個旗標，不新增參數、不覆蓋 WebView2 參數、不修改安裝、沙箱或政策。啟動準備以 ONCE_RESERVED 保留單次權限，接著 WAITING_FOR_EXISTING_APP_EXIT；這是等待狀態，不是成功啟動。

`15:32:25.757 UTC`（臺灣時間 23:32:25），主代理依新原生 File menu 執行「結束 ChatGPT」，該次 Computer Use tool call 被中斷，控制會話失去正常回傳。使用者回報 App 被代理關閉；代理已承認此操作，說明目的是讓相容啟動有乾淨起點，沒有把中斷歸咎使用者。

one-shot 階段 log 只記到等待退出，沒有記錄舊 App 全部退出、launch stage、新參數已生效或真實核准矩陣。原生事件只有 GUI Quit 操作開始，沒有 click／refresh 完成；主代理回報 tool call aborted，使用者確認 App 已被代理關閉，二者分開記錄。後續 `15:45:09 UTC` 的唯讀程序快照沒有本輪 one-shot helper；同一 App 路徑的 19 個新程序，既有四個旗標均為 false。只能記該截面 helper 不在執行與旗標未出現，**不猜測 watchdog exit code 20、精確退出時點或新 App 的啟動來源**。單次 ledger 保留，不刪除或重設來重試；相容試驗為 BLOCKED，不當成 profile 已失敗或背景已修好。目前 App 保持開啟，本輪不再關閉或重試。

最後清理以一般使用者完成，先確認專用工具 OFF，再只停止本輪 runtime 的前後端，確認退出。`15:47:29.982 UTC` 原設定以排他雜湊驗證恢復，結果 PriorRestored=true、沒有 Failure；`15:48:15.748 UTC` 再次核對原設定位元組／雜湊相符。測試設定另保存在本機證據，不覆蓋未知變更。專用遮蔽視窗原生關閉後重新查看視窗清單，App 仍開啟，沒有在清理時再次關閉 App。證據保留 C:\Temp，沒有刪除本輪資料或其他使用者資料。

另於 `15:54:47.850 UTC` 保存未編修的近期清理前 OFF 原生畫面為 `cleanup-before-stop-off.jpg`，JPEG 雜湊與保存 metadata 相符；沒有產生 PNG。畫面擷取 UTC 未記錄，保存晚於工具停止，**保存時間不等於擷取時間**。此圖只支持近期清理前工具為 OFF，不能倒填歷史矩陣、核准時點或背景／最小化條件。JPEG 與 metadata 留在本機，不提交或放入 ZIP。

## 控制中斷、候選排除與使用者回報

R5 G03-A03 在 `10:38:38.177 UTC` 兩次 captured window activation 失敗。條件未建立，舊座標操作停止，不能把此案寫成 provider 背景 FAIL。主代理其後僅停止本輪 runtime 的前後端，沒有確認前端 OFF ack，因此明記「工具已停止」。`11:49:01.015 UTC` 工具停止後人工拒絕原卡，marker 在停止前後都不存在；producer final 仍表示結果未知，保留限制，不改成成功。

中斷前原 helper 的 P6 只有 BN-S01／E17：重掃未取得候選，沒有 A01。原 helper 的 P7 在 `10:38:45` 記 A01，`10:38:47` 記 A02／A08／A03；停止清單 JSON 於 `10:38:47.715 UTC` 建立，host exec 返回本輪專用程序已停止，沒有獨立 UTC 返回值。時序只支持 P7 可能關聯控制核准的推論，沒有建立 App 原生請求身分。**原 helper P7 一律排除矩陣成功數**，其「仍在背景」A08 也不是 G03-A03 完成證據。恢復 helper 的 P7 是後來 G04-A03，來源不同，不與被排除候選混用。

停止前的 L03 短摘要取代錯誤後，完整主檔仍有 A02／A08／A03；不能只讀摘要最後一行便判定後端已退出。控制恢復後另啟可見工具、重新連接且 OFF，後續改用新的 CASE_ACTIVE main log 與 offset；不跨 helper 宣稱連續 ON，也不抹掉控制中斷。

使用者的 `USER_REPORT` 分開保留：早期 marker 曾人工允許，初始 OFF 完成不計自動；R4 G05-B01 最新回覆為「有切換視窗」，該案保留 BLOCKED，不算最大化背景 PASS。公司 0.3.7 背景與最小化有真實請求卻不核准、鎖定未測，仍是使用者回報；本輪沒有存取公司電腦或公司 log。

## 失敗階段、推論與未測項目

- **直接觀察／檔案核對：** R4 一般大小完全遮蔽與最小化新請求各三案，第一正式 20 秒只有新完整 S00、精確 Button 零、沒有 A01 與 marker；R4 最大化新請求亦有同階段失敗，保留 B03 計時略短限制。R5 一般大小完全遮蔽、最大化完全遮蔽與最小化新請求各三案，同樣在第一正式 20 秒沒有精確 Button／A01／marker，還原後才完成。這是候選建立前未取得精確 Button，不能記成已進入 Invoke 卻沒有返回。
- **直接觀察／檔案核對：** 遮蔽既有請求及部分遮蔽新請求可由單次 A01、marker、producer 相互核對完成，但 A08 返回後轉前景；與全程背景成功分開。R4 最小化既有請求三案 marker 已完成且 A08 仍最小化；A06／A05 表示 UIA 結果觀察未知、舊候選去重，沒有第二次 A01。後來還原不倒填最小化成功。
- **使用者回報：** 公司失敗、早期人工允許與 R4 G05-B01 切換視窗如上。回報與本機直接觀察不合併為同版本驗收。
- **推論／仍需確認：** 某些條件下真實 pending 沒有透過目前完整 UIA 目標子樹提供精確核准 Button。這不證明 renderer 停止，不證明所有 Windows／App 版本同根因，也不能由 S00 推論根元素、WindowPattern 或幾何讀取已失敗。R4 初次 MCP 派送候選及 R5 控制期 P7 均未建立請求身分，不能據此宣稱錯按哪一個 App 請求。
- **直接觀察／檔案核對：** R5 初始 OFF、切 OFF 後新請求與重啟後保留既有 pending 均在正式 20 秒沒有 marker／A01，清理在 OFF、期限後人工拒絕。G09 有量測前原生真卡確認，但 producer 早於重啟，不提升為重啟後新請求的測法。
- **畫面證據限制：** 歷史矩陣的直接畫面觀察來自本會話 Computer Use 工具輸出，沒有另存本機截圖檔。不能宣稱本機已有歷史截圖備份、完整畫面回放或每案對應的圖片檔；本機 log／journal 與 marker 等獨立工作證據仍可核對。任何其後保存的清理畫面只證明該次清理狀態，不倒填歷史矩陣。
- **受阻：** 相容冷啟動 preflight／armed 已執行，但 GUI Quit 中斷控制會話，沒有 launch stage 或相容 profile 真實矩陣，列 BLOCKED；單次 ledger 保留，不重試。
- **未測：** 公司固定 App 版本、其他語系、長時間穩定性與鎖定未執行驗收。背景／最小化新請求未通過且沒有安排人工解鎖，不啟動鎖定測試，不取得帳密或嘗試自動解鎖。

## 0.3.10 修改與程式回歸

原始碼與可重複測試確認另一項缺陷：0.3.9 的 `Observe` 用同一個 `ElementNotAvailableException` catch 包含原候選與根元素／WindowPattern 讀取。根元素讀取失效可能回傳 `ORIGINAL_ELEMENT_REMOVED`，即使沒有原候選已離開的證據也結束去重週期。因此歷史 0.3.9 A03 不能單憑標籤視為原 Button 已確定失效，更不能當成工作完成。

0.3.10 新增共用 `ObservationStep.ReadOnce`，真正後端與 Manual／Auto 回歸測試使用同一實作。根元素 Snapshot 放在候選 catch 外，讀取失敗由既有 A07「核准後結果觀察」記錄，維持未知結果與舊候選去重；production 的候選失效分類固定為真正 UIA `ElementNotAvailableException`，其他例外不泛稱候選消失。只有原候選本身讀取失效才可產生該自動模式 A03，既有前景 hidden-end 判定保留。

保持原 RuntimeId → Current → root Snapshot → candidate IsOffscreen 順序、五秒觀察期限及最小化不查 IsOffscreen。AutoGate、OFF 撤銷舊代次、每次啟動 OFF、持續 ON、唯一精確「允許一次」Button、完整重掃、目標／祖先／局部內容確認與每週期最多一次 Invoke 均未放寬。OFF 不清除已開始嘗試的未知候選，不重送不確定操作。沒有診斷面板、模糊按鈕匹配、輸入／搶前景／還原 API、新啟動旗標、提權或網路行為。

核心修改為 `desktop-probe/manual/ObservationStep.cs`、`ManualProgram.cs`、共用 helper 的 Manual／Auto 測試及打包 source mapping。三個專案、manifest、前後端版本比對、log 碼與打包版本一致為 0.3.10，舊基準保留；最終成品仍須另外確認封裝與雜湊，不能用本段取代封裝驗收。

Windows `scripts/check-desktop.ps1` 完成三個 net48 建置、十一組測試及三項成品 API audit，實際退出碼 0。NU1900 套件弱點資料來源不可達與既有 CS0649 欄位警告仍保留，沒有宣稱零警告。API audit 保留 asInvoker／uiAccess=false，後端唯一一次 Invoke API 參照與既有唯讀原生 API 範圍；靜態 API 檢查不是完整 runtime 證明。

另有 Windows net48 的 21 項檢查，直接連結同一 production helper、AutoGate、BackgroundPolicy，使用 UIAutomationTypes 的真正 UIA 例外，退出碼 0。根元素例外經 Task 保留為 AggregateException、未知 gate 不重送，ElementNotEnabledException 不被候選失效分類捕捉。這些只呼叫程式 delegate，沒有讀 App/provider 或 Invoke，屬程式回歸，**不是 App 背景驗收，也不是 Button 供應修復證據**。

R5 已稽核範圍顯示這項安全修正未破壞前景持續 ON、已測遮蔽／最小化既有請求與 OFF／重啟行為；但完全遮蔽及最小化後的新請求仍失敗。不能將候選建立後的分類修正，說成候選建立前 S00 的已確認根因或修復。普通使用者工作階段矩陣已結束，相容冷啟動受控制中斷而 BLOCKED，最終 ZIP 封裝另行核對。

## 本機證據與交付範圍

原始 main log、摘要、journal、marker、專用 producer 時序與停止清單保留本機 `C:\Temp\codex-autoaccept-revalidation-20261013-03`。R4／R5 與相容嘗試的整理稽核保存在本機 `evidence/audit-sanitized.md`、`evidence/audit-sanitized-0310.md`、`evidence/audit-compat-sanitized.md`；原始 journal 採追加更正，不刪除或重寫失敗。歷史矩陣畫面來自本會話 Computer Use 工具輸出，未另存本機截圖檔，不列為本機已保存檔案。

交付文件只引用整理後的案例、固定狀態、版本與成品雜湊，不帶原始命令、聊天內容、可及性樹、RuntimeId、憑證或公司內容。raw log、截圖、journal、marker 與專用 rollout 不提交 repository、不加入 ZIP。下方歷史文件仍保留：

- [R3 授權限制與證據更正](WINDOWS_TEST_RESULTS_20261013_R3.md)
- [更早的個人 Windows 結果及限制](WINDOWS_TEST_RESULTS_20261013.md)
- [驗證範圍與歷史](VERIFICATION.md)

本報告記錄本輪已完成矩陣、受阻相容實驗與尚未解決項目，不宣稱公司背景、最小化或鎖定問題已修好。外部封裝身分以 repository 的 `artifacts/SHA256SUMS` 為準，內部檔案雜湊由 ZIP 的 `SHA256SUMS` 核對；本報告不嵌入所屬 ZIP 的自我雜湊。
