# Verification — 2026-10-12

- Cross-built `net48` Release with .NET SDK on macOS: zero warnings/errors.
- Audited the compiled EXE using `System.Reflection.Metadata`: managed entry point and UIA assembly references present; embedded manifest uses `asInvoker` and `uiAccess=false`; 11 native imports are restricted to window enumeration/metadata and process-token queries; no referenced UIA Invoke, setters, focus, selection, scroll, toggle, move/resize actions or network/input APIs in the audited categories.
- The supervisor starts and, on timeout, terminates only its own UIA worker. It does not obtain a target process control handle. `OpenProcess` uses query-limited access (`0x1000`), and tokens use query access (`0x0008`). Reports are bounded, append per node and preserve partial capture.
- Source reviewed for capture scope: only the explicitly selected HWND subtree is traversed. Window selection is manual. TextPattern document text is opt-in and skipped for passwords/unknown password status. No ValuePattern values, screenshots, Codex configuration writes or approval actions.
- Packaged executable/config, source, pinned build dependency lock, documentation and MIT license; archive contents and SHA-256 verified by packaging script.

Not performed: Windows runtime execution, UIA provider compatibility, company application-control policy checks, actual ChatGPT Desktop App 26.803.81509 inspection, target token checks on Windows, worker termination on a stalled Windows provider, foreground/background comparisons, or live approval handling. The API audit is a bounded static check, not a proof of all behavior. This tool deliberately never invokes an approval button.

Reproduce build and compiled API audit:

```sh
dotnet build desktop-probe/CodexUiaProbe.csproj -c Release
dotnet run --project desktop-probe/verify/ProbeAudit.csproj -- desktop-probe/bin/Release/net48/CodexUiaProbe.exe
python3 scripts/package-probe.py
```

Windows acceptance: capture baseline and visible approval in a normal-user session, check header/summary/errors, confirm the pending approval remains pending, inspect relevant parent/child nodes and declared patterns. Missing or partial data must not be interpreted as lack of UIA support.

## 0.1.1 local summary

- Added `--summarize` to analyze an existing local report without another UIA scan. A successful summary has exit code 0 independently of the original capture result.
- Ten portable summary checks passed: content exclusion, missing summary, malformed JSON line status, timeout, button filtering, Invoke declaration, unavailable patterns, adjacent text scope, complete capture and partial error counts.
- Rebuilt net48 EXE with zero warnings/errors; compiled read-only API audit passed again.
- Windows execution and actual 0.1.0 company report analysis remain unverified. Exit code 2 reported by the user establishes an incomplete result, not its underlying cause.

```sh
dotnet run --project desktop-probe/verify/SummaryTests.csproj
```

## 0.1.2 manual one-shot mode

- Kept the read-only probe and the invoking helper in separate assemblies. Read-only API audit passed for CodexUiaProbe; helper audit permits only InvokePattern.Invoke as a UI mutation and rejects other UI/input/network APIs.
- Both net48 projects cross-built with zero warnings/errors.
- Six portable confirmation-gate scenarios passed: no confirmation, cancellation/exact input, one call only, stale target, provider exception with no retry, and validation exception with no retry. Ten existing summary checks also passed.
- Source review: explicit mode, exact-label unique usable button, complete bounded scan, medium integrity checks, local context presentation, fresh scan and identity/context revalidation, final pre-invoke checks, one consumed confirmation, no retry, bounded post-observation. These reduce stale targeting; UIA lacks an atomic approval-request transaction.
- Actual Windows Invoke, company policy allowing the helper EXE, provider timing, cancellation while waiting and observation outcomes remain unverified.
- User-reported 0.1.1 Windows probe result: depth 60 yielded exit 0, zero read errors/boundaries, no node cap, one label Button candidate, one Invoke declaration, one enabled/onscreen candidate. This establishes discovery, not invocation success.

