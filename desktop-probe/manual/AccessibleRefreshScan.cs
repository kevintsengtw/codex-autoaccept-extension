using System;
using System.Collections.Generic;

// A child ID is a leaf in MSAA; only IAccessible objects can expose descendants.
// This distinction keeps the production traversal portable for bounded tests.
internal sealed class AccessibleRefreshChild<T> where T : class
{
    public readonly T Node;
    public readonly bool IsLeaf;
    public AccessibleRefreshChild(T node) { Node = node; }
    public AccessibleRefreshChild() { IsLeaf = true; }
}

internal static class AccessibleRefreshScan
{
    sealed class Pending<T> where T : class { public T Node; public int Depth; }

    public static List<T> CollectDocuments<T>(T root, int maxDepth, int maxNodes,
        Func<T, bool> isDocument, Func<T, int> childCount,
        Func<T, int, IReadOnlyList<AccessibleRefreshChild<T>>> readChildren,
        IEqualityComparer<T> comparer = null) where T : class
    {
        if (root == null || maxDepth < 0 || maxNodes < 1 || isDocument == null
            || childCount == null || readChildren == null) throw Failure(ScanFailureReason.READ);
        try
        {
            var visited = new HashSet<T>(comparer);
            var stack = new Stack<Pending<T>>();
            var documents = new List<T>();
            visited.Add(root);
            stack.Push(new Pending<T> { Node = root });
            int count = 1;
            while (stack.Count != 0)
            {
                var item = stack.Pop();
                if (isDocument(item.Node))
                {
                    documents.Add(item.Node);
                    continue; // A refresh never reads the document's chat subtree.
                }
                int children = childCount(item.Node);
                if (children < 0) throw Failure(ScanFailureReason.READ);
                if (children == 0) continue;
                if (item.Depth >= maxDepth) throw Failure(ScanFailureReason.DEPTH_LIMIT);
                // Retain the conservative MSAA count check before allocating the
                // returned child array. An excessive declaration is a budget
                // failure; it does not justify an unbounded provider read.
                if (children > maxNodes - count) throw Failure(ScanFailureReason.NODE_LIMIT);
                var values = readChildren(item.Node, children);
                if (values == null || values.Count != children) throw Failure(ScanFailureReason.READ);
                var siblings = new HashSet<T>(comparer);
                foreach (var value in values)
                {
                    if (value == null || !value.IsLeaf && value.Node == null)
                        throw Failure(ScanFailureReason.READ);
                    if (!value.IsLeaf && (visited.Contains(value.Node) || !siblings.Add(value.Node)))
                        throw Failure(ScanFailureReason.TREE_REPEAT);
                }
                // Every returned child counts, including integer child IDs.
                // Validate known data first so repeats cannot become NODE_LIMIT.
                count += children;
                foreach (var value in values)
                {
                    if (value.IsLeaf) continue;
                    visited.Add(value.Node);
                    stack.Push(new Pending<T> { Node = value.Node, Depth = item.Depth + 1 });
                }
            }
            if (documents.Count == 0) throw Failure(ScanFailureReason.READ);
            return documents;
        }
        catch (ScanFailureException) { throw; }
        catch (Exception) { throw Failure(ScanFailureReason.READ); }
    }

    static ScanFailureException Failure(ScanFailureReason reason)
    { return new ScanFailureException(reason, ScanOrigin.MSAA); }
}
