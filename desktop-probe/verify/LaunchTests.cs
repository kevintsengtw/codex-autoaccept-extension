static void Check(bool v, string s) { if (!v) throw new Exception(s); }
static void Reject(Action action, string s) { try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; } throw new Exception(s); }
var dir = Path.Combine(Path.GetTempPath(), "codex-launch-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(dir);
try
{
    var exe = Path.Combine(dir,"ChatGPT.exe"); File.WriteAllText(exe,"fixture, never executed");
    Check(BackgroundLaunch.Detect(exe,Array.Empty<string>()) == BrowserHost.Unknown,"unknown host not inferred from name");
    Check(BackgroundLaunch.Detect(exe,new[]{"WEBVIEW2LOADER.DLL"}) == BrowserHost.WebView2,"module evidence");
    Directory.CreateDirectory(Path.Combine(dir,"resources")); File.WriteAllText(Path.Combine(dir,"resources","app.asar"),"");
    Check(BackgroundLaunch.Detect(exe,Array.Empty<string>()) == BrowserHost.Chromium,"Electron package evidence");
    Check(BackgroundLaunch.Detect(exe,new[]{"EmbeddedBrowserWebView.dll"}) == BrowserHost.Unknown,"ambiguous host not asserted");
    var electron = BackgroundLaunch.Create(exe,BrowserHost.Chromium,null);
    Check(!electron.UseShellExecute && electron.FileName==exe && electron.Arguments==BackgroundLaunch.Switches,"direct launch, static switches, no shell/UAC");
    string original = Environment.GetEnvironmentVariable(BackgroundLaunch.EnvironmentKey);
    var webview = BackgroundLaunch.Create(exe,BrowserHost.WebView2,null);
    Check(webview.Arguments=="" && webview.EnvironmentVariables[BackgroundLaunch.EnvironmentKey]==BackgroundLaunch.Switches,"WebView2 only in child environment");
    Check(Environment.GetEnvironmentVariable(BackgroundLaunch.EnvironmentKey)==original,"parent/global environment unchanged");
    Reject(()=>BackgroundLaunch.Create(exe,BrowserHost.WebView2,"--company-flags"),"company flags must not be overwritten");
    Reject(()=>BackgroundLaunch.Create(exe,BrowserHost.Unknown,null),"unknown mode rejected");
    Reject(()=>BackgroundLaunch.Create(Path.Combine(dir,"missing.exe"),BrowserHost.Chromium,null),"missing executable rejected");
    Reject(()=>BackgroundLaunch.Create("ChatGPT.exe",BrowserHost.Chromium,null),"relative path rejected");
    Check(BackgroundLaunch.BlocksLaunch(exe,exe,true),"existing target blocks relaunch");
    Check(BackgroundLaunch.BlocksLaunch(exe,null,false),"unreadable matching-name process blocks relaunch");
    Check(!BackgroundLaunch.BlocksLaunch(exe,Path.Combine(dir,"other","ChatGPT.exe"),true),"different executable does not block");
    Console.WriteLine("PASS: compatibility launch profiles, host evidence, process exclusion, company flags, transient environment. No App executed.");
}
finally { Directory.Delete(dir,true); }
