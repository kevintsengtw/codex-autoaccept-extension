static void Check(bool value, string name) { if (!value) throw new Exception(name); }
static RefusalCode Refusal(Action action)
{
    try { action(); } catch (ManualRefusalException e) { return e.Code; }
    throw new Exception("Expected a complete-scan refusal.");
}
static void Walk(Node root, ScanRecovery budget, ScanProgress progress, Action<Node> visit = null)
{ CompleteTreeScan.Run(root, budget, n => n.Id, visit ?? (_ => { }), n => n.Child, n => n.Sibling, progress); }

// A pending candidate is visible at the root before the old scan exceeds its
// budget. Partial reads must not be mistaken for a complete unique candidate.
var root = new Node("pending");
Node tail = root.Child = new Node("1");
for (int i = 2; i <= 7500; i++) tail = tail.Sibling = new Node(i.ToString());
var budget = new ScanRecovery(3000, 60, true, true);
var progress = new ScanProgress();
var gate = new AutoGate(); gate.SetEnabled(true); long epoch = gate.GetEpoch().Value;
int retries = 0, candidateReads = 0, invokes = 0;
while (true)
{
    try
    {
        Walk(root, budget, progress, n => { if (n == root) candidateReads++; });
        Check(progress.Nodes == 7501, "successful scan traverses the whole long tree");
        Walk(root, budget, progress); // Complete revalidation, not a partial-tree fallback.
        if (gate.TryBegin(epoch, "pending")) invokes++;
        break;
    }
    catch (ManualRefusalException e)
    {
        Check(e.Code == RefusalCode.NODE_LIMIT && invokes == 0, "partial long-tree scan never invokes");
        gate.Observe(epoch, true);
        Check(budget.TryGrow(e.Code), "default node limit grows for another complete scan"); retries++;
    }
}
Check(retries == 2 && candidateReads == 3 && budget.Nodes == 10000 && invokes == 1, "3000/6000/10000 recovery handles one pending approval once");
Check(!gate.TryBegin(epoch, "pending") && !budget.TryGrow(RefusalCode.NODE_LIMIT), "no duplicate and no growth beyond cap");
gate.SetEnabled(false); gate.SetEnabled(true); epoch = gate.GetEpoch().Value;
gate.Observe(epoch, true);
Check(!gate.TryBegin(epoch, "pending"), "recovery and OFF/ON cannot clear a consumed attempt");
gate.Observe(epoch, false); Check(!gate.TryBegin(epoch, "next"), "one absence cannot rearm");
gate.Observe(epoch, false); Check(gate.TryBegin(epoch, "next"), "two complete absences rearm");

var chain = new Node("0"); tail = chain;
for (int i = 1; i <= 90; i++) tail = tail.Child = new Node(i.ToString());
var deep = new ScanRecovery(3000, 60, true, true);
Check(Refusal(() => Walk(chain, deep, progress)) == RefusalCode.DEPTH_LIMIT, "depth limit is a distinct refusal");
Check(deep.TryGrow(RefusalCode.DEPTH_LIMIT) && deep.Depth == 80, "depth grows independently");
Check(Refusal(() => Walk(chain, deep, progress)) == RefusalCode.DEPTH_LIMIT && deep.TryGrow(RefusalCode.DEPTH_LIMIT), "second depth rescan grows to 100");
Walk(chain, deep, progress); Check(progress.Nodes == 91 && progress.Depth == 90, "deep tree completes after bounded recovery");
Check(deep.Depth == 100 && !deep.TryGrow(RefusalCode.DEPTH_LIMIT), "depth cap respected");

var fixedBudget = new ScanRecovery(3000, 60, false, false);
Check(Refusal(() => Walk(root, fixedBudget, progress)) == RefusalCode.NODE_LIMIT && !fixedBudget.TryGrow(RefusalCode.NODE_LIMIT), "explicit node budget remains fixed");
Check(!fixedBudget.TryGrow(RefusalCode.DEPTH_LIMIT), "manual and explicit depth budgets remain fixed");
var cycle = new Node("root") { Child = new Node("child") }; cycle.Child.Sibling = cycle.Child;
Check(Refusal(() => Walk(cycle, budget, progress)) == RefusalCode.TREE_REPEAT && !budget.TryGrow(RefusalCode.TREE_REPEAT), "cyclic siblings do not masquerade as a growing long conversation");
var repeated = new Node("same") { Child = new Node("same") };
Check(Refusal(() => Walk(repeated, budget, progress)) == RefusalCode.TREE_REPEAT, "duplicate runtime identity is refused");
var exact = new ScanRecovery(1, 1, false, false);
Walk(new Node("leaf"), exact, progress); Check(progress.Nodes == 1, "exact budget succeeds for a complete leaf");
Check(ScanRecovery.RequiresReselection(RefusalCode.ROOT_CHANGED) && ScanRecovery.RequiresReselection(RefusalCode.TARGET_CHANGED), "changed identities require reselection");
Check(!ScanRecovery.RequiresReselection(RefusalCode.NODE_LIMIT), "scan growth keeps monitoring the original window");

var reports = new FailureReport(); var now = DateTime.UtcNow;
string first = reports.Next(RefusalCode.NODE_LIMIT, 3000, 20, budget, false, false, now);
Check(first.Contains("AUTO_REASON: NODE_LIMIT") && first.Contains("PowerShell") && first.Contains("本輪沒有開始新的 Invoke"), "specific refusal and recovery guidance persist without private text");
Check(reports.Next(RefusalCode.NODE_LIMIT, 3000, 20, budget, false, false, now.AddSeconds(1)) == null, "repeated failures coalesce");
Check(reports.Next(RefusalCode.NODE_LIMIT, 3000, 20, budget, false, false, now.AddSeconds(10)) != null, "ongoing refusal remains observable after ten seconds");
string afterAttempt = reports.Next(RefusalCode.READ, 0, 0, budget, false, true, now);
Check(afterAttempt.Contains("結果可能已生效") && afterAttempt.Contains("不重送"), "attempt errors retain uncertainty");
string changed = reports.Next(RefusalCode.ROOT_CHANGED, 0, 0, budget, false, false, now);
Check(changed.Contains("已切 OFF") && changed.Contains("不需關閉 Codex Desktop"), "root recovery does not require stopping the App");
Console.WriteLine("PASS: production tree walker, 7501-node recovery, depth recovery, partial-scan refusal, explicit caps, cyclic trees, at-most-once gating and persistent diagnostics.");

sealed class Node
{
    public readonly string Id; public Node Child, Sibling;
    public Node(string id) { Id = id; }
}
