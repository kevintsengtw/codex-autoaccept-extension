using System;

internal sealed class ScanPair<T> where T : class
{
    public readonly T Candidate, Fresh;
    public readonly bool Ready, Unchanged;
    public ScanPair(T candidate, T fresh, bool ready, bool unchanged)
    { Candidate = candidate; Fresh = fresh; Ready = ready; Unchanged = unchanged; }
}

// The real auto-loop and regression tests use this same two-scan boundary.
// A failed revalidation cannot publish a pair or reuse the initial candidate.
internal static class AutoScanCycle
{
    public static ScanPair<T> Read<T>(Func<T> scan, Func<T, bool> ready,
        Func<T> revalidate, Func<T, T, bool> same) where T : class
    {
        var candidate = scan();
        if (!ready(candidate)) return new ScanPair<T>(candidate, null, false, false);
        var fresh = revalidate();
        return new ScanPair<T>(candidate, fresh, true, fresh != null && same(candidate, fresh));
    }
}
