using System;

// One provider-read iteration; null means keep observing until the caller's
// deadline. Production fixes TUnavailable to UIA ElementNotAvailableException.
// This class has no invocation/gate logic and performs no reads on its own.
internal static class ObservationStep
{
    public static string ReadOnce<TCurrent, TUnavailable>(bool requireRemoval,
        Func<bool> candidateIdentityChanged, Func<TCurrent> readCandidateCurrent,
        Func<WindowSnapshot> readRootSnapshot, Func<TCurrent, bool> readCandidateOffscreen,
        Func<TCurrent, bool> readCandidateUnavailable) where TUnavailable : Exception
    {
        TCurrent current;
        try
        {
            if (candidateIdentityChanged()) return "ELEMENT_CHANGED; APPROVAL_OUTCOME_UNKNOWN";
            current = readCandidateCurrent();
        }
        catch (Exception error) when (error is TUnavailable)
        { return requireRemoval ? "ORIGINAL_ELEMENT_REMOVED" : "BUTTON_UNAVAILABLE_OR_HIDDEN"; }

        if (requireRemoval)
        {
            // Root/WindowPattern failure does not prove original-button removal.
            // Keep it outside candidate catches so the caller preserves UNKNOWN.
            var window = readRootSnapshot();
            try
            {
                if (!window.Minimized && BackgroundPolicy.ProvesHiddenEnd(window, readCandidateOffscreen(current)))
                    return "ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW";
            }
            catch (Exception error) when (error is TUnavailable) { return "ORIGINAL_ELEMENT_REMOVED"; }
        }
        else
        {
            try { if (readCandidateUnavailable(current)) return "BUTTON_UNAVAILABLE_OR_HIDDEN"; }
            catch (Exception error) when (error is TUnavailable) { return "BUTTON_UNAVAILABLE_OR_HIDDEN"; }
        }
        return null;
    }
}
