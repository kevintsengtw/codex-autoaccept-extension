# Console 0.2.2 verification — 2026-10-15

## Baseline and scope

Source was extracted directly from the immutable `artifacts/codex-uia-probe-windows-0.2.1.zip`. `BASELINE.json` records the archive SHA-256 and every copied file's original hash. This is an independent console maintenance line, not a downgrade or modification of desktop 0.3.10 R11. R11 source, executables, verification records and historical ZIPs remain unchanged.

The user reported repeated `ManualRefusalException` after long use, persisting across tool/PowerShell restarts, and Q requiring Ctrl+C before returning to PowerShell. The photograph confirms startup with medium/medium integrity followed by the generic auto-loop exception; it does not identify the refusal site. Codex Desktop is doing ongoing work and must remain open.

## Source-confirmed defects and corrections

1. All manual refusals share one exception class and auto mode discards the reason. The original status log records only WAITING. Fixed enum reason codes, bounded scan counts and Chinese guidance are now printed and persisted; provider exception messages and private UI data are not logged. Repeated identical failures are coalesced for ten seconds.
2. A 3000-node or 60-level tree limit is unchanged by restarting PowerShell while the App retains the same conversation. Default auto scans can now grow to 6000/10000 nodes or 80/100 levels and must restart the entire scan and revalidation. Explicit budgets and manual/read-only modes stay fixed. Cyclic siblings are rejected as TREE_REPEAT rather than inducing unnecessary budget increases. At hard limits, incomplete trees remain refused.
3. A changed root is permanently compared against the original root in the old loop. Changed root/target now turns OFF and gives tool-only reselection guidance; no automatic rebinding or clearing of uncertain attempts is introduced.
4. The old post-invoke ElementNotAvailableException catch also covers the root read. Root failures could falsely report original-button removal and clear deduplication. The production observation helper now keeps root reads outside candidate-removal catches, retaining the original read order and unknown state.
5. The old auto launcher unconditionally executes PAUSE after the probe exits. Removing only that auto-mode pause lets the batch command return its exit code without another key. Manual/probe launcher pause behavior is retained.

## Validation scope

The production bounded tree walker is tested with a synthetic 7501-node tree containing a pending candidate before the budget failure; no invocation is permitted from partial scans, and a complete rescan/revalidation permits one attempt after bounded growth. Tests also cover deep trees, cyclic siblings, exact/explicit limits, consumed attempts across OFF/ON, incomplete absence, root/candidate exception isolation, provider exception propagation and persistent fixed-text diagnostics. These are production-helper regressions, not a live Windows App reproduction.

The baseline AutoGate, ManualGate and ReportSummary are retained byte-for-byte and their existing tests are rerun. The baseline PE auditor checks the two new net48 executables: standard-user manifests, one managed Invoke API in the helper, the same read-only native allowlist, and no input/focus/network APIs.

macOS checks cover two net48 builds, six portable suites and two compiled API audits. The launcher suite checks PAUSE removal and exit-code propagation structure on macOS; its real cmd.exe/child-process roundtrip runs only on Windows, using a harmless stub and no Codex App. No live Windows Q-to-PowerShell or long-running company App success is claimed here.

## Outstanding runtime acceptance

The actual cause of the original photographed refusal, company App/provider behavior after long operation, larger-tree timing within the existing timeout, and native Windows Q-to-PowerShell roundtrip remain unverified. The package does not require or perform App shutdown, chat switching, permission-policy changes or elevation. The user can replace only the helper package while keeping Codex Desktop open. Background, minimized and locked behavior remain outside this 0.2.x repair.
