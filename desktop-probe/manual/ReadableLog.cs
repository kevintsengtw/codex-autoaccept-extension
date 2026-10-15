using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

// Keys stay in memory. The exported number is only a local UIA-candidate sequence,
// never an App request ID, command, runtime ID or content fingerprint.
internal sealed class RequestTrace
{
    readonly object sync = new object();
    string key; long number; bool ended;
    public long Observe(string value, bool eligible)
    {
        if (value == null) return 0;
        lock (sync)
        {
            if (value != key || ended && eligible) { key=value; number++; ended=false; }
            return number;
        }
    }
    public void End(long value) { lock (sync) if (value == number) ended=true; }
}

// All descriptions are fixed tool text. Unknown strings are discarded, not logged.
// Caller serializes access and writes returned blocks; formatter performs no I/O.
internal sealed class ReadableLog
{
    readonly Dictionary<string,string> scanKeys = new Dictionary<string,string>();
    readonly Dictionary<string,string> eventKeys = new Dictionary<string,string>();
    readonly Dictionary<string,long> scanWrittenAt = new Dictionary<string,long>();
    readonly Dictionary<string,long> eventWrittenAt = new Dictionary<string,long>();
    readonly Stopwatch uptime = Stopwatch.StartNew();
    readonly Func<long> elapsedMilliseconds;
    const long REPEAT_INTERVAL_MS = 10000;
    public ReadableLog(Func<long> elapsedMilliseconds = null)
    { this.elapsedMilliseconds = elapsedMilliseconds ?? (() => uptime.ElapsedMilliseconds); }
    string issueBrief, actionBrief;
    string lastScanText, lastScanCode;
    readonly Dictionary<string,string> viewBriefs = new Dictionary<string,string>();
    int sequence;
    public const string Version = "0.3.10";
    public static string Header => "Codex Auto-Accept " + Version + " 本機執行紀錄\r\n"
        + "本檔只有工具狀態與計數，不含命令、對話、視窗標題、PID、路徑或內容指紋。\r\n"
        + "候選 P 編號是工具本機的 UIA 候選序號，不是 App 的原生請求 ID。\r\n"
        + "沒有候選編號時，不能判定是否存在待核准請求，也不能關聯到某次工作。\r\n"
        + "找到按鈕、呼叫返回、原元素失效，分別記錄；不自動宣稱 App 工作成功。\r\n"
        + "背景只表示不是前景，工具未自動判定遮蔽比例；桌面不可確認不等於已確認鎖定。\r\n"
        + "相同狀態重複掃描會合併；上筆滿十秒後，下一次完成掃描會保留新一筆，不刪除既有紀錄。\r\n"
        + "完整紀錄保留在本機，可在工作結束後檢查核准前後階段。\r\n\r\n";
    static string View(string mode,string size) => mode=="MINIMIZED" ? "M" : mode=="FOREGROUND" ? (size=="MAXIMIZED"?"FX":"FN")
        : mode=="BACKGROUND" ? (size=="MAXIMIZED"?"BX":"BN") : "U";
    static string ViewText(string mode,string size) => mode=="MINIMIZED" ? "最小化" : mode=="FOREGROUND" ? (size=="MAXIMIZED"?"最大化／前景":"一般大小／前景")
        : mode=="BACKGROUND" ? (size=="MAXIMIZED"?"最大化／背景":"一般大小／背景") : "無法確認";
    static int Count(int n) => Math.Max(0,Math.Min(10000,n));
    static string Reason(string code)
    {
        switch(code)
        {
            case "NATIVE": return "無法確認原生視窗狀態";
            case "HIDDEN": return "目標視窗已隱藏";
            case "CLOAKED": return "目標視窗被系統隱藏或位於其他桌面";
            case "DESKTOP": return "沒有可確認的前景互動桌面";
            case "ROOT": return "視窗不可見或前景／最小化狀態矛盾";
            case "FRAME": return "視窗範圍無法確認";
            case "MODAL": return "目標被其他對話框阻擋";
            case "TRANSITION": return "掃描期間視窗狀態切換，需重新掃描";
            case "ROOT_READ": return "無法讀取 UIA 根元素或根元素身分";
            case "WINDOW_PATTERN": return "無法讀取視窗互動狀態";
            case "BUTTON_READ": return "無法讀取控制項或候選按鈕屬性";
            case "CONTEXT_READ": return "無法完成父層與局部內容比對";
            case "NODE_LIMIT": return "節點數超過完整掃描上限";
            case "DEPTH_LIMIT": return "控制項深度超過完整掃描上限";
            case "TREE_REPEAT": return "控制項樹重複，掃描不完整";
            case "MULTIPLE": return "不是唯一的可用候選，未核准";
            default: return "讀取或驗證未完成";
        }
    }
    static string ReasonNumber(string reason)
    {
        var codes = new[]{"READ","NATIVE","HIDDEN","CLOAKED","DESKTOP","ROOT","FRAME","MODAL","TRANSITION","ROOT_READ","WINDOW_PATTERN","BUTTON_READ","CONTEXT_READ","NODE_LIMIT","DEPTH_LIMIT","TREE_REPEAT","MULTIPLE"};
        int index=Array.IndexOf(codes,reason); return (index<0?0:index).ToString("D2");
    }
    string Block(string mode,string size,long request,string phase,string result,string code,string detail,bool issue,bool action)
    {
        string number = request>0 && request<=1000000000 ? "P"+request.ToString("D6") : "未建立可追蹤候選";
        string block = "【紀錄 L"+(++sequence).ToString("D6")+"｜候選："+number+"】\r\n"
            + "本機時間："+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+"\r\n"
            + "視窗狀態（本次或最後一次重掃）："+ViewText(mode,size)+"\r\n"
            + "階段："+phase+"\r\n結果："+result+"\r\n"
            + "回報碼：0310-"+View(mode,size)+"-"+code+"\r\n"+detail+"\r\n\r\n";
        string brief="候選："+number+"；視窗："+ViewText(mode,size)+"\r\n回報碼：0310-"+View(mode,size)+"-"+code+"\r\n結果："+result+"\r\n\r\n";
        if(issue) issueBrief=brief;
        if(action) actionBrief=brief;
        return block;
    }
    public string Scan(ScanMetrics m,bool readOnly)
    {
        string view=View(m.Mode,m.WindowState);
        string code, text;
        bool issue=false;
        if(m.Result=="BLOCKED") { code="B"+ReasonNumber(m.Reason); text="未核准："+Reason(m.Reason); issue=true; }
        else if(m.Result!="COMPLETE") { code="R"+ReasonNumber(m.Reason); text="掃描未完成："+Reason(m.Reason); issue=true; }
        else if(m.Labels==0) { code="S00"; text="本次掃描沒有找到「允許一次」Button；不能據此認定當時有待核准請求"; }
        else if(m.Enabled==0) { code="S02"; text="找到核准按鈕，但按鈕未啟用"; issue=true; }
        else if(m.Invoke==0) { code="S03"; text="找到核准按鈕，但 App 未提供程式化按下能力"; issue=true; }
        else if(m.Eligible==0)
        {
            code=m.EligibilityReason=="EMPTY_BOUNDS"?"S05":m.EligibilityReason=="OUTSIDE_FRAME"?"S06":m.EligibilityReason=="OFFSCREEN"?"S07":"S04";
            text=code=="S05"?"找到按鈕，但按鈕座標為空或無效":code=="S06"?"找到按鈕，但座標中心不在目標視窗內":code=="S07"?"前景按鈕被標示為不可見":"找到按鈕，但候選條件未通過"; issue=true;
        }
        else { code="S01"; text="找到符合候選條件的按鈕；這筆只記錄掃描，尚不代表已核准"; }
        lastScanText=text; lastScanCode="0310-"+view+"-"+code;
        string key=code+"/"+m.Request+"/"+m.Labels+"/"+m.Enabled+"/"+m.Invoke+"/"+m.Eligible+"/"+readOnly
            +"/"+(m.Nodes<=1)+"/"+m.RootOffscreen+"/"+m.RootOffscreenRead;
        long now = elapsedMilliseconds(), written;
        string old; if(scanKeys.TryGetValue(view,out old) && old==key
            && scanWrittenAt.TryGetValue(view,out written) && now-written<REPEAT_INTERVAL_MS) return null;
        scanKeys[view]=key;
        scanWrittenAt[view]=now;
        string detail="掃描是否完整："+(m.Result=="COMPLETE"?"是":"否，以下為已讀到的部分資料")
            + "\r\n核准按鈕數："+Count(m.Labels)+"；已啟用："+Count(m.Enabled)+"；可程式化按下："+Count(m.Invoke)+"；符合候選條件："+Count(m.Eligible)
            + "\r\n已讀節點："+Count(m.Nodes)+"；Button："+Count(m.Buttons)
            + "\r\n根元素的 UIA 可見性提示："+(!m.RootOffscreenRead?"未讀取":m.RootOffscreen?"不可見":"可見")
            + "\r\n本筆是否呼叫核准：否（掃描紀錄）"+(readOnly?"；唯讀模式不會核准":"；真正的核准嘗試另記於同一候選編號")
            + (m.Request==0?"\r\n請求關聯：資料不足，不能判定是哪次工作或是否已有待核准請求":"");
        string block=Block(m.Mode,m.WindowState,m.Request,readOnly?"唯讀掃描（不核准）":"自動模式：按鈕掃描",text,code,detail,issue,false);
        viewBriefs[view]="視窗："+ViewText(m.Mode,m.WindowState)+"；候選："+(m.Request>0?"P"+m.Request.ToString("D6"):"未建立")
            +"\r\n回報碼：0310-"+view+"-"+code+"\r\n結果："+text+"\r\n\r\n";
        return block;
    }
    public string Event(string kind,long request,string mode,string size,string reason=null)
        => EventCore(kind,request,mode,size,reason,null);
    static string ActionStage(string reason)
    {
        switch(reason)
        {
            case "PRE_INVOKE_NATIVE": return "核准前原生視窗狀態查詢";
            case "INVOKE": return "Invoke 呼叫";
            case "POST_INVOKE_NATIVE": return "Invoke 返回後原生視窗狀態查詢";
            case "PERSIST_RETURNED": return "呼叫返回階段紀錄";
            case "OBSERVE": return "核准後結果觀察";
            default: return "未確認";
        }
    }
    string EventCore(string kind,long request,string mode,string size,string reason,int? nativeError)
    {
        string code,text,phase="自動核准流程",detail=""; bool issue=false,action=false;
        switch(kind)
        {
            case "RECHECK": code="E14"; text="核准前重掃的身分或局部內容變動，本次未呼叫核准"; issue=true; break;
            case "RECHECK_MISSING": code="E17"; text="核准前重掃未取得可用候選，本次未呼叫核准。重掃結果："+(lastScanText??"未取得")+"（"+(lastScanCode??"未取得")+"）"; issue=true; break;
            case "TARGET_CHANGED": code="E10"; text="目標視窗已變更，本次未呼叫核准"; issue=true; break;
            case "ROOT_CHANGED": code="E11"; text="目標根元素已變更，本次未呼叫核准"; issue=true; break;
            case "ANCESTRY_CHANGED": code="E12"; text="按鈕已不在原目標樹內，本次未呼叫核准"; issue=true; break;
            case "BUTTON_CHANGED": code="E13"; text="呼叫前按鈕不再符合條件，本次未呼叫核准"; issue=true; break;
            case "CONTEXT_CHANGED": code="E14"; text="呼叫前按鈕身分或局部內容變動，本次未呼叫核准"; issue=true; break;
            case "PATTERN_LOST": code="E15"; text="呼叫前程式化按下能力已消失，本次未呼叫核准"; issue=true; break;
            case "SWITCH_CHANGED": code="G02"; text="開關已變更，本次未開始新的核准嘗試"; break;
            case "WAIT_PREVIOUS": code="A05"; text="等待上一個核准候選離開，或相同候選已處理過；沒有重送"; break;
            case "ATTEMPT": code="A01"; text="已承諾開始一次核准嘗試，結果尚未確認"; action=true; break;
            case "RETURNED": code="A02"; text="程式化按鈕操作已返回；尚未證明 App 工作繼續或成功"; action=true; break;
            case "WINDOW_AFTER_INVOKE": code="A08"; text=reason=="TO_FOREGROUND" ? "核准呼叫返回後，目標已轉為前景；此筆不能證明全程留在背景或最小化"
                : reason=="TO_MINIMIZED" ? "核准呼叫返回後，目標已最小化；工作完成仍須獨立證據"
                : reason=="TO_BACKGROUND" ? "核准呼叫返回後，目標已轉為背景；工作完成仍須獨立證據"
                : reason=="MINIMIZED_RETAINED" ? "核准呼叫返回後，原生視窗仍為最小化；工作完成仍須獨立證據"
                : reason=="BACKGROUND_RETAINED" ? "核准呼叫返回後，原生視窗仍在背景；工作完成仍須獨立證據"
                : reason=="FOREGROUND_RETAINED" ? "核准呼叫返回後，原生視窗仍在前景；工作完成仍須獨立證據"
                : "核准呼叫返回後，原生視窗狀態無法確認；不推論工作結果"; action=true; break;
            case "LOG_WRITE_FAILURE": code=reason=="MAIN_APPEND"?"L01":reason=="SUMMARY_WRITE"?"L02":"L03";
                text=reason=="MAIN_APPEND"?"完整主 log 寫入失敗，停止新的核准嘗試"
                    :reason=="SUMMARY_WRITE"?"短摘要暫存檔寫入失敗；繼續使用完整主 log，摘要可能過期"
                    :"短摘要原子取代失敗；繼續使用完整主 log，摘要可能過期"; issue=true; break;
            case "REMOVED": code="A03"; text="原核准元素已失效，工具結束本次觀察並繼續監聽"; action=true; break;
            case "HIDDEN": code="A04"; text="原核准按鈕在前景下已隱藏，工具繼續監聽"; action=true; break;
            case "WAIT_RESULT": code="A06"; text="已開始核准嘗試，但觀察結果未確認；不重送"; issue=true; action=true; break;
            case "ACTION_ERROR": code="A07"; text="核准嘗試已開始後發生錯誤；可能已生效，不重送"; issue=true; action=true; break;
            case "VALIDATION_ERROR": code="R"+ReasonNumber(reason); text="呼叫前未完成："+Reason(reason)+"；未承諾新的核准嘗試"; issue=true; break;
            case "TIMEOUT_SCAN": code="T01"; text="按鈕掃描逾時，未能建立本次核准候選"; issue=true; break;
            case "TIMEOUT_RECHECK": code="T02"; text="核准前重掃逾時，未完成同一候選的確認"; issue=true; break;
            case "TIMEOUT_ACTION": code="T03"; text="單次核准或結果觀察逾時；是否已開始核准，以同一候選的 A01 紀錄為準，不重送"; issue=true; break;
            default: code="E00"; text="未辨識的工具事件，原始內容未寫入"; issue=true; break;
        }
        string key=kind+"/"+request+"/"+code+(kind=="RECHECK_MISSING"?"/"+lastScanCode:"")
            +(kind=="ACTION_ERROR"?"/"+ActionStage(reason):"")+(nativeError.HasValue?"/"+nativeError.Value:"");
        string view=View(mode,size),old;
        long now=elapsedMilliseconds(),written;
        bool repeatable=kind=="WAIT_PREVIOUS" || kind=="WAIT_RESULT";
        if(eventKeys.TryGetValue(view,out old) && old==key && (!repeatable
            || eventWrittenAt.TryGetValue(view,out written) && now-written<REPEAT_INTERVAL_MS)) return null;
        eventKeys[view]=key;
        eventWrittenAt[view]=now;
        detail=kind=="LOG_WRITE_FAILURE"?"是否已有核准嘗試須查看主 log 前後階段；本筆不推論結果，不重送核准。"
            :kind=="TIMEOUT_ACTION"?"本筆只記錄逾時；是否已有核准嘗試，須查看同一候選的 A01 紀錄。"
            : action?"本筆涉及已開始的核准嘗試；查看同一候選編號的前後階段。":"本筆未開始新的核准呼叫。";
        if(kind=="ACTION_ERROR") detail+="\r\n失敗階段："+ActionStage(reason)+"；不推論 App 工作結果。";
        if(kind=="LOG_WRITE_FAILURE" && nativeError.HasValue)
            detail+="\r\n本機檔案錯誤代碼（Win32）："+(nativeError.Value>=0?nativeError.Value.ToString():"未確認")+"。";
        return Block(mode,size,request,phase,text,code,detail,issue,action);
    }
    public string LocalLogFailure(string mode,string size,string stage,int nativeError)
    {
        return EventCore("LOG_WRITE_FAILURE",0,mode,size,stage,
            nativeError>=0 && nativeError<=65535?nativeError:-1);
    }
    public string Status(string message,bool richAuto=false)
    {
        // Rich scan/action records cover these; do not duplicate fleeting UI text.
        if(message.StartsWith("UIA_STATE:") || message.StartsWith("AUTO:") || richAuto && message.StartsWith("OBSERVATION:") || message=="INVOKE: RETURNED" || message=="ATTEMPT: AUTO_INVOKE_ONCE") return null;
        string text,code="G00";
        if(message=="STATUS: ON") { text="自動核准開啟"; code="G01"; scanKeys.Clear(); scanWrittenAt.Clear(); }
        else if(message=="STATUS: OFF" || message=="READY: OFF") { text="人工核准（OFF），不開始新的自動核准嘗試"; code="G02"; }
        else if(message.StartsWith("MODE: READ_ONLY")) text="唯讀模式：只讀取，不呼叫核准";
        else if(message.StartsWith("MODE: AUTO")) text="自動模式啟動，初始為 OFF";
        else if(message.StartsWith("MODE: MANUAL")) text="手動單次核准模式";
        else if(message.StartsWith("ATTEMPT: INVOKE_ONCE")) { text="使用者確認後開始一次手動核准嘗試"; code="A01"; }
        else if(message.StartsWith("INVOKE: RETURNED")) { text="手動核准呼叫已返回；結果仍需觀察"; code="A02"; }
        else if(message=="OBSERVATION: BUTTON_UNAVAILABLE_OR_HIDDEN") { text="手動核准後原按鈕已不可用或隱藏；仍需在 App 確認工作是否繼續"; code="A03"; }
        else if(message.StartsWith("OBSERVATION:")) { text="手動核准後的觀察未確認結果；不自動重送"; code="A06"; }
        else if(message.StartsWith("RESULT: TIMEOUT")) { text="後端逾時；若本次已有核准嘗試，結果可能已生效，請勿重送"; code="E16"; }
        else if(message.StartsWith("RESULT: CANCELLED")) text="使用者取消，未核准";
        else if(message.StartsWith("RESULT: NOT_INVOKED")) text="流程未記錄核准呼叫";
        else if(message.StartsWith("RESULT: UNKNOWN_AFTER")) { text="核准嘗試後結果不明，不重送"; code="A07"; }
        else if(message.StartsWith("ERROR_TYPE:")) { text="後端發生錯誤，原始例外訊息不寫入"; code="E00"; }
        else text="工具狀態變更；未辨識的原始內容不寫入";
        return Block("UNKNOWN","UNKNOWN",0,"工具控制",text,code,"此筆未關聯到自動模式的候選編號。",code.StartsWith("E"),code.StartsWith("A"));
    }
    public string Summary
    {
        get
        {
            var s=new StringBuilder("Codex Auto-Accept "+Version+"｜回報用短摘要\r\n");
            s.Append("完整紀錄保留在本機；短摘要可能過期，核准結果須另以工作完成確認。\r\n");
            s.Append("這是保存的紀錄，不必停住工作；完整前後階段在同名主 log。\r\n\r\n");
            s.Append("【最近一次受阻／錯誤：歷史紀錄，不代表目前仍失敗】\r\n").Append(issueBrief??"尚無此類紀錄。\r\n\r\n");
            s.Append("【最近一次核准嘗試的結果】\r\n").Append(actionBrief??"尚未記錄核准嘗試；不能宣稱核准成功。\r\n\r\n");
            s.Append("各視窗狀態的最後掃描\r\n");
            foreach(string view in new[]{"FX","FN","BX","BN","M","U"}) { string block; if(viewBriefs.TryGetValue(view,out block)) s.Append(block); }
            s.Append("---- 說明 ----\r\n").Append(Header);
            return s.ToString();
        }
    }
}
