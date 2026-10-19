static void Check(bool b, string s) { if (!b) throw new Exception(s); }
var d = new WorkerDiagnostics();
Check(!d.Compatible, "no unconfirmed backend accepted");
d.Read("BACKEND_VERSION: 0.3.0"); Check(!d.Compatible && d.Describe(4).Contains("版本"), "old backend detected");
d.Read("BACKEND_VERSION: 0.3.10"); Check(d.Compatible, "matching backend");
d.Read("FAILURE_CODE: IO"); d.Read("Unhandled System.IO.IOException: PRIVATE_ACCOUNT C:\\SECRET_PATH");
Check(d.Describe(-1).Contains("IOException") && d.Describe(-1).Contains("退出碼 -1"), "exit and type available");
Check(!d.Describe(-1).Contains("PRIVATE") && !d.Describe(-1).Contains("SECRET"), "no raw exception leakage");
d.Read("ATTEMPT: AUTO_INVOKE_ONCE"); Check(d.Describe(3).Contains("可能已送出"), "attempt warning scoped to actual attempt");
var fresh = new WorkerDiagnostics(); fresh.Read("BACKEND_VERSION: 0.3.10"); fresh.Read("FAILURE_CODE: TARGET_OR_PERMISSION");
Check(fresh.Describe(4).Contains("權限") && fresh.Describe(4).Contains("未記錄"), "startup vs attempted failure");
Console.WriteLine("PASS: 7 diagnostic checks (version matching, early exit, safe metadata, attempted status).");
var writerFailure=new WorkerDiagnostics(); writerFailure.Read("BACKEND_VERSION: 0.3.10"); writerFailure.Read("LOG_WRITE_FAILURE: SUMMARY_REPLACE:32");
Check(writerFailure.Describe(0).Contains("摘要可能過期"),"log failure is explicit even after clean backend exit");
var unknownWriterFailure=new WorkerDiagnostics(); unknownWriterFailure.Read("BACKEND_VERSION: 0.3.10");
unknownWriterFailure.Read("LOG_WRITE_FAILURE: MAIN_APPEND:-1");
Check(unknownWriterFailure.Describe(0).Contains("本機 log 寫入失敗") && unknownWriterFailure.Describe(0).Contains("LocalLogWriteFailure"),
    "unknown native file error is classified without a stale UIA error type");

fresh.Read("INTEGRITY: probe=medium; target=unavailable");
fresh.Read("INTEGRITY_QUERY: probe=none:0; target=OpenProcessToken:5");
Check(fresh.Describe(4).Contains("medium") && fresh.Describe(4).Contains("OpenProcessToken:5"), "precise safe integrity diagnostics");
fresh.Read("INTEGRITY: probe=PRIVATE_USER; target=C:\\SECRET_PATH");
Check(!fresh.IntegritySummary.Contains("PRIVATE") && !fresh.IntegritySummary.Contains("SECRET"), "invalid diagnostic values excluded");
fresh.Read("FAILURE_CODE: TARGET_ELEVATED"); Check(fresh.Describe(4).Contains("已提升"), "elevation distinguished from unavailable metadata");
Console.WriteLine("PASS: integrity-query classification and privacy regression checks.");
fresh.Read("LOG_NAME: auto-20261012-120000-0123456789abcdef0123456789abcdef.txt");
Check(fresh.LogName=="auto-20261012-120000-0123456789abcdef0123456789abcdef.txt","safe relative log name accepted");
fresh.Read("LOG_NAME: ..\\SECRET_PATH.txt"); fresh.Read("LOG_NAME: C:\\PRIVATE_USER\\report.txt");
Check(!fresh.LogName.Contains("SECRET") && !fresh.LogName.Contains("PRIVATE") && !fresh.LogName.Contains("\\"),"unsafe file link rejected and no path retained");

const string safeLog="auto-20261019-084139-0123456789abcdef0123456789abcdef.txt";
var faultTime=new DateTimeOffset(2026,10,19,13,53,31,TimeSpan.FromHours(8)).AddMilliseconds(456);
var fault=LogWriteDiagnostic.Create("FAILURE","MAIN_OPEN",32,faultTime,5);
var parsed=LogWriteDiagnostic.Parse(fault.Protocol);
Check(parsed!=null && parsed.Time==faultTime && parsed.Time.Offset==TimeSpan.FromHours(8) && parsed.Attempts==5,
    "protocol preserves backend failure time, millisecond, offset, phase, native error and bounded attempts");
foreach(string invalid in new[] {
    fault.Protocol+"\n",fault.Protocol+" PRIVATE_COMMAND",fault.Protocol.Replace("MAIN_OPEN","C:\\SECRET_PATH"),
    fault.Protocol.Replace("|32|","|65536|"),fault.Protocol.Replace("|32|","|-2|"),
    fault.Protocol.Replace("|5","|6"),fault.Protocol.Replace("2026-10-19","2026-02-30"),
    fault.Protocol.Replace("FAILURE","PRIVATE_USER"),fault.Protocol.Replace("+08:00","+99:00") })
    Check(LogWriteDiagnostic.Parse(invalid)==null,"malformed diagnostic discarded: "+invalid.Length);
