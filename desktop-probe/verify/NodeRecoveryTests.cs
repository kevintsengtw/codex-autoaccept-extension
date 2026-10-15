using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// These tests compile the production scanner, scan coordinator, bounded recovery
// policy and gate. They do not load UIA, query a window, or invoke an App.
internal static class NodeRecoveryTests
{
    sealed class Node
    {
        internal readonly string Id;
        internal readonly List<Node> Children = new List<Node>();
        internal Node Next;
        internal string Approval;
        internal bool Document;
        internal Node(string id) { Id = id; }
        internal Node Add(Node child)
        {
            if (Children.Count != 0) Children[Children.Count - 1].Next = child;
            Children.Add(child); return this;
        }
    }

    sealed class Snapshot
    {
        internal readonly List<string> Approvals = new List<string>();
    }

    static void Check(bool value, string name)
    { if (!value) throw new Exception("Node recovery: " + name); }

    static ScanFailureException Failure(Action action, ScanFailureReason reason, ScanOrigin origin)
    {
        try { action(); }
        catch (Exception error)
        {
            var failure = ScanFailureException.From(error);
            Check(failure != null && failure.Reason == reason && failure.Origin == origin,
                "precise failure " + origin + "/" + reason + "; actual=" + error.GetType().Name + "/" + error.Message);
            return failure;
        }
        throw new Exception("Node recovery: expected " + origin + "/" + reason);
    }

    static Node Wide(int count, string approval = null)
    {
        var root = new Node("root");
        for (int i = 1; i < count; i++) root.Add(new Node("node-" + i));
        if (approval != null) root.Children[root.Children.Count - 1].Approval = approval;
        return root;
    }

    static Node Deep(int depth)
    {
        var root = new Node("root"); var current = root;
        for (int i = 1; i <= depth; i++)
        { var child = new Node("level-" + i); current.Add(child); current = child; }
        return root;
    }

    static Snapshot Scan(Node root, int maxNodes, int maxDepth = 60, Action<Node> visited = null)
    {
        var snapshot = new Snapshot();
        CompleteTreeScan.Run(root, maxNodes, maxDepth, node => node.Id,
            node =>
            {
                visited?.Invoke(node);
                if (node.Approval != null) snapshot.Approvals.Add(node.Approval);
            }, node => node.Children.Count == 0 ? null : node.Children[0], node => node.Next, new ScanProgress());
        return snapshot;
    }

    static bool Ready(Snapshot snapshot) => snapshot.Approvals.Count == 1;
    static bool Same(Snapshot candidate, Snapshot fresh) => Ready(fresh)
        && candidate.Approvals[0] == fresh.Approvals[0];

    static AutoGate EnabledGate()
    { var gate = new AutoGate(); gate.SetEnabled(true); return gate; }

