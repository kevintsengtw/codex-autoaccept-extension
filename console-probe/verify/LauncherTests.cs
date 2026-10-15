using System.Diagnostics;

// A harmless stand-in for the probe, used only by this suite's Windows child.
if (args.Contains("--auto-accept")) return 17;
string root = Path.GetFullPath(args.Length == 1 ? args[0] : "console-probe");
string script = Path.Combine(root, "Run-Auto-Accept.cmd");
string text = File.ReadAllText(script);
if (text.Split('\n').Any(line => line.Trim().Equals("pause", StringComparison.OrdinalIgnoreCase)))
    throw new Exception("Q must not leave the batch wrapper waiting for another key.");
if (!text.Contains("exit /b %probe_exit%", StringComparison.OrdinalIgnoreCase))
    throw new Exception("Wrapper must preserve the probe exit code.");
if (!OperatingSystem.IsWindows())
{
    Console.WriteLine("PASS: launcher has no post-exit pause and preserves the exit code. Windows process roundtrip is not run on this OS.");
    return 0;
}

string temp = Path.Combine(Path.GetTempPath(), "codex-console-launcher-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string binaryDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
    foreach (string file in Directory.GetFiles(binaryDirectory))
        File.Copy(file, Path.Combine(temp, Path.GetFileName(file)));
    string apphost = Path.Combine(binaryDirectory, "LauncherTests.exe");
    if (!File.Exists(apphost)) throw new Exception("Build the Windows test apphost before testing the batch wrapper.");
    File.Copy(apphost, Path.Combine(temp, "CodexUiaProbe.exe"));
    string launcher = Path.Combine(temp, "Run-Auto-Accept.cmd");
    File.Copy(script, launcher);
    using var child = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c \"\"" + launcher + "\"\"")
    {
        UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = temp,
        RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
    })!;
    // Keep stdin open and send no key: an accidental PAUSE must time out.
    if (!child.WaitForExit(10000)) { child.Kill(true); child.WaitForExit(); throw new Exception("Launcher waited for extra input after probe exit."); }
    string output = child.StandardOutput.ReadToEnd();
    if (child.ExitCode != 17 || !output.Contains("Auto mode exit code: 17"))
        throw new Exception("Launcher did not return the probe exit code without further input.");
    Console.WriteLine("PASS: Windows batch process returns without another key and propagates the probe exit code. No App was opened.");
}
finally { Directory.Delete(temp, true); }
return 0;
