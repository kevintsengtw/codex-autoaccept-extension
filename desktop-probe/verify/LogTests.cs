static void Check(bool value,string name) { if (!value) throw new Exception(name); }
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
Check(blocked.Contains("最大化／背景") && blocked.Contains("P000001") && blocked.Contains("037-BX-S05") && blocked.Contains("本筆是否呼叫核准：否"),"formatted error includes condition/candidate/reason/report code, not implicit approval");
Check(blocked.Contains("\r\n結果：") && !blocked.Contains(@"\r\n"),"plain-text report has real Windows line breaks");
Check(log.Scan(m,false)==null,"identical scans are coalesced");
m.Nodes=1;
Check(log.Scan(m,false)!=null,"collapsed tree is not hidden by coalescing");
var attempt=log.Event("ATTEMPT",first,"BACKGROUND","MAXIMIZED");
var returned=log.Event("RETURNED",first,"BACKGROUND","MAXIMIZED");
Check(attempt.Contains("P000001") && returned.Contains("P000001") && returned.Contains("尚未證明"),"same candidate links invocation stages and returned is not success");
Check(log.Event("RETURNED",first,"BACKGROUND","MAXIMIZED")==null,"duplicate lifecycle event coalesces");
Check(log.Summary.Contains("037-BX-S05") && log.Summary.Contains("037-BX-A02"),"issue remains available after later execution stage");
Check(log.Summary.IndexOf("037-BX-S05")<log.Summary.IndexOf("---- 說明 ----"),"short report comes before detailed instructions");
var unknown=log.Scan(new ScanMetrics { Mode="MINIMIZED",WindowState="MINIMIZED",Result="COMPLETE",Reason="NONE",Nodes=10,Buttons=1,RootOffscreenRead=false },false);
Check(unknown.Contains("未建立可追蹤候選") && unknown.Contains("不能判定") && unknown.Contains("037-M-S00"),"no button does not invent a pending request or associate it with previous candidate");
var error=log.Scan(new ScanMetrics { Mode="BACKGROUND",WindowState="MAXIMIZED",Result="ERROR",Reason="WINDOW_PATTERN",Nodes=10 },false);
Check(error.Contains("037-BX-R10") && error.Contains("掃描是否完整：否"),"incomplete scans are identified precisely");
var missing=log.Event("RECHECK_MISSING",first,"BACKGROUND","MAXIMIZED");
Check(missing.Contains("037-BX-E17") && missing.Contains("037-BX-R10"),"failed revalidation retains the precise last scan reason");
Check(log.Event("TIMEOUT_SCAN",0,"MINIMIZED","MINIMIZED").Contains("037-M-T01"),"scan timeout has its own report code");
Check(log.Event("TIMEOUT_ACTION",first,"BACKGROUND","MAXIMIZED").Contains("037-BX-T03"),"invocation timeout does not claim nothing was invoked");
Check(log.Status("OBSERVATION: BUTTON_UNAVAILABLE_OR_HIDDEN").Contains("仍需在 App 確認") && log.Status("OBSERVATION: ORIGINAL_ELEMENT_REMOVED",true)==null,"manual observation is preserved and automatic rich stages are not duplicated");
var readOnly=log.Scan(m,true); Check(readOnly.Contains("唯讀模式不會核准"),"CLI read-only logs are explicit");
string malicious=log.Event("PRIVATE_COMMAND C:\\SECRET_PATH",1,"PRIVATE_NAME","SECRET_TITLE")+log.Status("ERROR_TYPE: PRIVATE_COMMAND C:\\SECRET_PATH")+log.Summary;
Check(!malicious.Contains("PRIVATE") && !malicious.Contains("SECRET") && !malicious.Contains("C:\\"),"unknown strings and private in-memory keys never enter log output");
Check(log.Event("VALIDATION_ERROR",1,"BACKGROUND","MAXIMIZED","CONTEXT_READ").Contains("父層與局部內容"),"validation error logs a specific stage");
if(args.Length==1)
{
    var sample=new ReadableLog();
    m.Nodes=20; sample.Scan(m,false); sample.Event("RECHECK",first,"BACKGROUND","MAXIMIZED");
    Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[0]))!);
    File.WriteAllText(args[0],"示例資料，非公司測試結果。\r\n\r\n"+sample.Summary,new System.Text.UTF8Encoding(true));
}
Console.WriteLine("PASS: readable log grouping, frozen issue summaries, status coalescing, no false success/request claims, code classification and privacy.");