```sh
dotnet build desktop-probe/manual/CodexUiaManualInvoke.csproj -c Release
dotnet build desktop-probe/CodexUiaProbe.csproj -c Release
dotnet run --project desktop-probe/verify/ManualTests.csproj
dotnet run --project desktop-probe/verify/ProbeAudit.csproj -- desktop-probe/manual/bin/Release/net48/CodexUiaManualInvoke.exe manual
```

## 0.2.0 ON/OFF automatic mode

- User confirmed manual INVOKE ONCE removed the pending approval and Codex continued in the company environment. This is user-reported live validation of manual mode only.
- Added an explicit auto mode with terminal O/F/Space/Q controls, default OFF, normal-user integrity checks and no persisted ON setting.
- Thirteen portable state tests passed: revocation/epochs, one commit, no retry across OFF/ON, absence handling, completed original/new identities, stale scan rejection, quit and concurrent commit. Six manual-gate and ten summary checks passed.
- Cross-built both Windows EXEs with zero warnings/errors. API audits passed for the read-only probe and the helper (only InvokePattern.Invoke allowed for UI mutation).
- Original-element removal is required for successful auto observation; enabled/offscreen changes alone are ambiguous and switch OFF. Consumed target keys persist across OFF/ON for the current process; this can conservatively block a new prompt reusing identical UI metadata.
- Automatic Windows execution, company helper execution policy, continuous background polling, actual OFF responsiveness and multiple real approval requests remain unverified. There are no command deny rules or per-chat binding in this UIA prototype.

```sh
dotnet run --project desktop-probe/verify/AutoTests.csproj
```

## User-reported company Windows auto-mode verification — 2026-10-12

Target: ChatGPT Desktop App (Codex) 26.803.81509, standard-user execution, UIA tool 0.2.0.

The user confirmed:

- OFF leaves the approval pending for manual handling.
- Switching ON invokes “允許一次” automatically.
- A second approval is handled normally.
- Switching OFF again stops automatic approvals.

This supersedes the earlier unverified entries for basic automatic invocation, consecutive approvals and ON/OFF behavior in this specific company environment. It is user-reported live validation, not direct agent observation or a general compatibility guarantee. Scan-time OFF races, provider hangs, ambiguous outcomes, terminal shutdown, long-running sessions, minimization, other App versions and multi-chat/window behavior remain unverified. Command rules and per-chat binding remain unimplemented.

Only documentation was updated after this report; the tested 0.2.0 ZIP and executable bytes were preserved.

## 0.2.1 continuous ON correction

The user clarified that 0.2.0 switches OFF after an approval. This supersedes the earlier interpretation of the 0.2.0 live report as successful continuous automatic operation. The implementation indeed switched OFF when a retained/hidden button did not become a stale element within five seconds.

- Removed normal post-invoke automatic OFF and transient scan-error OFF. ON remains ON while waiting/rescanning; failed status logging and provider timeouts can still stop the helper.
- Accepts a stale original element or an offscreen original button in a visible, non-minimized target window as a lifecycle boundary. Disabled visible buttons count as present. Incomplete scans and hidden/minimized windows cannot advance absence detection.
- Resets dedupe only at a verified lifecycle boundary, permitting new approvals that reuse the same UI identity. An OFF/ON toggle alone never retries a consumed attempt.
- Portable lifecycle tests cover twenty consecutive same-identity approvals without another ON action, no duplicate attempt within a cycle, waiting with ON preserved, generation revocation, absence detection and concurrent attempts. Manual and summary tests and API audits passed.
- Windows company validation of 0.2.1 remains pending. UIA lifecycle observations are not protocol request IDs; a boundary that cannot be observed will keep the tool waiting.

## User-reported 0.2.1 continuous auto-mode verification — 2026-10-12

Company environment: ChatGPT Desktop App (Codex) 26.803.81509; standard-user execution; UIA tool 0.2.1.

The user confirmed that pressing O only once handled three consecutive “允許一次” approvals automatically while remaining ON. Pressing F then stopped automatic approvals. This validates the continuous-ON correction in this specific environment and supersedes the preceding pending status for 0.2.1.

