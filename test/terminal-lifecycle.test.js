const test = require('node:test');
const assert = require('node:assert/strict');
const vm = require('node:vm');
const fs = require('node:fs');
const {createRequire} = require('node:module');
const localRequire = createRequire(require.resolve('../src/extension.js'));
const pause = ms => new Promise(resolve => setTimeout(resolve, ms));
const prompt = 'Would you like to run the following command?\r\n\r\n $ echo hello\r\n\r\n› 1. Yes, proceed (y)\r\n  3. No, and tell Codex what to do differently (esc)\r\n\r\nPress enter to confirm or esc to cancel';
function host(t, options = {}) {
  const listeners = {}, commands = {}, values = {enabled: true, ...options.values}, sent = [];
  const event = name => fn => { listeners[name] = fn; return {dispose() {}}; };
  const cfg = {inspect: key => ({globalValue: values[key.replace(/^codexAutoAccept\./, '')]}), update: async (key, value) => {if (options.fail) throw Error('settings file is read-only');values[key.replace(/^codexAutoAccept\./, '')] = value;if (!options.noEvent) listeners.config({affectsConfiguration:()=>true});}};
  const status = {show(){},dispose(){}};
  const errors = [];
  const vscode = {
    workspace: {isTrusted: true, getConfiguration: () => cfg, onDidChangeConfiguration:event('config')},
    extensions: {onDidChange:event('extensions')},
    commands: {registerCommand:(name,fn)=>{commands[name]=fn;return {dispose(){}};}},
    ConfigurationTarget: {Global: 1}, StatusBarAlignment: {Right: 1},
    window: {
      createOutputChannel: () => ({appendLine(){},dispose(){}}),
      createStatusBarItem: () => status,
      showErrorMessage: async message => {errors.push(message);},
      onDidStartTerminalShellExecution:event('start'), onDidEndTerminalShellExecution:event('end'),
      onDidCloseTerminal:event('close')
    }
  };
  const module = {exports:{}};
  vm.runInNewContext(fs.readFileSync(require.resolve('../src/extension.js'),'utf8'), {
    module, require: name => name === 'vscode' ? vscode : localRequire(name), setTimeout, clearTimeout
  });
  const context = {extensionPath: process.cwd(), globalState:{get:()=>false}, subscriptions:[]};
  module.exports.activate(context);
  let queue = [], waiting, closed = false;
  const execution = {commandLine:{value:'codex'}, read:()=>({
    [Symbol.asyncIterator]() {return this;},
    next() {
      if (queue.length) return Promise.resolve({value:queue.shift(),done:false});
      if (closed) return Promise.resolve({done:true});
      return new Promise(resolve=>{waiting=resolve;});
    }
  })};
  const terminal = {sendText:(...args)=>sent.push(args)};
  const finish = () => {closed=true;waiting?.({done:true});listeners.end({execution,terminal});};
  t.after(()=>{finish();for(const disposable of context.subscriptions) disposable.dispose();});
  listeners.start({execution, terminal});
  const write = data => {if(waiting){const resolve=waiting;waiting=undefined;resolve({value:data,done:false});}else queue.push(data);};
  return {write, finish, sent, values, listeners, terminal, commands, status, errors};
}
test('CLI sends a single y; repeated ANSI redraw does not resend', async t => {
  const h = host(t);
  h.write(prompt); await pause(320);
  assert.deepEqual(h.sent.map(a=>Array.from(a)),[['y',false]]);
  h.write('\x1b[H'+prompt); await pause(320);
  assert.equal(h.sent.length,1);
});
test('turning OFF before evaluation cancels approval, and ON re-evaluates the pending prompt', async t => {
  const h=host(t);
  h.write(prompt); await pause(20);
  await h.commands['codexAutoAccept.toggle'](); await pause(320);
  assert.equal(h.sent.length,0);
  await h.commands['codexAutoAccept.toggle'](); await pause(320);
  assert.equal(h.sent.length,1);
});
test('execution ending with a pending timer cannot type into the shell', async t => {
  const h=host(t);
  h.write(prompt); await pause(20); h.finish(); await pause(320);
  assert.equal(h.sent.length,0);
});
test('terminal closure and dry run leave confirmation manual', async t => {
  const h=host(t);
  h.values.dryRun=true; h.write(prompt); await pause(320);
  assert.equal(h.sent.length,0);
  h.values.dryRun=false; h.listeners.config({affectsConfiguration:()=>true});
  h.listeners.close(h.terminal); await pause(320);
  assert.equal(h.sent.length,0);
});

test('click command saves and refreshes the status even without a configuration event', async t => {
  const h=host(t,{noEvent:true,values:{enabled:false}});
  assert.match(h.status.text,/OFF/);
  await h.commands['codexAutoAccept.toggle']();
  assert.equal(h.values.enabled,true);assert.match(h.status.text,/ON/);
  await h.commands['codexAutoAccept.toggle']();
  assert.equal(h.values.enabled,false);assert.match(h.status.text,/OFF/);
});
test('malformed unrelated setting does not trap the ON/OFF switch', async t => {
  const h=host(t,{values:{enabled:false,terminalRows:0}});
  await h.commands['codexAutoAccept.toggle']();assert.equal(h.values.enabled,true);assert.match(h.status.text,/ON.*SETTINGS ERROR/);
  h.write(prompt);await pause(320);assert.equal(h.sent.length,0);
  await h.commands['codexAutoAccept.toggle']();assert.equal(h.values.enabled,false);assert.match(h.status.text,/OFF/);
});
test('failed writes show an error, preserve OFF, and allow retry', async t => {
  const options={fail:true,values:{enabled:false}};const h=host(t,options);
  await h.commands['codexAutoAccept.toggle']();assert.equal(h.values.enabled,false);assert.match(h.status.text,/OFF/);assert.equal(h.errors.length,1);
  options.fail=false;await h.commands['codexAutoAccept.toggle']();assert.equal(h.values.enabled,true);assert.match(h.status.text,/ON/);
});
test('rapid clicks are serialized and each toggles the saved value', async t => {
  const h=host(t,{values:{enabled:false}});
  await Promise.all([h.commands['codexAutoAccept.toggle'](),h.commands['codexAutoAccept.toggle']()]);
  assert.equal(h.values.enabled,false);assert.match(h.status.text,/OFF/);
});
