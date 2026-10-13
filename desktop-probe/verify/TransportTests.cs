using System.Diagnostics;
using System.Text;
if (args.Contains("--child"))
{
    HeadlessConsole.Configure();
    Console.WriteLine("READY: OFF"); Console.WriteLine("允許一次");
    string command;
    while ((command = Console.ReadLine()) != null)
    {
        if (command == "QUIT") break;
        Console.WriteLine("STATUS: " + command);
    }
    Console.Error.WriteLine("PIPE: UTF8_OK"); Console.WriteLine("STOPPED");
    return;
}
static void Check(bool b, string s) { if (!b) throw new Exception(s); }
static string Line(Process p) => p.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
for (int attempt = 0; attempt < 2; attempt++)
{
    using var p = Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "--child") {
        UseShellExecute=false, CreateNoWindow=true, RedirectStandardInput=true, RedirectStandardOutput=true,
        RedirectStandardError=true, StandardOutputEncoding=Encoding.UTF8, StandardErrorEncoding=Encoding.UTF8 });
    Check(Line(p) == "READY: OFF", "hidden redirected process must start");
    Check(Line(p) == "允許一次", "UTF-8 round trip");
    if (attempt == 0)
    {
        p.StandardInput.WriteLine("\uFEFFON"); p.StandardInput.Flush(); Check(Line(p) == "STATUS: ON", "ON command");
        p.StandardInput.WriteLine("OFF"); p.StandardInput.Flush(); Check(Line(p) == "STATUS: OFF", "OFF command");
        p.StandardInput.WriteLine("QUIT"); p.StandardInput.Flush();
    }
    else p.StandardInput.Close();
    Check(Line(p) == "STOPPED", "QUIT/EOF stops without console");
    Check(p.WaitForExit(5000) && p.ExitCode == 0, "clean child exit");
    Check(p.StandardError.ReadToEnd() == "PIPE: UTF8_OK" + Environment.NewLine, "stderr is readable UTF8");
}
Console.WriteLine("PASS: real redirected child-process transport (UTF8, READY, ON/OFF, QUIT and host EOF).");
