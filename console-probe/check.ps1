[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$probeRoot = $PSScriptRoot
$originalTestTemp = $env:TEMP
$originalTestTmp = $env:TMP
Push-Location $probeRoot
try {
    if ([Environment]::OSVersion.Platform -eq [PlatformID]::Win32NT) {
        $testTempRoot = Join-Path 'C:\Temp' ('codex-console-checks-' + [Guid]::NewGuid().ToString('N'))
        [IO.Directory]::CreateDirectory($testTempRoot) | Out-Null
        $env:TEMP = $testTempRoot
        $env:TMP = $testTempRoot
    }
    foreach ($project in @('CodexUiaProbe.csproj', 'manual/CodexUiaManualInvoke.csproj')) {
        & dotnet build $project -c Release -p:RestoreLockedMode=true --nologo
        if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
    }
    foreach ($suite in @('Auto', 'Manual', 'Summary', 'Recovery', 'Observation', 'Launcher')) {
        if ($suite -eq 'Launcher') { & dotnet run --project "verify/${suite}Tests.csproj" -- $probeRoot }
        else { & dotnet run --project "verify/${suite}Tests.csproj" }
        if ($LASTEXITCODE -ne 0) { throw "Test failed: $suite" }
    }
    & dotnet run --project verify/ProbeAudit.csproj -- bin/Release/net48/CodexUiaProbe.exe
    if ($LASTEXITCODE -ne 0) { throw 'Probe API audit failed.' }
    & dotnet run --project verify/ProbeAudit.csproj -- manual/bin/Release/net48/CodexUiaManualInvoke.exe manual
    if ($LASTEXITCODE -ne 0) { throw 'Helper API audit failed.' }
    Write-Host 'PASS: console 0.2.2 builds, six suites and two API audits. Live Windows UIA remains a separate acceptance test.'
} finally {
    $env:TEMP = $originalTestTemp
    $env:TMP = $originalTestTmp
    Pop-Location
}
