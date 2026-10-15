static void Check(bool value, string test) { if (!value) throw new Exception(test); }
static void Refuses(Action action) { try { action(); throw new Exception("Expected refusal"); } catch (InvalidOperationException) { } }
int calls = 0;
var unconfirmed = new ManualGate();
Refuses(() => unconfirmed.Execute(() => true, () => calls++));
Check(calls == 0, "unconfirmed must not invoke");
foreach (string input in new[] { "", "yes", "invoke once", "INVOKE ONCE ", null })
{
    var cancelled = new ManualGate(); Check(!cancelled.Confirm(input), "requires exact typed confirmation");
    Refuses(() => cancelled.Execute(() => true, () => calls++));
}
Check(calls == 0, "cancelled must not invoke");
var success = new ManualGate(); Check(success.Confirm("INVOKE ONCE"), "confirm");
success.Execute(() => true, () => calls++);
Refuses(() => success.Execute(() => true, () => calls++));
Check(calls == 1 && !success.Confirm("INVOKE ONCE"), "exactly once even after reconfirm");
var stale = new ManualGate(); stale.Confirm("INVOKE ONCE");
Refuses(() => stale.Execute(() => false, () => calls++));
Check(calls == 1 && !stale.Confirm("INVOKE ONCE"), "changed target must not invoke/retry");
var error = new ManualGate(); error.Confirm("INVOKE ONCE");
Refuses(() => error.Execute(() => true, () => { calls++; throw new InvalidOperationException("ambiguous provider failure"); }));
Refuses(() => error.Execute(() => true, () => calls++));
Check(calls == 2, "failed invocation must not retry");
var validateError = new ManualGate(); validateError.Confirm("INVOKE ONCE");
Refuses(() => validateError.Execute(() => throw new InvalidOperationException(), () => calls++));
Check(calls == 2 && !validateError.Confirm("INVOKE ONCE"), "validation exception consumes confirmation");
Console.WriteLine("PASS: 6 manual-gate scenarios: unconfirmed, cancellation, exact one call, stale target, provider failure, validation failure.");

static WindowSnapshot ObservationWindow(bool foreground=true, bool minimized=false) => new WindowSnapshot
{
    Visible=true, Minimized=minimized, Maximized=false, Cloaked=false, NativeKnown=true,
    HasForeground=true, Foreground=foreground, RootOffscreen=false, RootOffscreenKnown=true,
    ModalBlocked=false, Frame=new ScreenBox(0,0,1280,820)
};
static void Propagates(Exception expected, Func<string> read, string name)
{
    try { read(); }
    catch(Exception error) { Check(ReferenceEquals(error,expected),name); return; }
    throw new Exception("Expected provider error: "+name);
}
var reads=new List<string>();
var current=new object();
string available=ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>{reads.Add("identity");return false;},()=>{reads.Add("current");return current;},
    ()=>{reads.Add("root");return ObservationWindow();},value=>{reads.Add("offscreen");return false;},
    value=>throw new Exception("Auto observation must not read manual properties"));
Check(available==null && reads.SequenceEqual(new[]{"identity","current","root","offscreen"}),
    "shared production observation preserves read order and null means continue the caller's deadline loop");
foreach(string phase in new[]{"root","root-offscreen","window-pattern"})
{
    var failure=new ObservationUnavailableException();
    bool candidateScreenRead=false;
    Propagates(failure,()=>ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
        ()=>false,()=>current,()=>throw failure,
        value=>{candidateScreenRead=true;return false;},value=>false),
        "root/snapshot unavailable propagates without candidate-removal classification: "+phase);
    Check(!candidateScreenRead,"root failure stops before reading candidate screen properties");
}
for(int failureAt=0;failureAt<3;failureAt++)
{
    int candidateStage=failureAt, rootReads=0;
    string removed=ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
        ()=>{if(candidateStage==0) throw new ObservationUnavailableException();return false;},
        ()=>{if(candidateStage==1) throw new ObservationUnavailableException();return current;},
        ()=>{rootReads++;return ObservationWindow();},
        value=>{if(candidateStage==2) throw new ObservationUnavailableException();return false;},value=>false);
    Check(removed=="ORIGINAL_ELEMENT_REMOVED" && rootReads==(candidateStage==2?1:0),
        "only original candidate identity/current/offscreen unavailability proves removal");
}
var unrelated=new InvalidOperationException("Different provider failure");
Propagates(unrelated,()=>ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>false,()=>throw unrelated,()=>ObservationWindow(),value=>false,value=>false),
    "other candidate errors must not be swallowed as candidate removal");
Check(ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>true,()=>throw new Exception("Must not read changed candidate"),
    ()=>throw new Exception("Must not read root after changed identity"),value=>false,value=>false)
    =="ELEMENT_CHANGED; APPROVAL_OUTCOME_UNKNOWN","changed identity remains unknown and short-circuits reads");
int minimizedScreenReads=0;
Check(ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>false,()=>current,()=>ObservationWindow(false,true),
    value=>{minimizedScreenReads++;throw new ObservationUnavailableException();},value=>false)==null
    && minimizedScreenReads==0,"minimized provider screen properties remain unread");
Check(ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>false,()=>current,()=>ObservationWindow(),value=>true,value=>false)
    =="ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW","foreground hidden-end proof remains available");
Check(ObservationStep.ReadOnce<object,ObservationUnavailableException>(true,
    ()=>false,()=>current,()=>ObservationWindow(false),value=>true,value=>false)==null,
    "background offscreen hint cannot establish a completed approval cycle");
reads.Clear();
Check(ObservationStep.ReadOnce<object,ObservationUnavailableException>(false,
    ()=>{reads.Add("identity");return false;},()=>{reads.Add("current");return current;},
    ()=>throw new Exception("Manual observation must not read root"),
    value=>throw new Exception("Manual observation must use its original property chain"),
    value=>{reads.Add("manual-properties");return true;})=="BUTTON_UNAVAILABLE_OR_HIDDEN"
    && reads.SequenceEqual(new[]{"identity","current","manual-properties"}),
    "manual unavailable properties keep the original token and read sequence without a root query");
Check(ObservationStep.ReadOnce<object,ObservationUnavailableException>(false,
    ()=>false,()=>current,()=>throw new Exception("Manual root must remain unread"),value=>false,
    value=>throw new ObservationUnavailableException())=="BUTTON_UNAVAILABLE_OR_HIDDEN",
    "manual candidate unavailable exception keeps the original token");
Propagates(unrelated,()=>ObservationStep.ReadOnce<object,ObservationUnavailableException>(false,
    ()=>false,()=>current,()=>ObservationWindow(),value=>false,value=>throw unrelated),
    "manual non-unavailable provider errors still propagate");
Console.WriteLine("PASS: shared production observation-step regression (read order, candidate-only removal, root errors, other errors, minimized short-circuit, hidden-end and manual tokens).");

sealed class ObservationUnavailableException : Exception { }
