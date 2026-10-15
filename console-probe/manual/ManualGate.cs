using System;

internal sealed class ManualGate
{
    bool confirmed, consumed;
    public bool Confirm(string input)
    {
        confirmed = !consumed && string.Equals(input, "INVOKE ONCE", StringComparison.Ordinal);
        return confirmed;
    }
    public void Execute(Func<bool> revalidate, Action invoke)
    {
        if (!confirmed || consumed) throw new InvalidOperationException("Manual confirmation required; no automatic retry.");
        consumed = true; // Any attempt consumes confirmation, including failures/ambiguous outcomes.
        if (!revalidate()) throw new InvalidOperationException("Target changed or became unavailable. Nothing invoked.");
        invoke();
    }
}
