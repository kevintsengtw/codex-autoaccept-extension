internal sealed class IntegrityResult
{
    public int? Rid;
    public string QueryStage = "none";
    public int Error;
    public string Level => !Rid.HasValue ? "unavailable" : Rid.Value >= 0x4000 ? "system" : Rid.Value >= 0x3000 ? "high"
        : Rid.Value >= 0x2100 ? "medium-plus" : Rid.Value >= 0x2000 ? "medium" : Rid.Value >= 0x1000 ? "low" : "untrusted";
}
internal static class PermissionPolicy
{
    public static bool CanStart(IntegrityResult probe, IntegrityResult target, out string reason)
    {
        if (probe.Rid >= 0x3000) { reason = "PROBE_ELEVATED"; return false; }
        if (target.Rid >= 0x3000) { reason = "TARGET_ELEVATED"; return false; }
        // Token metadata access is not a prerequisite for UIA. Unknown does NOT
        // mean permitted: Windows still checks actual UIA access/Invoke rights.
        reason = !probe.Rid.HasValue || !target.Rid.HasValue ? "QUERY_UNAVAILABLE" : "STANDARD_OR_LOWER";
        return true;
    }
}