This is user-reported live verification, not direct agent observation. Other App versions, long-running sessions, race conditions, hidden/minimized windows and provider failures remain outside the reported validation. Terminal CLI scenarios remain pending independently.

Only README and verification documentation were updated; no executable changes or repackaging were performed.

## 0.3.0 Windows tray frontend

- Added net48 Windows GUI frontend with NotifyIcon, target picker, mouse ON/OFF, close-to-tray, explicit quit, single instance and optional HKCU Run login startup. User explicitly selected OFF on every process start; only App path is persisted.
- Headless backend uses inherited stdin/stdout pipes and CreateNoWindow. ON/OFF acknowledgement is separate from requested intent. EOF or QUIT stops the backend; callbacks are fenced to the active Process instance.
- App reattachment requires a unique matching saved executable path; PID/start-time checks protect against HWND reuse. If path metadata is inaccessible, explicitly selected windows still work for that session without automatic path binding.
- Frontend fault/timeout displays a paused state; it never silently restarts and repeats an ambiguous approval. Explicit reconnect resets OFF. Host exit closes pipe/requests worker termination; already submitted provider actions cannot be rolled back.
- Twelve portable frontend lifecycle tests passed, plus existing 20-request auto lifecycle simulation, six manual-gate and ten summary checks.
- All three net48 EXEs cross-built without warnings/errors. Frontend PE audit verifies WindowsGui subsystem and asInvoker, with six window/icon native APIs and no UIA mutations. Backend API audit still permits only InvokePattern.Invoke for UI mutation; probe remains read-only.
- Not executed on Windows: GUI layout/DPI, notification-area behavior, real pipe lifecycle, reconnect, corporate application control, registry registration and login startup. This is a desktop UX implementation built around the user-validated 0.2.1 core; Windows acceptance remains required.

```sh
dotnet build desktop-probe/tray/CodexAutoAcceptDesktop.csproj -c Release
dotnet run --project desktop-probe/verify/DesktopTests.csproj
dotnet run --project desktop-probe/verify/ProbeAudit.csproj -- desktop-probe/tray/bin/Release/net48/CodexAutoAcceptDesktop.exe tray
```

## 0.3.1 headless startup correction

User reported that 0.3.0 immediately shows backend stopped, including after reconnect. Inspection found an initialization defect: Console.OutputEncoding was set before the try block, although CreateNoWindow removes the console. The Framework reference source calls SetConsoleOutputCP and throws on failure without checking output redirection. This establishes a relevant defect, not a confirmed trace from the company machine.

- Headless mode now installs UTF-8 standard stream readers/writers without setting console code pages. Initialization is inside error handling, and backend version is emitted before argument/window checks.
- Added actual redirected child-process transport tests: READY, Chinese UTF-8 output, ON/OFF commands with optional input BOM, QUIT and host EOF. They run on the current host (.NET 8/macOS); exact net48/Windows execution remains pending.
- Added seven diagnostic tests for version mismatch, safe error classification, exit codes and attempt tracking. Frontend lifecycle now includes connecting-state checks (13 checks).
- UI sequence is numbered and ordered choose → ON → reconnect. ON is disabled until READY from a compatible backend. Reconnect reuses a valid explicitly selected target even when no path can be persisted.
- Exit handling drains redirected streams before showing safe diagnostic metadata; diagnostic capture does not discard early errors merely because the process has already exited. Late control messages still cannot revive an exited worker.
- All three net48 projects build and API audits pass. Continuous auto/manual/summary regression checks pass. Company 0.3.1 end-to-end execution remains unverified.

Reference: https://github.com/microsoft/referencesource/blob/main/mscorlib/system/console.cs (OutputEncoding setter).

## 0.3.2 startup permission metadata correction

User reported exit 4 / ManualRefusalException before an approval attempt in 0.3.1. The only such pre-READY refusal is the exact-medium token check; actual values were not previously surfaced in the GUI. We have not observed the company process tokens and cannot conclude elevated privileges are required.

