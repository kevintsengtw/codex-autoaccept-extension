static void Check(bool value,string name) { if (!value) throw new Exception(name); }
var fg = new ScanMetrics { Mode="FOREGROUND",Result="COMPLETE",Reason="NONE",Nodes=136,Buttons=20,Named=1,Labels=1,Enabled=1,Invoke=1,Eligible=1 };
var fgRead=ScanMetrics.Parse(fg.Encode());
Check(fgRead!=null && fgRead.Labels==1 && fgRead.Eligible==1 && fgRead.Mode=="FOREGROUND","foreground pending request roundtrip");
var bg = new ScanMetrics { Mode="BACKGROUND",Result="COMPLETE",Reason="NONE",Nodes=80,Buttons=15 };
Check(ScanMetrics.Parse(bg.Encode()).Labels==0,"nonempty background tree without approval remains visible in diagnostics");
var min = new ScanMetrics { Mode="MINIMIZED",Result="BLOCKED",Reason="CLOAKED",RootOffscreen=true };
Check(ScanMetrics.Parse(min.Encode()).Reason=="CLOAKED","minimized native blocker is not confused with missing button");
min.Result="ERROR"; min.Reason="READ"; Check(ScanMetrics.Parse(min.Encode()).Result=="ERROR","incomplete scan is explicit");
min.Result="BLOCKED"; min.Reason="TRANSITION"; Check(ScanMetrics.Parse(min.Encode()).Reason=="TRANSITION","transitional scan is explicit");
min.RootOffscreenRead=false;
var unread = ScanMetrics.Parse(min.Encode());
Check(unread!=null && !unread.RootOffscreenRead && unread.Display.Contains("rootOff=U"), "skipped screen query is unknown, not falsely reported as false");
foreach (string stage in new[]{"MODAL","ROOT_READ","WINDOW_PATTERN","BUTTON_READ","CONTEXT_READ"})
{ min.Reason=stage; Check(ScanMetrics.Parse(min.Encode()).Reason==stage,"specific minimized rejection/read stage preserved"); }
foreach (var input in new[]{fg.Encode()+" PRIVATE_TEXT", fg.Encode().Replace("NONE","C:\\SECRET_PATH"), fg.Encode().Replace("FOREGROUND","PRIVATE_USER"),fg.Encode().Replace("136","10001"),"UIA_SCAN: BACKGROUND COMPLETE NONE 1 2 0 0 0 0 0 0 0",fg.Encode().Replace("136","-1")})
    Check(ScanMetrics.Parse(input)==null,"untrusted/free-text/out-of-range diagnostic rejected");
var changing = new ScanMetrics { Mode="BACKGROUND",Result="COMPLETE",Reason="NONE",Nodes=10,Buttons=1,Named=1,Labels=1,Enabled=0,Invoke=1,Eligible=1 };
Check(ScanMetrics.Parse(changing.Encode())!=null,"live property changes do not falsely discard bounded scan metadata");
Check(!fg.Display.Contains("允許一次") && !fg.Display.Contains("SECRET"),"display contains only counts and fixed codes");
Console.WriteLine("PASS: scan protocol bounds/privacy, missing background approval, native blockers, incomplete/transition scans and live-property races.");
