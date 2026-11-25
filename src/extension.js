const vscode = require('vscode');
const path = require('node:path');
const {Terminal: HeadlessTerminal} = require('@xterm/headless');
const {readSettings, readEnabled} = require('./settings');
const {detect, isCodexCommand} = require('./detector');
const patcher = require('./patcher');
const GUI_CHECK_INTERVAL = 3000;
const TRANSIENT_INSTALL_ERRORS = new Set(['ENOENT', 'EEXIST', 'EACCES', 'EPERM', 'EBUSY']);

/** @param {import("vscode").ExtensionContext} context */
function activate(context) {
  const log = vscode.window.createOutputChannel('Codex Auto-Accept');
  const status = vscode.window.createStatusBarItem('codexAutoAccept.status', vscode.StatusBarAlignment.Right, 100);
  const watches = new Map();
  let accepted = 0;
  let guiCheck;
  let guiReloadPath;
  let guiReady = false;
  let guiError;
  const say = msg => log.appendLine(`${new Date().toISOString()} ${msg}`);
  const settings = () => readSettings(vscode);
  const update = () => {
    const s = settings();
    status.text = `$(check-all) Codex Auto-Accept: ${s.enabled ? (s.dryRun ? 'DRY RUN' : 'ON') : 'OFF'} (${accepted})`;
    status.name = 'Codex Auto-Accept';
    if (s.configurationError) status.text += ' (SETTINGS ERROR)';
    const showGui = s.enabled && s.guiEnabled && !s.configurationError;
    if (showGui && guiError) status.text += ' (GUI ERROR)';
    else if (showGui && guiReloadPath) status.text += ' (GUI RELOAD)';
    else if (showGui && guiReady) status.text += ' (GUI READY)';
    status.tooltip = [
      s.configurationError && `Invalid settings: ${s.configurationError}. Automatic approvals are paused.`,
      showGui && (guiError || (guiReloadPath && 'Reload Window to load the repaired Codex GUI integration.')),
      'Click to toggle. CLI requires shell integration and a newly started codex command.'
    ].filter(Boolean).join(' ');
    status.command = 'codexAutoAccept.toggle'; status.show();
  };
  const codexExtension = () => {
    const ext = vscode.extensions.getExtension('openai.chatgpt');
    if (!ext) throw Error('Codex extension openai.chatgpt is not installed in this extension host.');
    return ext;
  };
  const runtime = path.join(context.extensionPath, 'dist/gui-runtime.js');
  async function installGui(interactive) {
    const ext = vscode.extensions.getExtension('openai.chatgpt');
    const s = settings();
    if (!interactive && (!s.enabled || !s.guiEnabled || s.configurationError || !vscode.workspace.isTrusted
      || context.globalState.get('guiInstalled') === false || !ext)) return;
    const version = ext?.packageJSON.version;
    try {
      if (!ext) throw Error('Codex extension openai.chatgpt is not installed in this extension host.');
      const root = ext.extensionPath;
      const sameExtension = guiCheck?.root === root && guiCheck.version === version;
      if (!sameExtension) guiReloadPath = undefined;
      const stamp = patcher.fingerprint(root);
      const sameFiles = sameExtension && guiCheck.stamp === stamp;
      const retry = sameFiles && guiCheck.retryAt && Date.now() >= guiCheck.retryAt;
      let result;
      if (interactive || !sameFiles || retry) {
        guiCheck = {root, version, stamp, failures: sameFiles ? guiCheck.failures : 0, retryAt: 0};
        result = patcher.install(root, runtime);
        guiCheck.stamp = patcher.fingerprint(root);
        guiCheck.failures = 0;
        guiError = undefined;
        say(`GUI integration ${result} for Codex ${version}.`);
      } else if (guiError) return;
      const loaded = patcher.loadedState(root);
      const stale = loaded === 'stale' || (loaded === 'not loaded' && ext.isActive)
        || (loaded === 'ready' && result === 'runtime updated');
      const notifyReload = stale && guiReloadPath !== root;
      if (stale) guiReloadPath = root;
      const ready = loaded === 'ready' && !guiReloadPath;
      if (result || interactive || notifyReload || guiReady !== ready) {
        if (ready && !guiReady) say(`Codex ${version}: GUI integration loaded in this extension host.`);
        guiReady = ready;
        update();
      }
      if (interactive) await context.globalState.update('guiInstalled', true);
      if (notifyReload) say(`Codex ${version}: the running extension host needs to reload the GUI integration.`);
      if (notifyReload || (interactive && guiReloadPath)) {
        const answer = await vscode.window.showInformationMessage(`Reload this window to load the Codex ${version} GUI integration.`, 'Reload Window');
        if (answer) await vscode.commands.executeCommand('workbench.action.reloadWindow');
      } else if (interactive) {
        void vscode.window.showInformationMessage(`Codex ${version} GUI integration is installed.`);
      }
    } catch (err) {
      const message = `GUI integration${version ? ` for Codex ${version}` : ''}: ${err.message}`;
      const code = err.cause?.code ?? err.code;
      if (guiCheck?.root === ext?.extensionPath && TRANSIENT_INSTALL_ERRORS.has(code) && ++guiCheck.failures <= 2) {
        guiCheck.retryAt = Date.now() + GUI_CHECK_INTERVAL;
      }
      if (guiError !== message) say(message);
      guiError = message;
      guiReady = false;
      update();
      if (interactive) vscode.window.showErrorMessage(message);
    }
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
        void installGui(false);
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
      say(`GUI integration ${patcher.restore(codexExtension().extensionPath)}.`);
      await context.globalState.update('guiInstalled', false);
      guiError = undefined;
      guiReloadPath = undefined;
      guiReady = false;
      update();
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
  // VS Code can replace the same extension directory without publishing an extension-list event.
  const guiTimer = setInterval(() => { void installGui(false); }, GUI_CHECK_INTERVAL);
  guiTimer.unref();
  context.subscriptions.push(log, status,
    vscode.window.onDidStartTerminalShellExecution(e => { void watchExecution(e); }),
    vscode.window.onDidEndTerminalShellExecution(e => stop(e.execution)),
    vscode.window.onDidCloseTerminal(t => { for (const [execution, watch] of watches) if (watch.terminal === t) stop(execution); }),
    vscode.workspace.onDidChangeConfiguration(e => {
      if (!e.affectsConfiguration('codexAutoAccept')) return;
      refresh();
      void installGui(false);
    }),
    vscode.extensions.onDidChange(() => {
      if (!vscode.extensions.getExtension('openai.chatgpt')) {
        guiCheck = undefined;
        guiReloadPath = undefined;
        guiReady = false;
        guiError = undefined;
        update();
      }
      void installGui(false);
    }),
    {dispose: () => {
      clearInterval(guiTimer);
      for (const execution of watches.keys()) stop(execution);
    }}
  );
  update();
  // VS Code activates the wildcard extension before sending onStartupFinished to Codex.
  void installGui(false);
  return {guiAllowed: () => !guiError && context.globalState.get('guiInstalled') !== false};
}
module.exports = {activate};