- Integrity query now distinguishes untrusted, low, medium range, medium-plus range, high, system and unavailable. Current-process query uses its pseudo handle. Unavailable results include query stage and Win32 error, without paths/user names.
- Known high/system tool or target remains rejected. Other levels and unknown metadata can initialize, with all real UIA access and invocation still governed by Windows. This does not grant UIAccess, elevate, or change corporate policy. Unknown token metadata is not reported as success.
- Added permission-policy and diagnostic privacy tests for lower/standard ranges, unknown queries, known elevated rejection, parsing stage/error codes and preserving no-attempt vs ambiguous-attempt status.
- GUI exposes levels on successful handshake and startup failure, plus UIA minimized/root-offscreen/button-offscreen states. This is diagnostic instrumentation, not a background-occlusion fix.
- Windows company 0.3.2 startup and background behavior remain unverified. Existing transport, frontend/core/manual/summary checks and compiled API audits run as regression checks.

## User-reported 0.3.2 success — 2026-10-12

The user confirmed normal-user CodexAutoAcceptDesktop connects without startup errors, detects a subsequent “允許一次” approval, invokes it and continues monitoring with incrementing count. This supersedes the pending basic tray/startup status for this company environment. Background occlusion remained unverified and was specifically requested next.

## 0.3.3 background visibility policy

- Native read-only snapshot adds WS_VISIBLE, IsIconic, DWM frame bounds/cloak and GetForegroundWindow. No foreground/focus/restore/move APIs are added. Modal-blocked roots are not acted on.
- A background root/button IsOffscreen hint no longer unconditionally blocks eligible UIA buttons. Background candidates require exact label, enabled/Invoke, valid finite geometry with center inside target frame, uniqueness and existing fresh identity/context checks.
- Offscreen alone in background cannot complete the previous request. Root-offscreen/empty-tree uncertainty cannot advance absence rearming. A complete scan with old runtime ID absent and a distinct eligible new candidate can advance the cycle.
- Portable policy scenarios passed for covered windows, offscreen hints, geometry rejection, minimized/cloaked/hidden/no-active-desktop cases, foreground compatibility, negative monitor coordinates, dedupe and next-background-request progression.
- Existing startup/permission, transport, frontend, manual/core/summary tests run as regression checks. Three EXEs cross-build and compiled API audits verify the backend's only UI mutation is InvokePattern.Invoke, with 14 allowlisted read-only native imports.
- Not performed: actual company Windows occlusion, renderer accessibility updates while covered, multi-DPI DWM/UIA alignment, modal/provider behavior or focus changes initiated by the App itself. If App publishes no active UIA button in the background, this client cannot manufacture it; it reports BACKGROUND_PROVIDER_LIMITED and waits without foregrounding.


## User-reported 0.3.3 limitation — 2026-10-12

The user confirmed partial occlusion permits auto-approval. Full occlusion reports BACKGROUND_PROVIDER_LIMITED until the App is foreground again. Minimized App also does not auto-approve; 0.3.3 deliberately skipped minimized scans. Geometry-only background relaxation therefore does not solve full occlusion in company App 26.803.81509. Actual engine and internal throttling remain unknown.

## 0.3.4 optional compatibility launcher and minimized scanning

