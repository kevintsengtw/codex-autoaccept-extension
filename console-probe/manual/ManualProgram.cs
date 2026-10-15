using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Automation;

internal static class ManualProgram
{
    sealed class Candidate
    {
        public AutomationElement Element;
        public string Id, ParentId, ContextHash;
        public string[] Context;
    }
    static IntPtr hwnd;
    static int targetPid, depth = 60, nodes = 3000, timeout = 45;
    static long processStarted;
    static string rootId, log;
    static volatile bool invocationStarted;
    static bool auto;
    static bool lastScanVisible, lastScanApprovalPresent;
    static readonly AutoGate autoGate = new AutoGate();
    static ScanRecovery scanBudget;
    static readonly ScanProgress scanProgress = new ScanProgress();
    static readonly FailureReport failureReport = new FailureReport();
    static readonly object logSync = new object();
    static int Main(string[] args)
    {
        Console.OutputEncoding = new UTF8Encoding(false);
        try
        {
            bool fixedNodes = false, fixedDepth = false;
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (key == "--grow-nodes") { fixedNodes = false; continue; }
                if (key == "--grow-depth") { fixedDepth = false; continue; }
                if (key == "--auto") { auto = true; continue; }
                if (++i >= args.Length) throw new ArgumentException("Missing value.");
                int n;
                switch (key)
                {
                    case "--hwnd": hwnd = new IntPtr(long.Parse(args[i], CultureInfo.InvariantCulture)); break;
                    case "--max-depth": n = int.Parse(args[i]); if (n < 1 || n > 100) throw new ArgumentException("Invalid depth."); depth = n; fixedDepth = true; break;
                    case "--max-nodes": n = int.Parse(args[i]); if (n < 1 || n > 10000) throw new ArgumentException("Invalid node budget."); nodes = n; fixedNodes = true; break;
                    case "--timeout": n = int.Parse(args[i]); if (n < 5 || n > 120) throw new ArgumentException("Invalid timeout."); timeout = n; break;
                    default: throw new ArgumentException("Unknown option.");
                }
            }
            scanBudget = new ScanRecovery(nodes, depth, auto && !fixedNodes, auto && !fixedDepth);
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) throw new ArgumentException("A live target HWND is required. Start via Run-Manual-Once.cmd.");
            uint pid; GetWindowThreadProcessId(hwnd, out pid); targetPid = (int)pid;
            string probeLevel = Integrity(Process.GetCurrentProcess().Id), targetLevel = Integrity(targetPid);
            Console.WriteLine("INTEGRITY: probe={0}; target={1}", probeLevel, targetLevel);
            if (probeLevel != "medium" || targetLevel != "medium")
                throw new ManualRefusalException(RefusalCode.INTEGRITY);
            using (var process = Process.GetProcessById(targetPid)) processStarted = process.StartTime.ToUniversalTime().Ticks;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUiaProbe");
            Directory.CreateDirectory(directory);
            log = Path.Combine(directory, (auto ? "auto-" : "manual-") + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
            Status(auto ? "MODE: AUTO ACCEPT; starts OFF." : "MODE: MANUAL SINGLE INVOKE; no automated approvals or retries.");
            Console.WriteLine("Visible context is shown locally only. Log records statuses, not UI text.");
            var title = new StringBuilder(2048); GetWindowText(hwnd, title, title.Capacity);
            Console.WriteLine("Target HWND=0x{0:X}, PID={1}, title={2}", hwnd.ToInt64(), targetPid, Line(title.ToString()));
            if (auto) return RunAuto();
            var candidate = Timed(() => Scan(), "initial scan");
            Console.WriteLine("Exact button label: 允許一次; enabled, onscreen, Invoke available.");
            Console.WriteLine("Immediate-parent context (partial view; review the pending operation in ChatGPT itself):");
            foreach (string line in candidate.Context) Console.WriteLine("  " + line);
            Console.WriteLine("This will approve the currently displayed operation once. Confirm its command in ChatGPT before continuing.");
            Console.Write("Type INVOKE ONCE exactly to confirm, or Enter/anything else to cancel: ");
            var gate = new ManualGate();
            if (!gate.Confirm(Console.ReadLine())) { Status("RESULT: CANCELLED; nothing invoked."); return 0; }
            var fresh = Timed(() => Scan(), "revalidation scan");
            bool unchanged = Same(candidate, fresh);
            if (!unchanged) { Status("RESULT: TARGET_CHANGED; nothing invoked. Rerun and review again."); return 4; }
            Timed(() =>
            {
                gate.Execute(() => ValidWindow() && Id(AutomationElement.FromHandle(hwnd)) == rootId && Exact(fresh.Element) && Id(fresh.Element) == candidate.Id
                    && ParentId(fresh.Element) == candidate.ParentId && Context(fresh.Element).Item1 == candidate.ContextHash,
                    () =>
                    {
                        object pattern;
                        if (!fresh.Element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern))
                            throw new ManualRefusalException(RefusalCode.INVOKE_UNAVAILABLE);
                        // Persist attempt BEFORE the one call. A hang/error can mean the provider still acted.
                        Status("ATTEMPT: INVOKE_ONCE; user confirmed.");
                        invocationStarted = true;
                        InvokeOnce((InvokePattern)pattern);
                    });
                return true;
            }, "single invocation");
            Status("INVOKE: RETURNED; this alone does not prove approval succeeded.");
            string observed = Timed(() => Observe(fresh), "post-invoke observation");
            Status("OBSERVATION: " + observed);
            Console.WriteLine("Check ChatGPT: did the approval disappear and the operation continue? Do not retry automatically.");
            Console.WriteLine("Status log (no UI content): " + log);
            return observed == "BUTTON_UNAVAILABLE_OR_HIDDEN" ? 0 : 5;
        }
        catch (Exception e)
        {
            Console.WriteLine(invocationStarted ? "RESULT: UNKNOWN_AFTER_ATTEMPT; check ChatGPT before any new test. NO RETRY." : "RESULT: NOT_INVOKED.");
            try { Status("ERROR_TYPE: " + e.GetType().Name); } catch { Console.WriteLine("Status log unavailable."); }
            // Exception messages can contain UI information; only print the type and our own fixed guidance.
            var cause = e.GetBaseException();
            Console.WriteLine("Error type: " + cause.GetType().Name);
            if (cause is ManualRefusalException)
            {
                var refusal = (ManualRefusalException)cause;
                Console.WriteLine("REASON: " + refusal.Code + "; " + ScanRecovery.Guidance(refusal.Code, false));
                try { Status("REASON: " + refusal.Code); } catch { }
            }
            Console.WriteLine(invocationStarted ? "Provider may have acted even if it reported an error." : "Requires exactly one exact-label button, a complete scan and an unchanged target/context.");
            return invocationStarted ? 5 : 4;
        }
    }
    static void InvokeOnce(InvokePattern pattern) { pattern.Invoke(); } // Only mutation API; no retries.
    static int RunAuto()
    {
        if (Console.IsInputRedirected) throw new ManualRefusalException(RefusalCode.INTERACTIVE_REQUIRED);
        Console.WriteLine("O=ON, F=OFF, Space=toggle, Q=quit. Keys apply only while this terminal has focus.");
        Console.WriteLine("ON approves the exact usable 允許一次 button in the selected window. It does not classify commands or request types.");
        Console.WriteLine("OFF stops new attempts; an already committed Invoke may complete. Switching ChatGPT chats changes what this window displays.");
        Console.WriteLine("STATUS: OFF. Keep this terminal open. Status log contains no UI content: " + log);
        var loop = Task.Run((Action)AutoLoop);
        while (autoGate.Running)
        {
            if (loop.IsCompleted) { autoGate.Stop(); break; }
            if (!Console.KeyAvailable) { Thread.Sleep(50); continue; }
            var key = Console.ReadKey(true).Key;
            if (key == ConsoleKey.Q) { autoGate.Stop(); Status("STATUS: OFF; QUIT. A committed attempt cannot be undone."); break; }
            if (key == ConsoleKey.O || key == ConsoleKey.F || key == ConsoleKey.Spacebar)
            {
                bool on = key == ConsoleKey.O || key == ConsoleKey.Spacebar && !autoGate.Enabled;
                autoGate.SetEnabled(on);
                try { Status(on ? "STATUS: ON" : "STATUS: OFF"); }
                catch { autoGate.SetEnabled(false); Console.WriteLine("STATUS: OFF; log unavailable."); }
            }
        }
        // A quit terminates this helper and its background tasks. Submitted provider actions may still complete.
        return 0;
    }
    static void AutoLoop()
    {
        while (autoGate.Running)
        {
            long? epoch = autoGate.GetEpoch();
            if (epoch == null) { Thread.Sleep(100); continue; }
            bool committedThisIteration = false;
            try
            {
                var candidate = Timed(() => Scan(false), "auto scan");
                autoGate.Observe(epoch.Value, !lastScanVisible || lastScanApprovalPresent);
                if (candidate == null || autoGate.AwaitingAbsence) { Thread.Sleep(1500); continue; }
                var fresh = Timed(() => Scan(false), "auto revalidation");
                if (fresh == null || !Same(candidate, fresh)) { Thread.Sleep(1500); continue; }
                Timed(() =>
                {
                    if (!ValidWindow() || Id(AutomationElement.FromHandle(hwnd)) != rootId || !Exact(fresh.Element)
                        || Id(fresh.Element) != candidate.Id || ParentId(fresh.Element) != candidate.ParentId
                        || Context(fresh.Element).Item1 != candidate.ContextHash) return false;
                    object pattern;
                    if (!fresh.Element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) return false;
                    // Log must succeed before committing. OFF / a fresh ON invalidates the scan epoch.
                    lock (logSync)
                    {
                        if (autoGate.GetEpoch() != epoch) return false;
                        if (!autoGate.TryBegin(epoch.Value, candidate.Id + ":" + candidate.ParentId + ":" + candidate.ContextHash)) return false;
                        committedThisIteration = true;
                        Status("ATTEMPT: AUTO_INVOKE_ONCE");
                        invocationStarted = true;
                    }
                    InvokeOnce((InvokePattern)pattern);
                    Status("INVOKE: RETURNED");
                    string observation = Observe(fresh, true);
                    Status("OBSERVATION: " + observation);
                    if (observation == "ORIGINAL_ELEMENT_REMOVED" || observation == "ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW")
                    {
                        autoGate.Completed();
                        Status("AUTO: CONTINUING; switch stays ON unless you turn it OFF.");
                    }
                    else Status("AUTO: WAITING FOR PREVIOUS APPROVAL TO LEAVE; switch unchanged; no duplicate click.");
                    return true;
                }, "single invocation");
                Thread.Sleep(1500);
            }
            catch (Exception e)
            {
                autoGate.Observe(epoch.Value, true); // Incomplete scans cannot prove absence or advance a new approval cycle.
                try
                {
                    var refusal = e.GetBaseException() as ManualRefusalException;
                    var code = refusal == null ? RefusalCode.READ : refusal.Code;
                    bool expanded = !committedThisIteration && scanBudget.TryGrow(code);
                    if (ScanRecovery.RequiresReselection(code)) { autoGate.SetEnabled(false); Status("STATUS: OFF"); }
                    string report = failureReport.Next(code, scanProgress.Nodes, scanProgress.Depth, scanBudget,
                        expanded, committedThisIteration, DateTime.UtcNow);
                    if (report != null) Status(report);
                }
                catch { autoGate.Stop(); Console.WriteLine("AUTO: STOPPED; status log unavailable."); }
                Thread.Sleep(1500);
            }
        }
    }
    static T Timed<T>(Func<T> action, string stage)
    {
        var task = Task.Run(action);
        if (!task.Wait(timeout * 1000))
        {
            Status("RESULT: TIMEOUT during " + stage + (stage == "single invocation" || invocationStarted ? "; outcome UNKNOWN; NO RETRY." : "; nothing invoked."));
            Console.WriteLine("Check ChatGPT before repeating this test.");
            Environment.Exit(3); // Ends this process and background UIA work; never leaves an invocation worker alive.
        }
        return task.GetAwaiter().GetResult();
    }
    static Candidate Scan(bool requireCandidate = true)
    {
        scanProgress.Nodes = scanProgress.Depth = 0;
        if (!ValidWindow()) throw new ManualRefusalException(RefusalCode.TARGET_CHANGED);
        var root = AutomationElement.FromHandle(hwnd);
        lastScanVisible = !IsIconic(hwnd) && !root.Current.IsOffscreen;
        lastScanApprovalPresent = false;
        if (auto && !lastScanVisible) return null;
        string currentRoot = Id(root);
        if (rootId == null) rootId = currentRoot;
        else if (rootId != currentRoot) throw new ManualRefusalException(RefusalCode.ROOT_CHANGED);
        var found = new List<Candidate>();
        CompleteTreeScan.Run(root, scanBudget, Id, el =>
        {
            var current = el.Current;
            if (current.ControlType == ControlType.Button && current.Name == "允許一次" && !current.IsOffscreen) lastScanApprovalPresent = true;
            if (Exact(el))
            {
                var context = Context(el);
                found.Add(new Candidate { Element = el, Id = Id(el), ParentId = ParentId(el), ContextHash = context.Item1, Context = context.Item2 });
            }
        }, el => TreeWalker.RawViewWalker.GetFirstChild(el), el => TreeWalker.RawViewWalker.GetNextSibling(el), scanProgress);
        lastScanVisible = !IsIconic(hwnd) && !root.Current.IsOffscreen;
        if (!auto) Console.WriteLine("Scan complete: nodes={0}, exact usable candidates={1}", scanProgress.Nodes, found.Count);
        if (!requireCandidate && found.Count == 0) return null;
        if (found.Count != 1) throw new ManualRefusalException(found.Count == 0 ? RefusalCode.NO_CANDIDATE : RefusalCode.MULTIPLE_CANDIDATES);
        return found[0];
    }
    static bool Exact(AutomationElement el)
    {
        var c = el.Current;
        return c.ControlType == ControlType.Button && c.Name == "允許一次" && c.IsEnabled && !c.IsOffscreen
            && (bool)el.GetCurrentPropertyValue(AutomationElement.IsInvokePatternAvailableProperty);
    }
    static bool Same(Candidate a, Candidate b)
    { return a.Id == b.Id && a.ParentId == b.ParentId && a.ContextHash == b.ContextHash; }
    static string Id(AutomationElement el) { return string.Join(".", el.GetRuntimeId()); }
    static string ParentId(AutomationElement el) { var p = TreeWalker.RawViewWalker.GetParent(el); return p == null ? "" : Id(p); }
    static Tuple<string, string[]> Context(AutomationElement el)
    {
        var parent = TreeWalker.RawViewWalker.GetParent(el);
        if (parent == null) throw new ManualRefusalException(RefusalCode.CONTEXT_MISSING);
        var display = new List<string>(); var data = new StringBuilder();
        var elements = new List<AutomationElement> { parent };
        var child = TreeWalker.RawViewWalker.GetFirstChild(parent);
        while (child != null)
        {
            if (elements.Count >= 65) throw new ManualRefusalException(RefusalCode.CONTEXT_LIMIT);
            elements.Add(child); child = TreeWalker.RawViewWalker.GetNextSibling(child);
        }
        foreach (var e in elements)
        {
            var c = e.Current;
            string name = c.IsPassword ? "[password control omitted]" : (c.Name ?? "");
            // Full local fingerprint; console is clipped. Never stores context text in the status log.
            data.Append(Id(e)).Append('|').Append(c.ControlType.Id).Append('|').Append(name.Length).Append(':').Append(name).Append('\n');
            display.Add(c.ControlType.ProgrammaticName + ": " + Line(name.Length > 200 ? name.Substring(0, 200) + " [truncated]" : name));
        }
        using (var sha = SHA256.Create())
            return Tuple.Create(Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()))), display.ToArray());
    }
    static string Observe(Candidate candidate, bool requireRemoval = false)
    {
        var until = DateTime.UtcNow.AddSeconds(5);
        while (DateTime.UtcNow < until)
        {
            if (!ValidWindow()) return "WINDOW_CHANGED; APPROVAL_OUTCOME_UNKNOWN";
            string result = ObservationStep.ReadOnce<AutomationElement.AutomationElementInformation, ElementNotAvailableException>(requireRemoval,
                () => Id(candidate.Element) != candidate.Id,
                () => candidate.Element.Current,
                c => c.IsOffscreen,
                () => !IsIconic(hwnd) && !AutomationElement.FromHandle(hwnd).Current.IsOffscreen,
                c => !c.IsEnabled || c.IsOffscreen || c.Name != "允許一次");
            if (result != null) return result;
            Thread.Sleep(250);
        }
        return "BUTTON_STILL_AVAILABLE; APPROVAL_OUTCOME_UNKNOWN";
    }
    static bool ValidWindow()
    {
        if (!IsWindow(hwnd)) return false;
        uint pid; GetWindowThreadProcessId(hwnd, out pid);
        if (pid != targetPid) return false;
        using (var process = Process.GetProcessById(targetPid))
            return process.StartTime.ToUniversalTime().Ticks == processStarted;
    }
    static string Line(string s) { return s.Replace("\r", " ").Replace("\n", " ").Replace("\t", " "); }
    static void Status(string message)
    {
        lock (logSync)
        {
            Console.WriteLine(message);
            if (log != null) File.AppendAllText(log, DateTime.UtcNow.ToString("o") + " " + message + "\n", new UTF8Encoding(false));
        }
    }
    static string Integrity(int pid)
    {
        IntPtr process = OpenProcess(0x1000, false, pid), token = IntPtr.Zero, buffer = IntPtr.Zero;
        if (process == IntPtr.Zero) return "unavailable";
        try
        {
            if (!OpenProcessToken(process, 0x0008, out token)) return "unavailable";
            int length; GetTokenInformation(token, 25, IntPtr.Zero, 0, out length);
            if (length <= 0) return "unavailable";
            buffer = Marshal.AllocHGlobal(length);
            if (!GetTokenInformation(token, 25, buffer, length, out length)) return "unavailable";
            IntPtr sid = Marshal.ReadIntPtr(buffer);
            byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
            int rid = Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            return rid == 0x2000 ? "medium" : "other";
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); if (token != IntPtr.Zero) CloseHandle(token); CloseHandle(process); }
    }
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr buffer, int length, out int returnLength);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
}
