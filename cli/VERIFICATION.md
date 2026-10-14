# Windows CLI package verification — 2026-10-08

- 10 wrapper tests passed: option separation, JSON/regex validation, Windows npm shim and exe resolution, Windows destructive-command rules, real xterm ANSI parsing/deduplication, hotkey toggle/input forwarding, cancellation/resize, dry run, logging failure.
- Existing VS Code suite: 15 tests passed; existing checkJs check passed.
- Actual local macOS PTY smoke tests: --auto --self-test returned PASS; --off --self-test waited until Ctrl+] was supplied, then returned PASS; --auto --dry-run --self-test waited until a manual y and returned PASS. Tests do not invoke Codex or AI APIs.
- CLI npm audit: zero dependency advisories.
- Archive validates presence of win32-x64 and win32-arm64 conpty.node and OpenConsole.exe, package lock and source; excludes macOS native binaries and debug symbols. Included native files come from npm node-pty 1.1.0, not cross-compiled by this project.
- Windows ConPTY, PowerShell launch and a logged-in Codex session are not yet tested on Windows. Node.js and Codex must be installed separately. The ZIP is a test build, not a standalone EXE.


## 0.1.1 — 2026-10-13

User reports 0.1.0 auto-accept works on Windows but sometimes requires manual confirmation. No stalled prompt sample/version/log has yet been supplied, so the exact cause remains unconfirmed. Code-level reproductions exposed idle-debounce starvation during redraw, narrow soft-wrapped menus, and brief disappearance of identical prompts. Added fixed-deadline inspection, soft-wrap reconstruction, deduplication across resize, and metadata-only UNRECOGNIZED logs. 16 CLI tests pass, including regressions for these conditions; denied/clipped/unknown prompts remain manual. Windows live verification of this revision is pending.

Local macOS real-PTY self-test of the updated session returned PASS. This fixture does not invoke Codex or AI APIs.


## 0.1.2 — 2026-10-13

A user photograph shows an intact English Yes/No/footer approval menu, a long Reason section, and a long pwsh-related execution block. Photo resolution does not establish which clipping token caused the particular stop. A regression fixture reproduces the overly broad check: an ellipsis/deny token in Reason blocks an otherwise complete harmless command. Command policy and clipping checks now operate only on the `$` execution block before the menu. File approvals retain their content checks. 18 CLI tests pass, including Reason false-positive and genuine clipped/destructive command rejection. Company Windows live verification remains pending.


## 0.1.3 diagnostic build — 2026-10-13

User confirms 0.1.2 and supplies a log with both MANUAL clipped content and latest UNRECOGNIZED. The generic diagnostic cannot identify the failed parser stage. This build exposes heading/Yes/No/footer/command-prefix failures with terminal dimensions, logs CLI version on START, and prints MANUAL/UNRECOGNIZED messages in the terminal. Twenty tests pass, including a long multiline PowerShell command with Reason. No claim is made that this build fixes the exact reported prompt; live diagnostic output is needed.


## 0.1.4 — 2026-10-13

User log identifies missing complete approval heading despite a visibly present title. Reproduced using wrapped history then cursor-addressed writes padded with spaces without erase-line. Old reconstruction fails with exactly missing complete approval heading; fixed reconstruction passes. Structural approval rows now remain separate when stale isWrapped metadata would join them. Actual command wraps are still rejoined. Twenty-two CLI tests pass including dangerous commands on reused rows; company Windows verification pending.


## 0.1.5 — 2026-10-13

User reports diagnostic terminal output introduced in 0.1.3 occupies the TUI, confuses operation status, and led to manually repeating a command. Removed MANUAL/UNRECOGNIZED stderr writes; metadata diagnostics remain in cli.log. Startup, explicit user mode toggles and terminating errors still display. Session/detector behavior and 0.1.4 redraw fix remain unchanged. CLI tests and local macOS PTY self-test pass; company Windows verification pending.


## User-reported 0.1.5 Windows run and 0.1.6 test build — 2026-10-13

