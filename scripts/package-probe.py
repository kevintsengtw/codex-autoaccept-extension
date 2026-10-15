"""Package the prebuilt Windows UIA probe and explicitly confirmed manual helper; no target execution."""
from pathlib import Path
import argparse
import hashlib
import zipfile

root = Path(__file__).resolve().parent.parent
probe = root / "desktop-probe"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--name", default="codex-autoaccept-desktop-windows-0.3.10")
name = parser.parse_args().name
if not name.startswith("codex-autoaccept-desktop-windows-0.3.10") or any(c not in "abcdefghijklmnopqrstuvwxyz0123456789.-" for c in name):
    raise SystemExit("Invalid package name.")
files = {
    "CodexAutoAcceptDesktop.exe": probe / "tray/bin/Release/net48/CodexAutoAcceptDesktop.exe",
    "CodexAutoAcceptDesktop.exe.config": probe / "tray/bin/Release/net48/CodexAutoAcceptDesktop.exe.config",
    "source/tray/CodexAutoAcceptDesktop.csproj": probe / "tray/CodexAutoAcceptDesktop.csproj",
    "source/tray/Program.cs": probe / "tray/Program.cs",
    "source/shared/ScanMetrics.cs": probe / "shared/ScanMetrics.cs",
    "source/verify/ScanTests.csproj": probe / "verify/ScanTests.csproj",
    "source/verify/ScanTests.cs": probe / "verify/ScanTests.cs",
    "source/tray/BackgroundLaunch.cs": probe / "tray/BackgroundLaunch.cs",
    "source/verify/LaunchTests.csproj": probe / "verify/LaunchTests.csproj",
    "source/verify/LaunchTests.cs": probe / "verify/LaunchTests.cs",
    "source/tray/WorkerDiagnostics.cs": probe / "tray/WorkerDiagnostics.cs",
    "source/manual/ReadableLog.cs": probe / "manual/ReadableLog.cs",
    "source/manual/LocalLogWriter.cs": probe / "manual/LocalLogWriter.cs",
    "source/manual/ObservationStep.cs": probe / "manual/ObservationStep.cs",
    "source/verify/LogTests.csproj": probe / "verify/LogTests.csproj",
    "source/verify/LogTests.cs": probe / "verify/LogTests.cs",
    "examples/log-format-example.txt": probe / "examples/log-format-example.txt",
    "source/manual/BackgroundPolicy.cs": probe / "manual/BackgroundPolicy.cs",
    "source/manual/BackgroundProviderRefresh.cs": probe / "manual/BackgroundProviderRefresh.cs",
    "source/manual/FocusSafeAutomation.cs": probe / "manual/FocusSafeAutomation.cs",
    "source/manual/FocusPolicy.cs": probe / "manual/FocusPolicy.cs",
    "Interop.UIAutomationClient.dll": probe / "manual/bin/Release/net48/Interop.UIAutomationClient.dll",
    "licenses/Interop.UIAutomationClient.LICENSE.txt": probe / "manual/third-party/Interop.UIAutomationClient.LICENSE.txt",
    "source/manual/PermissionPolicy.cs": probe / "manual/PermissionPolicy.cs",
    "source/manual/HeadlessConsole.cs": probe / "manual/HeadlessConsole.cs",
    "source/tray/DesktopState.cs": probe / "tray/DesktopState.cs",
    "source/tray/app.ico": probe / "tray/app.ico",
    "source/tray/packages.lock.json": probe / "tray/packages.lock.json",
    "source/verify/BackgroundTests.csproj": probe / "verify/BackgroundTests.csproj",
    "source/verify/BackgroundTests.cs": probe / "verify/BackgroundTests.cs",
    "source/verify/PermissionTests.csproj": probe / "verify/PermissionTests.csproj",
    "source/verify/PermissionTests.cs": probe / "verify/PermissionTests.cs",
    "source/verify/TransportTests.csproj": probe / "verify/TransportTests.csproj",
    "source/verify/TransportTests.cs": probe / "verify/TransportTests.cs",
    "source/verify/DiagnosticsTests.csproj": probe / "verify/DiagnosticsTests.csproj",
    "source/verify/DiagnosticsTests.cs": probe / "verify/DiagnosticsTests.cs",
    "source/verify/DesktopTests.csproj": probe / "verify/DesktopTests.csproj",
    "source/verify/DesktopTests.cs": probe / "verify/DesktopTests.cs",
    "CodexUiaProbe.exe": probe / "bin/Release/net48/CodexUiaProbe.exe",
    "CodexUiaProbe.exe.config": probe / "bin/Release/net48/CodexUiaProbe.exe.config",
    "CodexUiaManualInvoke.exe": probe / "manual/bin/Release/net48/CodexUiaManualInvoke.exe",
    "CodexUiaManualInvoke.exe.config": probe / "manual/bin/Release/net48/CodexUiaManualInvoke.exe.config",
    "Run-Auto-Accept.cmd": probe / "Run-Auto-Accept.cmd",
    "source/manual/AutoGate.cs": probe / "manual/AutoGate.cs",
    "source/verify/AutoTests.csproj": probe / "verify/AutoTests.csproj",
    "source/verify/AutoTests.cs": probe / "verify/AutoTests.cs",
    "Run-Manual-Once.cmd": probe / "Run-Manual-Once.cmd",
    "source/manual/ManualProgram.cs": probe / "manual/ManualProgram.cs",
    "source/manual/ManualGate.cs": probe / "manual/ManualGate.cs",
    "source/manual/CodexUiaManualInvoke.csproj": probe / "manual/CodexUiaManualInvoke.csproj",
    "source/manual/packages.lock.json": probe / "manual/packages.lock.json",
    "source/verify/Directory.Build.props": probe / "verify/Directory.Build.props",
    "source/verify/ManualTests.csproj": probe / "verify/ManualTests.csproj",
    "source/verify/ManualTests.cs": probe / "verify/ManualTests.cs",
    "Run-Probe.cmd": probe / "Run-Probe.cmd",
    "README.md": probe / "tray/README.md",
    "LOG_FORMAT.md": probe / "LOG_FORMAT.md",
    "diagnostics/README.md": probe / "README.md",
    "VERIFICATION.md": probe / "VERIFICATION.md",
    "WINDOWS_HANDOFF.md": probe / "WINDOWS_HANDOFF.md",
    "WINDOWS_TEST_RESULTS_TEMPLATE.md": probe / "WINDOWS_TEST_RESULTS_TEMPLATE.md",
    "WINDOWS_TEST_RESULTS_20261013.md": probe / "WINDOWS_TEST_RESULTS_20261013.md",
    "WINDOWS_TEST_RESULTS_20261013_R3.md": probe / "WINDOWS_TEST_RESULTS_20261013_R3.md",
    "WINDOWS_TEST_RESULTS_20261013_R4.md": probe / "WINDOWS_TEST_RESULTS_20261013_R4.md",
    "WINDOWS_TEST_RESULTS_20261014_R11.md": probe / "WINDOWS_TEST_RESULTS_20261014_R11.md",
    "LICENSE": root / "LICENSE",
    "source/CodexUiaProbe.csproj": probe / "CodexUiaProbe.csproj",
    "source/Program.cs": probe / "Program.cs",
    "source/ReportSummary.cs": probe / "ReportSummary.cs",
    "source/app.manifest": probe / "app.manifest",
    "source/packages.lock.json": probe / "packages.lock.json",
    "source/verify/ProbeAudit.csproj": probe / "verify/ProbeAudit.csproj",
    "source/verify/Program.cs": probe / "verify/Program.cs",
    "source/verify/SummaryTests.csproj": probe / "verify/SummaryTests.csproj",
    "source/verify/SummaryTests.cs": probe / "verify/SummaryTests.cs",
}
for path in files.values():
    if not path.is_file():
        raise SystemExit(f"Missing {path}; build the Release project first.")
contents = {key: path.read_bytes() for key, path in files.items()}
for key in list(contents):
    if key.endswith(".cmd"):
        contents[key] = contents[key].replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
contents["SHA256SUMS"] = "".join(
    f"{hashlib.sha256(data).hexdigest()}  {key}\n" for key, data in sorted(contents.items())
).encode()
out = root / "artifacts" / (name + ".zip")
with zipfile.ZipFile(out, "w", zipfile.ZIP_DEFLATED) as archive:
    for key, data in contents.items():
        archive.writestr(name + "/" + key, data)
with zipfile.ZipFile(out) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist()) == {name + "/" + key for key in contents}
    for key, data in contents.items():
        assert archive.read(name + "/" + key) == data
checksums = root / "artifacts/SHA256SUMS"
existing = checksums.read_text().splitlines() if checksums.exists() else []
existing = [line for line in existing if line.split()[-1] != out.name]
existing.append(hashlib.sha256(out.read_bytes()).hexdigest() + "  " + out.name)
checksums.write_text("\n".join(existing) + "\n")
print(f"Verified archive: {out} ({out.stat().st_size} bytes)")
