[CmdletBinding()]
param()

# Build and check source only. Does not launch the net48 tray/helper/probe
# against an App. TransportTests launches its own harmless test child.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
Get-Command dotnet -ErrorAction Stop | Out-Null
Push-Location $repoRoot
try {
    foreach ($project in @(
        'desktop-probe/CodexUiaProbe.csproj',
        'desktop-probe/manual/CodexUiaManualInvoke.csproj',
        'desktop-probe/tray/CodexAutoAcceptDesktop.csproj'
    )) {
        & dotnet build $project -c Release -p:RestoreLockedMode=true --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    foreach ($suite in @('Scan', 'Background', 'Launch', 'Transport', 'Diagnostics', 'Desktop', 'Permission', 'Auto', 'Manual', 'Summary', 'Log')) {
        & dotnet run --project "desktop-probe/verify/${suite}Tests.csproj"
        if ($LASTEXITCODE -ne 0) { throw "Test failed: $suite" }
    }
    $auditor = 'desktop-probe/verify/ProbeAudit.csproj'
    & dotnet run --project $auditor -- 'desktop-probe/bin/Release/net48/CodexUiaProbe.exe'
    if ($LASTEXITCODE -ne 0) { throw 'Probe API audit failed.' }
    & dotnet run --project $auditor -- 'desktop-probe/manual/bin/Release/net48/CodexUiaManualInvoke.exe' manual
    if ($LASTEXITCODE -ne 0) { throw 'Helper API audit failed.' }
    & dotnet run --project $auditor -- 'desktop-probe/tray/bin/Release/net48/CodexAutoAcceptDesktop.exe' tray
    if ($LASTEXITCODE -ne 0) { throw 'Tray API audit failed.' }
    Write-Host 'PASS: three builds, eleven suites and three API audits. Windows App runtime behavior is not established by these checks.'
} finally { Pop-Location }