- Minimized windows continue full raw UIA traversal. Geometry is not relied on while minimized; exact unique enabled Invoke button, repeated complete scans, root HWND/PID/start-time/runtime ID, fresh target ancestry and parent/context checks remain required. DWM frame errors do not block a minimized scan, while cloak errors, hidden/cloaked windows, modal roots and missing interactive desktop still block it. Final native snapshot is rechecked after traversal.
- Minimized absence/offscreen is never completion evidence. Distinct new candidate with old ID absent in a complete scan may advance the cycle; repeated uncertainty cannot replay the previous candidate.
- Explicit GUI launch experiment uses the saved target executable. Chromium profile supplies four fixed switches; WebView2 profile supplies them only in ProcessStartInfo child environment, with no App CLI flags. Existing WebView2 environment arguments are preserved by refusing this experimental launch. No install/global environment changes, debugging ports, sandbox-disable flags, elevation, target process termination or focus/restore APIs.
- Module/package-file engine evidence is a hint. Unknown or mixed evidence is displayed as unknown; the user selects a profile. App still running (including same-name processes whose path is unreadable) refuses launch to avoid misleading single-instance success. The launched Process handle is disposed without waiting/killing; existing auto-attachment selects only a unique matching window. Compatibility launch resets OFF and requires manual ON.
- Three net48 Release projects cross-built with zero warnings/errors. Nine portable suites passed: Background, Launch, Transport, Diagnostics, Desktop, Permission, Auto, Manual, Summary. Launch suite never executed an App and verifies unknown/mixed detection, existing-process exclusion, exact direct profiles, inherited flags refusal, absolute executable validation and unchanged global environment. Minimized policy/lifecycle checks verify empty geometry support, unavailable/disabled rejection, no false completion, next distinct approval, sustained ON and OFF epoch cancellation.
- PE audits verify standard-user manifest, GUI subsystem, no networking or simulated input/focus APIs, unchanged native imports (tray 6, helper 14, probe 11), and helper's single Invoke reference. ZIP CRC/content/SHA-256 verification accompanies packaging.
- Not performed: actual Windows GUI/App launches, acceptance of flags in company version, fully occluded/minimized live UIA updates, MSIX direct activation, company allowlisting, startup broker environment inheritance or App-originated focus changes. Successful Process.Start is not reported as successful compatibility. Chromium switches and WebView2 environment are a diagnostic experiment (Microsoft advises against production dependence on WebView2 browser flags); no background/minimized success is claimed before company testing.


## User-reported 0.3.4 limitation — 2026-10-12

The user tested one compatibility profile after fully exiting the App; both complete coverage and minimizing still fail, with the GUI described as “正在監聽”. The tested profile is not yet known. This does not establish both profiles failed or that the App stopped rendering. Existing NO_APPROVAL_BUTTON status collapses a nonempty tree with no matching label into an insufficiently informative message.

## 0.3.5 bounded scan telemetry and read-only comparison

- Adds observation-only hidden backend started explicitly by GUI. It has a permanently OFF AutoGate, ignores ON requests through that gate, and the loop exits before any invocation path. GUI disables ON during observation, and ending observation starts a fresh normal OFF backend. Previously committed invocations cannot be revoked by switching modes.
- Displays last foreground/background/minimized scan separately, with receive timestamps and mode marker; updates do not redraw tray icons. Rows reset on worker replacement. These are sequential observations, not one atomic snapshot or proof of renderer freshness.
- Protocol allows only fixed status/reason/mode codes and eight bounded counters plus root-offscreen bit. No names, runtime IDs, fingerprints, process paths, commands or conversation content cross this diagnostic channel. Invalid tokens, extra text, negative/oversized/impossible counts are discarded. Live per-property changes can make enabled vs eligible counts differ.
- Traversal counts all nodes/buttons, exact label across roles, exact approval Buttons, enabled/Invoke/eligible candidates and known alternate-label Buttons (diagnostic only). No new automatic label acceptance or launch switches. Completeness, native blockers, read/budget errors and foreground/background/minimized transitions are explicit; transitional scans cannot rearm/invoke.
- Scan metrics are transmitted only, not appended to disk report. Normal OFF does not scan; explicit read-only observation does. All mutations still pass original fresh root/ancestry/context/epoch checks in normal mode.
- Portable scan protocol tests exercise privacy, malformed/out-of-range metadata, absent background labels, native/read/transition outcomes and live-property races; AutoGate tests ensure repeated ON/Completed/Observe cannot enable observation authority.
- Windows comparison on the company App remains pending. No background/minimized success is claimed, and no new global/native mutation APIs are added.

