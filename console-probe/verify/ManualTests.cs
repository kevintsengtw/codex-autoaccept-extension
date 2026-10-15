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
