const test = require('node:test');
const assert = require('node:assert/strict');
const {createExpandedApproval, candidate, pager, diagnosePager} = require('../lib/expanded-approval');
const modal = cmd => `Would you like to run the following command?\nReason: long script\n $ ${cmd}\n[\u2026 30 lines] ctrl+a view all\n› 1. Yes, proceed (y)\n 2. No, and tell Codex what to do differently (esc)\nPress enter to confirm or esc to cancel`;
const view = (rows, pct=100, title='E X E C') => `/ ${title} / / /\n${rows.join('\n')}\n──────── ${pct}% ─\n ↑/↓ to scroll · pgup/pgdn to page · home/end to jump\n q close`;
function fixture(extra={}) {
  const writes=[],logs=[];let time=0;
  const settings={enabled:true,dryRun:false,expandLongCommands:true,allowPatterns:[],denyPatterns:[],...extra};
  const s=createExpandedApproval({child:{write:x=>writes.push(x)},settings,log:(...x)=>logs.push(x),now:()=>time});
  return {s,writes,logs,settings,advance:ms=>time+=ms,feed(text){return s.inspect({logical:text,physical:text});}};
}
test('only an explicit Ctrl+A command-approval hint can initiate expansion',()=>{
  for(const extra of [{enabled:false},{dryRun:true},{expandLongCommands:false}]) {
    const f=fixture(extra);f.feed(modal('echo hi'));assert.deepEqual(f.writes,[]);
  }
  const f=fixture();f.feed(modal('echo hi').replace('ctrl+a','ctrl+o'));assert.deepEqual(f.writes,[]);
  assert.equal(candidate(modal('echo hi').replace('run the following command','make the following edits')),null);
  assert.equal(pager(view(['echo hi'],100,'P A T C H')),null);
});
test('complete one-page command is evaluated and approved only after original modal returns',()=>{
  const f=fixture(),origin=modal('echo hidden');
  f.feed(origin);assert.deepEqual(f.writes,['\x01']);
  f.feed(view(['echo full command','~','~']));assert.deepEqual(f.writes,['\x01','q']);
  f.feed(origin);assert.deepEqual(f.writes,['\x01','q','y']);
  f.feed(origin);assert.deepEqual(f.writes,['\x01','q','y']);
});
test('multi-page command is collected through unique last-page overlap',()=>{
  const f=fixture(),origin=modal('echo a');
  f.feed(origin);
  f.feed(view(['echo a','echo b','echo c','echo d'],0));
  assert.deepEqual(f.writes,['\x01','\x1b[6~']);
  f.feed(view(['echo d','echo e','echo f','echo g'],100));
  assert.deepEqual(f.writes,['\x01','\x1b[6~','q']);
  f.feed(origin);assert.equal(f.writes.at(-1),'y');
});
test('a destructive command hidden on the last page remains manual',()=>{
  const f=fixture(),origin=modal('echo a');f.feed(origin);
  f.feed(view(['echo a','echo b','echo c','echo d'],0));
  f.feed(view(['echo d','echo e','Remove-Item -Recurse x','echo g'],100));
  f.feed(origin);assert.equal(f.writes.includes('y'),false);assert.equal(f.logs.at(-1)[0],'MANUAL');
});
test('100 percent rounding, stalled output, and ambiguous overlaps never skip to approval',()=>{
  const f=fixture(),origin=modal('echo a');f.feed(origin);
  f.feed(view(['echo a','echo b','echo c'],0));f.feed(view(['echo d','echo e','echo f'],100));
  assert.equal(f.writes.at(-1),'\x1b[6~');f.advance(3100);f.feed(view(['echo d','echo e','echo f'],100));
  assert.equal(f.writes.at(-1),'q');f.feed(origin);assert.equal(f.writes.includes('y'),false);
  const g=fixture();g.feed(origin);g.feed(view(['echo a','echo b','echo b'],0));
  g.feed(view(['echo b','echo b','echo c'],100));g.feed(origin);assert.equal(g.writes.includes('y'),false);
});
test('changed original approval, OFF, cancellation and unknown viewers cannot receive automatic y',()=>{
  const f=fixture();f.feed(modal('echo a'));f.feed(view(['echo full']));f.feed(modal('echo another'));assert.equal(f.writes.includes('y'),false);
  const g=fixture();g.feed(modal('echo a'));g.settings.enabled=false;g.feed(view(['echo full']));assert.deepEqual(g.writes,['\x01']);
  const h=fixture();h.feed(modal('echo a'));h.s.cancel('user input');h.feed(view(['echo full']));assert.deepEqual(h.writes,['\x01']);
  const k=fixture();k.feed(modal('echo a'));k.advance(3100);k.feed(view(['echo full'],100,'P A T C H'));assert.deepEqual(k.writes,['\x01']);
});
test('log errors prevent opening a viewer or sending an approval',()=>{
  const writes=[];const s=createExpandedApproval({child:{write:x=>writes.push(x)},settings:{enabled:true,expandLongCommands:true},log:()=>{throw Error('disk full');}});
  assert.throws(()=>s.inspect({logical:modal('echo hi'),physical:modal('echo hi')}),/disk full/);assert.deepEqual(writes,[]);
});
test('official bracketed ellipsis hints initiate expansion without requiring a plus sign',()=>{
  const original=modal('echo shown');
  assert.ok(original.includes('[\u2026 30 lines]'));
  for(const hint of ['[\u2026 38 lines] ctrl+a view all','[... 38 lines] ctrl+a view all','+38 lines ctrl+a view all','[\u2026 38 lines] | ctrl+a view all']) {
    const f=fixture();const actual=original.replace('[\u2026 30 lines] ctrl+a view all',hint);
    f.feed(actual);assert.deepEqual(f.writes,['\x01'],hint);
    f.feed(view(['echo full command']));f.feed(actual);assert.equal(f.writes.at(-1),'y');
  }
});
test('ellipsis without explicit expansion hint, remapped shortcut and missing approval options stay manual',()=>{
  const origin=modal('echo shown');
  for(const changed of [origin.replace('ctrl+a view all',''),origin.replace('ctrl+a','ctrl+o'),origin.replace('Yes, proceed (y)','Continue')]) {
    const f=fixture();f.feed(changed);assert.deepEqual(f.writes,[]);
  }
});
test('expanded-command deny logs carry rule IDs but no private command text',()=>{
  const f=fixture(),origin=modal('echo shown'),secret='C:\\PRIVATE_COMPANY_PATH';
  f.feed(origin);f.feed(view([`Remove-Item -Recurse "${secret}"`]));f.feed(origin);
  assert.equal(f.writes.includes('y'),false);
  assert.equal(f.logs.at(-1)[1],'expanded approval: deny rule; rules=B001:windows-file-delete');
  const serialized=JSON.stringify(f.logs);
  assert.equal(serialized.includes(secret),false);
  assert.equal(serialized.includes('Remove-Item'),false);
});
test('cropped heading and command start can open EXEC but cannot skip full review',()=>{
  const origin=modal('echo shown').split('\n').slice(3).join('\n');
  const f=fixture();assert.equal(candidate(origin).cropped,true);
  f.feed(origin);assert.deepEqual(f.writes,['\x01']);
  f.feed(view(['echo complete']));assert.deepEqual(f.writes,['\x01','q']);
  f.feed(origin);assert.deepEqual(f.writes,['\x01','q','y']);
  const g=fixture();g.feed(origin);g.feed(view(['rm -rf private']));g.feed(origin);
  assert.equal(g.writes.includes('y'),false);
  const h=fixture();h.feed(origin);h.advance(3100);h.feed(view(['echo text'],100,'P A T C H'));
  assert.deepEqual(h.writes,['\x01']);
});
test('cropped recovery requires complete hint, both options and footer before any key',()=>{
  const origin=modal('echo shown').split('\n').slice(3).join('\n');
  for(const text of [origin.replace('ctrl+a view all',''),origin.replace('ctrl+a','ctrl+o'),
    origin.replace('Yes, proceed (y)','Continue'),origin.replace('No, and tell Codex what to do differently (esc)','Cancel'),
    origin.replace('Press enter to confirm or esc to cancel',''),
    `Would you like to make the following edits?\n${origin}`]){
    const f=fixture();f.feed(text);assert.deepEqual(f.writes,[],text);
  }
  const f=fixture();f.feed(origin);f.feed(view(['echo complete']));
  f.feed(origin.replace('30 lines','31 lines'));assert.equal(f.writes.includes('y'),false);
});
test('expanded JSON query using rd variable passes but actual deletion remains manual',()=>{
  const f=fixture(),origin=modal('echo shown');f.feed(origin);
  f.feed(view(["$rd = Get-Content 'diagnostics.json' -Raw | ConvertFrom-Json",'$rd.PSObject.Properties.Name | ConvertTo-Json']));
  f.feed(origin);assert.deepEqual(f.writes,['\x01','q','y']);
  const g=fixture();g.feed(origin);g.feed(view(['$rd = 1; rd /s /q example']));g.feed(origin);
  assert.equal(g.writes.includes('y'),false);
  assert.equal(g.logs.at(-1)[1],'expanded approval: deny rule; rules=B001:windows-file-delete');
});
test('pager accepts blank padding but not a nonblank unknown header or remapped close key',()=>{
  assert.deepEqual(pager('\n\n'+view(['echo hi'])).rows,['echo hi']);
  assert.equal(pager('unknown banner\n'+view(['echo hi'])),null);
  assert.equal(pager(view(['echo hi']).replace('q close','esc close')),null);
});
test('last percentage separator preserves script rows that resemble pager chrome',()=>{
  const text=view(['echo hi','──── 50% ─','Remove-Item example']);
  assert.deepEqual(pager(text).rows,['echo hi','──── 50% ─','Remove-Item example']);
  const f=fixture(),origin=modal('echo shown');f.feed(origin);f.feed(text);f.feed(origin);
  assert.equal(f.writes.includes('y'),false);
});
test('slow initial viewer can complete after three seconds but unknown viewer expires privately',()=>{
  const origin=modal('echo shown'),f=fixture();f.feed(origin);f.advance(4000);
  f.feed('still rendering');assert.deepEqual(f.writes,['\x01']);
  f.feed(view(['echo complete']));f.feed(origin);assert.equal(f.writes.at(-1),'y');
  const g=fixture();g.feed(origin);g.advance(8100);
  g.feed(view(['PRIVATE_COMMAND_PATH'],100,'P A T C H'));
  assert.deepEqual(g.writes,['\x01']);
  assert.ok(g.logs.at(-1)[1].includes('missing EXEC header'));
  assert.equal(JSON.stringify(g.logs).includes('PRIVATE_COMMAND_PATH'),false);
});
test('pager diagnostics identify each structural boundary without command content',()=>{
  const text=view(['PRIVATE_COMMAND_PATH']);
  assert.equal(diagnosePager(text),'');
  assert.ok(diagnosePager(text.replace('100%','percent')).includes('percentage separator'));
  assert.ok(diagnosePager(text.replace('pgup/pgdn','unknown')).includes('pgup/pgdn'));
  assert.ok(diagnosePager(text.replace('q close','unknown')).includes('q close'));
});
test('expanded Git report is approved while actual forced Git operation stays manual',()=>{
  const origin=modal('echo shown'),f=fixture();f.feed(origin);
  f.feed(view(["$report = 'No Git write command executed; clean status; 06-preflight-inventory.stdout.txt'",'Set-Content report.json $report']));
  f.feed(origin);assert.deepEqual(f.writes,['\x01','q','y']);
  const g=fixture();g.feed(origin);g.feed(view(['git -C repo push origin main --force']));g.feed(origin);
  assert.equal(g.writes.includes('y'),false);
  assert.equal(g.logs.at(-1)[1],'expanded approval: deny rule; rules=B005:git-destructive');
});
