static void Check(bool b, string s) { if (!b) throw new Exception(s); }
var d = new WorkerDiagnostics();
Check(!d.Compatible, "no unconfirmed backend accepted");
d.Read("BACKEND_VERSION: 0.3.0"); Check(!d.Compatible && d.Describe(4).Contains("版本"), "old backend detected");
d.Read("BACKEND_VERSION: 0.3.7"); Check(d.Compatible, "matching backend");
d.Read("FAILURE_CODE: IO"); d.Read("Unhandled System.IO.IOException: PRIVATE_ACCOUNT C:\\SECRET_PATH");
Check(d.Describe(-1).Contains("IOException") && d.Describe(-1).Contains("退出碼 -1"), "exit and type available");
Check(!d.Describe(-1).Contains("PRIVATE") && !d.Describe(-1).Contains("SECRET"), "no raw exception leakage");
d.Read("ATTEMPT: AUTO_INVOKE_ONCE"); Check(d.Describe(3).Contains("可能已送出"), "attempt warning scoped to actual attempt");
var fresh = new WorkerDiagnostics(); fresh.Read("BACKEND_VERSION: 0.3.7"); fresh.Read("FAILURE_CODE: TARGET_OR_PERMISSION");
Check(fresh.Describe(4).Contains("權限") && fresh.Describe(4).Contains("未記錄"), "startup vs attempted failure");
Console.WriteLine("PASS: 7 diagnostic checks (version matching, early exit, safe metadata, attempted status).");

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
