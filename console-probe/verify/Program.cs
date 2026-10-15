using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

bool manual = args.Length == 2 && args[1] == "manual";
if (args.Length != 1 && !manual) throw new ArgumentException("Pass the built CodexUiaProbe.exe path.");
var bytes = File.ReadAllBytes(args[0]);
using var pe = new PEReader(new MemoryStream(bytes));
if (!pe.HasMetadata || pe.PEHeaders.CorHeader.EntryPointTokenOrRelativeVirtualAddress == 0)
    throw new Exception("Expected managed executable with entry point.");
var metadata = pe.GetMetadataReader();
if (metadata.GetAssemblyDefinition().Version != new Version(0, 2, 2, 0))
    throw new Exception("Expected console repair version 0.2.2.0.");
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
    ["IsIconic"]="user32.dll",
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
if (imports != (manual ? 10 : native.Count - 1)) throw new Exception("Unexpected native import count.");
if (manual && invokeReferences != 1) throw new Exception("Manual helper must reference exactly one Invoke API.");
string raw = Encoding.UTF8.GetString(bytes);
if (!raw.Contains("level=\"asInvoker\" uiAccess=\"false\"")) throw new Exception("Expected embedded standard-user manifest.");
var refs = metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name)).ToHashSet();
if (!refs.Contains("UIAutomationClient") || !refs.Contains("UIAutomationTypes")) throw new Exception("Missing UIA references.");
if (refs.Any(r => r.StartsWith("System.Net"))) throw new Exception("Unexpected networking assembly.");
Console.WriteLine(manual ? "PASS: manual helper allows only InvokePattern.Invoke as UI mutation; standard-user manifest; 10 read-only native imports; no network/input APIs." : "PASS: managed EXE, UIA references, embedded asInvoker manifest, 11 allowlisted native read APIs, no listed UI/input action references.");
Console.WriteLine("This audit checks API references, not Windows runtime/provider behavior or all possible side effects.");
