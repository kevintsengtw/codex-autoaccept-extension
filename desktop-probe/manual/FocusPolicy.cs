using System;

// The client setting must be verified at startup and immediately before Invoke.
// A failed read is not evidence that implicit focus changes are disabled.
internal static class FocusPolicy
{
    public static void Configure(Action disable, Func<bool> readEnabled)
    {
        disable();
        RequireDisabled(readEnabled);
    }

    public static void RequireDisabled(Func<bool> readEnabled)
    {
        if (readEnabled()) throw new InvalidOperationException("UIA automatic focus is not disabled.");
    }
}
