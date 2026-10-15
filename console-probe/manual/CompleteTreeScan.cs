using System;
using System.Collections.Generic;

internal sealed class ScanProgress
{
    public int Nodes, Depth;
}

// Production UIA traversal and portable regression tests use the same walker.
// Run returns only after every reachable node was visited within both budgets.
internal static class CompleteTreeScan
{
    sealed class Pending<T> { public T Element; public int Depth; }
    public static void Run<T>(T root, ScanRecovery budget, Func<T, string> identity,
        Action<T> visit, Func<T, T> firstChild, Func<T, T> nextSibling,
        ScanProgress progress) where T : class
    {
        progress.Nodes = progress.Depth = 0;
        var stack = new Stack<Pending<T>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        stack.Push(new Pending<T> { Element = root });
        while (stack.Count > 0)
        {
            if (progress.Nodes >= budget.Nodes) throw new ManualRefusalException(RefusalCode.NODE_LIMIT);
            var item = stack.Pop();
            if (!seen.Add(identity(item.Element))) throw new ManualRefusalException(RefusalCode.TREE_REPEAT);
            progress.Nodes++; progress.Depth = Math.Max(progress.Depth, item.Depth);
            visit(item.Element);
            var child = firstChild(item.Element);
            if (child != null && item.Depth >= budget.Depth) throw new ManualRefusalException(RefusalCode.DEPTH_LIMIT);
            int count = 0;
            var siblings = new HashSet<string>(StringComparer.Ordinal);
            while (child != null)
            {
                if (!siblings.Add(identity(child))) throw new ManualRefusalException(RefusalCode.TREE_REPEAT);
                if (++count > budget.Nodes) throw new ManualRefusalException(RefusalCode.NODE_LIMIT);
                stack.Push(new Pending<T> { Element = child, Depth = item.Depth + 1 });
                child = nextSibling(child);
            }
        }
    }
}