- Final validation: all three net48 Release builds completed with zero warnings/errors; ten portable suites passed (Scan, Background, Launch, Transport, Diagnostics, Desktop, Permission, Auto, Manual, Summary). All three compiled API audits passed, retaining the same native import counts and single Invoke mutation reference. GUI layout and Windows observation transport/provider behavior are not runtime-tested on this macOS host. Archive content/CRC and every internal SHA-256 entry are checked during delivery.


## User-reported locked-computer limitation — 2026-10-12

The user additionally reports that automatic approval does not occur while the computer is locked. The tested version/profile and post-unlock recovery are not yet known. This is an additional failure scenario, distinct from occlusion and minimizing; it does not establish an elevated-privilege requirement or universal UIA impossibility on inactive user desktops.

Current implementation requires a nonzero GetForegroundWindow result before an auto scan is usable. The DESKTOP diagnostic means this foreground handle is absent, not a verified session-lock event; Microsoft documents that an activation transition can also return NULL. Snapshot currently queries UIA before that guard, so provider failures/timeouts remain possible and post-unlock worker recovery needs to be observed.

Windows switches between securable input desktops; ordinary Apps remain on the user desktop. Restricted access to Winlogon UI is not evidence that the tool needs to access Winlogon to approve its normal target. Neither compatibility flags nor the current HasForeground heuristic establish reliable locked-session operation. Reliable diagnosis needs explicit current-session lock/connect information and separate provider availability evidence. No lock-screen control, desktop switching, elevation or anti-lock workaround is introduced.

Do not report locked-session auto-approval as supported. The three-state v0.3.5 comparison still applies while the user session is unlocked. When interpreting locked tests, collect only fixed status and whether the existing ON worker resumes after unlocking or requires reconnect. Existing release archives are preserved; this note does not modify shipped binaries or their checksums.

References: https://learn.microsoft.com/en-us/windows/win32/winstation/desktops ; https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getforegroundwindow


## User-reported post-unlock recovery — 2026-10-12

The user confirmed approvals resume automatically after unlocking, without manual reconnect. This establishes observed recovery of the approval workflow; it does not identify which internal guard/provider prevented locked-session approval or prove that the same worker remained alive throughout. The tested version/profile remains unspecified. No user intervention after unlock is reported as necessary. Locked-session continuous approval remains unsupported/unverified; pending v0.3.5 unlocked foreground/occlusion/minimized comparison is still needed to locate those failures.


## User-reported background candidate metadata — 2026-10-12

For the BACKGROUND row under complete coverage, the user reported n=1633, btn=134, name=1, allow=1, e=1, i=1, alt=0, rootOff=0, and corrected ok from 0 to 1. This shows one eligible candidate was reached in that traversal; it does not yet establish a completed scan or successful invocation. The COMPLETE/BLOCKED/ERROR reason is still missing. Observation-only mode deliberately does not invoke. Do not infer missing-provider or geometry rejection from the superseded ok=0, and do not relax eligibility based on that value. Normal-mode background execution and creation of later approvals while already covered remain unverified by these counters.


## User-confirmed full-occlusion success, minimized still fails — 2026-10-12

The user explicitly confirmed auto-approval works while the App is completely covered by other windows, and does not work after minimizing. Accept this clarification as the current acceptance status, superseding the generalized full-occlusion-failure diagnosis. Do not continue questioning whether the user confused observation with auto mode. Exact successful launch profile/version and background generation of successive new requests are not independently established by this statement.

## 0.3.6 minimized property-query correction

