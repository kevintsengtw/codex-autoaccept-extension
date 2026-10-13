using System;
using System.Globalization;
using System.Text.RegularExpressions;

// Only fixed tokens and bounded counts cross the pipe. No UI text, IDs, paths or hashes.
internal sealed class ScanMetrics
{
    public string Mode = "UNKNOWN", Result = "ERROR", Reason = "READ";
    public string WindowState = "UNKNOWN", EligibilityReason = "NONE";
    public long Request = 0;
    public int Nodes, Buttons, Named, Labels, Enabled, Invoke, Eligible, Alternatives;
    public bool RootOffscreen, RootOffscreenRead = true;
    public string Encode() => string.Format(CultureInfo.InvariantCulture,
        "UIA_SCAN: {0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10} {11}",
        Mode, Result, Reason, Nodes, Buttons, Named, Labels, Enabled, Invoke, Eligible, Alternatives, RootOffscreenRead ? (RootOffscreen ? "1" : "0") : "U");
    public static ScanMetrics Parse(string line)
    {
        var m = Regex.Match(line, @"^UIA_SCAN: (FOREGROUND|BACKGROUND|MINIMIZED|UNKNOWN) (COMPLETE|BLOCKED|ERROR) (NONE|NATIVE|HIDDEN|CLOAKED|DESKTOP|ROOT|FRAME|NODE_LIMIT|DEPTH_LIMIT|TREE_REPEAT|MULTIPLE|READ|TRANSITION|MODAL|ROOT_READ|WINDOW_PATTERN|BUTTON_READ|CONTEXT_READ) (\d{1,5}) (\d{1,5}) (\d{1,5}) (\d{1,5}) (\d{1,5}) (\d{1,5}) (\d{1,5}) (\d{1,5}) ([01U])$");
        if (!m.Success) return null;
        var n = new int[8];
        for (int i=0;i<n.Length;i++) if (!int.TryParse(m.Groups[i+4].Value,NumberStyles.None,CultureInfo.InvariantCulture,out n[i]) || n[i]>10000) return null;
        if (n[1]>n[0] || n[2]>n[0] || n[3]>n[1] || n[3]>n[2] || n[4]>n[3] || n[5]>n[3] || n[6]>n[3] || n[7]>n[1]) return null;
        return new ScanMetrics { Mode=m.Groups[1].Value, Result=m.Groups[2].Value, Reason=m.Groups[3].Value,
            Nodes=n[0], Buttons=n[1], Named=n[2], Labels=n[3], Enabled=n[4], Invoke=n[5], Eligible=n[6], Alternatives=n[7], RootOffscreen=m.Groups[12].Value=="1", RootOffscreenRead=m.Groups[12].Value!="U" };
    }
    public string Display => Mode + " " + Result + "/" + Reason + " n=" + Nodes + " btn=" + Buttons + " name=" + Named
        + " allow=" + Labels + "\n  e=" + Enabled + " i=" + Invoke + " ok=" + Eligible + " alt=" + Alternatives + " rootOff=" + (RootOffscreenRead ? (RootOffscreen ? "1" : "0") : "U");
}