Check(!fault.LocalText(@"C:\PRIVATE_PATH\a.txt").Contains("PRIVATE")
    && !fault.LocalText(safeLog+"\n").Contains("log="+safeLog),"diagnostic file accepts only one safe relative main-log basename");
var beforeCulture=System.Globalization.CultureInfo.CurrentCulture;
try
{
    System.Globalization.CultureInfo.CurrentCulture=System.Globalization.CultureInfo.GetCultureInfo("th-TH");
    Check(fault.Protocol.Contains("2026-10-19T13:53:31.456+08:00") && fault.Description.Contains("2026-10-19 13:53:31.456 +08:00"),
        "failure timestamp uses invariant Gregorian dates in protocol, disk record and GUI");
}
finally { System.Globalization.CultureInfo.CurrentCulture=beforeCulture; }
var saveFailed=new WorkerDiagnostics((record,name)=>false);
saveFailed.Read("BACKEND_VERSION: 0.3.10"); saveFailed.Read(fault.Protocol);
Check(saveFailed.Describe(0).Contains("13:53:31.456 +08:00") && saveFailed.Describe(0).Contains("MAIN_OPEN")
    && saveFailed.Describe(0).Contains("Windows 錯誤碼：32") && saveFailed.Describe(0).Contains("未另存成功"),
    "GUI retains exact backend metadata even if the independent diagnostic destination also fails");
saveFailed.Read("LOG_WRITE_FAILURE: MAIN_OPEN:32");
Check(saveFailed.Describe(0).Contains("13:53:31.456 +08:00"),"legacy exit marker cannot overwrite the precise failure time");
Check(!saveFailed.DescribeLogFailure().Contains("退出碼") && saveFailed.Describe(-1).Contains("退出碼 -1"),
    "live failure message does not invent an exit code before the backend has exited");
var oldProtocol=new WorkerDiagnostics(); oldProtocol.Read("LOG_WRITE_FAILURE: MAIN_APPEND:5");
Check(oldProtocol.LogFailureDetails.Contains("失敗時間（本機）：未確認") && oldProtocol.LogFailureDetails.Contains("Windows 錯誤碼：5"),
    "old protocol preserves its native code without inventing a historical timestamp");
var unknownCode=LogWriteDiagnostic.Create("FAILURE","MAIN_APPEND",-1,faultTime);
Check(unknownCode.Description.Contains("Windows 錯誤碼：未確認"),"non-Win32 errors do not invent a Windows error code");
Console.WriteLine("PASS: strict log diagnostic metadata, failure times, offsets/culture, legacy protocol and save-failure GUI fallback.");

if(OperatingSystem.IsWindows())
{
    string root=Path.GetFullPath(Path.Combine(Path.GetTempPath(),"codex-diagnostic-tests-"+Guid.NewGuid().ToString("N")));
    if(!root.StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe diagnostic test root");
    Directory.CreateDirectory(root);
    try
    {
        string main=Path.Combine(root,"blocked-main.txt"); File.WriteAllText(main,"original evidence");
        var disk=new LocalLogDiagnostics(Path.Combine(root,"independent-diagnostics"));
        var live=new WorkerDiagnostics(disk.Append); live.Read("LOG_NAME: "+safeLog);
        using(var locked=new FileStream(main,FileMode.Open,FileAccess.Read,FileShare.Read))
        {
            live.Read(LogWriteDiagnostic.Create("RETRY","MAIN_OPEN",32,faultTime,1).Protocol);
            live.Read(fault.Protocol);
            Check(File.ReadAllText(disk.FilePath).Contains("local_time=2026-10-19T13:53:31.456+08:00 kind=FAILURE stage=MAIN_OPEN win32=32 attempts=5 log="+safeLog),
                "frontend independently persists exact failure metadata while the main log remains write-locked");
        }
        Check(File.ReadAllText(main)=="original evidence" && live.Describe(0).Contains("本機診斷已另存"),
            "diagnostic persistence cannot change main-log evidence and reports actual save success");
        File.SetAttributes(disk.FilePath,FileAttributes.ReadOnly);
        try
        {
            live.Read(LogWriteDiagnostic.Create("FAILURE","MAIN_APPEND",112,faultTime.AddSeconds(1)).Protocol);
            Check(live.Describe(0).Contains("Windows 錯誤碼：112") && live.Describe(0).Contains("未另存成功")
                && live.Describe(0).Contains("13:53:32.456"),"a second disk failure preserves the newer real diagnostic in GUI memory");
        }
        finally { File.SetAttributes(disk.FilePath,FileAttributes.Normal); }
        string oldEvidence=File.ReadAllText(disk.FilePath);
        var reconnected=new LocalLogDiagnostics(Path.Combine(root,"independent-diagnostics"));
        Check(reconnected.FilePath!=disk.FilePath && reconnected.Append(unknownCode,safeLog)
            && File.ReadAllText(disk.FilePath)==oldEvidence,"reconnecting creates separate diagnostics and preserves the old incident");
        Console.WriteLine("PASS: Windows independent diagnostic persistence with locked main log, denied diagnostic file and separate reconnect sessions.");
    }
    finally
    {
        if(!Path.GetFullPath(root).StartsWith(@"C:\Temp\",StringComparison.OrdinalIgnoreCase)) throw new Exception("Unsafe diagnostic cleanup");
        Directory.Delete(root,true);
    }
}
