using System;

internal enum ScanFailureReason { NODE_LIMIT, DEPTH_LIMIT, TREE_REPEAT, READ }
internal enum ScanOrigin { UIA, MSAA }
internal enum ScanStage { Scan, Revalidation }

internal sealed class ScanFailureException : InvalidOperationException
{
    public readonly ScanFailureReason Reason;
    public readonly ScanOrigin Origin;
    public ScanFailureException(ScanFailureReason reason, ScanOrigin origin) : base(reason.ToString())
    { Reason = reason; Origin = origin; }

    public static ScanFailureException From(Exception error)
    {
        while (error is AggregateException)
        {
            var errors = ((AggregateException)error).Flatten().InnerExceptions;
            if (errors.Count != 1) return null;
            error = errors[0];
        }
        return error as ScanFailureException;
    }
}

internal sealed class NodeBudgetChange
{
    public readonly int PreviousLimit, NextLimit;
    public readonly ScanOrigin Origin;
    public bool Expanded { get { return NextLimit > PreviousLimit; } }
    public NodeBudgetChange(int previousLimit, int nextLimit, ScanOrigin origin)
    { PreviousLimit = previousLimit; NextLimit = nextLimit; Origin = origin; }
}

// One auto-loop owns this budget. Recovery only schedules a fresh iteration;
// it never resets the gate, selects a candidate, or retries an approval.
internal sealed class NodeScanBudget
{
    readonly bool canGrow;
    public int Nodes { get; private set; }
    public NodeScanBudget(int nodes, bool automatic, bool readOnly, bool explicitlySet)
    {
        if (nodes < 1 || nodes > 10000) throw new ArgumentOutOfRangeException(nameof(nodes));
        Nodes = nodes;
        canGrow = automatic && !readOnly && !explicitlySet;
    }
    public bool TryRecover(Exception error, bool committed, AutoGate gate, long? epoch, Action<NodeBudgetChange> record)
    {
        var failure = ScanFailureException.From(error);
        if (!canGrow || committed || failure == null || failure.Reason != ScanFailureReason.NODE_LIMIT
            || (failure.Origin != ScanOrigin.UIA && failure.Origin != ScanOrigin.MSAA)
            || !epoch.HasValue || gate.GetEpoch() != epoch) return false;
        int next = Math.Min(10000, Nodes * 2);
        record(new NodeBudgetChange(Nodes, next, failure.Origin)); // A failed log must not silently apply recovery.
        Nodes = next;
        return true;
    }
}
