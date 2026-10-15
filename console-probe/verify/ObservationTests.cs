static void Check(bool value, string name) { if (!value) throw new Exception(name); }
var gate = new AutoGate(); gate.SetEnabled(true); long epoch = gate.GetEpoch().Value;
Check(gate.TryBegin(epoch, "pending"), "attempt committed before observation");
bool propagated = false;
try
{
    ObservationStep.ReadOnce<int, MissingElement>(true, () => false, () => 1,
        _ => true, () => throw new MissingElement(), _ => false);
    gate.Completed(); // The old broad catch would wrongly allow this path.
}
catch (MissingElement) { propagated = true; }
Check(propagated && gate.AwaitingAbsence && !gate.TryBegin(epoch, "pending"), "root failure cannot clear unknown attempt");
gate.SetEnabled(false); gate.SetEnabled(true); epoch = gate.GetEpoch().Value;
Check(!gate.TryBegin(epoch, "pending"), "root failure remains consumed across OFF/ON");
string missing = ObservationStep.ReadOnce<int, MissingElement>(true, () => false,
    () => throw new MissingElement(), _ => true, () => true, _ => false);
Check(missing == "ORIGINAL_ELEMENT_REMOVED", "only candidate-current failure proves removal");
Check(ObservationStep.ReadOnce<int, MissingElement>(true, () => true, () => 1,
    _ => true, () => true, _ => false).StartsWith("ELEMENT_CHANGED"), "identity change keeps outcome unknown");
int rootReads = 0;
Check(ObservationStep.ReadOnce<int, MissingElement>(true, () => false, () => 1,
    _ => false, () => { rootReads++; return true; }, _ => false) == null && rootReads == 0, "onscreen candidate retains original short-circuit order");
Check(ObservationStep.ReadOnce<int, MissingElement>(true, () => false, () => 1,
    _ => true, () => false, _ => false) == null, "minimized/hidden App is not a candidate departure");
Check(ObservationStep.ReadOnce<int, MissingElement>(true, () => false, () => 1,
    _ => true, () => true, _ => false) == "ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW", "hidden button in visible App proves departure");
Check(ObservationStep.ReadOnce<int, MissingElement>(false, () => false, () => 1,
    _ => false, () => throw new Exception("must not read root"), _ => true) == "BUTTON_UNAVAILABLE_OR_HIDDEN", "manual unavailable behavior preserved");
bool other = false;
try { ObservationStep.ReadOnce<int, MissingElement>(true, () => false, () => throw new InvalidOperationException(), _ => true, () => true, _ => false); }
catch (InvalidOperationException) { other = true; }
Check(other, "unrelated provider failure is never relabeled candidate removal");
Console.WriteLine("PASS: root/candidate failure isolation, unknown-result deduplication, read order and original manual observation behavior.");
sealed class MissingElement : Exception { }
