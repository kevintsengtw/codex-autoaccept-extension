using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

internal enum BrowserHost { Unknown, WebView2, Chromium }

// Transient, explicit compatibility experiment. Never edits installation/global environment,
// terminates the target, changes its sandbox, or asserts that a host accepted these switches.
internal static class BackgroundLaunch
{
    public const string EnvironmentKey = "WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS";
    public const string Switches = "--disable-backgrounding-occluded-windows --disable-renderer-backgrounding --disable-background-timer-throttling --force-renderer-accessibility";

    public static BrowserHost Detect(string executable, IEnumerable<string> moduleNames)
    {
        var names = new HashSet<string>(moduleNames ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        bool webview = names.Contains("WebView2Loader.dll") || names.Contains("EmbeddedBrowserWebView.dll");
        string dir = string.IsNullOrEmpty(executable) ? null : Path.GetDirectoryName(executable);
        bool chromium = dir != null && (File.Exists(Path.Combine(dir, "resources", "app.asar"))
            || File.Exists(Path.Combine(dir, "resources", "electron.asar")));
        if (!webview && dir != null) webview = File.Exists(Path.Combine(dir, "WebView2Loader.dll"));
        if (webview == chromium) return BrowserHost.Unknown; // No evidence, or mixed evidence: user chooses.
        return webview ? BrowserHost.WebView2 : BrowserHost.Chromium;
    }

    public static bool BlocksLaunch(string expectedPath, string processPath, bool metadataAvailable)
        => !metadataAvailable || string.Equals(Path.GetFullPath(expectedPath), Path.GetFullPath(processPath), StringComparison.OrdinalIgnoreCase);

    public static ProcessStartInfo Create(string executable, BrowserHost host, string existingWebViewArguments)
    {
        if (host != BrowserHost.Chromium && host != BrowserHost.WebView2) throw new ArgumentException("Choose a browser host.");
        if (!Path.IsPathRooted(executable) || !File.Exists(executable) || !string.Equals(Path.GetExtension(executable), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Selected executable is unavailable.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable) };
        if (host == BrowserHost.Chromium) start.Arguments = Switches;
        else
        {
            // Do not replace flags inherited from company configuration, or create conflicting switches.
            if (!string.IsNullOrWhiteSpace(existingWebViewArguments)) throw new InvalidOperationException("Existing WebView2 arguments must be preserved.");
            start.EnvironmentVariables[EnvironmentKey] = Switches;
        }
        return start;
    }
}
