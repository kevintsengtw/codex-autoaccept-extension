using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// Produces metadata-only output. Never includes names, text, paths, titles or raw error messages.
internal static class ReportSummary
{
    public static string[] Describe(IEnumerable<Dictionary<string, object>> source, int malformed = 0)
    {
        var records = source.ToList();
        var nodes = records.Where(r => Str(r, "kind") == "node").ToList();
        var header = records.FirstOrDefault(r => Str(r, "kind") == "header");
        var summary = records.LastOrDefault(r => Str(r, "kind") == "summary");
        var supervisor = records.LastOrDefault(r => Str(r, "kind") == "supervisor");
        bool timedOut = Str(supervisor, "status") == "timeout";
        bool complete = summary != null && Bool(summary, "complete") == true && malformed == 0
            && !timedOut && !records.Any(r => Str(r, "kind") == "error")
            && (supervisor == null || Str(supervisor, "status") == "completed");
        var candidates = nodes.Where(r => Bool(r, "approvalLabelHint") == true
            && Str(r, "controlType") == "ControlType.Button").ToList();
        int invoke = candidates.Count(r => Sequence(r, "patterns").Any(p => Convert.ToString(p) == "InvokePatternIdentifiers.Pattern" || Convert.ToString(p) == "InvokePattern.Pattern"));
        // UIA uses InvokePatternIdentifiers.Pattern; tolerate older diagnostic spellings.
        int patternUnknown = candidates.Count(r => !r.ContainsKey("patterns") || r["patterns"] == null);
        int ready = candidates.Count(r => Bool(r, "enabled") == true && Bool(r, "offscreen") == false);
        int nearby = candidates.Count(c => nodes.Any(n => Int(n, "parent") == Int(c, "parent")
            && Int(n, "index") != Int(c, "index") && Str(n, "controlType") != "ControlType.Button"
            && Bool(n, "password") == false && (!string.IsNullOrWhiteSpace(Str(n, "name")) || !string.IsNullOrWhiteSpace(Str(n, "text")))));
        var issues = nodes.SelectMany(n => Sequence(n, "errors").Select(v => Convert.ToString(v)))
            .Concat(records.Where(r => Str(r, "kind") == "error").Select(r => Str(r, "error"))).ToList();
        int access = issues.Count(s => s != null && (s.IndexOf("UnauthorizedAccessException", StringComparison.Ordinal) >= 0
            || s.IndexOf("SecurityException", StringComparison.Ordinal) >= 0));
        int stale = issues.Count(s => s != null && s.IndexOf("ElementNotAvailableException", StringComparison.Ordinal) >= 0);
        return new[] {
            "READ-ONLY LOCAL SUMMARY (no UI content included)",
            "Capture: " + (complete ? "COMPLETE" : "PARTIAL / UNKNOWN"),
            "Probe integrity: " + Level(header, "probeIntegrity") + "; target integrity: " + Level(header, "targetIntegrity"),
            "Nodes read: " + nodes.Count + "; malformed lines: " + malformed,
            "Approval label BUTTON candidates: " + candidates.Count,
            "Candidates declaring Invoke: " + invoke + "; pattern information unavailable: " + patternUnknown,
            "Candidates enabled and onscreen: " + ready,
            "Candidates with readable adjacent non-button labels/text: " + nearby,
            "Read errors (summary): " + (summary == null ? "UNKNOWN" : Int(summary, "errors").ToString()),
            "Explicit access/security exceptions: " + access + "; stale-element exceptions: " + stale,
            "Depth boundaries reached: " + (summary == null ? "UNKNOWN" : Int(summary, "depthLimits").ToString()),
            "Node budget reached: " + (summary == null ? "UNKNOWN" : Bool(summary, "nodeLimitReached").ToString()),
            "Timed out: " + timedOut + "; capture error records: " + records.Count(r => Str(r, "kind") == "error"),
            "Label candidates are unverified. Adjacent text does NOT establish readable command context.",
            "No click/Invoke was performed. Missing candidates or partial capture do NOT establish lack of UIA support."
        };
    }
    static string Level(Dictionary<string, object> r, string k)
    {
        string v = Str(r, k);
        return new[] { "medium", "medium-plus", "high", "system", "low", "untrusted" }.Contains(v) ? v : "unavailable";
    }
    static string Str(Dictionary<string, object> r, string k)
    { object v; return r != null && r.TryGetValue(k, out v) ? v as string : null; }
    static bool? Bool(Dictionary<string, object> r, string k)
    { object v; return r != null && r.TryGetValue(k, out v) && v is bool ? (bool?)v : null; }
    static int Int(Dictionary<string, object> r, string k)
    { object v; return r != null && r.TryGetValue(k, out v) && v is int ? (int)v : -1; }
    static IEnumerable<object> Sequence(Dictionary<string, object> r, string k)
    { object v; return r.TryGetValue(k, out v) && v is IEnumerable && !(v is string) ? ((IEnumerable)v).Cast<object>() : Enumerable.Empty<object>(); }
}
