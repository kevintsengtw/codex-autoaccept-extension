using System;
using System.Collections.Generic;

internal sealed class ScanProgress { public int Nodes, Depth; }

// Shared by the production UIA scan and portable tests. Partial scans never
// return a candidate collection; every retry starts with new traversal state.
internal static class CompleteTreeScan
{
    sealed class Pending<T> { public T Element; public int Depth; }
    public static HashSet<string> Run<T>(T root, int maxNodes, int maxDepth, Func<T, string> identity,
        Action<T> visit, Func<T, T> firstChild, Func<T, T> nextSibling, ScanProgress progress) where T : class
    {
        progress.Nodes = progress.Depth = 0;
        var stack = new Stack<Pending<T>>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        stack.Push(new Pending<T> { Element = root });
        while (stack.Count > 0)
        {
            var item = stack.Pop();
            string id = identity(item.Element);
            if (seen.Contains(id)) throw new ScanFailureException(ScanFailureReason.TREE_REPEAT, ScanOrigin.UIA);
            if (progress.Nodes >= maxNodes) throw new ScanFailureException(ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
            seen.Add(id);
            progress.Nodes++; progress.Depth = Math.Max(progress.Depth, item.Depth);
            visit(item.Element);
            var child = firstChild(item.Element);
            if (child != null && item.Depth >= maxDepth) throw new ScanFailureException(ScanFailureReason.DEPTH_LIMIT, ScanOrigin.UIA);
            int children = 0;
            var siblings = new HashSet<string>(StringComparer.Ordinal);
            while (child != null)
            {
                if (!siblings.Add(identity(child))) throw new ScanFailureException(ScanFailureReason.TREE_REPEAT, ScanOrigin.UIA);
                if (++children > maxNodes) throw new ScanFailureException(ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
                stack.Push(new Pending<T> { Element = child, Depth = item.Depth + 1 });
                child = nextSibling(child);
            }
        }
        return seen;
    }
}
