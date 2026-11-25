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
  const errors = [], notices = [];
  const vscode = {
    workspace: {isTrusted: true, getConfiguration: () => cfg, onDidChangeConfiguration:event('config')},
    extensions: {onDidChange:event('extensions'), getExtension: () => options.vendor},
    commands: {registerCommand:(name,fn)=>{commands[name]=fn;return {dispose(){}};}},
    ConfigurationTarget: {Global: 1}, StatusBarAlignment: {Right: 1},
    window: {
      createOutputChannel: () => ({appendLine(){},dispose(){}}),
      createStatusBarItem: () => status,
      showErrorMessage: async message => {errors.push(message);},
      showInformationMessage: async message => {notices.push(message);},
      onDidStartTerminalShellExecution:event('start'), onDidEndTerminalShellExecution:event('end'),
      onDidCloseTerminal:event('close')
    }
  };
  const module = {exports:{}};
  vm.runInNewContext(fs.readFileSync(require.resolve('../src/extension.js'),'utf8'), {
    module, require: name => {
      if (name === 'vscode') return vscode;
      if (name === './patcher' && options.patcher) return options.patcher;
      return localRequire(name);
    },
    setTimeout, clearTimeout,
    setInterval: fn => {listeners.guiCheck = fn; return {unref(){}};},
    clearInterval() {}
  });
  const context = {
    extensionPath: process.cwd(),
    globalState: {
      get: () => options.guiInstalled,
      update: async (key, value) => {options.guiInstalled = value;}
    },
    subscriptions: []
  };
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
  return {write, finish, sent, values, listeners, terminal, commands, status, errors, notices};
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

test('ON prepares a new Codex install before activation and repairs a later version once', t => {
  const installed = [];
  const options = {
    vendor: {extensionPath: '/old-codex', packageJSON: {version: 'old'}, isActive: false},
    patcher: {install: path => {installed.push(path); return 'installed';}, fingerprint: () => 'same', loadedState: () => 'not loaded'}
  };
  const h = host(t, options);
  assert.deepEqual(installed, ['/old-codex']);
  assert.equal(h.notices.length, 0);
  options.vendor = {extensionPath: '/updated-codex', packageJSON: {version: 'new'}, isActive: false};
  options.patcher.loadedState = () => 'stale';
  h.listeners.extensions();
  h.listeners.extensions();
  assert.deepEqual(installed, ['/old-codex', '/updated-codex']);
  assert.match(h.status.text, /ON.*GUI RELOAD/);
  assert.equal(h.notices.length, 1);
});

test('OFF leaves Codex untouched and explicitly restored GUI stays manual until reinstalled', async t => {
  let installs = 0;
  const options = {
    values: {enabled: false},
    vendor: {extensionPath: '/codex', packageJSON: {version: 'test'}},
    patcher: {
      install: () => {installs++; return 'installed';},
      restore: () => 'restored',
      fingerprint: () => 'same',
      loadedState: () => 'not loaded'
    }
  };
  const h = host(t, options);
  assert.equal(installs, 0);
  await h.commands['codexAutoAccept.toggle']();
  assert.equal(installs, 1);
  await h.commands['codexAutoAccept.restoreGui']();
  options.vendor.extensionPath = '/updated-codex';
  h.listeners.extensions();
  assert.equal(installs, 1);
  await h.commands['codexAutoAccept.installGui']();
  assert.equal(installs, 2);
  assert.equal(options.guiInstalled, true);
});

test('unsupported Codex integration is visible while ON and explicit repair can retry', async t => {
  let attempts = 0;
  const h = host(t, {
    vendor: {extensionPath: '/codex', packageJSON: {version: 'test'}},
    patcher: {
      fingerprint: () => 'same',
      loadedState: () => 'not loaded',
      install: () => {
        attempts++;
        if (attempts === 1) throw Error('Unsupported Codex bundle');
        return 'already installed';
      }
    }
  });
  assert.match(h.status.text, /ON.*GUI ERROR/);
  assert.match(h.status.tooltip, /Codex test.*Unsupported Codex bundle/);
  await h.commands['codexAutoAccept.installGui']();
  assert.equal(attempts, 2);
  assert.doesNotMatch(h.status.text, /GUI ERROR/);
});

test('a disk patch from another host still prompts to reload when this host loaded the stock bundle', t => {
  let installs = 0;
  const h = host(t, {
    vendor: {extensionPath: '/codex', packageJSON: {version: 'test'}, isActive: false},
    patcher: {
      fingerprint: () => 'already patched',
      loadedState: () => 'stale',
      install: () => {installs++; return 'already installed';}
    }
  });
  assert.match(h.status.text, /GUI RELOAD/);
  h.listeners.guiCheck();
  assert.equal(installs, 1);
  assert.equal(h.notices.length, 1);
});

test('health check repairs same-path replacement and confirms when the running host loads the hook', t => {
  let stamp = 'stock';
  let loaded = 'not loaded';
  let installs = 0;
  const h = host(t, {
    vendor: {extensionPath: '/codex', packageJSON: {version: 'test'}},
    patcher: {
      fingerprint: () => stamp,
      loadedState: () => loaded,
      install: () => {stamp = 'patched'; installs++; return 'installed';}
    }
  });
  assert.doesNotMatch(h.status.text, /GUI READY/);
  loaded = 'ready';
  h.listeners.guiCheck();
  assert.match(h.status.text, /GUI READY/);
  assert.equal(installs, 1);
  stamp = 'stock restored by updater';
  h.listeners.guiCheck();
  h.listeners.guiCheck();
  assert.equal(installs, 2);
  assert.match(h.status.text, /GUI READY/);
  assert.equal(h.notices.length, 0);
});
