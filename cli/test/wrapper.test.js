const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const {parse, settings} = require('../lib/options');
const {resolveCodex} = require('../lib/resolve');
const {createSession} = require('../lib/session');
const {evaluate} = require('../lib/policy');
const sleep = ms => new Promise(resolve=>setTimeout(resolve,ms));
const prompt = cmd => `Would you like to run the following command?\r\n\r\n $ ${cmd}\r\n\r\n› 1. Yes, proceed (y)\r\n  3. No, and tell Codex what to do differently (esc)\r\n\r\nPress enter to confirm or esc to cancel`;
function session(t, enabled=true, extra={}) {
  const sent=[], logs=[], sizes=[];
  const s=createSession({child:{write:s=>sent.push(s),resize:(...s)=>sizes.push(s)}, settings:{...settings({}),enabled,...extra},cols:120,rows:40,log:(...s)=>logs.push(s)});
  t.after(()=>s.stop());return {s,sent,logs,sizes};
}
test('wrapper flags stay separate from Codex arguments',()=>{
  assert.deepEqual(parse(['--auto','--','resume','--last']),{enabled:true,codexArgs:['resume','--last']});
  assert.throws(()=>parse(['--config']),/Missing/);
  assert.throws(()=>parse(['resume']),/after --/);
  assert.equal(settings({}).enabled,false);
});
test('config validates before spawn and --off overrides enabled',t=>{
  const dir=fs.mkdtempSync(path.join(os.tmpdir(),'codex-auto-cli-'));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));
  const config=path.join(dir,'config.json');
  fs.writeFileSync(config,JSON.stringify({enabled:true}));assert.equal(settings({config,enabled:false}).enabled,false);
  fs.writeFileSync(config,JSON.stringify({denyPatterns:['[']}));assert.throws(()=>settings({config}));
  fs.writeFileSync(config,JSON.stringify({enabled:'false'}));assert.throws(()=>settings({config}),/boolean/);
});
test('Windows npm shim launches official script through node without a command shell',()=>{
  const files=new Set(['C:\\Program Files\\nodejs\\codex.cmd','C:\\Program Files\\nodejs\\node_modules\\@openai\\codex\\bin\\codex.js']);
  const result=resolveCodex(undefined,{platform:'win32',env:{Path:'C:\\Program Files\\nodejs'},node:'C:\\node.exe',exists:f=>files.has(f)});
  assert.deepEqual(result,{file:'C:\\node.exe',args:['C:\\Program Files\\nodejs\\node_modules\\@openai\\codex\\bin\\codex.js']});
  assert.throws(()=>resolveCodex(undefined,{platform:'win32',env:{PATH:'C:\\tools'},exists:f=>f.endsWith('codex.cmd')}),/without the official/);
});
test('Windows executable override is passed directly',()=>{
  assert.deepEqual(resolveCodex('C:\\tools\\codex.exe',{platform:'win32',exists:()=>true}),{file:'C:\\tools\\codex.exe',args:[]});
});
test('PowerShell/cmd destructive commands remain manual',()=>{
  for(const cmd of ['Remove-Item -Recurse x','ri x','cmd /c del x','rd /s /q x','Format-Volume x','diskpart','iex x']) assert.equal(evaluate(cmd,settings({})).allow,false,cmd);
});
test('real ANSI processing sends y exactly once per visible prompt',async t=>{
  const {s,sent}=session(t);s.feed('\x1b[2J\x1b[H'+prompt('echo hello'));await sleep(330);assert.deepEqual(sent,['y']);
  s.feed('\x1b[H'+prompt('echo hello'));await sleep(330);assert.deepEqual(sent,['y']);
  s.feed('\x1b[2J\x1b[Hfinished');await sleep(330);
  s.feed('\x1b[2J\x1b[H'+prompt('echo hello'));await sleep(330);assert.deepEqual(sent,['y','y']);
});
test('Ctrl+] toggles; Ctrl+C and other input reach the child',async t=>{
  const {s,sent}=session(t,false);s.feed(prompt('echo hi'));await sleep(330);assert.deepEqual(sent,[]);
  s.input('\x1d');await sleep(330);assert.deepEqual(sent,['y']);
  s.input('abc\x03');assert.deepEqual(sent,['y','abc\x03']);
  s.input('\x1d');s.feed('\x1b[2J\x1b[H'+prompt('echo other'));await sleep(330);assert.equal(sent.length,2);
});
test('stop cancels pending keys; resize reaches PTY and emulator',async t=>{
  const {s,sent,sizes}=session(t);s.resize(100,30);assert.deepEqual(sizes,[[100,30]]);
  s.feed(prompt('echo hi'));s.stop();await sleep(330);assert.deepEqual(sent,[]);
});
test('dry run and denied prompt do not auto-answer',async t=>{
  const {s,sent,logs}=session(t,true,{dryRun:true});s.feed(prompt('echo hi'));await sleep(330);assert.deepEqual(sent,[]);assert.equal(logs[0][0],'DRY_RUN');
  const blocked=session(t);blocked.s.feed(prompt('Remove-Item -Recurse x'));await sleep(330);assert.deepEqual(blocked.sent,[]);assert.equal(blocked.logs[0][0],'MANUAL');
});
test('log errors stop approval before writing a key',async t=>{
  const sent=[],errors=[];
  const s=createSession({child:{write:s=>sent.push(s)},settings:{...settings({}),enabled:true},cols:120,rows:40,log:()=>{throw Error('disk full');},onError:e=>errors.push(e.message)});
  t.after(()=>s.stop());s.feed(prompt('echo hi'));await sleep(330);assert.deepEqual(sent,[]);assert.deepEqual(errors,['disk full']);
});