    static void CompleteScanBoundaries()
    {
        foreach (int limit in new[] { 3000, 6000, 10000 })
        {
            foreach (int count in new[] { limit - 1, limit })
            {
                var progress = new ScanProgress();
                var seen = CompleteTreeScan.Run(Wide(count), limit, 60, node => node.Id,
                    node => { }, node => node.Children.Count == 0 ? null : node.Children[0], node => node.Next, progress);
                Check(progress.Nodes == count && seen.Count == count,
                    "root-inclusive complete boundary " + count + "/" + limit);
            }
            var over = new ScanProgress();
            Failure(() => CompleteTreeScan.Run(Wide(limit + 1), limit, 60, node => node.Id,
                node => { }, node => node.Children.Count == 0 ? null : node.Children[0], node => node.Next, over),
                ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
            Check(over.Nodes == limit, "never visit node above " + limit);
        }

        var cumulative = new Node("cumulative-root");
        for (int i = 0; i < 4; i++)
        {
            var branch = new Node("branch-" + i); cumulative.Add(branch);
            for (int j = 0; j < 2500; j++) branch.Add(new Node(i + "/" + j));
        }
        Failure(() => Scan(cumulative, 10000), ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
        foreach (var branch in cumulative.Children) branch.Children.RemoveRange(0, 2);
        Scan(cumulative, 10000);
    }

    static void RecoveryAndHardCap()
    {
        var gate = EnabledGate(); long epoch = gate.GetEpoch().Value;
        var budget = new NodeScanBudget(3000, true, false, false);
        var changes = new List<NodeBudgetChange>(); var tree = Wide(7501, "request");
        ScanPair<Snapshot> pair = null; int scans = 0, revalidations = 0, readyCalls = 0;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                pair = AutoScanCycle.Read(() => { scans++; return Scan(tree, budget.Nodes); },
                    snapshot => { readyCalls++; return Ready(snapshot); },
                    () => { revalidations++; return Scan(tree, budget.Nodes); }, Same);
            }
            catch (ScanFailureException error)
            {
                Check(pair == null && readyCalls == 0 && revalidations == 0,
                    "incomplete initial scan never publishes partial candidates or calls ready/revalidate");
                Check(budget.TryRecover(error, false, gate, epoch, changes.Add), "confirmed node failure is handled");
            }
        }
        Check(budget.Nodes == 10000 && changes.Count == 2
            && changes[0].PreviousLimit == 3000 && changes[0].NextLimit == 6000
            && changes[1].PreviousLimit == 6000 && changes[1].NextLimit == 10000,
            "bounded recovery is exactly 3000 to 6000 to 10000");
        Check(scans == 3 && revalidations == 1 && readyCalls == 1 && pair != null && pair.Ready && pair.Unchanged,
            "7501 nodes require a new complete initial scan and complete revalidation");
        Check(gate.TryBegin(epoch, pair.Fresh.Approvals[0]) && !gate.TryBegin(epoch, pair.Fresh.Approvals[0]),
            "only a complete unchanged pair can commit, once");

        var capped = new NodeScanBudget(10000, true, false, false); var capChanges = new List<NodeBudgetChange>();
        ScanPair<Snapshot> cappedPair = null; int capReadyCalls = 0, capRevalidations = 0;
        var capFailure = Failure(() => cappedPair = AutoScanCycle.Read(() => Scan(Wide(10001, "too-wide"), capped.Nodes),
            snapshot => { capReadyCalls++; return true; },
            () => { capRevalidations++; return new Snapshot(); }, Same), ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
        Check(capped.TryRecover(capFailure, false, gate, epoch, capChanges.Add)
            && capChanges.Count == 1 && !capChanges[0].Expanded && capped.Nodes == 10000,
            "hard cap records refusal without increasing above 10000");
        Check(cappedPair == null && capReadyCalls == 0 && capRevalidations == 0,
            "10001 nodes never produce a usable pair at the hard cap");
    }

    static void PartialCandidatesAndRevalidation()
    {
        var gate = EnabledGate(); long epoch = gate.GetEpoch().Value;
        var budget = new NodeScanBudget(3000, true, false, false);
        var tree = new Node("root"); var left = new Node("left");
        for (int i = 0; i < 3000; i++) left.Add(new Node("left-" + i));
        left.Children[0].Approval = "second";
        tree.Add(left).Add(new Node("right") { Approval = "first" });
        int partialApprovals = 0, readyCalls = 0, invokes = 0;
        ScanPair<Snapshot> pair = null;
        var failure = Failure(() => pair = AutoScanCycle.Read(
            () => Scan(tree, budget.Nodes, 60, node => { if (node.Approval != null) partialApprovals++; }),
            snapshot => { readyCalls++; return Ready(snapshot); }, () => Scan(tree, budget.Nodes), Same),
            ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
        Check(partialApprovals == 1 && readyCalls == 0 && pair == null,
            "candidate found before exhaustion is not returned or considered ready");
        Check(budget.TryRecover(failure, false, gate, epoch, change => { }), "recover partial candidate scan");
        pair = AutoScanCycle.Read(() => Scan(tree, budget.Nodes), Ready, () => Scan(tree, budget.Nodes), Same);
        if (pair.Ready && pair.Unchanged && gate.TryBegin(epoch, pair.Fresh.Approvals[0])) invokes++;
        Check(!pair.Ready && pair.Candidate.Approvals.Count == 2 && pair.Fresh == null && invokes == 0,
            "full rescan discovers the second candidate and refuses invocation");

        var small = Wide(20, "stale-request"); var changed = Wide(3001, "fresh-request");
        var revalidationBudget = new NodeScanBudget(3000, true, false, false);
        int initial = 0, fresh = 0; ScanPair<Snapshot> revalidated = null;
        Func<Snapshot> initialScan = () => Scan(++initial == 1 ? small : changed, revalidationBudget.Nodes);
        Func<Snapshot> freshScan = () => { fresh++; return Scan(changed, revalidationBudget.Nodes); };
        var revalidationFailure = Failure(() => revalidated = AutoScanCycle.Read(initialScan, Ready, freshScan, Same),
            ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
        Check(revalidated == null && initial == 1 && fresh == 1,
            "failed complete revalidation publishes no pair");
        Check(revalidationBudget.TryRecover(revalidationFailure, false, gate, epoch, change => { }),
            "node exhaustion during revalidation schedules recovery");
        revalidated = AutoScanCycle.Read(initialScan, Ready, freshScan, Same);
        Check(initial == 2 && fresh == 2 && revalidated.Unchanged
            && revalidated.Candidate.Approvals[0] == "fresh-request" && revalidated.Fresh.Approvals[0] == "fresh-request",
            "revalidation recovery repeats initial scan plus fresh scan; stale candidate is discarded");
        var mismatch = AutoScanCycle.Read(() => Scan(small, 3000), Ready, () => Scan(Wide(20, "different"), 3000), Same);
        Check(mismatch.Ready && !mismatch.Unchanged, "changed candidate still fails original pre-invoke comparison");
    }

    static void FailureClassificationAndRecoveryPolicy()
    {
        var gate = EnabledGate(); long epoch = gate.GetEpoch().Value;
        var budget = new NodeScanBudget(3000, true, false, false);
        var siblingLoop = new Node("root"); var a = new Node("a"); var b = new Node("b");
        siblingLoop.Add(a).Add(b); b.Next = a;
        var repeated = Failure(() => Scan(siblingLoop, 3000), ScanFailureReason.TREE_REPEAT, ScanOrigin.UIA);
        var ancestorLoop = Wide(3000);
        ancestorLoop.Children[0].Add(ancestorLoop); // Visited last by the production LIFO traversal.
        int ancestorVisited = 0;
        var ancestorRepeat = Failure(() => Scan(ancestorLoop, 3000, 60, node => ancestorVisited++),
            ScanFailureReason.TREE_REPEAT, ScanOrigin.UIA);
        Check(ancestorVisited == 3000, "ancestor cycle at the exact node cap stays TREE_REPEAT, not NODE_LIMIT");
        var deep = Failure(() => Scan(Deep(61), 3000), ScanFailureReason.DEPTH_LIMIT, ScanOrigin.UIA);
        var typedRead = Failure(() => Scan(Wide(2), 3000, 60,
            node => { throw new ScanFailureException(ScanFailureReason.READ, ScanOrigin.UIA); }),
            ScanFailureReason.READ, ScanOrigin.UIA);
        var nodeFailure = new ScanFailureException(ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
        foreach (var error in new Exception[] { repeated, ancestorRepeat, deep, typedRead, new IOException("provider read"),
            new InvalidOperationException("NODE_LIMIT"), new TimeoutException(),
            new AggregateException(nodeFailure, new IOException()),
            new ScanFailureException(ScanFailureReason.NODE_LIMIT, (ScanOrigin)99) })
            Check(!budget.TryRecover(error, false, gate, epoch, change => { throw new Exception("must not log recovery"); })
                && budget.Nodes == 3000, "only one typed recognized node-limit failure can expand");
        foreach (var policy in new[] { new NodeScanBudget(3000, true, false, true),
            new NodeScanBudget(3000, false, false, false), new NodeScanBudget(3000, true, true, false) })
            Check(!policy.TryRecover(nodeFailure, false, gate, epoch, change => { }) && policy.Nodes == 3000,
                "explicit/manual/read-only policy never expands");
        Check(!budget.TryRecover(nodeFailure, true, gate, epoch, change => { }) && budget.Nodes == 3000,
            "committed invocation cannot schedule a recovery retry");
        gate.SetEnabled(false); gate.SetEnabled(true);
        Check(!budget.TryRecover(nodeFailure, false, gate, epoch, change => { }) && budget.Nodes == 3000,
            "revoked epoch cannot expand");
        Check(!budget.TryRecover(nodeFailure, false, gate, null, change => { }) && budget.Nodes == 3000,
            "missing epoch cannot expand");
        gate.SetEnabled(false);
        Check(!budget.TryRecover(nodeFailure, false, gate, gate.GetEpoch(), change => { }) && budget.Nodes == 3000,
            "OFF cannot expand");
        gate.SetEnabled(true); epoch = gate.GetEpoch().Value;
        bool logFailed = false;
        try { budget.TryRecover(nodeFailure, false, gate, epoch, change => { throw new IOException("record failed"); }); }
        catch (IOException) { logFailed = true; }
        Check(logFailed && budget.Nodes == 3000, "failed recovery record leaves the budget unchanged");
        Check(budget.TryRecover(new AggregateException(new AggregateException(nodeFailure)), false, gate, epoch, change => { })
            && budget.Nodes == 6000, "single wrapped typed failure retains confirmed reason");
    }

    static void UnknownOutcomeRemainsDeduplicated()
    {
        var gate = EnabledGate(); long epoch = gate.GetEpoch().Value;
        Check(gate.TryBegin(epoch, "unknown"), "prepare one unknown invocation");
        gate.Observe(epoch, false); // One earlier complete absence cannot complete the pending cycle.
        var budget = new NodeScanBudget(3000, true, false, false);
        for (int i = 0; i < 3; i++)
        {
            var failure = Failure(() => AutoScanCycle.Read(() => Scan(Wide(10001), budget.Nodes),
                Ready, () => Scan(Wide(10001), budget.Nodes), Same), ScanFailureReason.NODE_LIMIT, ScanOrigin.UIA);
            gate.Observe(epoch, true); // The production catch revokes any partial complete-absence streak.
            Check(budget.TryRecover(failure, false, gate, epoch, change => { }), "handle node failure while prior outcome is unknown");
            Check(gate.AwaitingAbsence && !gate.TryBegin(epoch, "unknown") && !gate.TryBegin(epoch, "other"),
                "incomplete scans and budget changes cannot prove absence or release deduplication");
        }
        gate.Observe(epoch, false);
        Check(gate.AwaitingAbsence && !gate.TryBegin(epoch, "unknown") && !gate.TryBegin(epoch, "other"),
            "complete absence, incomplete scan, then one complete absence cannot release an unknown result");
        gate.SetEnabled(false); gate.SetEnabled(true); long resumed = gate.GetEpoch().Value;
        Check(resumed != epoch && gate.AwaitingAbsence && !gate.TryBegin(resumed, "unknown")
            && !gate.TryBegin(resumed, "other"), "OFF/ON preserves unknown-result deduplication after recovery");
        Check(budget.Nodes == 10000, "OFF/ON does not reset the expanded node budget");
    }

    static List<Node> BackgroundScan(Node root, int nodes = 3000, int depth = 60)
    {
        return AccessibleRefreshScan.CollectDocuments(root, depth, nodes, node => node.Document || node.Children.Count == 0,
            node => node.Children.Count,
            (node, count) => node.Children.Select(child => new AccessibleRefreshChild<Node>(child)).ToArray());
    }

    static void BackgroundClassification()
    {
        foreach (int limit in new[] { 3000, 6000, 10000 })
        {
            Check(BackgroundScan(Wide(limit - 1), limit).Count == limit - 2
                && BackgroundScan(Wide(limit), limit).Count == limit - 1, "MSAA complete root-inclusive boundaries " + limit);
            Failure(() => BackgroundScan(Wide(limit + 1), limit), ScanFailureReason.NODE_LIMIT, ScanOrigin.MSAA);
        }
        var depthFailure = Failure(() => BackgroundScan(Deep(61)), ScanFailureReason.DEPTH_LIMIT, ScanOrigin.MSAA);
        var root = new Node("root"); var repeated = new Node("same"); root.Add(repeated).Add(repeated);
        var repeatedFailure = Failure(() => BackgroundScan(root), ScanFailureReason.TREE_REPEAT, ScanOrigin.MSAA);
        var cycle = new Node("cycle"); cycle.Add(cycle);
        Failure(() => BackgroundScan(cycle), ScanFailureReason.TREE_REPEAT, ScanOrigin.MSAA);
        var oneChild = Wide(2);
        var readFailure = Failure(() => AccessibleRefreshScan.CollectDocuments(oneChild, 60, 3000, node => false,
            node => -1, (node, count) => Array.Empty<AccessibleRefreshChild<Node>>()), ScanFailureReason.READ, ScanOrigin.MSAA);
        Failure(() => AccessibleRefreshScan.CollectDocuments(oneChild, 60, 3000, node => false,
            node => node.Children.Count, (node, count) => Array.Empty<AccessibleRefreshChild<Node>>()),
            ScanFailureReason.READ, ScanOrigin.MSAA);
        Failure(() => AccessibleRefreshScan.CollectDocuments(oneChild, 60, 3000, node => false,
            node => node.Children.Count, (node, count) => new AccessibleRefreshChild<Node>[] { null }),
            ScanFailureReason.READ, ScanOrigin.MSAA);
        Failure(() => AccessibleRefreshScan.CollectDocuments(oneChild, 60, 3000, node => false,
            node => node.Children.Count, (node, count) => new[] { new AccessibleRefreshChild<Node>(null) }),
            ScanFailureReason.READ, ScanOrigin.MSAA);
        Failure(() => AccessibleRefreshScan.CollectDocuments(oneChild, 60, 3000, node => false,
            node => { throw new IOException("provider read"); },
            (node, count) => Array.Empty<AccessibleRefreshChild<Node>>()), ScanFailureReason.READ, ScanOrigin.MSAA);
        var document = new Node("document") { Document = true };
        var documents = AccessibleRefreshScan.CollectDocuments(document, 0, 1, node => node.Document,
            node => { throw new Exception("document children must not be read"); },
            (node, count) => { throw new Exception("document children must not be read"); });
        Check(documents.Count == 1 && ReferenceEquals(documents[0], document), "document boundary retains original no-child-read policy");
        var integerRoot = new Node("integer-root"); var integerDocument = new Node("integer-document");
        Func<Node, bool> isIntegerDocument = node => ReferenceEquals(node, integerDocument);
        Func<Node, int, IReadOnlyList<AccessibleRefreshChild<Node>>> integerChildren = (node, count) =>
            new[] { new AccessibleRefreshChild<Node>(), new AccessibleRefreshChild<Node>(integerDocument) };
        var integerLeaves = AccessibleRefreshScan.CollectDocuments(integerRoot, 60, 3, isIntegerDocument,
            node => 2, integerChildren);
        Check(integerLeaves.Count == 1, "non-object accessible child is a counted leaf");
        Failure(() => AccessibleRefreshScan.CollectDocuments(integerRoot, 60, 2, isIntegerDocument,
            node => 2, integerChildren),
            ScanFailureReason.NODE_LIMIT, ScanOrigin.MSAA);
        Failure(() => AccessibleRefreshScan.CollectDocuments(new Node("no-documents"), 60, 3000, node => false,
            node => 0, (node, count) => Array.Empty<AccessibleRefreshChild<Node>>()),
            ScanFailureReason.READ, ScanOrigin.MSAA);
        var gate = EnabledGate(); var budget = new NodeScanBudget(3000, true, false, false);
        foreach (var error in new[] { depthFailure, repeatedFailure, readFailure })
            Check(!budget.TryRecover(error, false, gate, gate.GetEpoch(), change => { }) && budget.Nodes == 3000,
                "MSAA depth/repeat/read failures do not enlarge node budget");
        var changes = new List<NodeBudgetChange>(); var wide = Wide(7501);
        for (int i = 0; i < 2; i++)
        {
            var error = Failure(() => BackgroundScan(wide, budget.Nodes), ScanFailureReason.NODE_LIMIT, ScanOrigin.MSAA);
            Check(budget.TryRecover(error, false, gate, gate.GetEpoch(), changes.Add), "confirmed MSAA count failure recovers");
        }
        Check(budget.Nodes == 10000 && BackgroundScan(wide, budget.Nodes).Count == 7500
            && changes.All(change => change.Origin == ScanOrigin.MSAA), "MSAA recovery uses full fresh bounded traversal");
    }

    internal static void Run()
    {
        CompleteScanBoundaries();
        RecoveryAndHardCap();
        PartialCandidatesAndRevalidation();
        FailureClassificationAndRecoveryPolicy();
        UnknownOutcomeRemainsDeduplicated();
        BackgroundClassification();
        Console.WriteLine("PASS: production scan/recovery cores: 2999/3000/3001, 5999/6000/6001, 9999/10000/10001; UIA/MSAA complete rescans; partial/second candidates; full revalidation restart; typed failures; explicit/manual/read-only/epoch/commit/timeout/log restrictions; unknown-outcome deduplication.");
    }
}
