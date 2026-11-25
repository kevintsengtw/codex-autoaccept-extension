const vscode = require('vscode');
const {readSettings} = require('./settings');
const {decision} = require('./gui-decision');
let log;
let entryPath;
function markLoaded(filename) { entryPath = filename; }
function loadedFrom() { return entryPath; }
function decide(request) {
  try {
    const extension = vscode.extensions.getExtension('local-tools.codex-autoaccept');
    if (!vscode.workspace.isTrusted || !extension?.isActive || extension.exports?.guiAllowed?.() === false) return null;
    const d = decision(request, readSettings(vscode));
    if (!d) return null;
    log ??= vscode.window.createOutputChannel('Codex Auto-Accept GUI');
    // Only metadata: never log command text, tool arguments, paths, or secrets.
    log.appendLine(`${new Date().toISOString()} ${d.respond ? 'ACCEPT' : 'MANUAL'} ${request.method} ${d.reason ?? 'once'}`);
    return d.respond ? d.result : null;
  } catch { return null; }
}
module.exports = {decide, markLoaded, loadedFrom};
