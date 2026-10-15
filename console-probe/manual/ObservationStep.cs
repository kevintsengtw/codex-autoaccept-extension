using System;

// Only original-button read failures can prove original-button removal.
// A stale window root must leave the attempted approval in its unknown state.
internal static class ObservationStep
{
    public static string ReadOnce<T, TUnavailable>(bool requireRemoval,
        Func<bool> identityChanged, Func<T> readCurrent, Func<T, bool> readOffscreen,
        Func<bool> readVisibleWindow, Func<T, bool> readUnavailable) where TUnavailable : Exception
    {
        T current;
        try
        {
            if (identityChanged()) return "ELEMENT_CHANGED; APPROVAL_OUTCOME_UNKNOWN";
            current = readCurrent();
        }
        catch (Exception e) when (e is TUnavailable)
        { return requireRemoval ? "ORIGINAL_ELEMENT_REMOVED" : "BUTTON_UNAVAILABLE_OR_HIDDEN"; }
        if (requireRemoval)
        {
            bool offscreen;
            try { offscreen = readOffscreen(current); }
            catch (Exception e) when (e is TUnavailable) { return "ORIGINAL_ELEMENT_REMOVED"; }
            // This root read is intentionally outside the candidate catch.
            if (offscreen && readVisibleWindow()) return "ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW";
        }
        else
        {
            try { if (readUnavailable(current)) return "BUTTON_UNAVAILABLE_OR_HIDDEN"; }
            catch (Exception e) when (e is TUnavailable) { return "BUTTON_UNAVAILABLE_OR_HIDDEN"; }
        }
        return null;
    }
}