- Found a concrete client defect: minimized policy skipped geometry validation but its caller eagerly queried BoundingRectangle/IsOffscreen anyway. Root IsOffscreen and DWM frame were also still queried although not used for minimized eligibility. No trace currently proves these were the actual company failure cause.
- Production eligibility now receives lazy screen-property readers. Minimized enabled Invoke candidates can pass without calling them; normal foreground/background readers retain existing screen policy. Root-offscreen and DWM frame queries are skipped while minimized; post-invoke hidden-boundary observation also skips button offscreen on minimized windows. Original removal/distinct verified new-request boundaries still control dedupe.
- Native snapshot precedes root/provider reads, retaining minimized mode when UIA root/property reads fail and rejecting unavailable desktops before initial UIA traversal. Native HWND/PID/time, visible/cloak, foreground availability, modal blockers and original root/ancestry/context/epoch revalidation remain required. Modal is explicitly tracked instead of folded into generic NativeKnown failure.
- Diagnostic protocol adds rootOff=U for unqueried hints and fixed MODAL/ROOT_READ/WINDOW_PATTERN/BUTTON_READ/CONTEXT_READ stages. GUI surfaces minimized failure category directly; diagnostics do not claim success merely from eligible counts.
- Added meaningful fault-injection checks: minimized eligibility succeeds even when both screen-property getters would throw, disabled/non-Invoke/modal targets reject without querying, and covered-window geometry validation remains active. Protocol tests cover U and new fixed reason codes.
- No launch switches, focus/restore/input APIs, services, elevation or network behavior added. No successful minimized runtime behavior is claimed until company testing. Locked-session approval remains unverified/unsupported; user-observed post-unlock recovery is retained as separate evidence.

- Final validation: all three net48 Release projects cross-built with zero warnings/errors. All ten portable suites and three compiled API audits passed. Native import counts and single Invoke reference are unchanged. Fault injection validates the skipped-property behavior, not the actual Windows provider failure cause. Company minimized execution, focus effects and consecutive request generation remain runtime acceptance checks. Archive contents/CRC/internal SHA-256 are verified during packaging.

## 0.3.6 company result correction

- Latest company report: with App maximized and fully covered by another window, 0.3.6 does not auto-approve. Minimization also remains unsuccessful. The earlier blanket full-coverage acceptance statement is withdrawn; previous raw-counter reports did not establish this behavior reliably. Historical statements above describe what was reported at the time, not final acceptance.
- User cannot export company data or pause ongoing work to answer later diagnostic questions. They can only manually transcribe fixed tool messages. They cannot create a separate test window and do not want a read-only diagnostic workflow or timestamp matching.

## 0.3.7 readable local logs; no background fix claimed

- Removed tray diagnostic rows/counters and read-only diagnostic controls. Normal ON use produces a UTF-8 BOM main log and an atomically replaced short summary in LocalAppData/CodexUiaProbe. Explicit Open Log button opens only the sanitized current backend basename under that fixed directory, using the local text-file association; no upload or clipboard copy. Reconnection creates a new pair and retains old files.
- Chinese fixed descriptions distinguish complete/incomplete scans, eligibility reasons, revalidation refusal, attempt committed before Invoke, provider return, observed original-element boundary, unknown outcome and separate scan/recheck/action timeouts. Summary places report code and result before explanations. Duplicate unchanged states coalesce; persisted history is append-only. A prior issue remains in the short summary after later recovery, explicitly marked historical.
- Local P numbers correlate UIA candidates using already-read root/element runtime identifiers held in memory, not native App request IDs. Missing candidates explicitly cannot be associated with a pending business request. No command, dialogue, title, PID, path, runtime identifier or context hash is written to these logs; unrecognized input is translated into fixed text. Legacy CLI probe reports are separate existing artifacts, not this auto log.
- Added read-only IsZoomed metadata to distinguish maximized/background from normal/background. Background is not a coverage measurement, and missing foreground HWND is not a confirmed session-lock event. No focus, restore, synthetic input, launch flags, service, elevation or network behavior added. Candidate policy, full revalidation, OFF epochs, unique exact-label Invoke, lifecycle deduplication and one mutation call remain intact. Geometry rejection now supplies a fixed reason without changing eligibility.
- Read-only CLI mode remains in backend source; the product UI no longer exposes it. GUI keeps ordinary ON/OFF, monitoring and backend-failure status. No user is asked to pause a request, match timestamps, supply screenshots or export files. Example log is generated from synthetic test fixtures and labelled as such.
- Covered/maximized, minimized and locked operation are unresolved. The new logs and UI have not been executed on the company Windows machine; portable checks cannot establish provider availability or business-task continuation.

