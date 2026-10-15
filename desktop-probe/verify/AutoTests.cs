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

static WindowSnapshot ObservationWindow() => new WindowSnapshot
{
    Visible=true, Minimized=false, Maximized=false, Cloaked=false, NativeKnown=true,
    HasForeground=true, Foreground=true, RootOffscreen=false, RootOffscreenKnown=true,
    ModalBlocked=false, Frame=new ScreenBox(0,0,1280,820)
};
var pendingGate=new AutoGate(); pendingGate.SetEnabled(true); long pendingEpoch=pendingGate.GetEpoch().Value;
Check(pendingGate.TryBegin(pendingEpoch,"pending"),"prepare one unknown committed candidate");
pendingGate.Observe(pendingEpoch,false); // One prior absence cannot complete the cycle.
try
{
    ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
        ()=>false,()=>new object(),()=>throw new ObservationUnavailableException(),value=>false,value=>false);
    throw new Exception("Root failure must propagate");
}
catch(ObservationUnavailableException) { pendingGate.Observe(pendingEpoch,true); }
Check(pendingGate.Enabled && pendingGate.AwaitingAbsence && !pendingGate.TryBegin(pendingEpoch,"pending")
    && !pendingGate.TryBegin(pendingEpoch,"other"),"root error does not complete or repeat the committed approval cycle");
pendingGate.Observe(pendingEpoch,false);
Check(pendingGate.AwaitingAbsence,"root error resets a partial absence streak instead of advancing it");
pendingGate.SetEnabled(false); pendingGate.SetEnabled(true); long resumedEpoch=pendingGate.GetEpoch().Value;
Check(!pendingGate.TryBegin(pendingEpoch,"pending") && !pendingGate.TryBegin(resumedEpoch,"pending")
    && pendingGate.AwaitingAbsence,"OFF revokes the epoch and OFF/ON cannot release an unknown root-error candidate");
string stillPending=ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>false,()=>new object(),ObservationWindow,value=>false,value=>false);
Check(stillPending==null && pendingGate.AwaitingAbsence && !pendingGate.TryBegin(resumedEpoch,"pending"),
    "readable still-present candidate after root recovery remains deduplicated");
string removedCandidate=ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>false,()=>throw new ObservationUnavailableException(),ObservationWindow,value=>false,value=>false);
Check(removedCandidate=="ORIGINAL_ELEMENT_REMOVED","original candidate unavailability still proves the permitted boundary");
pendingGate.Completed();
Check(pendingGate.Enabled && pendingGate.TryBegin(resumedEpoch,"new-cycle") && !pendingGate.TryBegin(resumedEpoch,"new-cycle"),
    "candidate-only completion permits the next cycle once and keeps ON");

var concurrentOff=new AutoGate(); concurrentOff.SetEnabled(true); long beforeOff=concurrentOff.GetEpoch().Value;
Check(concurrentOff.TryBegin(beforeOff,"uncertain"),"prepare candidate before OFF during root read");
try
{
    ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
        ()=>false,()=>new object(),()=>{concurrentOff.SetEnabled(false);throw new ObservationUnavailableException();},
        value=>false,value=>false);
    throw new Exception("Root failure after OFF must propagate");
}
catch(ObservationUnavailableException) { concurrentOff.Observe(beforeOff,true); }
Check(!concurrentOff.Enabled && concurrentOff.AwaitingAbsence && !concurrentOff.TryBegin(beforeOff,"uncertain"),
    "root error cannot undo OFF or release its committed candidate");
concurrentOff.SetEnabled(true);
Check(!concurrentOff.TryBegin(concurrentOff.GetEpoch().Value,"uncertain"),
    "a later ON cannot retry the same uncertain candidate after OFF during a root read");
Console.WriteLine("PASS: shared observation step plus production auto gate (unknown root result, no repeat, absence reset, OFF epochs and candidate-only completion).");

var focusSteps = new List<string>();
bool implicitFocus = true;
FocusPolicy.Configure(() => { focusSteps.Add("disable"); implicitFocus = false; },
    () => { focusSteps.Add("readback"); return implicitFocus; });
Check(focusSteps.SequenceEqual(new[] { "disable", "readback" }), "disable focus before checking actual client value");
int focusActions = 0;
FocusPolicy.RequireDisabled(() => implicitFocus); focusActions++;
foreach (var readFocus in new Func<bool>[] { () => true, () => throw new IOException("client read failed") })
{
    bool refused = false;
    try { FocusPolicy.RequireDisabled(readFocus); focusActions++; }
    catch (Exception e) when (e is InvalidOperationException || e is IOException) { refused = true; }
    Check(refused && focusActions == 1, "changed or unreadable focus policy must stop before action");
}
bool configurationRefused = false;
try { FocusPolicy.Configure(() => { }, () => true); }
catch (InvalidOperationException) { configurationRefused = true; }
Check(configurationRefused, "a successful setter without a false readback is not sufficient");
bool readAfterFailedSet = false;
try { FocusPolicy.Configure(() => throw new IOException("set failed"), () => { readAfterFailedSet = true; return false; }); }
catch (IOException) { }
Check(!readAfterFailedSet, "failed focus setup propagates without declaring readiness");
Console.WriteLine("PASS: automatic-focus policy fails closed for failed setup, true readback, changed policy and unreadable policy.");

sealed class ObservationUnavailableException : Exception { }
