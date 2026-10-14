const fs = require('node:fs');
const DEFAULTS = {enabled: false, dryRun: false, approveFileChanges: true, expandLongCommands: true, logMaxBytes: 5 * 1024 * 1024, logBackupCount: 3, allowPatterns: [], denyPatterns: []};
function parse(args) {
  const options = {codexArgs: []};
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === '--') { options.codexArgs = args.slice(i + 1); break; }
    if (a === '--auto') options.enabled = true;
    else if (a === '--off') options.enabled = false;
    else if (a === '--dry-run') options.dryRun = true;
    else if (a === '--self-test') options.selfTest = true;
    else if (a === '--self-test-expanded') {options.selfTest = true; options.expandedSelfTest = true;}
    else if (a === '--help' || a === '-h') options.help = true;
    else if (['--config', '--codex', '--log'].includes(a)) {
      if (!args[i + 1] || args[i + 1].startsWith('--')) throw Error(`Missing value for ${a}`);
      options[a.slice(2)] = args[++i];
    } else throw Error(`Unknown wrapper option: ${a}. Put Codex arguments after --.`);
  }
  return options;
}
function settings(options) {
  const file = options.config ? JSON.parse(fs.readFileSync(options.config, 'utf8').replace(/^\uFEFF/, '')) : {};
  const result = {...DEFAULTS, ...file};
  for (const key of ['enabled', 'dryRun', 'approveFileChanges', 'expandLongCommands']) if (typeof result[key] !== 'boolean') throw Error(`Invalid ${key}: expected boolean`);
  for (const key of ['allowPatterns', 'denyPatterns']) {
    if (!Array.isArray(result[key]) || result[key].some(x => typeof x !== 'string')) throw Error(`Invalid ${key}: expected string array`);
    for (const pattern of result[key]) new RegExp(pattern, 'i');
  }
  if (!Number.isInteger(result.logMaxBytes) || result.logMaxBytes < 65536 || result.logMaxBytes > 104857600) throw Error('Invalid logMaxBytes: expected 65536..104857600');
  if (!Number.isInteger(result.logBackupCount) || result.logBackupCount < 0 || result.logBackupCount > 20) throw Error('Invalid logBackupCount: expected 0..20');
  if (options.enabled !== undefined) result.enabled = options.enabled;
  if (options.dryRun) result.dryRun = true;
  return result;
}
module.exports = {parse, settings};