User reports one uninterrupted Windows 0.1.5 run using right-click paste; log shows repeated ACCEPT. Later a long pwsh approval stops with missing command start ($ prefix), terminal=209x51. Ctrl+V prompt splitting remains unresolved.

0.1.6 retries unrecognized logical layouts with independent physical rows; supports standalone dollar markers followed by execution content; normalizes decision identity across representations. Policy/clipping denials are not overridden. Explicit hidden-lines markers remain manual. No input-forwarding change or UI diagnostic output. Twenty-six tests pass including physical-row recovery, deny/clipping enforcement and deduplication. Original 0.1.5 fails the recovery regression with the user's same diagnostic; the updated logic passes. This demonstrates a possible cause, not proof of the exact company screen. Windows live verification pending.

## 0.1.7 - 2026-10-14

User's 0.1.6 log reports MANUAL clipped content with an explicit Ctrl+A view-all hint. Added opt-out expandLongCommands (default true), a bounded EXEC pager reader, PageDown collection with unique final-page overlap checks, full-command policy evaluation, and same-modal return verification before y. Unknown/incomplete/ambiguous/stalled pages never receive approval. OFF/dry-run/input/resize/end stop navigation. No added terminal diagnostics or paste-forwarding change.

34 tests pass, including hidden destructive content on last page, rounded 100%, ambiguous overlap, original-prompt change, cancellation and log failure. macOS real PTY ordinary self-test and new two-page expanded self-test pass. Windows live verification pending.

Official source reviewed: openai/codex codex-rs/tui/src/app/event_dispatch.rs (FullScreenApprovalRequest uses full_cmd and E X E C title), bottom_pane/approval_overlay.rs (open_fullscreen shortcut), pager_overlay.rs (page-height, percentage rounding, q-close hints). This is a format-specific text/PTY integration, not a protocol-level guarantee. Source reviewed from GitHub main; the user's installed CLI compatibility must be verified in Windows.

## 0.1.8 - 2026-10-14

User 0.1.7 log shows MANUAL clipped content with no EXPAND despite a visible official view-all hint. Verified ListSelectionView renders "[ellipsis N lines] ctrl+a view all" while the original candidate required +N lines. Fixed recognition and replaced the default test/self-test hint with the official bracketed form. Added ASCII-ellipsis, separator, absent-hint and remapped-key cases. Old 0.1.7 fails the exact official-format regression; updated recognition passes. 36 tests and real macOS PTY expanded test pass. Windows viewer compatibility still pending. This corrects an implementation and fixture error, not a newly assumed root cause.

## 0.1.9 - 2026-10-14

User confirms Windows 0.1.8 expanded approvals succeed for both one- and two-page commands, then supplies a two-page deny-rule result. Full company command cannot be shared. Added stable B001-B011 built-in identifiers and positional D001... custom identifiers, with static categories. All matching rule IDs are included in ordinary and expanded deny logs; no pattern, command, path or match snippet is returned/logged. Matching regexes and allow/deny decisions are unchanged. 40 tests cover classifications, multiple matches, custom-rule privacy and expanded log privacy. Windows metadata-log validation pending.

## 0.1.10 - 2026-10-14

Added CLI-only bounded log rotation: 5 MiB active file plus 3 backups by default, configurable logMaxBytes/logBackupCount. Existing oversized files retain a bounded tail of complete UTF-8 lines. Pruning touches only the selected log and exact numeric backups; other files, desktop logs and UIA reports are not managed here. Short-lived locks coordinate cooperating CLI processes; confirmed exited owners can be recovered, unverified locks fail rather than overwrite. Approval and expansion rules are unchanged.

49 tests cover retention, oversized legacy files, UTF-8 boundaries, reduced/zero backups, unrelated files, unsafe targets, parallel processes, stale-lock recovery and validation. Windows live filesystem validation pending. Existing successful 0.1.9 Windows approval behavior remains the baseline; the new manager is not yet claimed verified there.

