const vscode = require('vscode');
const path = require('node:path');
const {Terminal: HeadlessTerminal} = require('@xterm/headless');
const {readSettings, readEnabled} = require('./settings');
const {detect, isCodexCommand} = require('./detector');
const patcher = require('./patcher');

/** @param {import("vscode").ExtensionContext} context */
function activate(context) {
  const log = vscode.window.createOutputChannel('Codex Auto-Accept');
  const status = vscode.window.createStatusBarItem('codexAutoAccept.status', vscode.StatusBarAlignment.Right, 100);
  const watches = new Map();
  let accepted = 0;
  const say = msg => log.appendLine(`${new Date().toISOString()} ${msg}`);
  const settings = () => readSettings(vscode);
  const update = () => {
    const s = settings();
    status.text = `$(check-all) Codex Auto-Accept: ${s.enabled ? (s.dryRun ? 'DRY RUN' : 'ON') : 'OFF'} (${accepted})`;
    status.name = 'Codex Auto-Accept';
    if (s.configurationError) status.text += ' (SETTINGS ERROR)';
    status.tooltip = (s.configurationError ? `Invalid settings: ${s.configurationError}. Automatic approvals are paused. ` : '') + 'Click to toggle. GUI integration needs a one-time install and window reload. CLI requires shell integration and a newly started codex command.';
    status.command = 'codexAutoAccept.toggle'; status.show();
  };
  const codexRoot = () => {
    const ext = vscode.extensions.getExtension('openai.chatgpt');
    if (!ext) throw Error('Codex extension openai.chatgpt is not installed in this extension host.');
    return ext.extensionPath;
  };
  const runtime = path.join(context.extensionPath, 'dist/gui-runtime.js');
  async function installGui(interactive) {
    try {
      const result = patcher.install(codexRoot(), runtime);
      await context.globalState.update('guiInstalled', true);
      say(`GUI integration ${result}.`);
      if (interactive) {
        const answer = await vscode.window.showInformationMessage('Codex GUI integration installed. Reload this window to load it; the ON/OFF switch then works immediately.', 'Reload Window');
        if (answer) await vscode.commands.executeCommand('workbench.action.reloadWindow');
      }
    } catch (err) { say(`GUI integration: ${err.message}`); if (interactive) vscode.window.showErrorMessage(err.message); }
  }
  function stop(execution) {
    const watch = watches.get(execution);
    if (!watch) return;
    watch.ended = true; clearTimeout(watch.timer); watch.screen.dispose(); watches.delete(execution);
  }
  function inspect(watch) {
    if (watch.ended || watches.get(watch.execution) !== watch) return;
    const s = settings();
    if (s.configurationError || !s.enabled || !s.terminalEnabled || !vscode.workspace.isTrusted) return;
    const buffer = watch.screen.buffer.active;
    const screen = Array.from({length: watch.screen.rows}, (_, i) => buffer.getLine(buffer.viewportY + i)?.translateToString(true) ?? '').join('\n').trimEnd();
    const d = detect(screen, s);
    if (!d) { watch.last = ''; return; }
    if (d.identity === watch.last) return;
    watch.last = d.identity;
    if (!d.allow || !('key' in d) || s.dryRun) { say(`CLI MANUAL: ${s.dryRun ? 'dry run' : d.reason}`); return; }
    // One key, no Enter needed for Codex's explicit (y) shortcut.
    // End/close events synchronously cancel pending evaluation before another shell receives input.
    watch.terminal.sendText(d.key, false);
    accepted++; say('CLI ACCEPT once'); update();
  }
  async function watchExecution(event) {
    if (!isCodexCommand(event.execution.commandLine.value)) return;
    // read() must happen synchronously at execution-start; awaiting first loses early output.
    const stream = event.execution.read();
    for (const [execution, prior] of watches) if (prior.terminal === event.terminal) stop(execution);
    const screen = new HeadlessTerminal({cols: settings().terminalColumns, rows: settings().terminalRows, scrollback: 0, allowProposedApi: true});
    const watch = {execution: event.execution, terminal: event.terminal, screen, ended: false, timer: undefined, last: ''};
    watches.set(event.execution, watch);
    say('Watching Codex CLI execution.');
    try {
      for await (const chunk of stream) {
        if (watch.ended) break;
        clearTimeout(watch.timer);
        // xterm maintains cursor moves, erase commands, wraps, and split escape sequences.
        await new Promise(resolve => screen.write(chunk, () => resolve(undefined)));
        if (!watch.ended) watch.timer = setTimeout(() => inspect(watch), 250);
      }
    } catch (err) { if (!watch.ended) say(`Terminal read stopped: ${err.message}`); }
    finally { stop(event.execution); }
  }
  function refresh() {
    update();
    const s = settings();
    for (const watch of watches.values()) {
      watch.last = '';
      watch.screen.resize(s.terminalColumns, s.terminalRows);
      clearTimeout(watch.timer);
      watch.timer = setTimeout(() => inspect(watch), 250);
    }
  }
  const register = (name, fn) => context.subscriptions.push(vscode.commands.registerCommand(`codexAutoAccept.${name}`, fn));
  /** @type {Promise<boolean | void>} */
  let toggleQueue = Promise.resolve();
  register('toggle', () => {
    toggleQueue = toggleQueue.then(async () => {
      const next = !readEnabled(vscode);
      try {
        // Use the canonical full key. Verify persistence using a fresh configuration snapshot.
        await vscode.workspace.getConfiguration().update('codexAutoAccept.enabled', next, vscode.ConfigurationTarget.Global);
        if (readEnabled(vscode) !== next) throw Error('The saved user setting does not match the requested value.');
        refresh();
        say(`Auto-Accept switched ${next ? 'ON' : 'OFF'}; user setting saved.`);
        return next;
      } catch (err) {
        update();
        say(`Toggle failed: ${err.message}`);
        await vscode.window.showErrorMessage(`Codex Auto-Accept 無法切換：${err.message}`);
        return undefined;
      }
    });
    return toggleQueue;
  });
  register('showLog', () => log.show());
  register('installGui', () => installGui(true));
  register('restoreGui', async () => {
    try {
      say(`GUI integration ${patcher.restore(codexRoot())}.`);
      await context.globalState.update('guiInstalled', false);
      const answer = await vscode.window.showInformationMessage('Original Codex extension restored. Reload to unload the integration.', 'Reload Window');
      if (answer) await vscode.commands.executeCommand('workbench.action.reloadWindow');
    } catch (err) { say(err.message); vscode.window.showErrorMessage(err.message); }
  });
  register('startTerminal', () => {
    const terminal = vscode.window.createTerminal({name: 'Codex Auto-Accept'});
    terminal.show();
    let started = false;
    let listener;
    const launch = () => {
      if (started || !terminal.shellIntegration) return;
      started = true; clearTimeout(timeout); listener?.dispose();
      terminal.shellIntegration.executeCommand('codex', []);
    };
    listener = vscode.window.onDidChangeTerminalShellIntegration(e => { if (e.terminal === terminal) launch(); });
    const timeout = setTimeout(() => {
      listener.dispose();
      if (!started) vscode.window.showWarningMessage('Shell integration is unavailable. Enable terminal.integrated.shellIntegration.enabled and use bash, zsh, fish, or PowerShell, then start Codex again.');
    }, 10000);
    context.subscriptions.push(listener, {dispose: () => clearTimeout(timeout)});
    launch();
  });
  context.subscriptions.push(log, status,
    vscode.window.onDidStartTerminalShellExecution(e => { void watchExecution(e); }),
    vscode.window.onDidEndTerminalShellExecution(e => stop(e.execution)),
    vscode.window.onDidCloseTerminal(t => { for (const [execution, watch] of watches) if (watch.terminal === t) stop(execution); }),
    vscode.workspace.onDidChangeConfiguration(e => {
      if (!e.affectsConfiguration('codexAutoAccept')) return;
      refresh();
    }),
    vscode.extensions.onDidChange(() => { if (context.globalState.get('guiInstalled')) void installGui(false); }),
    {dispose: () => { for (const execution of watches.keys()) stop(execution); }}
  );
  update();
  if (context.globalState.get('guiInstalled')) void installGui(false);
}
module.exports = {activate};
