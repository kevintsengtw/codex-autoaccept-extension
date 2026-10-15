using System;
using System.Collections.Generic;

// OFF revokes outstanding scan epochs. A committed invocation is never retried until
// a verified disappearance, or two complete visible-window scans with no approval label.
internal sealed class AutoGate
{
    readonly object sync = new object();
    bool enabled, running = true, awaitingAbsence;
    readonly HashSet<string> consumed = new HashSet<string>(StringComparer.Ordinal);
    long epoch;
    int absent;
    public bool Enabled { get { lock (sync) return enabled; } }
    public bool AwaitingAbsence { get { lock (sync) return awaitingAbsence; } }
    public bool Running { get { lock (sync) return running; } }
    public void SetEnabled(bool value) { lock (sync) { enabled = running && value; epoch++; absent = 0; } }
    public void Stop() { lock (sync) { running = false; enabled = false; epoch++; } }
    public long? GetEpoch() { lock (sync) return enabled && running ? (long?)epoch : null; }
    public void Observe(long scanEpoch, bool present)
    {
        lock (sync)
        {
            if (!enabled || !running || scanEpoch != epoch) return;
            if (present) absent = 0;
            else if (++absent >= 2) { awaitingAbsence = false; consumed.Clear(); }
        }
    }
    public void Completed() { lock (sync) { awaitingAbsence = false; absent = 0; consumed.Clear(); } }
    public bool TryBegin(long scanEpoch, string requestKey)
    {
        lock (sync)
        {
            if (!running || !enabled || epoch != scanEpoch || awaitingAbsence || consumed.Contains(requestKey)) return false;
            if (consumed.Count >= 10000) throw new InvalidOperationException("Session attempt budget reached; restart after review.");
            consumed.Add(requestKey);
            awaitingAbsence = true; absent = 0; return true;
        }
    }
}
