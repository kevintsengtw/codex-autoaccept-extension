const vscode = require('vscode');
const {readSettings} = require('./settings');
const {decision} = require('./gui-decision');
let log;
function decide(request) {
  try {
    if (!vscode.workspace.isTrusted || !vscode.extensions.getExtension('local-tools.codex-autoaccept')?.isActive) return null;
    const d = decision(request, readSettings(vscode));
    if (!d) return null;
    log ??= vscode.window.createOutputChannel('Codex Auto-Accept GUI');
    // Only metadata: never log command text, tool arguments, paths, or secrets.
    log.appendLine(`${new Date().toISOString()} ${d.respond ? 'ACCEPT' : 'MANUAL'} ${request.method} ${d.reason ?? 'once'}`);
    return d.respond ? d.result : null;
  } catch { return null; }
}
module.exports = {decide};
