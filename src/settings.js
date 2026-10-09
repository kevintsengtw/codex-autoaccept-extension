const DEFAULTS = {enabled: false, terminalEnabled: true, guiEnabled: true,
  approveFileChanges: true, dryRun: false, allowPatterns: [], denyPatterns: [], terminalColumns: 120, terminalRows: 40};
function readEnabled(vscode) {
  return vscode.workspace.getConfiguration('codexAutoAccept').inspect('enabled')?.globalValue === true;
}
function readSettings(vscode) {
  const cfg = vscode.workspace.getConfiguration('codexAutoAccept');
  // User intent remains independently readable even if another setting is malformed.
  const result = {...DEFAULTS, ...Object.fromEntries(Object.entries(DEFAULTS).map(([key, value]) =>
    [key, cfg.inspect(key)?.globalValue ?? value]))};
  const invalid = Object.entries(DEFAULTS).filter(([key, value]) =>
    Array.isArray(value) ? !Array.isArray(result[key]) || result[key].some(x => typeof x !== 'string') : typeof result[key] !== typeof value).map(([key]) => key);
  for (const [key, [min, max]] of Object.entries({terminalColumns: [20, 1000], terminalRows: [10, 300]})) {
    if (!Number.isInteger(result[key]) || result[key] < min || result[key] > max) invalid.push(key);
  }
  if (invalid.length) return {...DEFAULTS, enabled: readEnabled(vscode), configurationError: [...new Set(invalid)].join(', ')};
  return {...result, configurationError: ''};
}
module.exports = {readSettings, readEnabled};
