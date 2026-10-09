const fs = require('node:fs');
const path = require('node:path');
function resolveCodex(override, {platform = process.platform, env = process.env, node = process.execPath, exists = fs.existsSync} = {}) {
  const p = platform === 'win32' ? path.win32 : path.posix;
  const dirs = (env.PATH ?? env.Path ?? '').split(platform === 'win32' ? ';' : ':').map(s => s.replace(/^"|"$/g, ''));
  const candidates = override ? [p.resolve(override)] : dirs.flatMap(d => platform === 'win32' ? [p.join(d, 'codex.exe'), p.join(d, 'codex.cmd'), p.join(d, 'codex.ps1')] : [p.join(d, 'codex')]);
  for (const candidate of candidates) {
    if (!exists(candidate)) continue;
    if (platform === 'win32' && /\.(cmd|bat|ps1)$/i.test(candidate)) {
      // Launch the official npm CLI entry directly: no cmd.exe /c string or shell interpolation.
      const script = p.join(p.dirname(candidate), 'node_modules', '@openai', 'codex', 'bin', 'codex.js');
      if (!exists(script)) throw Error(`Found a shell shim without the official npm Codex entry: ${candidate}. Use --codex with codex.exe or bin/codex.js.`);
      return {file: node, args: [script]};
    }
    if (/\.js$/i.test(candidate)) return {file: node, args: [candidate]};
    return {file: candidate, args: []};
  }
  throw Error('Codex was not found. Check codex --version, or supply --codex with an absolute executable path.');
}
module.exports = {resolveCodex};
