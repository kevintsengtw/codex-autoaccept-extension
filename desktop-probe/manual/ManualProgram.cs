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
    sealed class ManualRefusalException : InvalidOperationException
    { public ManualRefusalException(string message) : base(message) { } }
    sealed class Candidate
    {
        public AutomationElement Element;
        public string Id, ParentId, ContextHash;
        public string[] Context;
        public long Request;
        public string Mode, WindowState;
    }
    sealed class Pending { public AutomationElement Element; public int Depth; }
    static IntPtr hwnd;
    static int targetPid, depth = 60, nodes = 3000, timeout = 45;
    static long processStarted;
    static string rootId, log;
    static volatile bool invocationStarted;
    static bool auto, headless, observationOnly;
    static string startupFailure, lastUiaState;
    static bool lastScanVisible, lastScanApprovalPresent, lastScanUsable, previousAttemptPresent = true;
    static string previousAttemptId;
    static long previousRequest;
    static volatile string lastScanMode="UNKNOWN", lastScanWindowState="UNKNOWN";
    static readonly RequestTrace requestTrace = new RequestTrace();
    static readonly ReadableLog readableLog = new ReadableLog();
    static AutoGate autoGate;
    static readonly object logSync = new object();
    static int Main(string[] args)
    {
        try
        {
            headless = args.Contains("--headless");
            if (headless) HeadlessConsole.Configure();
            else Console.OutputEncoding = new UTF8Encoding(false);
            Console.WriteLine("BACKEND_VERSION: 0.3.7");
            for (int i = 0; i < args.Length; i++)
            {
                string key = args[i];
                if (key == "--headless") { headless = true; auto = true; continue; }
                if (key == "--auto") { auto = true; continue; }
                if (key == "--observe") { observationOnly = true; auto = true; continue; }
                if (++i >= args.Length) throw new ArgumentException("Missing value.");
                int n;
                switch (key)
                {
                    case "--hwnd": hwnd = new IntPtr(long.Parse(args[i], CultureInfo.InvariantCulture)); break;
                    case "--max-depth": n = int.Parse(args[i]); if (n < 1 || n > 100) throw new ArgumentException("Invalid depth."); depth = n; break;
                    case "--max-nodes": n = int.Parse(args[i]); if (n < 1 || n > 10000) throw new ArgumentException("Invalid node budget."); nodes = n; break;
                    case "--timeout": n = int.Parse(args[i]); if (n < 5 || n > 120) throw new ArgumentException("Invalid timeout."); timeout = n; break;
                    default: throw new ArgumentException("Unknown option.");
                }
            }
            if (observationOnly && !headless) throw new ArgumentException("Observation requires headless mode.");
            autoGate = new AutoGate(observationOnly);
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd)) throw new ArgumentException("A live target HWND is required. Start via Run-Manual-Once.cmd.");
            uint pid; GetWindowThreadProcessId(hwnd, out pid); targetPid = (int)pid;
            var probeLevel = Integrity(Process.GetCurrentProcess().Id); var targetLevel = Integrity(targetPid);
            Console.WriteLine("INTEGRITY: probe={0}; target={1}", probeLevel.Level, targetLevel.Level);
            Console.WriteLine("INTEGRITY_QUERY: probe={0}:{1}; target={2}:{3}", probeLevel.QueryStage, probeLevel.Error, targetLevel.QueryStage, targetLevel.Error);
            string permissionReason;
            if (!PermissionPolicy.CanStart(probeLevel, targetLevel, out permissionReason))
            {
                startupFailure = permissionReason;
                throw new ManualRefusalException("Known elevated process rejected; use the normal-user tool and target. No elevation is attempted.");
            }
            Console.WriteLine("PERMISSION_CHECK: " + permissionReason);
            using (var process = Process.GetProcessById(targetPid)) processStarted = process.StartTime.ToUniversalTime().Ticks;
            var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUiaProbe");
            Directory.CreateDirectory(directory);
            log = Path.Combine(directory, (auto ? "auto-" : "manual-") + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".txt");
            File.WriteAllText(log, ReadableLog.Header, new UTF8Encoding(true));
            if (headless) Console.WriteLine("LOG_NAME: " + Path.GetFileName(log));
            Status(observationOnly ? "MODE: READ_ONLY_OBSERVATION; approval gate permanently OFF." : auto ? "MODE: AUTO ACCEPT; starts OFF." : "MODE: MANUAL SINGLE INVOKE; no automated approvals or retries.");
            Console.WriteLine("Visible context is shown locally only. Log records statuses, not UI text.");
            var title = new StringBuilder(2048); GetWindowText(hwnd, title, title.Capacity);
            if (!headless) Console.WriteLine("Target HWND=0x{0:X}, PID={1}, title={2}", hwnd.ToInt64(), targetPid, Line(title.ToString()));
            if (headless) return RunHeadless();
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
                            throw new ManualRefusalException("Invoke became unavailable; nothing invoked.");
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
            string failure = startupFailure ?? (e.GetBaseException() is ArgumentException ? "ARGUMENTS_OR_WINDOW"
                : e.GetBaseException() is ManualRefusalException ? "TARGET_OR_PERMISSION"
                : e.GetBaseException() is IOException ? "IO" : "OTHER");
            try { Console.WriteLine("FAILURE_CODE: " + failure); } catch { }
            Console.WriteLine(invocationStarted ? "RESULT: UNKNOWN_AFTER_ATTEMPT; check ChatGPT before any new test. NO RETRY." : "RESULT: NOT_INVOKED.");
            try { Status("ERROR_TYPE: " + e.GetType().Name); } catch { Console.WriteLine("Status log unavailable."); }
            // Exception messages can contain UI information; only print the type and our own fixed guidance.
            var cause = e.GetBaseException();
            Console.WriteLine("Error type: " + cause.GetType().Name);
            if (cause is ManualRefusalException) Console.WriteLine("Reason: " + cause.Message);
            Console.WriteLine(invocationStarted ? "Provider may have acted even if it reported an error." : "Requires exactly one exact-label button, a complete scan and an unchanged target/context.");
            return invocationStarted ? 5 : 4;
        }
    }
    static void InvokeOnce(InvokePattern pattern) { pattern.Invoke(); } // Only mutation API; no retries.
    static int RunHeadless()
    {
        Status("READY: OFF");
        var loop = Task.Run((Action)AutoLoop);
        while (autoGate.Running && !loop.IsCompleted)
        {
            var input = Task.Run(() => Console.ReadLine());
            while (!input.IsCompleted && autoGate.Running && !loop.IsCompleted) Thread.Sleep(50);
            if (!input.IsCompleted) break;
            string command = input.GetAwaiter().GetResult();
            if (command == null || command == "QUIT") { autoGate.Stop(); break; }
            if (command == "ON" || command == "OFF")
            {
                autoGate.SetEnabled(command == "ON");
                try { Status(autoGate.Enabled ? "STATUS: ON" : "STATUS: OFF"); }
                catch { autoGate.Stop(); break; }
            }
        }
        autoGate.Stop();
        return 0;
    }
    static int RunAuto()
    {
        if (Console.IsInputRedirected) throw new ManualRefusalException("Auto mode requires an interactive terminal.");
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
            if (epoch == null && !observationOnly) { Thread.Sleep(100); continue; }
            bool committedThisIteration = false;
            long request = 0;
            string mode = "UNKNOWN", windowState = "UNKNOWN";
            string failureStage="READ";
            try
            {
                var candidate = Timed(() => Scan(false), "auto scan");
                if (observationOnly) { Thread.Sleep(1500); continue; } // Cannot enter invocation path; gate also refuses ON.
                if (autoGate.AwaitingAbsence && BackgroundPolicy.NewCycle(lastScanUsable, previousAttemptPresent,
                    candidate != null && candidate.Id != previousAttemptId)) { autoGate.Completed(); requestTrace.End(previousRequest); }
                bool awaiting = autoGate.AwaitingAbsence;
                autoGate.Observe(epoch.Value, !lastScanVisible || lastScanApprovalPresent);
                if (awaiting && !autoGate.AwaitingAbsence) requestTrace.End(previousRequest);
                if (candidate == null || autoGate.AwaitingAbsence)
                {
                    if (autoGate.AwaitingAbsence) LogEvent("WAIT_PREVIOUS", previousRequest, lastScanMode, lastScanWindowState);
                    Thread.Sleep(1500); continue;
                }
                request=candidate.Request; mode=candidate.Mode; windowState=candidate.WindowState;
                var fresh = Timed(() => Scan(false), "auto revalidation",request);
                if (fresh == null || !Same(candidate, fresh)) { LogEvent(fresh==null?"RECHECK_MISSING":"RECHECK", request, lastScanMode, lastScanWindowState); Thread.Sleep(1500); continue; }
                request=fresh.Request; mode=fresh.Mode; windowState=fresh.WindowState;
                failureStage="ROOT_READ";
                Timed(() =>
                {
                    if (!ValidWindow()) { LogEvent("TARGET_CHANGED",request,mode,windowState); return false; }
                    if (Id(AutomationElement.FromHandle(hwnd)) != rootId) { LogEvent("ROOT_CHANGED",request,mode,windowState); return false; }
                    if (!InTargetTree(fresh.Element)) { LogEvent("ANCESTRY_CHANGED",request,mode,windowState); return false; }
                    failureStage="BUTTON_READ";
                    if (!Exact(fresh.Element)) { LogEvent("BUTTON_CHANGED",request,mode,windowState); return false; }
                    failureStage="CONTEXT_READ";
                    if (Id(fresh.Element) != candidate.Id || ParentId(fresh.Element) != candidate.ParentId
                        || Context(fresh.Element).Item1 != candidate.ContextHash) { LogEvent("CONTEXT_CHANGED",request,mode,windowState); return false; }
                    object pattern;
                    if (!fresh.Element.TryGetCurrentPattern(InvokePattern.Pattern, out pattern)) { LogEvent("PATTERN_LOST",request,mode,windowState); return false; }
                    // Persist the committed attempt before Invoke. OFF / a fresh ON invalidates the scan epoch.
                    lock (logSync)
                    {
                        if (autoGate.GetEpoch() != epoch) { LogEvent("SWITCH_CHANGED",request,mode,windowState); return false; }
                        if (!autoGate.TryBegin(epoch.Value, candidate.Id + ":" + candidate.ParentId + ":" + candidate.ContextHash)) { LogEvent("WAIT_PREVIOUS",request,mode,windowState); return false; }
                        committedThisIteration = true;
                        LogEvent("ATTEMPT",request,mode,windowState);
                        Status("ATTEMPT: AUTO_INVOKE_ONCE");
                        invocationStarted = true; previousAttemptId = candidate.Id; previousAttemptPresent = true; previousRequest=request;
                    }
                    InvokeOnce((InvokePattern)pattern);
                    Status("INVOKE: RETURNED");
                    LogEvent("RETURNED",request,mode,windowState);
                    string observation = Observe(fresh, true);
                    Status("OBSERVATION: " + observation);
                    if (observation == "ORIGINAL_ELEMENT_REMOVED" || observation == "ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW")
                    {
                        autoGate.Completed();
                        requestTrace.End(request);
                        LogEvent(observation=="ORIGINAL_ELEMENT_REMOVED"?"REMOVED":"HIDDEN",request,mode,windowState);
                        Status("AUTO: CONTINUING; switch stays ON unless you turn it OFF.");
                    }
                    else { LogEvent("WAIT_RESULT",request,mode,windowState); Status("AUTO: WAITING FOR PREVIOUS APPROVAL TO LEAVE; switch unchanged; no duplicate click."); }
                    return true;
                }, "single invocation",request);
                Thread.Sleep(1500);
            }
            catch (Exception e)
            {
                if (epoch.HasValue) autoGate.Observe(epoch.Value, true); // Incomplete scans cannot advance a new approval cycle.
                Console.WriteLine("AUTO: READ/ACTION ERROR; switch unchanged; no duplicate invocation. Error type: " + e.GetBaseException().GetType().Name);
                try { if (!observationOnly && (committedThisIteration || request>0)) LogEvent(committedThisIteration?"ACTION_ERROR":"VALIDATION_ERROR",request,mode,windowState,failureStage); Status("AUTO: WAITING; will rescan while ON."); }
                catch { autoGate.Stop(); Console.WriteLine("AUTO: STOPPED; status log unavailable."); }
                Thread.Sleep(1500);
            }
        }
    }
    static T Timed<T>(Func<T> action, string stage,long request=0)
    {
        var task = Task.Run(action);
        if (!task.Wait(timeout * 1000))
        {
            Status("RESULT: TIMEOUT during " + stage + (stage == "single invocation" || invocationStarted ? "; outcome UNKNOWN; NO RETRY." : "; nothing invoked."));
            if(auto) LogEvent(stage=="auto revalidation"?"TIMEOUT_RECHECK":stage=="single invocation"?"TIMEOUT_ACTION":"TIMEOUT_SCAN",request,lastScanMode,lastScanWindowState);
            Console.WriteLine("Check ChatGPT before repeating this test.");
            Environment.Exit(3); // Ends this process and background UIA work; never leaves an invocation worker alive.
        }
        return task.GetAwaiter().GetResult();
    }
    static Candidate Scan(bool requireCandidate = true)
    {
        var metrics = new ScanMetrics { RootOffscreenRead = false };
        try { return ScanCore(metrics, requireCandidate); }
        catch { metrics.Result = "ERROR"; if (metrics.Reason == "NONE") metrics.Reason = "READ"; throw; }
        finally
        {
            lastScanMode=metrics.Mode; lastScanWindowState=metrics.WindowState;
            if (auto) lock (logSync) { Console.WriteLine(metrics.Encode()); AppendLog(readableLog.Scan(metrics,observationOnly)); }
        }
    }
    static void MeasureWindow(ScanMetrics metrics, WindowSnapshot w)
    {
        metrics.Mode = w.Minimized ? "MINIMIZED" : w.Foreground ? "FOREGROUND" : "BACKGROUND";
        metrics.WindowState = w.Minimized ? "MINIMIZED" : w.Maximized ? "MAXIMIZED" : "NORMAL";
        lastScanMode=metrics.Mode; lastScanWindowState=metrics.WindowState;
        metrics.RootOffscreen = w.RootOffscreen;
        metrics.RootOffscreenRead = w.RootOffscreenKnown;
        metrics.Reason = !w.NativeKnown ? "NATIVE" : !w.Visible ? "HIDDEN" : w.Cloaked ? "CLOAKED" : !w.HasForeground ? "DESKTOP"
            : w.ModalBlocked ? "MODAL" : w.Minimized && w.Foreground ? "ROOT" : !w.Minimized && !w.Frame.Valid ? "FRAME" : w.Foreground && w.RootOffscreen ? "ROOT" : "NONE";
    }
    static Candidate ScanCore(ScanMetrics metrics, bool requireCandidate)
    {
        if (!ValidWindow()) throw new ManualRefusalException("Target window changed.");
        var window = NativeSnapshot();
        MeasureWindow(metrics, window);
        string initialMode = metrics.Mode;
        lastScanVisible = false; lastScanUsable = false; lastScanApprovalPresent = false; previousAttemptPresent = true;
        if (auto && !BackgroundPolicy.CanScan(window))
        { metrics.Result = "BLOCKED"; UiaState(window.Minimized ? "MINIMIZED" : window.Cloaked ? "CLOAKED" : !window.Visible ? "WINDOW_HIDDEN" : "NATIVE_STATE_UNAVAILABLE"); return null; }
        metrics.Reason = "ROOT_READ";
        var root = AutomationElement.FromHandle(hwnd);
        CompleteSnapshot(root, window, metrics);
        MeasureWindow(metrics, window);
        if (auto && !BackgroundPolicy.CanScan(window))
        { metrics.Result = "BLOCKED"; UiaState("NATIVE_STATE_UNAVAILABLE"); return null; }
        metrics.Reason = "ROOT_READ";
        string currentRoot = Id(root);
        if (rootId == null) rootId = currentRoot;
        else if (rootId != currentRoot) throw new ManualRefusalException("Root changed.");
        var stack = new Stack<Pending>(); var seen = new HashSet<string>(); var found = new List<Candidate>(); int count = 0, labels = 0;
        string labelId = null;
        stack.Push(new Pending { Element = root });
        while (stack.Count > 0)
        {
            if (count >= nodes) { metrics.Reason = "NODE_LIMIT"; throw new ManualRefusalException("Node budget reached."); }
            var item = stack.Pop(); var el = item.Element;
            string elementId = Id(el);
            if (!seen.Add(elementId)) { metrics.Reason = "TREE_REPEAT"; throw new ManualRefusalException("Tree repeated an element."); }
            count++; metrics.Nodes = count;
            metrics.Reason = "BUTTON_READ";
            var current = el.Current;
            var type = current.ControlType;
            string name = current.IsPassword ? "" : current.Name;
            if (name == "允許一次") metrics.Named++;
            if (type == ControlType.Button) metrics.Buttons++;
            bool exactLabel = type == ControlType.Button && name == "允許一次";
            if (exactLabel)
            {
                labels++; metrics.Labels++; lastScanApprovalPresent = true; labelId=elementId;
                if (current.IsEnabled) metrics.Enabled++;
                if ((bool)el.GetCurrentPropertyValue(AutomationElement.IsInvokePatternAvailableProperty)) metrics.Invoke++;
            }
            else if (type == ControlType.Button && (name == "Allow once" || name == "Approve once" || name == "Approve" || name == "Allow" || name == "允許")) metrics.Alternatives++;
            string eligibilityReason = "NONE";
            if (exactLabel && Exact(el, window, out eligibilityReason))
            {
                metrics.Reason = "CONTEXT_READ";
                var context = Context(el);
                found.Add(new Candidate { Element = el, Id = Id(el), ParentId = ParentId(el), ContextHash = context.Item1, Context = context.Item2 });
                metrics.Eligible = found.Count;
            }
            if (exactLabel && eligibilityReason!="NONE") metrics.EligibilityReason=eligibilityReason;
            metrics.Reason = "READ";
            var child = TreeWalker.RawViewWalker.GetFirstChild(el);
            if (child != null && item.Depth >= depth) { metrics.Reason = "DEPTH_LIMIT"; throw new ManualRefusalException("Depth budget reached."); }
            int childCount = 0;
            while (child != null)
            {
                if (++childCount > nodes) { metrics.Reason = "NODE_LIMIT"; throw new ManualRefusalException("Sibling budget reached."); }
                stack.Push(new Pending { Element = child, Depth = item.Depth + 1 });
                child = TreeWalker.RawViewWalker.GetNextSibling(child);
            }
        }
        window = NativeSnapshot();
        MeasureWindow(metrics, window);
        CompleteSnapshot(root, window, metrics);
        MeasureWindow(metrics, window);
        lastScanUsable = BackgroundPolicy.CanScan(window);
        lastScanVisible = BackgroundPolicy.ProvesAbsence(window, count);
        previousAttemptPresent = previousAttemptId == null || seen.Contains(previousAttemptId);
        if (auto && initialMode != metrics.Mode)
        { metrics.Result = "BLOCKED"; metrics.Reason = "TRANSITION"; lastScanUsable = lastScanVisible = false; UiaState("WINDOW_TRANSITION"); return null; }
        if (auto && !lastScanUsable)
        { metrics.Result = "BLOCKED"; UiaState(window.Minimized ? "MINIMIZED" : window.Cloaked ? "CLOAKED" : !window.Visible ? "WINDOW_HIDDEN" : "NATIVE_STATE_UNAVAILABLE"); return null; }
        metrics.Result = "COMPLETE";
        if (found.Count==1 || labels==1) metrics.Request=requestTrace.Observe(currentRoot+":"+(found.Count==1?found[0].Id:labelId),found.Count==1);
        if (!auto) Console.WriteLine("Scan complete: nodes={0}, exact usable candidates={1}", count, found.Count);
        if (!requireCandidate && found.Count == 0)
        { UiaState(labels > 0 ? "BUTTON_NOT_READY" : window.Minimized ? "MINIMIZED_PROVIDER_LIMITED" : !window.Foreground && (window.RootOffscreen || count <= 1) ? "BACKGROUND_PROVIDER_LIMITED" : "NO_APPROVAL_BUTTON"); return null; }
        if (found.Count != 1) { metrics.Reason = "MULTIPLE"; throw new ManualRefusalException("Requires exactly one candidate."); }
        UiaState(window.Minimized ? "MINIMIZED_CANDIDATE_READY" : window.Foreground ? "CANDIDATE_READY" : "BACKGROUND_CANDIDATE_READY");
        found[0].Request=metrics.Request; found[0].Mode=metrics.Mode; found[0].WindowState=metrics.WindowState;
        return found[0];
    }
    static void UiaState(string value)
    {
        if (auto && value != lastUiaState) { lastUiaState = value; Status("UIA_STATE: " + value); }
    }
    static bool Exact(AutomationElement el, WindowSnapshot window = null)
    { string ignored; return Exact(el,window,out ignored); }
    static bool Exact(AutomationElement el, WindowSnapshot window, out string reason)
    {
        reason="NONE";
        var c = el.Current;
        if (c.ControlType != ControlType.Button || c.Name != "允許一次" || !c.IsEnabled
            || !(bool)el.GetCurrentPropertyValue(AutomationElement.IsInvokePatternAvailableProperty)) { reason="BUTTON_CHANGED"; return false; }
        if (!auto) return !c.IsOffscreen;
        if (window == null) window = Snapshot(AutomationElement.FromHandle(hwnd));
        return BackgroundPolicy.EligibleFromProperties(window, c.IsEnabled, true, () => c.IsOffscreen, () =>
        { var r = c.BoundingRectangle; return new ScreenBox(r.X, r.Y, r.Width, r.Height); }, out reason);
    }
    static WindowSnapshot Snapshot(AutomationElement root)
    {
        var w = NativeSnapshot(); CompleteSnapshot(root, w); return w;
    }
    static WindowSnapshot NativeSnapshot()
    {
        var w = new WindowSnapshot { Visible = IsWindowVisible(hwnd), Minimized = IsIconic(hwnd), Maximized = IsZoomed(hwnd) };
        IntPtr front = GetForegroundWindow(); w.HasForeground = front != IntPtr.Zero; w.Foreground = front == hwnd;
        NativeRect frame = default(NativeRect); int cloak;
        int frameResult = w.Minimized ? 0 : DwmGetFrame(hwnd, 9, out frame, Marshal.SizeOf(typeof(NativeRect)));
        int cloakResult = DwmGetCloak(hwnd, 14, out cloak, sizeof(int));
        w.NativeKnown = (w.Minimized || frameResult == 0) && cloakResult == 0; w.Cloaked = cloak != 0;
        w.Frame = new ScreenBox(frame.Left, frame.Top, (double)frame.Right-frame.Left, (double)frame.Bottom-frame.Top);
        return w;
    }
    static void CompleteSnapshot(AutomationElement root, WindowSnapshot w, ScanMetrics metrics = null)
    {
        if (!w.Minimized)
        {
            if (metrics != null) metrics.Reason = "ROOT_READ";
            w.RootOffscreen = root.Current.IsOffscreen; w.RootOffscreenKnown = true;
        }
        if (metrics != null) metrics.Reason = "WINDOW_PATTERN";
        object pattern;
        if (root.TryGetCurrentPattern(WindowPattern.Pattern, out pattern)
            && ((WindowPattern)pattern).Current.WindowInteractionState == WindowInteractionState.BlockedByModalWindow) w.ModalBlocked = true;
    }
    static bool Same(Candidate a, Candidate b)
    { return a.Id == b.Id && a.ParentId == b.ParentId && a.ContextHash == b.ContextHash; }
    static string Id(AutomationElement el) { return string.Join(".", el.GetRuntimeId()); }
    static string ParentId(AutomationElement el) { var p = TreeWalker.RawViewWalker.GetParent(el); return p == null ? "" : Id(p); }
    static bool InTargetTree(AutomationElement el)
    {
        for (int i = 0; el != null && i <= depth; i++, el = TreeWalker.RawViewWalker.GetParent(el))
            if (Id(el) == rootId) return true;
        return false;
    }
    static Tuple<string, string[]> Context(AutomationElement el)
    {
        var parent = TreeWalker.RawViewWalker.GetParent(el);
        if (parent == null) throw new ManualRefusalException("Missing parent context.");
        var display = new List<string>(); var data = new StringBuilder();
        var elements = new List<AutomationElement> { parent };
        var child = TreeWalker.RawViewWalker.GetFirstChild(parent);
        while (child != null)
        {
            if (elements.Count >= 65) throw new ManualRefusalException("Context too large to validate.");
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
            try
            {
                if (Id(candidate.Element) != candidate.Id) return "ELEMENT_CHANGED; APPROVAL_OUTCOME_UNKNOWN";
                var c = candidate.Element.Current;
                if (requireRemoval)
                {
                    var window = Snapshot(AutomationElement.FromHandle(hwnd));
                    if (!window.Minimized && BackgroundPolicy.ProvesHiddenEnd(window, c.IsOffscreen))
                        return "ORIGINAL_BUTTON_HIDDEN_IN_VISIBLE_WINDOW";
                }
                if (!requireRemoval && (!c.IsEnabled || c.IsOffscreen || c.Name != "允許一次")) return "BUTTON_UNAVAILABLE_OR_HIDDEN";
            }
            catch (ElementNotAvailableException) { return requireRemoval ? "ORIGINAL_ELEMENT_REMOVED" : "BUTTON_UNAVAILABLE_OR_HIDDEN"; }
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
            AppendLog(readableLog.Status(message,auto));
        }
    }
    static void LogEvent(string kind,long request,string mode,string state,string reason=null)
    { lock(logSync) AppendLog(readableLog.Event(kind,request,mode,state,reason)); }
    static void AppendLog(string block)
    {
        if (log==null || block==null) return;
        File.AppendAllText(log,block,new UTF8Encoding(false));
        string summary=Path.ChangeExtension(log,null)+"-summary.txt", temp=summary+".tmp";
        File.WriteAllText(temp,readableLog.Summary,new UTF8Encoding(true));
        if(File.Exists(summary)) File.Replace(temp,summary,null); else File.Move(temp,summary);
    }
    static IntegrityResult Integrity(int pid)
    {
        var result = new IntegrityResult();
        // A current-process pseudo handle avoids unnecessary OpenProcess metadata permissions.
        bool ownProcess = pid == Process.GetCurrentProcess().Id;
        IntPtr process = ownProcess ? new IntPtr(-1) : OpenProcess(0x1000, false, pid);
        IntPtr token = IntPtr.Zero, buffer = IntPtr.Zero;
        if (process == IntPtr.Zero) { result.QueryStage = "OpenProcess"; result.Error = Marshal.GetLastWin32Error(); return result; }
        try
        {
            if (!OpenProcessToken(process, 0x0008, out token)) { result.QueryStage = "OpenProcessToken"; result.Error = Marshal.GetLastWin32Error(); return result; }
            int length; GetTokenInformation(token, 25, IntPtr.Zero, 0, out length);
            if (length <= 0) { result.QueryStage = "TokenLength"; result.Error = Marshal.GetLastWin32Error(); return result; }
            buffer = Marshal.AllocHGlobal(length);
            if (!GetTokenInformation(token, 25, buffer, length, out length)) { result.QueryStage = "GetTokenInformation"; result.Error = Marshal.GetLastWin32Error(); return result; }
            IntPtr sid = Marshal.ReadIntPtr(buffer);
            byte count = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
            if (count == 0) { result.QueryStage = "SidCount"; return result; }
            result.Rid = Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(count - 1)));
            return result;
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); if (token != IntPtr.Zero) CloseHandle(token); if (!ownProcess) CloseHandle(process); }
    }
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int DwmGetFrame(IntPtr hwnd, int attribute, out NativeRect value, int size);
    [DllImport("dwmapi.dll", EntryPoint = "DwmGetWindowAttribute")] static extern int DwmGetCloak(IntPtr hwnd, int attribute, out int value, int size);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsZoomed(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr buffer, int length, out int returnLength);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
}