test('continuous redraw cannot starve approval evaluation',async t=>{
  const {s,sent}=session(t);
  s.feed('\x1b[2J\x1b[H'+prompt('echo hi'));
  for(let i=0;i<10;i++) {await sleep(60);s.feed('\x1b[H'+prompt('echo hi'));}
  assert.deepEqual(sent,['y']);
});
test('soft wrapped menu and command are reconstructed in a narrow terminal',async t=>{
  const {s,sent}=session(t);s.resize(28,40);
  s.feed('\x1b[2J\x1b[H'+prompt('echo this-is-a-long-harmless-command'));
  await sleep(330);assert.deepEqual(sent,['y']);
  s.resize(35,40);await sleep(330);assert.deepEqual(sent,['y']);
});
test('two identical approvals separated by brief output each receive one key',async t=>{
  const {s,sent}=session(t);s.feed('\x1b[2J\x1b[H'+prompt('echo hi'));
  await sleep(330);assert.deepEqual(sent,['y']);
  s.feed('\x1b[2J\x1b[Hdone');await sleep(30);
  s.feed('\x1b[2J\x1b[H'+prompt('echo hi'));await sleep(330);
  assert.deepEqual(sent,['y','y']);
});
test('incomplete menu logs metadata and remains manual until complete',async t=>{
  const {s,sent,logs}=session(t);
  s.feed('Would you like to run the following command?');await sleep(330);
  assert.deepEqual(sent,[]);assert.equal(logs[0][0],'UNRECOGNIZED');
  assert.equal(logs[0][1].includes('echo'),false);
  s.feed('\x1b[2J\x1b[H'+prompt('echo hi'));await sleep(330);assert.deepEqual(sent,['y']);
});
test('wrapped destructive commands and clipped content still stay manual',async t=>{
  const {s,sent,logs}=session(t);s.resize(28,40);
  s.feed('\x1b[2J\x1b[H'+prompt('Remove-Item -Recurse company-folder'));await sleep(330);
  assert.deepEqual(sent,[]);assert.deepEqual(logs[0],['MANUAL','deny rule']);
  s.feed('\x1b[2J\x1b[H'+prompt('echo clipped…'));await sleep(330);
  assert.deepEqual(sent,[]);assert.equal(logs.at(-1)[1],'clipped content');
});
test('partial redraw of an accepted menu does not send another approval',async t=>{
  const {s,sent}=session(t);s.feed('\x1b[2J\x1b[H'+prompt('echo hi'));await sleep(330);
  s.feed('\x1b[2J\x1b[HWould you like to run the following command?');await sleep(330);
  s.feed('\x1b[2J\x1b[H'+prompt('echo hi'));await sleep(330);
  assert.deepEqual(sent,['y']);
});
test('reason summaries do not mark a complete pwsh command as clipped or denied',async t=>{
  const {s,sent}=session(t);
  const full=prompt('pwsh -Command "Write-Output hello; Get-Process | Select-Object Name"')
    .replace('\r\n\r\n $', '\r\nEnvironment: local\r\nReason: pwsh ... diagnostic summary … Ctrl+O; mentions Remove-Item for context\r\n\r\n $');
  s.feed('\x1b[2J\x1b[H'+full);await sleep(330);assert.deepEqual(sent,['y']);
});
test('actual execution block clipping and destructive pwsh remain manual despite harmless reason',()=>{
  const {detect}=require('../lib/detector');
  for (const cmd of ['pwsh -Command "echo …"','pwsh -Command "Remove-Item -Recurse x"']) {
    const full=prompt(cmd).replace('\r\n\r\n $','\r\nReason: harmless diagnostic\r\n\r\n $');
    assert.equal(detect(full,settings({})).allow,false);
  }
});
test('diagnostics distinguish missing heading, options, footer and command without exposing script',()=>{
  const {diagnose}=require('../lib/detector');
  const full=prompt('pwsh -Command "private-company-script"');
  assert.equal(diagnose(full),'');
  const cases=[
    ['Would you like to run the following command?','not an approval heading','heading'],
    ['Yes, proceed (y)','Allow once','Yes'],
    ['No, and tell Codex what to do differently (esc)','Cancel','No'],
    ['Press enter to confirm or esc to cancel','confirm','footer'],
    [' $ pwsh',' pwsh','command start']
  ];
  for(const [from,to,reason] of cases) {
    const diagnostic=diagnose(full.replace(from,to));
    assert.ok(diagnostic.includes(reason),diagnostic);
    assert.ok(!diagnostic.includes('private-company-script'));
  }
  assert.ok(diagnose(full+'\nextra status').includes('footer'));
});
test('long multiline pwsh execution block with reason and complete footer is recognized',async t=>{
  const {s,sent}=session(t);s.resize(180,60);
  const script='$ErrorActionPreference = "Continue";\r\n'+Array.from({length:20},(_,i)=>`Write-Output "diagnostic ${i}"`).join('\r\n');
  s.feed('\x1b[2J\x1b[H'+prompt(script).replace('\r\n\r\n $','\r\nEnvironment: local\r\nReason: pwsh diagnostics\r\n\r\n $'));
  await sleep(330);assert.deepEqual(sent,['y']);
});
test('cursor redraw onto previously wrapped rows preserves approval boundaries',async t=>{
  const {s,sent,logs}=session(t);s.resize(120,40);
  // First create wrapped rows. TUI then reuses those rows via cursor movement,
  // without an erase-screen sequence that would clear their wrap flags.
  s.feed('\x1b[H'+'old history '.repeat(180));await sleep(40);
  const rows=prompt('echo harmless').split('\r\n');
  rows.forEach((line,i)=>s.feed(`\x1b[${i+2};1H${line.padEnd(119)}`));
  s.feed(`\x1b[${rows.length+2};1H\x1b[J`);
  await sleep(330);assert.deepEqual(sent,['y'],JSON.stringify(logs));
});
test('dangerous command on previously wrapped rows remains manual',async t=>{
  const {s,sent,logs}=session(t);s.resize(120,40);
  s.feed('\x1b[H'+'old history '.repeat(180));await sleep(40);
  const rows=prompt('pwsh -Command "Remove-Item -Recurse x"').split('\r\n');
  rows.forEach((line,i)=>s.feed(`\x1b[${i+2};1H${line.padEnd(119)}`));
  s.feed(`\x1b[${rows.length+2};1H\x1b[J`);
  await sleep(330);assert.deepEqual(sent,[]);assert.deepEqual(logs.at(-1),['MANUAL','deny rule']);
});
test('standalone dollar marker is followed by full execution content and policy',()=>{
  const {detect}=require('../lib/detector');
  const full=prompt('echo hello').replace(' $ echo hello',' $\r\n echo hello');
  assert.equal(detect(full,settings({})).allow,true);
  assert.equal(detect(full.replace('echo hello','Remove-Item -Recurse x'),settings({})).allow,false);
  assert.equal(detect(full.replace('echo hello','echo …'),settings({})).allow,false);
  assert.equal(detect(full.replace(' $\r\n echo hello','Reason: echo hello'),settings({})),null);
});
test('physical rows recover a dollar marker merged into reason by stale wrap flags',async t=>{
  const {s,sent,logs}=session(t);s.resize(120,40);
  s.feed('\x1b[H'+'old history '.repeat(180));await sleep(40);
  const full=prompt('echo hello').replace(' $ echo hello',' $\r\n echo hello')
    .replace('\r\n\r\n $','\r\nReason: diagnostic\r\n $');
  const rows=full.split('\r\n');
  rows.forEach((line,i)=>s.feed(`\x1b[${i+2};1H${line.trim() === '$' ? ' '.repeat(119) + '$' : line.padEnd(119)}`));
  s.feed(`\x1b[${rows.length+2};1H\x1b[J`);await sleep(330);
  assert.deepEqual(sent,['y'],JSON.stringify(logs));
  assert.deepEqual(logs.at(-1),['ACCEPT','once; physical-row fallback']);
  // Same prompt redrawn without stale wrap metadata must not cause another key.
  s.feed('\x1b[2J\x1b[H'+full);await sleep(330);assert.deepEqual(sent,['y']);
});
test('physical fallback never bypasses deny policy on the recovered execution',async t=>{
  const {s,sent,logs}=session(t);s.resize(120,40);
  s.feed('\x1b[H'+'old history '.repeat(180));await sleep(40);
  const full=prompt('Remove-Item -Recurse x').replace(' $ Remove-Item',' $\r\n Remove-Item')
    .replace('\r\n\r\n $','\r\nReason: harmless summary\r\n $');
  const rows=full.split('\r\n');
  rows.forEach((line,i)=>s.feed(`\x1b[${i+2};1H${line.padEnd(119)}`));
  s.feed(`\x1b[${rows.length+2};1H\x1b[J`);await sleep(330);
  assert.deepEqual(sent,[]);assert.deepEqual(logs.at(-1),['MANUAL','deny rule']);
});
test('a known hidden-lines footer does not allow unseen command content',()=>{
  const {detect}=require('../lib/detector');
  const full=prompt('echo shown\r\n… +188 lines ctrl+a to view all');
  assert.equal(detect(full,settings({})).allow,false);
  const noEllipsis=prompt('echo shown\r\n+188 lines ctrl+a to view all');
  assert.equal(detect(noEllipsis,settings({})).reason,'clipped content');
});
test('an expansion log failure stops keys and cleanup cannot throw again',async t=>{
  const sent=[],errors=[];
  const s=createSession({child:{write:x=>sent.push(x)},settings:{...settings({}),enabled:true},cols:120,rows:40,
    log:()=>{throw Error('disk full');},onError:e=>errors.push(e.message)});
  t.after(()=>s.stop());
  s.feed(prompt('echo shown\r\n[\u2026 70 lines] ctrl+a view all'));await sleep(330);
  assert.deepEqual(sent,[]);assert.deepEqual(errors,['disk full']);
  assert.doesNotThrow(()=>s.stop());
});
