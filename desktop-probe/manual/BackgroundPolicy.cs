using System;
internal struct ScreenBox
{
    public double X, Y, Width, Height;
    public ScreenBox(double x, double y, double w, double h) { X=x; Y=y; Width=w; Height=h; }
    public bool Valid => Finite(X) && Finite(Y) && Finite(Width) && Finite(Height) && Width > 0 && Height > 0;
    static bool Finite(double v) { return !double.IsNaN(v) && !double.IsInfinity(v); }
    public bool ContainsCenter(ScreenBox child) => Valid && child.Valid && child.X + child.Width/2 >= X
        && child.X + child.Width/2 < X+Width && child.Y+child.Height/2 >= Y && child.Y+child.Height/2 < Y+Height;
}
internal sealed class WindowSnapshot
{
    public bool Visible, Minimized, Maximized, Cloaked, NativeKnown, HasForeground, Foreground, RootOffscreen, RootOffscreenKnown, ModalBlocked;
    public ScreenBox Frame;
}
internal static class BackgroundPolicy
{
    public static bool CanScan(WindowSnapshot w) => w.NativeKnown && w.Visible && !w.Cloaked && !w.ModalBlocked
        && w.HasForeground && (w.Minimized ? !w.Foreground : w.Frame.Valid && (!w.RootOffscreen || !w.Foreground));
    public static bool Eligible(WindowSnapshot w, bool enabled, bool invoke, bool offscreen, ScreenBox bounds)
        // Minimized providers may publish empty/off-desktop bounds. Selection then relies on the
        // complete target-root traversal and fresh ancestry/runtime-ID/context checks in the caller.
        => CanScan(w) && enabled && invoke && (w.Minimized || w.Frame.ContainsCenter(bounds))
        && ((!offscreen && !w.RootOffscreen) || !w.Foreground);
    public static bool EligibleFromProperties(WindowSnapshot w, bool enabled, bool invoke, Func<bool> offscreen, Func<ScreenBox> bounds)
    { string ignored; return EligibleFromProperties(w, enabled, invoke, offscreen, bounds, out ignored); }
    public static bool EligibleFromProperties(WindowSnapshot w, bool enabled, bool invoke, Func<bool> offscreen, Func<ScreenBox> bounds, out string reason)
    {
        reason="NONE";
        if (!CanScan(w)) { reason="WINDOW_BLOCKED"; return false; }
        if (!enabled) { reason="DISABLED"; return false; }
        if (!invoke) { reason="NO_INVOKE"; return false; }
        if (w.Minimized) return true; // Do not even query provider screen properties in this branch.
        var box = bounds();
        bool hidden = offscreen();
        if (!box.Valid) { reason="EMPTY_BOUNDS"; return false; }
        if (!w.Frame.ContainsCenter(box)) { reason="OUTSIDE_FRAME"; return false; }
        if (!((!hidden && !w.RootOffscreen) || !w.Foreground)) { reason="OFFSCREEN"; return false; }
        return true;
    }
    public static bool ProvesAbsence(WindowSnapshot w, int nodes) => CanScan(w) && !w.Minimized && !w.RootOffscreen && nodes > 1;
    public static bool ProvesHiddenEnd(WindowSnapshot w, bool offscreen) => CanScan(w) && w.Foreground && !w.RootOffscreen && offscreen;
    public static bool NewCycle(bool complete, bool previousPresent, bool newEligibleCandidate)
        => complete && !previousPresent && newEligibleCandidate;
}
