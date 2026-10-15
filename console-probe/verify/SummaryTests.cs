using R = System.Collections.Generic.Dictionary<string, object>;
static void Check(bool value, string name) { if (!value) throw new Exception(name); }
var header = new R { ["kind"]="header", ["probeIntegrity"]="medium", ["targetIntegrity"]="unavailable: PRIVATE_PATH" };
var summary = new R { ["kind"]="summary", ["complete"]=true, ["errors"]=0, ["depthLimits"]=0, ["nodeLimitReached"]=false };
var button = new R { ["kind"]="node", ["index"]=2, ["parent"]=1, ["approvalLabelHint"]=true,
    ["controlType"]="ControlType.Button", ["name"]="PRIVATE_COMMAND", ["enabled"]=true, ["offscreen"]=false,
    ["patterns"]=new[] { "InvokePatternIdentifiers.Pattern" }, ["errors"]=new string[0] };
var text = new R { ["kind"]="node", ["index"]=3, ["parent"]=1, ["controlType"]="ControlType.Text",
    ["name"]="SECRET_ACCOUNT", ["password"]=false, ["errors"]=new string[0] };
string Describe(params R[] r) => string.Join("\n", ReportSummary.Describe(r));
string s = Describe(header, summary, button, text);
Check(s.Contains("Capture: COMPLETE") && s.Contains("declaring Invoke: 1"), "complete candidate");
Check(s.Contains("labels/text: 1"), "adjacent label");
Check(!s.Contains("PRIVATE") && !s.Contains("SECRET"), "content leakage");
Check(Describe(header).Contains("PARTIAL / UNKNOWN"), "missing summary");
Check(string.Join("\n", ReportSummary.Describe(new[] { summary }, 1)).Contains("PARTIAL / UNKNOWN"), "malformed line");
Check(Describe(summary, new R { ["kind"]="supervisor", ["status"]="timeout" }).Contains("Timed out: True"), "timeout");
var nonButton = new R(button); nonButton["controlType"]="ControlType.Text";
Check(Describe(summary, nonButton).Contains("BUTTON candidates: 0"), "label isn't button");
var unknown = new R(button); unknown.Remove("patterns");
Check(Describe(summary, unknown).Contains("information unavailable: 1"), "unknown patterns");
var other = new R(text); other["parent"]=5;
Check(Describe(summary, button, other).Contains("labels/text: 0"), "unrelated text");
var partial = new R(summary); partial["complete"]=false; partial["errors"]=2;
Check(Describe(partial).Contains("Read errors (summary): 2"), "partial reason");
Console.WriteLine("PASS: 10 summary checks (privacy, partial/unknown, timeout, Invoke metadata, context scope).");
