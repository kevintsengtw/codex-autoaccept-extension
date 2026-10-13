using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

bool tray = args.Length == 2 && args[1] == "tray";
bool manual = args.Length == 2 && args[1] == "manual";
if (args.Length != 1 && !manual && !tray) throw new ArgumentException("Pass the built CodexUiaProbe.exe path.");
var bytes = File.ReadAllBytes(args[0]);
using var pe = new PEReader(new MemoryStream(bytes));
if (!pe.HasMetadata || pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress == 0)
    throw new Exception("Expected managed executable with entry point.");
var metadata = pe.GetMetadataReader();
var forbidden = new HashSet<string> {
    "Invoke", "SetFocus", "SetValue", "Move", "Resize", "Select", "AddToSelection", "RemoveFromSelection",
    "Expand", "Collapse", "Scroll", "ScrollIntoView", "Toggle", "SetWindowVisualState", "SetForegroundWindow",
    "SendInput", "SendKeys", "mouse_event", "keybd_event", "SetCursorPos"
};
int invokeReferences = 0;
foreach (var handle in metadata.MemberReferences)
{
    var member = metadata.GetMemberReference(handle);
    var name = metadata.GetString(member.Name);
    if (member.Parent.Kind == HandleKind.TypeReference)
    {
        var type = metadata.GetTypeReference((TypeReferenceHandle)member.Parent);
        string ns = metadata.GetString(type.Namespace);
        bool allowedInvoke = manual && ns == "System.Windows.Automation" && metadata.GetString(type.Name) == "InvokePattern" && name == "Invoke";
        if (allowedInvoke) invokeReferences++;
        if (ns.StartsWith("System.Windows.Automation") && (forbidden.Contains(name) || name.StartsWith("set_")) && !allowedInvoke)
            throw new Exception("UIA action/setter referenced: " + name);
        if (ns.StartsWith("System.Net") || ns == "System.Windows.Forms" && metadata.GetString(type.Name) == "SendKeys")
            throw new Exception("Unexpected network/input API: " + ns + "." + name);
    }
}
var native = new Dictionary<string, string> {
    ["GetForegroundWindow"]="user32.dll", ["DwmGetWindowAttribute"]="dwmapi.dll",
    ["DestroyIcon"]="user32.dll",
    ["IsIconic"]="user32.dll",
    ["IsZoomed"]="user32.dll",
    ["EnumWindows"]="user32.dll", ["IsWindowVisible"]="user32.dll", ["IsWindow"]="user32.dll",
    ["GetWindowText"]="user32.dll", ["GetWindowThreadProcessId"]="user32.dll",
    ["OpenProcess"]="kernel32.dll", ["CloseHandle"]="kernel32.dll",
    ["OpenProcessToken"]="advapi32.dll", ["GetTokenInformation"]="advapi32.dll",
    ["GetSidSubAuthorityCount"]="advapi32.dll", ["GetSidSubAuthority"]="advapi32.dll"
};
int imports = 0;
foreach (var handle in metadata.MethodDefinitions)
{
    var method = metadata.GetMethodDefinition(handle);
    var import = method.GetImport();
    if (import.Module.IsNil) continue;
    string name = metadata.GetString(import.Name);
    string module = metadata.GetString(metadata.GetModuleReference(import.Module).Name);
    if (!native.TryGetValue(name, out var allowed) || allowed != module)
        throw new Exception("Unexpected native API: " + module + "!" + name);
    imports++;
}
if (imports != (tray ? 6 : manual ? 15 : 11)) throw new Exception("Unexpected native import count.");
if (manual && invokeReferences != 1) throw new Exception("Manual helper must reference exactly one Invoke API.");
string raw = Encoding.UTF8.GetString(bytes);
if (!raw.Contains("level=\"asInvoker\" uiAccess=\"false\"")) throw new Exception("Expected embedded standard-user manifest.");
var refs = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToHashSet();
if (!tray && (!refs.Contains("UIAutomationClient") || !refs.Contains("UIAutomationTypes"))) throw new Exception("Missing UIA references.");
if (refs.Any(r => r.StartsWith("System.Net"))) throw new Exception("Unexpected networking assembly.");
if (tray && pe.PEHeaders.PEHeader.Subsystem != Subsystem.WindowsGui) throw new Exception("Tray must be WindowsGui, not a console subsystem.");
Console.WriteLine(tray ? "PASS: Windows GUI subsystem, asInvoker manifest, six window/icon native APIs, no UIA mutation/network/input APIs." : manual ? "PASS: helper permits only InvokePattern.Invoke as UI mutation; standard-user manifest; 15 read-only native imports (incl. maximized-state query); no network/input/focus APIs." : "PASS: managed EXE, UIA references, embedded asInvoker manifest, 11 allowlisted native read APIs, no listed UI/input action references.");
Console.WriteLine("This audit checks API references, not Windows runtime/provider behavior or all possible side effects.");
