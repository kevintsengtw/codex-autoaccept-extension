const {evaluate} = require('./policy');
const COMMANDS = new Set(['item/commandExecution/requestApproval', 'execCommandApproval']);
const FILES = new Set(['item/fileChange/requestApproval', 'applyPatchApproval']);
function decision(request, settings) {
  if (settings.configurationError || !settings.enabled || !settings.guiEnabled) return null;
  const command = COMMANDS.has(request.method), file = FILES.has(request.method);
  if (!command && !file) return null; // permissions, MCP, user input stay interactive
  if (file && !settings.approveFileChanges) return null;
  const p = request.params;
  if (!p || request.id == null) return null;
  if (command && (typeof p.command !== 'string' && !Array.isArray(p.command))) return null;
  const text = command ? (Array.isArray(p.command) ? p.command.join(' ') : p.command) : JSON.stringify(p);
  if (command && !text.trim()) return null;
  const outcome = evaluate(text, settings);
  if (!outcome.allow || settings.dryRun) return {respond: false, reason: settings.dryRun ? 'dry run' : outcome.reason};
  const modern = request.method.startsWith('item/');
  if (command && p.availableDecisions && !p.availableDecisions.includes('accept')) return null;
  return {respond: true, result: {decision: modern ? 'accept' : 'approved'}};
}
module.exports = {decision};
