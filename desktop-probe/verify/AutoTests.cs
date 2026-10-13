static void Check(bool b, string s) { if (!b) throw new Exception(s); }
var gate = new AutoGate();
Check(!gate.Enabled && gate.GetEpoch() == null && !gate.TryBegin(0, "a"), "defaults OFF");
gate.SetEnabled(true); long first = gate.GetEpoch().Value;
gate.SetEnabled(false); Check(!gate.TryBegin(first, "a"), "OFF revokes pending scan");
gate.SetEnabled(true); long second = gate.GetEpoch().Value;
Check(!gate.TryBegin(first, "a") && gate.TryBegin(second, "a"), "new ON rejects previous epoch");
Check(!gate.TryBegin(second, "a") && !gate.TryBegin(second, "b"), "single commit until approval leaves");
Check(gate.Enabled, "invocation must keep ON");
gate.SetEnabled(false); gate.SetEnabled(true); long third = gate.GetEpoch().Value;
Check(!gate.TryBegin(third, "a"), "OFF ON alone does not clear consumed attempt");
gate.Observe(third, false); Check(!gate.TryBegin(third, "b"), "one absence isn't enough");
gate.Observe(third, true); gate.Observe(third, false);
Check(!gate.TryBegin(third, "b"), "visible label including disabled resets absence count");
gate.Observe(second, false); Check(!gate.TryBegin(third, "b"), "old scan cannot rearm");
gate.Observe(third, false);
Check(gate.Enabled && gate.TryBegin(third, "a"), "same UI identity can be reused for a new approval after verified absence");
Check(!gate.TryBegin(third, "a"), "reused identity cannot double invoke in same cycle");
for (int i = 0; i < 20; i++)
{
    gate.Completed();
    Check(gate.Enabled && gate.TryBegin(third, "a"), "success keeps ON and accepts successive reused identities");
    Check(!gate.TryBegin(third, "a"), "successive requests invoke only once each");
}
gate.Observe(third, true);
Check(gate.Enabled && !gate.TryBegin(third, "b"), "waiting with no departure keeps ON and blocks retries");
gate.Stop(); Check(!gate.Enabled && !gate.Running && !gate.TryBegin(third, "c"), "quit revokes work");
gate.SetEnabled(true); Check(!gate.Enabled, "quit cannot re-enable");
var race = new AutoGate(); race.SetEnabled(true); long epoch = race.GetEpoch().Value;
int commits = 0;
Parallel.For(0, 100, i => { if (race.TryBegin(epoch, "race")) Interlocked.Increment(ref commits); });
Check(commits == 1, "concurrent workers commit at most once");
Console.WriteLine("PASS: auto lifecycle incl. 20 consecutive approvals with reused UI identity, ON persistence, OFF revocation, no duplicate calls, waiting and concurrency.");
var observer = new AutoGate(true);
for (int i=0;i<10;i++) { observer.SetEnabled(true); observer.Completed(); observer.Observe(i,false); }
Check(observer.Running && !observer.Enabled && observer.GetEpoch()==null && !observer.TryBegin(10,"approval"),"read-only backend cannot acquire an invocation epoch even after ON commands");
observer.Stop(); Check(!observer.Running && !observer.TryBegin(10,"approval"),"read-only quit stops monitoring");
Console.WriteLine("PASS: permanently OFF observation gate.");
