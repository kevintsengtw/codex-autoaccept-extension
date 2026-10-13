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
