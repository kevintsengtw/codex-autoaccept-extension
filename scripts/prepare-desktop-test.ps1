[CmdletBinding()]
param(
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string]$Version = '0.3.7',
    [string]$OutputDirectory
)

# Verify and extract a committed release only. Never launch an EXE, change
# execution policy, elevate, alter App settings, or enable auto-accept.
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$bundle = "codex-autoaccept-desktop-windows-$Version"
$archive = Join-Path $repoRoot "artifacts/$bundle.zip"
$checksums = Join-Path $repoRoot 'artifacts/SHA256SUMS'
$pattern = '^([a-fA-F0-9]{64})\s+' + [regex]::Escape("$bundle.zip") + '$'
$matchesFound = @(Get-Content -LiteralPath $checksums | Where-Object { $_ -match $pattern })
if ($matchesFound.Count -ne 1) { throw 'Expected one release checksum.' }
$expected = [regex]::Match($matchesFound[0], $pattern).Groups[1].Value
$actual = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash
if ($expected -ne $actual) { throw 'Release ZIP checksum mismatch. Nothing extracted.' }

Add-Type -AssemblyName System.IO.Compression.FileSystem
$zip = [System.IO.Compression.ZipFile]::OpenRead($archive)
try {
    foreach ($entry in $zip.Entries) {
        $name = $entry.FullName
        if (-not $name.StartsWith("$bundle/", [StringComparison]::Ordinal) -or
            $name.Contains('\') -or $name.Contains(':') -or
            @($name.Split('/') | Where-Object { $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
            throw 'Unexpected ZIP entry. Nothing extracted.'
        }
    }
} finally { $zip.Dispose() }

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $suffix = Get-Date -Format 'yyyyMMdd-HHmmss'
    $unique = [Guid]::NewGuid().ToString('N').Substring(0, 8)
    $OutputDirectory = Join-Path $repoRoot ".desktop-test/$Version-$suffix-$unique"
}
$destination = [System.IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Destination already exists. Choose a new directory.' }
Expand-Archive -LiteralPath $archive -DestinationPath $destination
$packageDirectory = Join-Path $destination $bundle
$verified = 0
foreach ($line in Get-Content -LiteralPath (Join-Path $packageDirectory 'SHA256SUMS')) {
    if ($line -notmatch '^([a-fA-F0-9]{64})  (.+)$') { throw 'Invalid internal checksum entry.' }
    $fileHash = $Matches[1]
    $relative = $Matches[2]
    if ([System.IO.Path]::IsPathRooted($relative) -or $relative.Contains(':') -or
        @($relative.Split([char[]]@('/', '\')) | Where-Object { $_ -eq '.' -or $_ -eq '..' }).Count -gt 0) {
        throw 'Invalid internal checksum path.'
    }
    if ((Get-FileHash -LiteralPath (Join-Path $packageDirectory $relative) -Algorithm SHA256).Hash -ne $fileHash) {
        throw 'Extracted file checksum mismatch. Do not launch this package.'
    }
    $verified++
}
if ($verified -eq 0) { throw 'Empty internal checksum manifest.' }
$entryPoint = Join-Path $packageDirectory 'CodexAutoAcceptDesktop.exe'
if (-not (Test-Path -LiteralPath $entryPoint -PathType Leaf)) { throw 'Frontend EXE missing.' }
[pscustomobject]@{
    Version = $Version
    PackageSHA256 = $actual.ToLowerInvariant()
    VerifiedFiles = $verified
    PackageDirectory = $packageDirectory
    EntryPoint = $entryPoint
}
