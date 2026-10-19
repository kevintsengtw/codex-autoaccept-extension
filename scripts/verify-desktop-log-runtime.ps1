[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$EvidenceDirectory)
$ErrorActionPreference='Stop'
if([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Requires Windows .NET Framework.' }
$destination=[IO.Path]::GetFullPath($EvidenceDirectory)
if(-not $destination.StartsWith('C:\Temp\',[StringComparison]::OrdinalIgnoreCase)) { throw 'Evidence must be under C:\Temp.' }
if(Test-Path -LiteralPath $destination) { throw 'Choose a new evidence directory; existing results are preserved.' }
[IO.Directory]::CreateDirectory($destination) | Out-Null
$repoRoot=Split-Path -Parent $PSScriptRoot
$compiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output=Join-Path $destination 'LogRuntimeVerification.exe'
$started=[DateTimeOffset]::Now
$result=1
try {
    & $compiler /nologo /codepage:65001 /target:exe /reference:System.Drawing.dll /reference:System.Windows.Forms.dll "/out:$output" (Join-Path $repoRoot 'desktop-probe/verify/LogRuntimeVerification.cs') *> (Join-Path $destination 'compile.txt')
    if($LASTEXITCODE -ne 0) { throw 'Runtime verification compilation failed; see compile.txt.' }
    & $output (Join-Path $repoRoot 'desktop-probe/manual/bin/Release/net48/CodexUiaManualInvoke.exe') (Join-Path $repoRoot 'desktop-probe/tray/bin/Release/net48/CodexAutoAcceptDesktop.exe') (Join-Path $destination 'fixtures') *> (Join-Path $destination 'runtime.txt')
    if($LASTEXITCODE -ne 0) { throw 'Runtime verification failed; see runtime.txt.' }
    $result=0
    Get-Content -LiteralPath (Join-Path $destination 'runtime.txt')
} finally {
    [pscustomobject]@{StartedAt=$started.ToString('o');EndedAt=[DateTimeOffset]::Now.ToString('o');ExitCode=$result;Compiler=$compiler;Source='desktop-probe/verify/LogRuntimeVerification.cs';NoProductEntryPoint=$true} |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $destination 'result.json') -Encoding utf8
}
