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
            catch(LogWriteFailure e) { Check(e.Stage=="MAIN_APPEND","main append denial is not a summary failure"); }
        }
        Check(!File.ReadAllText(main).Contains("must not appear"),"main append is never retried after failure");
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
                Check(e.Stage=="MAIN_APPEND" && e.NativeError==5 && e.InnerException is UnauthorizedAccessException,
                    "read-only main is classified as a non-retried MAIN_APPEND access denial");
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
        Console.WriteLine("PASS: Windows real-file read-only tests (MAIN_APPEND:5, SUMMARY_WRITE:5, SUMMARY_REPLACE:"+destinationError+", attribute restoration, safe summary fallback, no replay).");
    }
    finally
    {
        if(!Path.GetFullPath(writerRoot).StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe test cleanup path");
        Directory.Delete(writerRoot,true);
    }
}
if(args.Length==1)
{
    var sample=new ReadableLog();
    m.Nodes=20; sample.Scan(m,false); sample.Event("RECHECK",first,"BACKGROUND","MAXIMIZED");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
    File.WriteAllText(args[0],"示例資料，非公司測試結果。\r\n\r\n"+sample.Summary,new System.Text.UTF8Encoding(true));
}
Console.WriteLine("PASS: readable log grouping, frozen issue summaries, status coalescing, no false success/request claims, code classification and privacy.");
