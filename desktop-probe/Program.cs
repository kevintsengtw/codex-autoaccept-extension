using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Automation;

internal static class Program
{
    static readonly JavaScriptSerializer Json = new JavaScriptSerializer();
    static readonly Encoding Utf8 = new UTF8Encoding(false);
    sealed class Options
    {
        public long Hwnd; public bool List, All, Text, Worker, Manual, Auto;
        public int Delay = 5, Nodes = 3000, Depth = 30, Timeout = 45;
        public string View = "raw", Output, Summarize;
    }
    sealed class Window
    {
        public long hwnd; public int pid; public string title, process;
    }
    sealed class Pending
    {
        public AutomationElement Element; public int Parent, Depth;
    }
    static int Main(string[] args)
    {
        Console.OutputEncoding = Utf8;
        try
        {
            if (args.Contains("--help") || args.Contains("-h")) { Help(); return 0; }
            var o = Parse(args);
            if (o.Summarize != null) { LocalSummary(Path.GetFullPath(o.Summarize), false); return 0; }
            if (Environment.OSVersion.Platform != PlatformID.Win32NT)
                throw new InvalidOperationException("Windows only. Run this EXE on the company Windows computer.");
            if (o.Worker) return Capture(o);
            Console.WriteLine(o.Auto ? "Codex UIA Probe 0.3.3 - AUTO ACCEPT (starts OFF)" : o.Manual ? "Codex UIA Probe 0.3.3 - MANUAL SINGLE INVOKE" : "Codex UIA Probe 0.3.3 - READ ONLY (no clicks, keys or approvals)");
            Console.WriteLine("Reports can contain visible chat text, commands and paths. Keep them local and redact before sharing.");
            if (o.Hwnd == 0)
            {
                var windows = Windows(o.All);
                for (int i = 0; i < windows.Count; i++)
                    Console.WriteLine("{0}. HWND=0x{1:X} PID={2} Process={3} Title={4}", i + 1,
                        windows[i].hwnd, windows[i].pid, windows[i].process, OneLine(windows[i].title));
                if (o.List) return 0;
                if (windows.Count == 0)
                    throw new InvalidOperationException("No Codex/ChatGPT candidate. Open the app, or use --list-all to select a window explicitly.");
                Console.Write("Select target window number (Enter cancels): ");
                var choice = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(choice)) return 0;
                int n;
                if (!int.TryParse(choice, out n) || n < 1 || n > windows.Count)
                    throw new ArgumentException("Invalid window number.");
                o.Hwnd = windows[n - 1].hwnd;
            }
            if (!IsWindow(new IntPtr(o.Hwnd))) throw new ArgumentException("Target HWND no longer exists. List windows again.");
            if (o.Manual)
            {
                var tool = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CodexUiaManualInvoke.exe");
                if (!File.Exists(tool)) throw new FileNotFoundException("Manual helper missing; extract the complete ZIP.");
                using (var p = Process.Start(new ProcessStartInfo(tool, "--hwnd " + o.Hwnd.ToString(CultureInfo.InvariantCulture)
                    + " --max-depth " + o.Depth + " --max-nodes " + o.Nodes + " --timeout " + o.Timeout + (o.Auto ? " --auto" : "")) { UseShellExecute = false }))
                { p.WaitForExit(); return p.ExitCode; }
            }
            if (o.Output == null)
                o.Output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "CodexUiaProbe", "probe-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".jsonl");
            o.Output = Path.GetFullPath(o.Output);
            Directory.CreateDirectory(Path.GetDirectoryName(o.Output));
            if (File.Exists(o.Output + ".txt") || File.Exists(o.Output + ".summary.txt")) throw new IOException("Readable report already exists; choose a new output path.");
            // Reserve the path before launching the worker; never overwrite a report.
            using (File.Open(o.Output, FileMode.CreateNew, FileAccess.Write)) { }
            Console.WriteLine("Capture starts in {0}s. Leave the approval visible; do not approve it yet.", o.Delay);
            System.Threading.Thread.Sleep(o.Delay * 1000);
            var workerArgs = "--worker --hwnd " + o.Hwnd.ToString(CultureInfo.InvariantCulture)
                + " --output " + Quote(o.Output) + " --max-nodes " + o.Nodes + " --max-depth " + o.Depth
                + " --view " + o.View + (o.Text ? " --include-text" : "");
            int code;
            using (var p = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, workerArgs)
                { UseShellExecute = false, CreateNoWindow = true }))
            {
                if (!p.WaitForExit(o.Timeout * 1000))
                {
                    p.Kill(); p.WaitForExit(); code = 3;
                    File.AppendAllText(o.Output, "\n", Utf8); // Separate a possibly interrupted JSON line.
                    Write(o.Output, new { kind = "supervisor", status = "timeout", seconds = o.Timeout,
                        message = "UIA worker stopped; earlier lines retained. Capture is incomplete." });
                }
                else
                {
                    code = p.ExitCode;
                    Write(o.Output, new { kind = "supervisor", status = code == 0 ? "completed" : "incomplete", workerExitCode = code });
                }
            }
            Render(o.Output);
            LocalSummary(o.Output, true);
            Console.WriteLine("JSONL: " + o.Output);
            Console.WriteLine("Readable report: " + o.Output + ".txt");
            Console.WriteLine(code == 0 ? "Capture completed. Invoke support is metadata only; no invocation was tested."
                : "Capture incomplete. See error/summary/supervisor records; do not infer unsupported UIA from missing nodes.");
            return code;
        }
        catch (Exception e) { Console.Error.WriteLine(e.GetType().Name + ": " + e.Message); return 1; }
    }
    static Options Parse(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            switch (a)
            {
                case "--auto-accept": o.Auto = true; o.Manual = true; break;
                case "--manual-invoke": o.Manual = true; break;
                case "--worker": o.Worker = true; break;
                case "--list": o.List = true; break;
                case "--list-all": o.All = true; break;
                case "--include-text": o.Text = true; break;
                case "--hwnd":
                    string h = Value(args, ref i);
                    o.Hwnd = h.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? long.Parse(h.Substring(2), NumberStyles.HexNumber) : long.Parse(h, CultureInfo.InvariantCulture);
                    if (o.Hwnd <= 0) throw new ArgumentException("HWND must be positive."); break;
                case "--summarize": o.Summarize = Value(args, ref i); break;
                case "--output": o.Output = Value(args, ref i); break;
                case "--view": o.View = Value(args, ref i); if (o.View != "raw" && o.View != "control") throw new ArgumentException("View must be raw or control."); break;
                case "--delay": o.Delay = Number(args, ref i, 0, 60); break;
                case "--max-nodes": o.Nodes = Number(args, ref i, 1, 10000); break;
                case "--max-depth": o.Depth = Number(args, ref i, 1, 100); break;
                case "--timeout": o.Timeout = Number(args, ref i, 5, 120); break;
                default: throw new ArgumentException("Unknown option: " + a);
            }
        }
        if (o.Worker && (o.Hwnd == 0 || o.Output == null)) throw new ArgumentException("Worker requires target and output.");
        if (o.Manual && !args.Contains("--max-depth")) o.Depth = 60;
        if (o.Manual && (o.Worker || o.Summarize != null || o.List || o.Text || o.Output != null || o.View != "raw"))
            throw new ArgumentException("Manual mode cannot combine with worker, summarize, list, text, output or control view.");
        return o;
    }
    static string Value(string[] args, ref int i)
    { if (++i >= args.Length) throw new ArgumentException("Missing option value."); return args[i]; }
    static int Number(string[] args, ref int i, int min, int max)
    { int n = int.Parse(Value(args, ref i), CultureInfo.InvariantCulture); if (n < min || n > max) throw new ArgumentException("Value must be between " + min + " and " + max); return n; }
    static string Quote(string s) { return "\"" + s.Replace("\"", "\\\"") + "\""; }
    static string OneLine(string s) { return (s ?? "").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t"); }
    static string Clip(string s, int length) { return s != null && s.Length > length ? s.Substring(0, length) + " [truncated]" : s; }
    static void Write(string file, object record) { File.AppendAllText(file, Json.Serialize(record) + "\n", Utf8); }
    static List<Window> Windows(bool all)
    {
        var result = new List<Window>();
        EnumWindows((h, l) =>
        {
            if (!IsWindowVisible(h)) return true;
            var b = new StringBuilder(2048); GetWindowText(h, b, b.Capacity);
            uint pid; GetWindowThreadProcessId(h, out pid);
            string name;
            try { using (var p = Process.GetProcessById((int)pid)) name = p.ProcessName; }
            catch { name = "unavailable"; }
            string title = b.ToString();
            if (all || (name + " " + title).IndexOf("codex", StringComparison.OrdinalIgnoreCase) >= 0
                || (name + " " + title).IndexOf("chatgpt", StringComparison.OrdinalIgnoreCase) >= 0)
                result.Add(new Window { hwnd = h.ToInt64(), pid = (int)pid, process = name, title = title });
            return true;
        }, IntPtr.Zero);
        return result;
    }
    static int Capture(Options o)
    {
        int count = 0, errors = 0, depthLimits = 0;
        try
        {
            uint pid; GetWindowThreadProcessId(new IntPtr(o.Hwnd), out pid);
            Write(o.Output, new { kind = "header", schemaVersion = 1, toolVersion = "0.3.3", utc = DateTime.UtcNow.ToString("o"),
                expectedAppVersion = "26.803.81509 (user supplied; not verified by this field)", os = Environment.OSVersion.ToString(),
                clr = Environment.Version.ToString(), process64Bit = Environment.Is64BitProcess,
                probeIntegrity = Integrity(Process.GetCurrentProcess().Id), targetIntegrity = Integrity((int)pid),
                targetProcess = ProcessInfo((int)pid), hwnd = "0x" + o.Hwnd.ToString("X"),
                view = o.View, includeText = o.Text, maxNodes = o.Nodes, maxDepth = o.Depth,
                readOnly = true, privacy = "Names may contain chat/command/path data. Text is opt-in. No screenshots, file reads or network calls." });
            var walker = o.View == "raw" ? TreeWalker.RawViewWalker : TreeWalker.ControlViewWalker;
            var stack = new Stack<Pending>();
            stack.Push(new Pending { Element = AutomationElement.FromHandle(new IntPtr(o.Hwnd)), Parent = -1, Depth = 0 });
            var seen = new HashSet<string>();
            while (stack.Count > 0 && count < o.Nodes)
            {
                var item = stack.Pop(); var el = item.Element;
                var node = new Dictionary<string, object> { ["kind"] = "node", ["index"] = count, ["parent"] = item.Parent, ["depth"] = item.Depth };
                var issues = new List<string>();
                try
                {
                    var rid = el.GetRuntimeId(); string key = string.Join(".", rid);
                    if (!seen.Add(key)) continue;
                    node["runtimeId"] = rid;
                }
                catch (Exception e) { issues.Add("RuntimeId: " + e.GetType().Name); }
                Add(node, issues, el, "name", AutomationElement.NameProperty);
                Add(node, issues, el, "automationId", AutomationElement.AutomationIdProperty);
                Add(node, issues, el, "className", AutomationElement.ClassNameProperty);
                Add(node, issues, el, "frameworkId", AutomationElement.FrameworkIdProperty);
                Add(node, issues, el, "pid", AutomationElement.ProcessIdProperty);
                Add(node, issues, el, "enabled", AutomationElement.IsEnabledProperty);
                Add(node, issues, el, "offscreen", AutomationElement.IsOffscreenProperty);
                Add(node, issues, el, "password", AutomationElement.IsPasswordProperty);
                Add(node, issues, el, "controlType", AutomationElement.ControlTypeProperty);
                Add(node, issues, el, "bounds", AutomationElement.BoundingRectangleProperty);
                try { node["patterns"] = el.GetSupportedPatterns().Select(p => p.ProgrammaticName).ToArray(); }
                catch (Exception e) { issues.Add("Patterns: " + e.GetType().Name); }
                string name = node.ContainsKey("name") ? node["name"] as string : null;
                node["approvalLabelHint"] = name != null && new[] { "允許一次", "允许一次", "allow once", "approve once", "yes, proceed" }
                    .Any(label => name.IndexOf(label, StringComparison.OrdinalIgnoreCase) >= 0);
                // Merely reports a label match; never treats it as a verified approval request.
                if (o.Text && node.ContainsKey("password") && node["password"] is bool && !(bool)node["password"])
                {
                    try
                    {
                        object pattern;
                        if (el.TryGetCurrentPattern(TextPattern.Pattern, out pattern))
                            node["text"] = ((TextPattern)pattern).DocumentRange.GetText(2048);
                    }
                    catch (Exception e) { issues.Add("Text: " + e.GetType().Name); }
                }
                node["errors"] = issues; errors += issues.Count;
                int parent = count++; Write(o.Output, node);
                if (item.Depth >= o.Depth) { depthLimits++; continue; }
                try
                {
                    // Only walks the explicitly selected HWND subtree. Do not search the desktop descendants.
                    var children = new List<AutomationElement>();
                    var child = walker.GetFirstChild(el);
                    while (child != null && children.Count < o.Nodes - count)
                    { children.Add(child); child = walker.GetNextSibling(child); }
                    if (child != null)
                    { errors++; Write(o.Output, new { kind = "error", index = parent, stage = "children", error = "Child enumeration capped by node budget" }); }
                    for (int i = children.Count - 1; i >= 0; i--)
                        stack.Push(new Pending { Element = children[i], Parent = parent, Depth = item.Depth + 1 });
                }
                catch (Exception e)
                { errors++; Write(o.Output, new { kind = "error", index = parent, stage = "walk", error = e.GetType().Name }); }
            }
            bool capped = stack.Count > 0;
            Write(o.Output, new { kind = "summary", nodes = count, errors, depthLimits, nodeLimitReached = capped,
                complete = !capped && errors == 0 && depthLimits == 0,
                note = "Snapshot can change while reading. Pattern availability does not prove successful invocation." });
            return capped || errors > 0 || depthLimits > 0 ? 2 : 0;
        }
        catch (Exception e)
        { Write(o.Output, new { kind = "error", stage = "capture", error = e.GetType().Name, message = Clip(e.Message, 500), nodes = count }); return 2; }
    }
    static void Add(Dictionary<string, object> node, List<string> issues, AutomationElement el, string key, AutomationProperty property)
    {
        try
        {
            object value = el.GetCurrentPropertyValue(property, true);
            if (value == AutomationElement.NotSupported) { node[key] = null; return; }
            if (value is ControlType) value = ((ControlType)value).ProgrammaticName;
            else if (value is System.Windows.Rect)
            {
                var r = (System.Windows.Rect)value;
                value = r.IsEmpty ? null : new double[] { r.X, r.Y, r.Width, r.Height };
            }
            else if (value is string) value = Clip((string)value, 1024);
            node[key] = value;
        }
        catch (Exception e) { node[key] = null; issues.Add(key + ": " + e.GetType().Name); }
    }
    static object ProcessInfo(int pid)
    {
        try
        {
            using (var p = Process.GetProcessById(pid))
            {
                string version = null, versionError = null;
                try { version = p.MainModule.FileVersionInfo.FileVersion; } catch (Exception e) { versionError = e.GetType().Name; }
                return new { pid, name = p.ProcessName, fileVersion = version, versionError };
            }
        }
        catch (Exception e) { return new { pid, error = e.GetType().Name }; }
    }
    static string Integrity(int pid)
    {
        IntPtr process = OpenProcess(0x1000, false, pid), token = IntPtr.Zero, buffer = IntPtr.Zero;
        if (process == IntPtr.Zero) return "unavailable: OpenProcess " + Marshal.GetLastWin32Error();
        try
        {
            if (!OpenProcessToken(process, 0x0008, out token)) return "unavailable: OpenProcessToken " + Marshal.GetLastWin32Error();
            int length; GetTokenInformation(token, 25, IntPtr.Zero, 0, out length);
            if (length <= 0) return "unavailable: token length";
            buffer = Marshal.AllocHGlobal(length);
            if (!GetTokenInformation(token, 25, buffer, length, out length)) return "unavailable: token query " + Marshal.GetLastWin32Error();
            IntPtr sid = Marshal.ReadIntPtr(buffer);
            byte subCount = Marshal.ReadByte(GetSidSubAuthorityCount(sid));
            int rid = Marshal.ReadInt32(GetSidSubAuthority(sid, (uint)(subCount - 1)));
            return rid >= 0x4000 ? "system" : rid >= 0x3000 ? "high" : rid >= 0x2100 ? "medium-plus" : rid >= 0x2000 ? "medium" : rid >= 0x1000 ? "low" : "untrusted";
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); if (token != IntPtr.Zero) CloseHandle(token); CloseHandle(process); }
    }
    static void LocalSummary(string path, bool save)
    {
        var records = new List<Dictionary<string, object>>();
        int malformed = 0;
        foreach (string line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            try { var record = Json.Deserialize<Dictionary<string, object>>(line); if (record == null) malformed++; else records.Add(record); }
            catch { malformed++; }
        }
        var lines = ReportSummary.Describe(records, malformed);
        foreach (string line in lines) Console.WriteLine(line);
        if (save) File.WriteAllLines(path + ".summary.txt", lines, Utf8);
    }
    static void Render(string path)
    {
        using (var output = new StreamWriter(path + ".txt", false, Utf8))
        {
            output.WriteLine("Codex UIA Probe: read-only snapshot. Label hints are NOT verified approval requests.");
            foreach (string line in File.ReadLines(path))
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var r = Json.Deserialize<Dictionary<string, object>>(line);
                    if ((string)r["kind"] != "node") { output.WriteLine(line); continue; }
                    output.WriteLine(new string(' ', Convert.ToInt32(r["depth"]) * 2) + "#" + r["index"] + " parent=" + r["parent"]
                        + " " + Get(r, "controlType") + " Name=" + OneLine(Get(r, "name"))
                        + " AutomationId=" + OneLine(Get(r, "automationId")) + " Enabled=" + Get(r, "enabled")
                        + " Offscreen=" + Get(r, "offscreen") + " Hint=" + Get(r, "approvalLabelHint")
                        + " Patterns=" + Json.Serialize(r.ContainsKey("patterns") ? r["patterns"] : null));
                    if (r.ContainsKey("text")) output.WriteLine("  Text=" + OneLine(Get(r, "text")));
                    if (r.ContainsKey("errors")) output.WriteLine("  Errors=" + Json.Serialize(r["errors"]));
                }
                catch { output.WriteLine("[unparsed / interrupted line] " + line); }
            }
        }
    }
    static string Get(Dictionary<string, object> r, string k) { object v; return r.TryGetValue(k, out v) ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
    static void Help()
    {
        Console.WriteLine(@"CodexUiaProbe 0.3.3 - Windows .NET Framework 4.8, read-only
No arguments: choose a candidate window interactively, capture after 5 seconds.
--auto-accept          Launch ON/OFF auto helper (starts OFF)
--manual-invoke        Launch manual one-shot helper; requires typed confirmation
--summarize report.jsonl Show metadata-only summary of an existing report; no UIA scan
--list                 List Codex/ChatGPT candidate windows; no UIA capture
--list-all             Include all visible top-level windows for explicit selection
--hwnd 0x1234          Capture one explicitly selected window handle
--output report.jsonl  New report path (existing files are refused)
--include-text         Read TextPattern text (max 2048 chars/node, excludes passwords)
--view raw|control     Tree view (default raw)
--delay 0..60          Seconds before capture (default 5)
--max-nodes 1..10000    Node budget (default 3000)
--max-depth 1..100     Depth budget (default 30)
--timeout 5..120       UIA worker timeout seconds (default 45)
Exit codes: 0 complete/list/cancel, 1 startup/argument error, 2 partial, 3 timeout.
Reports contain visible Names by default; review and redact before sharing.");
    }
    delegate bool EnumProc(IntPtr hwnd, IntPtr param);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc callback, IntPtr param);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr hwnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int max);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
    [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr buffer, int length, out int returnLength);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthorityCount(IntPtr sid);
    [DllImport("advapi32.dll")] static extern IntPtr GetSidSubAuthority(IntPtr sid, uint index);
}
