# Verification — 2026-10-08

- `npm run check`: passed with checkJs enabled and VS Code 1.96 API types.
- `npm test`: 15 tests, including real xterm ANSI processing, mocked shell-execution lifecycle, dry run, switching OFF while evaluation is pending, end/close cancellation, RPC decisions, malformed settings, and patch restoration/hash checks.
- `npm run package`: produced `codex-autoaccept-0.1.0.vsix`. Bundle has no native PTY dependency and includes GUI runtime and third-party license notice.
- `npm audit --omit=dev`: zero runtime dependency advisories. Development packaging dependency `@vscode/vsce` has six transitive high advisories involving braces/secretlint; `npm audit fix` found no resolved upgrade in the installed version range. These dependencies are not included in the VSIX.
- Read-only compatibility check: loaded installed `openai.chatgpt 26.1002.51308` bundle, verified a unique adapter anchor and parsed the modified source with `vm.Script`. The installed bundle was not changed.
- User-provided reference reviewed: varfmx/claude-code-auto-accept, commit `9e38ba1a974db9754f6519e42e6e5e7c7d46800b`, `src/extension.ts`.

Not performed: installing the VSIX into the user's normal VS Code profile, altering the installed Codex extension, authenticated live GUI/CLI approvals, enterprise MDM/policy validation, Windows/remote-host end-to-end tests. Unit/mock tests do not establish those outcomes. GUI compatibility is tied to the observed bundle layout; terminal recognition is tied to the official English approval menu.


## 0.1.1 — status toggle fix

19 unit/lifecycle tests passed, including missing configuration notifications, invalid unrelated settings, failed write/retry and serialized rapid clicks. checkJs passed. A real VS Code Extension Host was launched with separate temporary user-data/extensions directories: the toggle command saved true then false in actual user settings, did the same with an invalid terminalRows value, and returned to false after two rapid clicks. The normal user profile was not changed by this test. Test runner: test/vscode/run.cjs. Real host testing verifies command/config persistence; status text rendering is additionally checked in unit tests. The exact trigger in the user's reported Windows environment remains unobserved.


## User-reported company Windows GUI verification — 2026-10-09

The user confirmed VS Code GUI auto-accept works with Codex Auto-Accept 0.1.1 and official openai.chatgpt 26.1002.51308, including the Traditional Chinese “允許一次” prompt. The initial ON-but-no-approval result was resolved by executing Install / Repair Codex GUI Integration and reloading. This is user-reported live verification; it does not establish VS Code integrated-terminal CLI or Windows Terminal CLI results. Those remain pending. Earlier “Not performed” entries describe the original build-time scope.

## VS Code GUI compatibility fix for 0.1.2 — 2026-11-23

- The 0.1.1 patcher rejected the Codex 26.1117.31558 and 26.1117.41037 bundles when the minifier renamed the request variable. 0.1.2 matched the request route and passed syntax, installation, repeat installation, and byte-for-byte restoration checks on copies of macOS arm64 bundles 26.1111.21308, 26.1117.31558, and 26.1117.41037.
- An isolated VS Code 1.147.0 Extension Host ran Install / Repair and loaded the patched Codex 26.1117.41037 extension on the next launch. With a simulated app-server, ON sent one `accept` for each of two command requests and one file-change request. The denied command, permission request, and command sent after toggling OFF received no automatic response.
- All 21 unit and lifecycle tests passed, along with `npm run check` and VSIX packaging. The regular user profile and installed extension bundles were left untouched.
- The repository VSIX includes the updated README. Its manifest and compiled JavaScript match the package used for the Extension Host test. The package checksum is listed in [SHA256SUMS](../artifacts/SHA256SUMS).

### Company computer follow-up — 2026-11-23

After installing Auto-Accept 0.1.2 on the company computer with VS Code 1.147.0 and Codex 26.1117.41037, the user reported seeing no confirmation dialogs during use so far. This is a preliminary observation from regular use. Individual approval requests and responses were outside the scope of the report.

### Codex update follow-up — 2026-11-24

- The reported Codex version was 26.1120.41423. The Marketplace catalog returned 26.51120.41243 for this release date. Verification below applies to that published build.
- The Windows x64 and macOS arm64 packages for 26.51120.41243 contained byte-identical extension JavaScript. On copies of both bundles, 0.1.2 passed installation, repeat installation, and byte-for-byte restoration.
- In an isolated macOS VS Code 1.147.0 Extension Host with a simulated app-server, 0.1.2 was ON before GUI integration and left the command request pending. After running Install / Repair and reopening the host, it sent one `accept` per eligible command or file-change request. The denied command, permission request, and command sent after switching OFF received no automatic response.

### 0.1.3 — automatic GUI integration on startup after a Codex update

- Tested the packaged 0.1.3 VSIX in an isolated macOS VS Code 1.147.0 profile with Auto-Accept ON. An existing 26.1117.41037 installation with the 0.1.2 patch still sent single approvals.
- Installed the official 26.51120.41243 VSIX into the same profile through the VS Code CLI. On the first launch after the update, 0.1.3 installed the integration during startup. With a simulated app-server, it sent one `accept` for each eligible request without a manual repair command or a second reload. Denied commands, permission requests, and commands after switching OFF received no automatic approval.
- All 24 unit and lifecycle tests passed, including reload status when an extension entry point is already loaded, error status for unsupported bundles, and manual mode after explicitly restoring the original bundle. Type checking and the VSIX checksum passed.

### Company computer verification for 0.1.3 — 2026-11-24

In a follow-up to the Codex update report, the user confirmed that Auto-Accept 0.1.3 successfully auto-approved Codex GUI requests on their company computer.

The repository VSIX was rebuilt with the updated README and verified to contain the same manifest and compiled JavaScript as the tested package. Its checksum is listed in [SHA256SUMS](../artifacts/SHA256SUMS).
