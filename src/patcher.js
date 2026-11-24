const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const vm = require('node:vm');
const MARKER = '/* codex-autoaccept:v1 */';
// Match the verified request branch even when the minifier renames its local variables.
const REQUEST_ROUTE = /(?<before>if\(this\.isMcpRequestMessage\((?<request>[\w$]+)\)\)\{this\.turnCwds\.observeServerRequest\(\k<request>\),this\.requestUserInputAutoResolutionCoordinator\.observeServerRequest\(\k<request>\);)for\(let\[,(?<provider>[\w$]+)\]of this\.providers\)\k<provider>\.onRequest\?\.\(\k<request>\);return\{routeKind:"request",method:\k<request>\.method\}\}/g;
const RESPONSE_SENDER = /\bsendResponse\(([\w$]+),([\w$]+)\)\{this\.turnCwds\.observeClientResponse\(\{id:\1,result:\2\}\)/;
const hash = s => crypto.createHash('sha256').update(s).digest('hex');
function paths(root) {
  const pkg = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8'));
  if (pkg.publisher !== 'openai' || pkg.name !== 'chatgpt' || pkg.main !== './out/extension.js') throw Error('Unsupported Codex extension layout.');
  const file = path.join(root, 'out/extension.js');
  return {file, backup: file + '.codex-autoaccept-original', meta: file + '.codex-autoaccept.json', helper: path.join(root, 'out/codex-autoaccept-runtime.cjs')};
}
function patchedSource(source) {
  const routes = Array.from(source.matchAll(REQUEST_ROUTE));
  if (source.includes(MARKER) || routes.length !== 1 || !RESPONSE_SENDER.test(source)) throw Error('Unsupported or already modified Codex bundle. No patch applied.');
  const route = routes[0];
  const {before, request} = route.groups;
  const index = route.index + before.length;
  const inject = `${MARKER}try{let __caa=require("./codex-autoaccept-runtime.cjs").decide(${request});if(__caa){this.sendResponse(${request}.id,__caa);return{routeKind:"request",method:${request}.method}}}catch{};`;
  const updated = source.slice(0, index) + inject + source.slice(index);
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
    const helper = fs.readFileSync(runtime);
    if (fs.existsSync(p.helper) && fs.readFileSync(p.helper).equals(helper)) return 'already installed';
    new vm.Script(helper.toString());
    atomicWrite(p.helper, helper);
    return 'runtime updated';
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
// isActive can still be false while VS Code is activating an already-loaded entry point.
function isLoaded(root) {
  return Boolean(require.cache[fs.realpathSync(path.join(root, 'out/extension.js'))]);
}
module.exports = {install, restore, patchedSource, isLoaded, MARKER};
