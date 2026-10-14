const test = require('node:test');
const assert = require('node:assert/strict');
const {createExpandedApproval, candidate, pager} = require('../lib/expanded-approval');
const modal = cmd => `Would you like to run the following command?\nReason: long script\n $ ${cmd}\n+30 lines ctrl+a to view all\n› 1. Yes, proceed (y)\n 2. No, and tell Codex what to do differently (esc)\nPress enter to confirm or esc to cancel`;
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
