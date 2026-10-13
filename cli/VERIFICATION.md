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