- Final local validation: all three net48 Release projects built with zero warnings/errors. Eleven portable suites passed (Scan, Background, Launch, Transport, Diagnostics, Desktop, Permission, Auto, Manual, Summary, Log). New log checks cover actual CRLF formatting, local candidate lifecycle, missing-candidate limits, preserved historical issues, no false success, coalescing, precise revalidation/timeout codes, manual observation and private-data rejection. Three compiled API audits passed: probe 11 read-only native imports; helper 15 read-only native imports and exactly one InvokePattern.Invoke reference; tray six window/icon imports and Windows GUI subsystem. All retain asInvoker/uiAccess=false and no network/input/focus APIs. Rebuilt affected binaries and reran log tests/audits after the final display-text/timeout-description edits.
- Graph updated with AST-only extraction; its known parser warning for top-level SummaryTests does not represent a compiler failure. Package verifies archive CRC, complete file set and SHA-256 contents; log format reference and synthetic example are included. Actual company Windows runtime and the unresolved extra desktop states remain outside these checks.

## Company 0.3.7 result and Windows 11 handoff — 2026-10-13

- User reports a real approval request in background but no auto-accept, and a real approval request while minimized but no auto-accept. Locking was not tested because the first two cases failed. No log reason codes were supplied; do not infer an exact provider/geometry cause or exact coverage percentage from this report.
- Company VS Code extension, CLI and Desktop permit only request approvals; Approve for me and Full Access are unavailable. Existing standard-user/company App version 26.803.81509 and no-company-data-export constraints remain. Testing on another personal Windows 11 machine was proposed, not yet performed here.
- Added WINDOWS_HANDOFF.md, a pasteable WINDOWS_CODEX_PROMPT.md and a blank WINDOWS_TEST_RESULTS_TEMPLATE.md. They require actual native Windows Computer Use capability checks, a frozen 0.3.7 baseline, real harmless approval requests, independent business-completion evidence, separate existing/new background/minimized requests, version/locale recording and no foreground restoration during the measured interval. Locking is deferred; no company Windows success is claimed from portable tests or a different App version.
- Committing source and prior versioned packages preserves the historical baseline. The delivered 0.3.7 ZIP is immutable; new handoff/results live in the repository and its checksum remains 3fa6fc9a3e61c36b58716925146ceb73a6e7d4bf79d1fc51432191f5d5fbe51c. No product executable or auto-accept behavior changed for this handoff.
- Preparation script only verifies/extracts release files to a new ignored directory; it neither launches the App nor changes privileges or policies. check-desktop.ps1 builds three projects, runs eleven suites and audits three compiled artifacts. Raw runtime logs/screenshots, extracted packages, bin/obj and local graph caches are excluded from commits.

- Handoff validation on macOS PowerShell: prepare-desktop-test.ps1 verified and extracted the unchanged 0.3.7 ZIP plus 62 internal file hashes; checks also confirmed refusal of a mismatched ZIP checksum before extraction and preservation of an existing destination. Script ASCII source avoids Windows PowerShell legacy encoding ambiguity. This is preparation behavior, not native Windows App verification.
- All 15 artifact hashes, ZIP CRCs and available internal manifests verified. The old 0.1.1 probe outer checksum was stale and is corrected to the current archive bytes; no historical archive is rewritten. Intended source/document files checked for common credential formats and runtime-data exclusion.
- The committed check-desktop.ps1 itself executed successfully under macOS PowerShell: three locked-restore Release builds (zero warnings/errors), all eleven suites and all three API audits passed. Windows PowerShell 5.1/native Desktop runtime remain to be exercised on the target machine.
