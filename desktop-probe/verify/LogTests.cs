static void Check(bool value,string name) { if (!value) throw new Exception(name); }
static void WhileReadOnly(string path,Action action)
{
    if(!Path.GetFullPath(path).StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe read-only fixture path");
    var original=File.GetAttributes(path);
    try
    {
        File.SetAttributes(path,original | FileAttributes.ReadOnly);
        Check((File.GetAttributes(path) & FileAttributes.ReadOnly)!=0,"read-only fixture is active");
        action();
    }
    finally { File.SetAttributes(path,original); }
    Check(File.GetAttributes(path)==original,"read-only fixture restores its original attributes");
}
var trace=new RequestTrace();
long first=trace.Observe("PRIVATE_RUNTIME_ID",true);
Check(first==1 && trace.Observe("PRIVATE_RUNTIME_ID",true)==first,"same live candidate gets one number across scans");
trace.End(first);
Check(trace.Observe("PRIVATE_RUNTIME_ID",false)==first,"retained hidden/unusable element does not become a new request");
long second=trace.Observe("PRIVATE_RUNTIME_ID",true);
Check(second==2,"eligible reused element after completed cycle gets new local number");
long third=trace.Observe("DIFFERENT_PRIVATE_ID",false);
trace.End(second);
Check(trace.Observe("DIFFERENT_PRIVATE_ID",true)==third,"ending earlier candidate does not close the newer candidate");
Check(trace.Observe(null,true)==0,"missing source cannot claim a known request");
var log=new ReadableLog();
var m=new ScanMetrics { Mode="BACKGROUND",WindowState="MAXIMIZED",Result="COMPLETE",Reason="NONE",EligibilityReason="EMPTY_BOUNDS",Nodes=20,Buttons=3,Named=1,Labels=1,Enabled=1,Invoke=1,Eligible=0,Request=first };
var blocked=log.Scan(m,false);
Check(blocked.Contains("最大化／背景") && blocked.Contains("P000001") && blocked.Contains("0310-BX-S05") && blocked.Contains("本筆是否呼叫核准：否"),"formatted error includes condition/candidate/reason/report code, not implicit approval");
Check(blocked.Contains("\r\n結果：") && !blocked.Contains(@"\r\n"),"plain-text report has real Windows line breaks");
Check(log.Scan(m,false)==null,"identical scans are coalesced");
m.Nodes=1;
Check(log.Scan(m,false)!=null,"collapsed tree is not hidden by coalescing");
var attempt=log.Event("ATTEMPT",first,"BACKGROUND","MAXIMIZED");
var returned=log.Event("RETURNED",first,"BACKGROUND","MAXIMIZED");
Check(attempt.Contains("P000001") && returned.Contains("P000001") && returned.Contains("尚未證明"),"same candidate links invocation stages and returned is not success");
Check(log.Event("RETURNED",first,"BACKGROUND","MAXIMIZED")==null,"duplicate lifecycle event coalesces");
Check(log.Summary.Contains("0310-BX-S05") && log.Summary.Contains("0310-BX-A02"),"issue remains available after later execution stage");
Check(log.Summary.IndexOf("0310-BX-S05")<log.Summary.IndexOf("---- 說明 ----"),"short report comes before detailed instructions");
var unknown=log.Scan(new ScanMetrics { Mode="MINIMIZED",WindowState="MINIMIZED",Result="COMPLETE",Reason="NONE",Nodes=10,Buttons=1,RootOffscreenRead=false },false);
Check(unknown.Contains("未建立可追蹤候選") && unknown.Contains("不能判定") && unknown.Contains("0310-M-S00"),"no button does not invent a pending request or associate it with previous candidate");
var error=log.Scan(new ScanMetrics { Mode="BACKGROUND",WindowState="MAXIMIZED",Result="ERROR",Reason="WINDOW_PATTERN",Nodes=10 },false);
Check(error.Contains("0310-BX-R10") && error.Contains("掃描是否完整：否"),"incomplete scans are identified precisely");
var missing=log.Event("RECHECK_MISSING",first,"BACKGROUND","MAXIMIZED");
Check(missing.Contains("0310-BX-E17") && missing.Contains("0310-BX-R10"),"failed revalidation retains the precise last scan reason");
Check(log.Event("TIMEOUT_SCAN",0,"MINIMIZED","MINIMIZED").Contains("0310-M-T01"),"scan timeout has its own report code");
Check(log.Event("TIMEOUT_ACTION",first,"BACKGROUND","MAXIMIZED").Contains("0310-BX-T03"),"invocation timeout does not claim nothing was invoked");
Check(log.Status("OBSERVATION: BUTTON_UNAVAILABLE_OR_HIDDEN").Contains("仍需在 App 確認") && log.Status("OBSERVATION: ORIGINAL_ELEMENT_REMOVED",true)==null,"manual observation is preserved and automatic rich stages are not duplicated");
var readOnly=log.Scan(m,true); Check(readOnly.Contains("唯讀模式不會核准"),"CLI read-only logs are explicit");
string malicious=log.Event("PRIVATE_COMMAND C:\\SECRET_PATH",1,"PRIVATE_NAME","SECRET_TITLE")+log.Status("ERROR_TYPE: PRIVATE_COMMAND C:\\SECRET_PATH")+log.Summary;
Check(!malicious.Contains("PRIVATE") && !malicious.Contains("SECRET") && !malicious.Contains("C:\\"),"unknown strings and private in-memory keys never enter log output");
Check(log.Event("VALIDATION_ERROR",1,"BACKGROUND","MAXIMIZED","CONTEXT_READ").Contains("父層與局部內容"),"validation error logs a specific stage");
Check(log.Event("WINDOW_AFTER_INVOKE",third,"MINIMIZED","MINIMIZED","TO_FOREGROUND").Contains("0310-M-A08"),"post-invoke foreground transition is explicit");
Check(log.Event("LOG_WRITE_FAILURE",0,"BACKGROUND","MAXIMIZED","SUMMARY_REPLACE").Contains("0310-BX-L03"),"summary failure does not claim a provider or approval failure");
Check(new ReadableLog().LocalLogFailure("BACKGROUND","MAXIMIZED","SUMMARY_REPLACE",32).Contains("Win32）：32"),"numeric file cause stays in the local main log without paths or exception messages");
long sampleClock=0;
var periodic=new ReadableLog(()=>sampleClock);
var empty=new ScanMetrics { Mode="BACKGROUND",WindowState="MAXIMIZED",Result="COMPLETE",Reason="NONE",Nodes=10,Buttons=2,Labels=0 };
Check(periodic.Scan(empty,false)!=null,"first empty background scan is retained");
sampleClock=9999; Check(periodic.Scan(empty,false)==null,"identical scan is bounded by the repeat interval");
sampleClock=10000; Check(periodic.Scan(empty,false)!=null,"fresh repeated scan is retained after ten seconds");
periodic.Status("STATUS: ON"); Check(periodic.Scan(empty,false)!=null,"new ON records the first scan instead of reusing an earlier ON result");
Check(periodic.Event("WAIT_PREVIOUS",1,"MINIMIZED","MINIMIZED")!=null,"first deduplication wait is retained");
sampleClock=19999; Check(periodic.Event("WAIT_PREVIOUS",1,"MINIMIZED","MINIMIZED")==null,"deduplication wait is coalesced briefly");
sampleClock=20000; Check(periodic.Event("WAIT_PREVIOUS",1,"MINIMIZED","MINIMIZED")!=null,"continued deduplication gets a fresh wait record");
var stages=new ReadableLog();
Check(stages.Event("ACTION_ERROR",1,"BACKGROUND","NORMAL","INVOKE").Contains("失敗階段：Invoke 呼叫"),"invocation error stage is explicit without claiming non-execution");
Check(stages.Event("ACTION_ERROR",1,"BACKGROUND","NORMAL","OBSERVE").Contains("核准後結果觀察"),"changed post-invoke stage is not hidden by coalescing");
Check(!stages.Event("ACTION_ERROR",2,"BACKGROUND","NORMAL","PRIVATE_SECRET_PATH").Contains("PRIVATE"),"unknown stage text never enters the log");
var fileCauses=new ReadableLog();
Check(fileCauses.LocalLogFailure("BACKGROUND","NORMAL","SUMMARY_REPLACE",32)!=null
    && fileCauses.LocalLogFailure("BACKGROUND","NORMAL","SUMMARY_REPLACE",1175).Contains("1175"),"different numeric file causes are not hidden by event coalescing");
var securityFailure=new LogWriteFailure("MAIN_APPEND",new System.Security.SecurityException("PRIVATE_SECURITY_MESSAGE"));
Check(securityFailure.NativeError==-1 && !securityFailure.Message.Contains("PRIVATE"),"non-Win32 security HRESULT does not invent a Win32 error or expose its message");
Check(new ReadableLog().LocalLogFailure("BACKGROUND","MAXIMIZED",securityFailure.Stage,securityFailure.NativeError).Contains("Win32）：未確認"),"unknown native file cause remains explicitly unconfirmed");
long recoveryClock=0;
var recoveryLog=new ReadableLog(()=>recoveryClock);
var firstGrowth=new NodeBudgetChange(3000,6000,ScanOrigin.UIA);
var secondGrowth=new NodeBudgetChange(6000,10000,ScanOrigin.UIA);
var atMaximum=new NodeBudgetChange(10000,10000,ScanOrigin.UIA);
var growthOne=recoveryLog.NodeRecovery(firstGrowth,ScanStage.Scan,"BACKGROUND","MAXIMIZED");
Check(growthOne.Contains("0310-BX-R13") && growthOne.Contains("掃描來源：UIA；掃描階段：初掃")
    && growthOne.Contains("舊節點上限：3000；新節點上限：6000") && growthOne.Contains("從根元素完整重掃")
    && growthOne.Contains("本輪是否呼叫 Invoke：否") && growthOne.Contains("核准前重驗"),
    "first bounded recovery retains NODE_LIMIT code and explicitly requires fresh scan/revalidation without invoking");
recoveryClock=1;
var growthTwo=recoveryLog.NodeRecovery(secondGrowth,ScanStage.Scan,"BACKGROUND","MAXIMIZED");
Check(growthTwo!=null && growthTwo.Contains("舊節點上限：6000；新節點上限：10000"),
    "both growth steps remain visible inside the ten-second repeated-state interval");
Check(recoveryLog.NodeRecovery(firstGrowth,ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(secondGrowth,ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null,
    "repeated identical growth notices do not duplicate a completed budget change");
var hardLimit=recoveryLog.NodeRecovery(atMaximum,ScanStage.Scan,"BACKGROUND","MAXIMIZED");
Check(hardLimit.Contains("0310-BX-R13") && hardLimit.Contains("已達最高上限，本輪拒絕核准")
    && hardLimit.Contains("舊節點上限：10000；新節點上限：10000") && hardLimit.Contains("掃描是否完整：否"),
    "hard cap remains an incomplete NODE_LIMIT rejection, never an approval");
recoveryClock=10000;
Check(recoveryLog.NodeRecovery(atMaximum,ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null,
    "hard cap notice coalesces until ten seconds from its own last write");
recoveryLog.Event("WAIT_PREVIOUS",1,"BACKGROUND","MAXIMIZED");
recoveryLog.Scan(empty,false);
recoveryLog.Status("STATUS: ON");
Check(recoveryLog.NodeRecovery(atMaximum,ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null,
    "intervening scan, event and ON do not bypass the hard-cap notice throttle");
recoveryClock=10001;
Check(recoveryLog.NodeRecovery(atMaximum,ScanStage.Scan,"BACKGROUND","MAXIMIZED")!=null,
    "continued hard-cap rejection remains visible at a bounded ten-second cadence");
var revalidationRecovery=recoveryLog.NodeRecovery(new NodeBudgetChange(3000,6000,ScanOrigin.MSAA),ScanStage.Revalidation,"BACKGROUND","NORMAL");
Check(revalidationRecovery.Contains("0310-BN-R13") && revalidationRecovery.Contains("掃描來源：MSAA；掃描階段：重驗"),
    "MSAA and revalidation have fixed, distinct recovery descriptions");
string recoverySummary=recoveryLog.Summary;
Check(recoverySummary.Contains("0310-BN-R13") && recoverySummary.Contains("尚未記錄核准嘗試"),
    "recovery updates the issue summary without claiming an approval attempt");
Check(recoveryLog.NodeRecovery(null,ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(new NodeBudgetChange(3000,6000,(ScanOrigin)99),ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(firstGrowth,(ScanStage)99,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(new NodeBudgetChange(-1,6000,ScanOrigin.UIA),ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(new NodeBudgetChange(3000,10001,ScanOrigin.UIA),ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(new NodeBudgetChange(3000,10000,ScanOrigin.UIA),ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(new NodeBudgetChange(6000,3000,ScanOrigin.UIA),ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(new NodeBudgetChange(6000,6000,ScanOrigin.UIA),ScanStage.Scan,"BACKGROUND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(firstGrowth,ScanStage.Scan,"PRIVATE_UI_COMMAND","MAXIMIZED")==null
    && recoveryLog.NodeRecovery(firstGrowth,ScanStage.Scan,"BACKGROUND",@"C:\PRIVATE_PROVIDER_MESSAGE")==null
    && recoveryLog.NodeRecovery(firstGrowth,ScanStage.Scan,"MINIMIZED","MAXIMIZED")==null,
    "recovery accepts only fixed origin/stage/view tokens and the bounded monotonic ladder");
Check(recoveryLog.Summary==recoverySummary && !recoveryLog.Summary.Contains("PRIVATE")
    && !recoveryLog.Summary.Contains(@"C:\"),"invalid recovery input cannot expose UI/provider content or alter exported summaries");

static string SummarySection(string summary,string heading,string nextHeading)
{
    int start=summary.IndexOf(heading,StringComparison.Ordinal);
    int end=summary.IndexOf(nextHeading,start+heading.Length,StringComparison.Ordinal);
    Check(start>=0 && end>start,"summary section boundaries exist");
    return summary.Substring(start,end-start);
}
DateTimeOffset wallTime=new DateTimeOffset(2026,10,19,23,59,58,TimeSpan.FromHours(8)).AddMilliseconds(125);
long monotonicTime=0;
int wallReads=0;
var timedLog=new ReadableLog(()=>monotonicTime,()=>{wallReads++;return wallTime;});
Check(timedLog.Summary.Contains("最近紀錄時間（本機）：尚無紀錄") && wallReads==0,
    "an empty summary does not invent an event time or query the clock");
var timedIssue=timedLog.Scan(new ScanMetrics { Mode="BACKGROUND",WindowState="NORMAL",Result="ERROR",Reason="ROOT_READ",Nodes=3 },false);
const string issueTime="2026-10-19 23:59:58.125 +08:00";
Check(timedIssue.Contains("本機時間："+issueTime) && timedLog.Summary.Contains("紀錄：L000001\r\n本機時間："+issueTime) && wallReads==1,
    "main and issue summary share one captured local timestamp, offset and record number");
wallTime=wallTime.AddSeconds(1);
var timedAction=timedLog.Event("RETURNED",1,"BACKGROUND","NORMAL");
const string actionTime="2026-10-19 23:59:59.125 +08:00";
Check(timedAction.Contains("本機時間："+actionTime) && wallReads==2,"action gets its own event time");
wallTime=wallTime.AddSeconds(2);
var timedEmpty=new ScanMetrics { Mode="BACKGROUND",WindowState="NORMAL",Result="COMPLETE",Reason="NONE",Nodes=10,Buttons=2,Labels=0 };
var timedScan=timedLog.Scan(timedEmpty,false);
const string scanTime="2026-10-20 00:00:01.125 +08:00";
string timedSummary=timedLog.Summary;
Check(timedScan.Contains("本機時間："+scanTime) && timedSummary.Contains("最近紀錄時間（本機）："+scanTime) && wallReads==3,
    "a new record updates the summary timestamp across midnight with the full date");
string issueSection=SummarySection(timedSummary,"【最近一次受阻／錯誤", "【最近一次核准嘗試的結果】");
string actionSection=SummarySection(timedSummary,"【最近一次核准嘗試的結果】", "各視窗狀態的最後掃描");
string scanSection=SummarySection(timedSummary,"各視窗狀態的最後掃描", "---- 說明 ----");
Check(issueSection.Contains("紀錄：L000001") && issueSection.Contains(issueTime) && !issueSection.Contains(scanTime),
    "a later successful scan does not retimestamp the historical issue");
Check(actionSection.Contains("紀錄：L000002") && actionSection.Contains(actionTime) && !actionSection.Contains(scanTime),
    "the latest action retains its own time after later scans");
Check(scanSection.Contains("紀錄：L000003") && scanSection.Contains(scanTime) && !scanSection.Contains(issueTime),
    "the saved scan summary identifies the actual scan record and time");
wallTime=wallTime.AddHours(4);
Check(timedLog.Summary==timedSummary && timedLog.Summary==timedSummary && wallReads==3,
    "opening or rereading a summary cannot make old records appear fresh");
monotonicTime=9999;
Check(timedLog.Scan(timedEmpty,false)==null && timedLog.Summary==timedSummary && wallReads==3,
    "coalesced scans do not refresh timestamps or read the wall clock");
wallTime=new DateTimeOffset(2026,10,19,8,0,0,TimeSpan.FromHours(8));
monotonicTime=10000;
var rollbackScan=timedLog.Scan(timedEmpty,false);
Check(rollbackScan!=null && rollbackScan.Contains("2026-10-19 08:00:00.000 +08:00") && wallReads==4,
    "wall-clock rollback does not change the existing monotonic ten-second retention interval");
Check(SummarySection(timedLog.Summary,"【最近一次受阻／錯誤", "【最近一次核准嘗試的結果】").Contains(issueTime),
    "clock adjustment leaves a frozen historical issue unchanged");
var savedCulture=System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("th-TH");
    var cultureTime=new DateTimeOffset(2026,10,20,7,8,9,TimeSpan.FromMinutes(-210)).AddMilliseconds(10);
    var cultureLog=new ReadableLog(localNow:()=>cultureTime);
    Check(cultureLog.Status("STATUS: ON").Contains("2026-10-20 07:08:09.010 -03:30")
        && cultureLog.Summary.Contains("2026-10-20 07:08:09.010 -03:30"),
        "timestamps use a fixed Gregorian date format and preserve the actual local UTC offset");
}
finally { System.Globalization.CultureInfo.CurrentCulture=savedCulture; }
Console.WriteLine("PASS: summary record timestamps, frozen historical issues/actions, scan correlation, midnight, read stability, monotonic throttling and culture/offset formatting.");

var failureTime=new DateTimeOffset(2026,10,19,13,53,31,TimeSpan.FromHours(8)).AddMilliseconds(456);
var retryRecords=new List<LogWriteDiagnostic>();
var memoryMain=new MemoryStream();
int opens=0;
LocalLogWriter.AppendMain("unused","one record",retryRecords.Add,()=>failureTime,path=>
{
    opens++;
    if(opens<=2) throw new IOException("PRIVATE_OPEN_MESSAGE",unchecked((int)(opens==1 ? 0x80070020u : 0x80070021u)));
    return memoryMain;
});
Check(opens==3 && System.Text.Encoding.UTF8.GetString(memoryMain.ToArray())=="one record"
    && retryRecords.Count==2 && retryRecords[0].Kind=="RETRY" && retryRecords[0].NativeError==32
    && retryRecords[1].Kind=="RECOVERED" && retryRecords[1].NativeError==33 && retryRecords[1].Attempts==3
    && retryRecords.All(record=>record.Time==failureTime),
    "only unopened sharing/lock conflicts are retried with fixed times, bounded metadata and one committed record");
int partialOpens=0;
var partialStream=new PartialWriteStream();
try
{
    LocalLogWriter.AppendMain("unused","never replay",localNow:()=>failureTime,open:path=>{partialOpens++;return partialStream;});
    throw new Exception("Expected partial-write failure");
}
catch(LogWriteFailure partialFailure)
{
    Check(partialFailure.Stage=="MAIN_APPEND" && partialFailure.NativeError==32 && partialFailure.Time==failureTime && partialFailure.Attempts==1
        && partialOpens==1 && partialStream.Writes==1 && partialStream.ToArray().Length>0,
        "even Win32 32 after writing starts is never reopened/replayed and retains the actual failure timestamp");
}
Check(new ReadableLog().LocalLogFailure("BACKGROUND","NORMAL","MAIN_OPEN",32).Contains("0310-BN-L01"),
    "main-open failure uses L01 rather than a summary replacement code");
int fatalOpens=0;
try
{
    LocalLogWriter.AppendMain("unused","no record",localNow:()=>failureTime,open:path=>
    {fatalOpens++;throw new IOException("PRIVATE_DISK_FULL_MESSAGE",unchecked((int)0x80070070u));});
    throw new Exception("Expected disk-full open failure");
}
catch(LogWriteFailure fatalOpen)
{ Check(fatalOpen.NativeError==112 && fatalOpen.Attempts==1 && fatalOpens==1,"non-sharing open errors are not retried"); }
var flushStream=new FailingFlushStream();
int flushOpens=0;
try
{
    LocalLogWriter.AppendMain("unused","already written",localNow:()=>failureTime,open:path=>{flushOpens++;return flushStream;});
    throw new Exception("Expected flush failure");
}
catch(LogWriteFailure flushFailure)
{ Check(flushFailure.Stage=="MAIN_APPEND" && flushFailure.NativeError==112 && flushOpens==1
    && System.Text.Encoding.UTF8.GetString(flushStream.ToArray())=="already written","flush failure cannot reopen or replay persisted bytes"); }
Console.WriteLine("PASS: deterministic main-open retry and partial-write no-replay tests.");

if (OperatingSystem.IsWindows())
{
    string writerRoot=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"codex-log-tests-"+Guid.NewGuid().ToString("N")));
    if(!writerRoot.StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Windows test files must be under C:\\Temp.");
    Directory.CreateDirectory(writerRoot);
    try
    {
        string main=Path.Combine(writerRoot,"auto-test.txt"), summary=Path.Combine(writerRoot,"auto-test-summary.txt");
        LocalLogWriter.Append(main,"first\n","old summary");
        var reader=new FileStream(summary,FileMode.Open,FileAccess.Read,FileShare.Read);
        var release=Task.Run(()=> { Thread.Sleep(150); reader.Dispose(); });
        LocalLogWriter.Append(main,"second\n","new summary");
        release.GetAwaiter().GetResult();
        Check(File.ReadAllText(main)=="first\nsecond\n" && File.ReadAllText(summary)=="new summary","temporary Windows sharing conflict only retries summary, without duplicating main evidence");
        using(var locked=new FileStream(summary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            try { LocalLogWriter.Append(main,"third\n","blocked summary"); throw new Exception("Expected bounded sharing failure"); }
            catch(LogWriteFailure e) { Check(e.Stage=="SUMMARY_REPLACE" && (e.NativeError==32 || e.NativeError==33),"persistent sharing denial is bounded and classified"); }
        }
        Check(File.ReadAllText(main)=="first\nsecond\nthird\n" && File.ReadAllText(summary)=="new summary","failed summary retains old atomic contents and one main append");
        bool warned=false;
        using(var locked=new FileStream(summary,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            LocalLogWriter.AppendWithSummaryFallback(main,"fourth\n","unavailable summary",
                e=>"summary warning "+e.NativeError+"\n",e=>warned=true);
        }
        Check(warned && File.ReadAllText(main).Contains("fourth\nsummary warning ")
            && File.ReadAllText(main).Split("fourth\n").Length==2 && File.ReadAllText(summary)=="new summary",
            "persistent summary denial keeps the complete record once and allows the caller to continue");
        using(var locked=new FileStream(main,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            try { LocalLogWriter.Append(main,"must not appear\n","never"); throw new Exception("Expected main append denial"); }
            catch(LogWriteFailure e) { Check(e.Stage=="MAIN_OPEN" && e.Attempts==5,"persistent main open denial is bounded before any write"); }
        }
        Check(!File.ReadAllText(main).Contains("must not appear"),"main append is never retried after failure");
        var mainEvents=new List<LogWriteDiagnostic>();
        var mainReader=new FileStream(main,FileMode.Open,FileAccess.Read,FileShare.Read);
        var releaseMain=Task.Run(()=>{Thread.Sleep(150);mainReader.Dispose();});
        try { LocalLogWriter.Append(main,"after brief main conflict\n","main recovered",mainEvents.Add); }
        finally { releaseMain.GetAwaiter().GetResult(); mainReader.Dispose(); }
        Check(mainEvents.Count==2 && mainEvents[0].Kind=="RETRY" && mainEvents[0].NativeError==32
            && mainEvents[1].Kind=="RECOVERED" && mainEvents[1].Attempts>=2 && mainEvents[1].Attempts<=5
            && File.ReadAllText(main).Split("after brief main conflict\n").Length==2 && File.ReadAllText(summary)=="main recovered",
            "real Windows main-open conflict recovers before writing, records first failure/recovery and keeps exactly one append");
        Console.WriteLine("PASS: Windows real-file sharing tests (temporary recovery, bounded persistent failure, atomic old summary, no duplicated main append).");

        string deniedMain=Path.Combine(writerRoot,"readonly-main.txt");
        LocalLogWriter.Append(deniedMain,"initial\n","initial summary");
        bool mainWarning=false;
        WhileReadOnly(deniedMain,()=>
        {
            try
            {
                LocalLogWriter.AppendWithSummaryFallback(deniedMain,"must not appear\n","never",
                    e=>"must not warn\n",e=>mainWarning=true);
                throw new Exception("Expected read-only main denial");
            }
            catch(LogWriteFailure e)
            {
                Check(e.Stage=="MAIN_OPEN" && e.NativeError==5 && e.Attempts==1 && e.InnerException is UnauthorizedAccessException,
                    "read-only main is classified as a non-retried MAIN_OPEN access denial");
            }
        });
        Check(!mainWarning && File.ReadAllText(deniedMain)=="initial\n","read-only main failure cannot become a summary fallback or duplicate main contents");
        LocalLogWriter.Append(deniedMain,"after restore\n","restored summary");
        Check(File.ReadAllText(deniedMain)=="initial\nafter restore\n","main can be written after fixture attributes are restored");

        string tempMain=Path.Combine(writerRoot,"readonly-temp.txt");
        string tempSummary=Path.Combine(writerRoot,"readonly-temp-summary.txt"), summaryTemp=tempSummary+".tmp";
        LocalLogWriter.Append(tempMain,"initial\n","old temp summary");
        File.WriteAllText(summaryTemp,"retained temp");
        int tempWarnings=0;
        WhileReadOnly(summaryTemp,()=>
        {
            LocalLogWriter.AppendWithSummaryFallback(tempMain,"second\n","new temp summary",
                e=>
                {
                    Check(e.Stage=="SUMMARY_WRITE" && e.NativeError==5 && e.InnerException is UnauthorizedAccessException,
                        "read-only summary temp is classified as SUMMARY_WRITE access denial");
                    return "temp warning\n";
                },e=>tempWarnings++);
        });
        Check(tempWarnings==1 && File.ReadAllText(tempMain)=="initial\nsecond\ntemp warning\n"
            && File.ReadAllText(tempSummary)=="old temp summary" && File.ReadAllText(summaryTemp)=="retained temp",
            "read-only summary temp keeps one complete main record plus warning and preserves previous files");
        LocalLogWriter.Append(tempMain,"after restore\n","recovered temp summary");
        Check(File.ReadAllText(tempSummary)=="recovered temp summary" && File.ReadAllText(tempMain)=="initial\nsecond\ntemp warning\nafter restore\n",
            "summary temp recovery updates only the next record without replaying the failed summary's main append");

        string doubleMain=Path.Combine(writerRoot,"double-failure.txt"), doubleTemp=Path.Combine(writerRoot,"double-failure-summary.txt.tmp");
        LocalLogWriter.Append(doubleMain,"initial\n","old"); File.WriteAllText(doubleTemp,"blocked");
        var doubleEvents=new List<LogWriteDiagnostic>();
        WhileReadOnly(doubleTemp,()=>
        {
            var original=File.GetAttributes(doubleMain);
            try
            {
                LocalLogWriter.AppendWithSummaryFallback(doubleMain,"one complete record\n","new",
                    failedSummary=>{File.SetAttributes(doubleMain,original|FileAttributes.ReadOnly);return "warning\n";},
                    failedSummary=>{throw new Exception("A failed main-warning append cannot report continuation");},doubleEvents.Add);
                throw new Exception("Expected warning append failure");
            }
            catch(LogWriteFailure warningFailure) { doubleEvents.Add(warningFailure.Diagnostic("FAILURE")); }
            finally { File.SetAttributes(doubleMain,original); }
        });
        Check(doubleEvents.Count==2 && doubleEvents[0].Kind=="WARNING" && doubleEvents[0].Stage=="SUMMARY_WRITE"
            && doubleEvents[0].NativeError==5 && doubleEvents[1].Kind=="FAILURE" && doubleEvents[1].Stage=="MAIN_OPEN"
            && doubleEvents[1].NativeError==5 && File.ReadAllText(doubleMain)=="initial\none complete record\n",
            "summary cause is retained independently before its main-log warning also fails; the complete record stays unique");

        string destinationMain=Path.Combine(writerRoot,"readonly-destination.txt");
        string destinationSummary=Path.Combine(writerRoot,"readonly-destination-summary.txt");
        LocalLogWriter.Append(destinationMain,"initial\n","old destination summary");
        int destinationWarnings=0, destinationError=-1;
        WhileReadOnly(destinationSummary,()=>
        {
            LocalLogWriter.AppendWithSummaryFallback(destinationMain,"second\n","new destination summary",
                e=>
                {
                    Check(e.Stage=="SUMMARY_REPLACE" && (e.InnerException is IOException || e.InnerException is UnauthorizedAccessException),
                        "read-only destination denies atomic replacement with a classified filesystem exception");
                    destinationError=e.NativeError;
                    return "destination warning\n";
                },e=>destinationWarnings++);
        });
        Check(destinationWarnings==1 && File.ReadAllText(destinationMain)=="initial\nsecond\ndestination warning\n"
            && File.ReadAllText(destinationSummary)=="old destination summary",
            "read-only summary destination preserves the old atomic summary and one main append with warning");
        LocalLogWriter.Append(destinationMain,"after restore\n","recovered destination summary");
        Check(File.ReadAllText(destinationSummary)=="recovered destination summary"
            && File.ReadAllText(destinationMain)=="initial\nsecond\ndestination warning\nafter restore\n",
            "summary destination recovers after restoring attributes without replaying the previous record");
        Console.WriteLine("PASS: Windows real-file read-only tests (MAIN_OPEN:5, SUMMARY_WRITE:5, SUMMARY_REPLACE:"+destinationError+", attribute restoration, safe summary fallback, no replay).");
    }
    finally
    {
        if(!Path.GetFullPath(writerRoot).StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe test cleanup path");
        Directory.Delete(writerRoot,true);
    }
}
if(args.Length==1)
{
    var sampleTime=new DateTimeOffset(2026,10,19,9,30,0,TimeSpan.FromHours(8));
    var sample=new ReadableLog(localNow:()=>sampleTime);
    m.Nodes=20; sample.Scan(m,false);
    sampleTime=sampleTime.AddMinutes(2); sample.Event("RECHECK",first,"BACKGROUND","MAXIMIZED");
    sampleTime=sampleTime.AddMinutes(1); sample.Event("ATTEMPT",first,"BACKGROUND","MAXIMIZED");
    sampleTime=sampleTime.AddSeconds(1); sample.Event("RETURNED",first,"BACKGROUND","MAXIMIZED");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
    File.WriteAllText(args[0],"示例資料，非公司測試結果。\r\n\r\n"+sample.Summary,new System.Text.UTF8Encoding(true));
}
Console.WriteLine("PASS: readable log grouping, frozen issue summaries, status coalescing, no false success/request claims, code classification and privacy.");

sealed class PartialWriteStream : MemoryStream
{
    public int Writes;
    public override void Write(byte[] buffer,int offset,int count)
    {
        Writes++;
        base.Write(buffer,offset,Math.Max(1,count/2));
        throw new IOException("PRIVATE_PARTIAL_WRITE_MESSAGE",unchecked((int)0x80070020u));
    }
}
sealed class FailingFlushStream : MemoryStream
{
    public override void Flush() { throw new IOException("PRIVATE_FLUSH_MESSAGE",unchecked((int)0x80070070u)); }
}
