const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const MARKER = '/* codex-autoaccept:v1 */';
// Verified against openai.chatgpt 26.1002.51308. Refuse unknown/minifier-changed layouts.
const ANCHOR = 'this.requestUserInputAutoResolutionCoordinator.observeServerRequest(e);for(let[,n]of this.providers)n.onRequest?.(e);';
const INJECT = `${MARKER}try{let __caa=require("./codex-autoaccept-runtime.cjs").decide(e);if(__caa){this.sendResponse(e.id,__caa);return{routeKind:"request",method:e.method}}}catch{};`;
const hash = s => crypto.createHash('sha256').update(s).digest('hex');
function paths(root) {
  const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'));
  if (pkg.publisher !== 'openai' || pkg.name !== 'chatgpt' || pkg.main !== './out/extension.js') throw Error('Unsupported Codex extension layout.');
  const file = path.join(root, 'out/extension.js');
  return {file, backup: file + '.codex-autoaccept-original', meta: file + '.codex-autoaccept.json', helper: path.join(root, 'out/codex-autoaccept-runtime.cjs')};
}
function patchedSource(source) {
  if (source.includes(MARKER) || source.split(ANCHOR).length !== 2 || !source.includes('sendResponse(e,r){this.turnCwds.observeClientResponse')) throw Error('Unsupported or already modified Codex bundle. No patch applied.');
  const updated = source.replace(ANCHOR, ANCHOR.replace(';for', ';' + INJECT + 'for'));
  new vm.Script(updated); // Parse before writing anything.
  return updated;
}
function atomicWrite(file, data) {
  const tmp = file + '.tmp-' + crypto.randomUUID();
  try { fs.writeFileSync(tmp, data, {mode: fs.existsSync(file) ? fs.statSync(file).mode : 0o600}); fs.renameSync(tmp, file); }
  finally { if (fs.existsSync(tmp)) fs.unlinkSync(tmp); }
}
function install(root, runtime) {
  const p = paths(root), source = fs.readFileSync(p.file, 'utf8');
  if (source.includes(MARKER)) {
    const m = JSON.parse(fs.readFileSync(p.meta, 'utf8'));
    if (hash(source) !== m.patched || hash(fs.readFileSync(p.backup)) !== m.original) throw Error('Patched bundle or backup changed; refusing to overwrite.');
    atomicWrite(p.helper, fs.readFileSync(runtime));
    return 'already installed';
  }
  if (fs.existsSync(p.backup) || fs.existsSync(p.meta)) throw Error('Existing backup metadata found. Restore/inspect before reinstalling.');
  const updated = patchedSource(source);
  const helper = fs.readFileSync(runtime);
  new vm.Script(helper.toString());
  fs.writeFileSync(p.backup, source, {flag: 'wx', mode: fs.statSync(p.file).mode});
  try {
    atomicWrite(p.helper, helper);
    atomicWrite(p.meta, JSON.stringify({original: hash(source), patched: hash(updated)}));
    atomicWrite(p.file, updated);
  } catch (err) {
    // The original bundle is written last; preserve backup for recovery on any failure.
    throw Error(`Install failed; original backup retained: ${err.message}`);
  }
  return 'installed';
}
function restore(root) {
  const p = paths(root);
  if (!fs.existsSync(p.meta) && !fs.existsSync(p.backup)) return 'not installed';
  const source = fs.readFileSync(p.file), backup = fs.readFileSync(p.backup);
  if (hash(source) !== hash(backup)) {
    const m = JSON.parse(fs.readFileSync(p.meta, 'utf8'));
    if (hash(source) !== m.patched || hash(backup) !== m.original) throw Error('Bundle or backup changed; refusing unsafe restore.');
    atomicWrite(p.file, backup);
  }
  for (const f of [p.meta, p.backup, p.helper]) if (fs.existsSync(f)) fs.unlinkSync(f);
  return 'restored';
}
module.exports = {install, restore, patchedSource, ANCHOR, MARKER};