Local macOS real PTY expanded self-test passed using a temporary prefilled oversized log and a 64 KiB limit. Existing log rotated into .1, both files stayed within limit, and the final ACCEPT record was present. No normal-profile log was changed by this test.

## 0.1.11 - 2026-10-14

Screenshot shows B004 rejecting a tool inventory: a quoted rm.exe array member followed by Get-Command -ErrorAction. The old regex joined unrelated text and interpreted letters in ErrorAction as a recursive short option. B004 now requires an invocation-shaped rm/rm.exe token followed by whitespace and a complete supported recursive option token; it does not scan across semicolons or pipes. Other deny rules and static diagnostic IDs remain unchanged. This remains a heuristic, not a shell parser.

52 local tests cover the synthetic tool inventory, wrapped PowerShell, custom deny precedence, actual recursive options, executable paths, shell wrappers, and a terminal session that sends one y for inventory but no key for recursive deletion. Full company command was neither required nor stored. Windows live validation pending. Includes 0.1.10 log retention.

## 0.1.12 - 2026-10-14

User reports reducing Windows Terminal font size lets 0.1.11 resume. Reproduced one layout-dependent failure: long Reason text scrolls the heading out of the zero-scrollback viewport. Old candidate recognition fails at fixed 80x24, 60x18 and 120x30; new recognition can open the explicit official view-all hint with complete Yes/No/footer even when the heading is cropped. No ordinary approval detector was relaxed. Full verified EXEC content, policy evaluation and same-modal return remain mandatory before y. Known file-edit headings cannot use this recovery. Cropped recovery is log-only metadata, never command content.

55 local tests include fixed-geometry ANSI integration for all three sizes without resize, destructive command rejection, incomplete cropped menus, changed modal identity and unknown viewers. The existing real PTY expanded self-test now includes long Reason overflow; it passed on macOS with two pages and one final y. Company Windows normal-font validation pending; this reproduction does not prove every reported stall has the same cause. Includes prior log retention and B004 fixes.

## 0.1.13 - 2026-10-14

User confirms 0.1.12 and supplies final MANUAL B001 logs plus a JSON query screenshot with an rd variable. Synthetic equivalent reproduces B001 rejection in committed 0.1.12. B001 now masks simple PowerShell variable identifiers (including scoped and braced forms) before matching its command aliases. This preprocessing is restricted to B001; custom and other built-in patterns inspect unchanged text. Braced expressions are not broadly stripped, preserving detection of nested delete commands. This is still a text heuristic, not a complete shell parser.

59 local tests cover variable names, scoped/braced forms, mixed actual deletion, nested expressions, user-defined rules and ANSI ordinary/expanded approval flows. Windows live validation pending. No company command or path is included in the regression fixtures. Keeps 0.1.12 viewport recovery and existing log retention.

## 0.1.14 - 2026-10-14

User supplies multiple expansion-open logs followed by unknown EXEC viewer/response timeout without page reads, plus input cancellation entries. No failed pager screenshot/version confirmation is available yet. Checked upstream rust-v0.162.1 pager_overlay.rs and key_hint.rs. Add leading-blank support (no unknown banners), select the last percentage separator, extend initial viewer wait to 8 seconds within the existing 20-second total bound, and classify complete CSI focus-in/out notifications separately from user input while forwarding unchanged. Fragmented input remains conservatively treated as before; no buffering or paste rewrite introduced.

Add metadata-only structural diagnostics and geometry, never raw screen text. 65 local tests pass, covering delayed first frame, malformed/unknown viewers, privacy, separator-like script content with actual deletion, focus-only events and mixed user input, plus previous approval/retention behavior. Live Windows validation pending; these reproduced defects do not establish the cause of every reported timeout. Unknown viewers still cannot receive automatic close or approval keys.

User subsequently supplies a physical pager photo showing 100 percent, pgup/pgdn and q close; the left header is not fully readable in the photo. This does not establish what the headless decoder saw. Structural diagnostics remain necessary rather than assuming the footer is absent. macOS real PTY expanded self-test passed with two page reads, q return and one final y.
