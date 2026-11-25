const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const LEGACY_MARKER = '/* codex-autoaccept:v1 */';
const MARKER = '/* codex-autoaccept:v2 */';
const HELPER = 'codex-autoaccept-runtime.cjs';
// Verify the request guard, provider dispatch and return. Observer names and ordering can change.
const REQUEST_ROUTE = /(?<before>if\(this\.isMcpRequestMessage\((?<request>[\w$]+)\)\)\{(?:this\.[\w$]+\.observeServerRequest\(\k<request>\)[,;])*?)for\(let\[,(?<provider>[\w$]+)\]of this\.providers\)\k<provider>\.onRequest\?\.\(\k<request>\);return\{routeKind:"request",method:\k<request>\.method\}\}/g;
const RESPONSE_SENDER = /\bsendResponse\(([\w$]+),([\w$]+)\)\{this\.[\w$]+\.observeClientResponse\(\{id:\1,result:\2\}\)/;
const hash = s => crypto.createHash('sha256').update(s).digest('hex');
function files(root) {
  const file = path.join(root, 'out/extension.js');
  return {file, backup: file + '.codex-autoaccept-original', meta: file + '.codex-autoaccept.json', helper: path.join(root, 'out', HELPER)};
}
function paths(root) {
  const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'));
  if (pkg.publisher !== 'openai' || pkg.name !== 'chatgpt' || pkg.main !== './out/extension.js') throw Error('Unsupported Codex extension layout.');
  return files(root);
}
function patchedSource(source) {
  const routes = Array.from(source.matchAll(REQUEST_ROUTE));
  if (source.includes(MARKER) || source.includes(LEGACY_MARKER) || routes.length !== 1 || !RESPONSE_SENDER.test(source)) throw Error('Unsupported or already modified Codex bundle. No patch applied.');
  const route = routes[0];
  const {before, request} = route.groups;
  const index = route.index + before.length;
  const inject = `${MARKER}try{let __caa=require("./${HELPER}").decide(${request});if(__caa){this.sendResponse(${request}.id,__caa);return{routeKind:"request",method:${request}.method}}}catch{};`;
  // Record execution of this bundle in the current extension host, independently of disk state.
  const announce = `\n;try{require("./${HELPER}").markLoaded(__filename)}catch{};\n`;
  const updated = source.slice(0, index) + inject + source.slice(index) + announce;
  new vm.Script(updated); // Parse before writing anything.
  return updated;
}
function atomicWrite(file, data) {
  const tmp = file + '.tmp-' + crypto.randomUUID();
  try { fs.writeFileSync(tmp, data, {mode: fs.existsSync(file) ? fs.statSync(file).mode : 0o600}); fs.renameSync(tmp, file); }
  finally { if (fs.existsSync(tmp)) fs.unlinkSync(tmp); }
}
function install(root, runtime) {
  const p = paths(root);
  const source = fs.readFileSync(p.file, 'utf8');
  const sourceHash = hash(source);
  const hasBackup = fs.existsSync(p.backup);
  const meta = fs.existsSync(p.meta) ? JSON.parse(fs.readFileSync(p.meta, 'utf8')) : null;
  const original = hasBackup ? fs.readFileSync(p.backup, 'utf8') : source;
  const originalHash = hash(original);
  const knownPatch = hasBackup && meta && (sourceHash === meta.patched || sourceHash === meta.previousPatched);
  if ((meta && (!hasBackup || meta.original !== originalHash)) || (hasBackup && sourceHash !== originalHash && !knownPatch)) {
    throw Error('Patched bundle or backup changed; refusing to overwrite.');
  }
  const helper = fs.readFileSync(runtime);
  if (source.includes(MARKER) && knownPatch) {
    if (fs.existsSync(p.helper) && fs.readFileSync(p.helper).equals(helper)) return 'already installed';
    new vm.Script(helper.toString());
    atomicWrite(p.helper, helper);
    return 'runtime updated';
  }
  const updated = patchedSource(original);
  new vm.Script(helper.toString());
  if (!hasBackup) fs.writeFileSync(p.backup, original, {flag: 'wx', mode: fs.statSync(p.file).mode});
  try {
    atomicWrite(p.helper, helper);
    // Permit the verified old copy during the atomic replacement and after an interrupted upgrade.
    atomicWrite(p.meta, JSON.stringify({original: originalHash, patched: hash(updated), previousPatched: knownPatch ? sourceHash : undefined}));
    atomicWrite(p.file, updated);
  } catch (err) {
    // The original bundle is written last; preserve backup for recovery on any failure.
    throw new Error(`Install failed; original backup retained: ${err.message}`, {cause: err});
  }
  return 'installed';
}
function restore(root) {
  const p = paths(root);
  if (!fs.existsSync(p.meta) && !fs.existsSync(p.backup)) return 'not installed';
  const source = fs.readFileSync(p.file), backup = fs.readFileSync(p.backup);
  if (hash(source) !== hash(backup)) {
    const m = JSON.parse(fs.readFileSync(p.meta, 'utf8'));
    const sourceHash = hash(source);
    if ((sourceHash !== m.patched && sourceHash !== m.previousPatched) || hash(backup) !== m.original) throw Error('Bundle or backup changed; refusing unsafe restore.');
    atomicWrite(p.file, backup);
  }
  for (const f of [p.meta, p.backup, p.helper]) if (fs.existsSync(f)) fs.unlinkSync(f);
  return 'restored';
}
function fingerprint(root) {
  return Object.values(files(root)).map(file => {
    const stat = fs.statSync(file, {throwIfNoEntry: false});
    return stat ? `${stat.ino}:${stat.size}:${stat.mtimeMs}:${stat.ctimeMs}` : '';
  }).join('|');
}
function loadedState(root) {
  const {file, helper} = files(root);
  const entry = fs.existsSync(file) ? fs.realpathSync(file) : file;
  const cachedHelper = fs.existsSync(helper) ? require.cache[fs.realpathSync(helper)] : undefined;
  if (cachedHelper?.exports?.loadedFrom?.() === entry) return 'ready';
  return require.cache[entry] ? 'stale' : 'not loaded';
}
module.exports = {install, restore, patchedSource, fingerprint, loadedState, MARKER};
