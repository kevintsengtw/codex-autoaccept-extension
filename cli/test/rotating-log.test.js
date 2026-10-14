const test=require('node:test');
const assert=require('node:assert/strict');
const fs=require('node:fs');
const os=require('node:os');
const path=require('node:path');
const {createRotatingLog}=require('../lib/rotating-log');
const {settings}=require('../lib/options');
function temp(t) {const dir=fs.mkdtempSync(path.join(os.tmpdir(),'codex-log-'));t.after(()=>fs.rmSync(dir,{recursive:true,force:true}));return path.join(dir,'cli.log');}
test('rotation bounds active/backup sizes, preserves latest records and removes oldest',t=>{
 const file=temp(t),log=createRotatingLog(file,{maxBytes:30,backups:2});
 for(let i=0;i<20;i++)log.append(`record-${String(i).padStart(2,'0')}\n`);
 const names=fs.readdirSync(path.dirname(file)).sort();assert.deepEqual(names,['cli.log','cli.log.1','cli.log.2']);
 for(const name of names)assert.ok(fs.statSync(path.join(path.dirname(file),name)).size<=30);
 assert.ok(fs.readFileSync(file,'utf8').endsWith('record-19\n'));
 const combined=names.map(n=>fs.readFileSync(path.join(path.dirname(file),n),'utf8')).join('');
 assert.equal(combined.includes('record-00'),false);assert.ok(combined.includes('record-19'));
});
test('old unlimited files are bounded using complete UTF-8 tail lines and unrelated files survive',t=>{
 const file=temp(t),text='\u4e2d\u6587-record\n';fs.writeFileSync(file,text.repeat(500));
 fs.writeFileSync(file+'.1',text.repeat(300));fs.writeFileSync(file+'.4','old backup');
 fs.writeFileSync(file+'.notes','keep');fs.writeFileSync(path.join(path.dirname(file),'other.log'),'keep');
 createRotatingLog(file,{maxBytes:100,backups:2}).append('newest\n');
 for(const name of ['cli.log','cli.log.1']) {const p=path.join(path.dirname(file),name);assert.ok(fs.statSync(p).size<=100);assert.equal(fs.readFileSync(p,'utf8').includes('\ufffd'),false);}
 assert.equal(fs.existsSync(file+'.4'),false);assert.equal(fs.readFileSync(file+'.notes','utf8'),'keep');assert.equal(fs.readFileSync(path.join(path.dirname(file),'other.log'),'utf8'),'keep');
});
test('zero backups discards only old active content; setting changes prune owned archives',t=>{
 const file=temp(t);fs.writeFileSync(file,'old\n');fs.writeFileSync(file+'.1','archive');
 const log=createRotatingLog(file,{maxBytes:10,backups:0});log.append('new-1\n');log.append('new-2\n');
 assert.equal(fs.readFileSync(file,'utf8'),'new-2\n');assert.equal(fs.existsSync(file+'.1'),false);
});
test('multiple logger objects share retention safely and leave no lock behind',t=>{
 const file=temp(t),a=createRotatingLog(file,{maxBytes:40,backups:2}),b=createRotatingLog(file,{maxBytes:40,backups:2});
 for(let i=0;i<10;i++)(i%2?a:b).append(`entry-${i}\n`);
 assert.equal(fs.existsSync(file+'.lock'),false);assert.ok(fs.readFileSync(file,'utf8').includes('entry-9'));
});
test('oversized records and unsafe file targets fail without truncating existing data',t=>{
 const file=temp(t);fs.writeFileSync(file,'keep\n');const log=createRotatingLog(file,{maxBytes:10,backups:1});
 assert.throws(()=>log.append('x'.repeat(11)),/exceeds/);assert.equal(fs.readFileSync(file,'utf8'),'keep\n');
 fs.unlinkSync(file);fs.mkdirSync(file);assert.throws(()=>log.append('line\n'),/regular file/);assert.equal(fs.existsSync(file+'.lock'),false);
});
test('retention config defaults are bounded and invalid settings are rejected',t=>{
 assert.equal(settings({}).logMaxBytes,5242880);assert.equal(settings({}).logBackupCount,3);
 const file=temp(t);
 for(const config of [{logMaxBytes:0},{logMaxBytes:1.5},{logBackupCount:-1},{logBackupCount:21}]) {
 fs.writeFileSync(file,JSON.stringify(config));assert.throws(()=>settings({config:file}));
 }
});
test('parallel processes rotate one shared log without partial entries or surplus files',async t=>{
 const {spawn}=require('node:child_process');const file=temp(t);
 const modulePath=require.resolve('../lib/rotating-log');
 const script=`const {createRotatingLog}=require(${JSON.stringify(modulePath)});const log=createRotatingLog(process.argv[1],{maxBytes:160,backups:3});for(let i=0;i<40;i++)log.append(process.argv[2]+':'+i+'\\n');`;
 await Promise.all(['A','B','C'].map(id=>new Promise((resolve,reject)=>{
  const child=spawn(process.execPath,['-e',script,file,id],{stdio:['ignore','ignore','pipe']});let error='';
  child.stderr.on('data',data=>error+=data);child.on('error',reject);child.on('exit',code=>code===0?resolve():reject(Error(error)));
 })));
 const files=fs.readdirSync(path.dirname(file)).sort();assert.ok(files.length<=4);
 for(const name of files) {
  const target=path.join(path.dirname(file),name);assert.ok(fs.statSync(target).size<=160);
  const lines=fs.readFileSync(target,'utf8').trim().split('\n');for(const line of lines)assert.match(line,/^[ABC]:\d+$/);
 }
 assert.equal(fs.existsSync(file+'.lock'),false);assert.equal(fs.existsSync(file+'.lock.reclaim'),false);
});
test('a lock belonging to an exited process can be recovered',async t=>{
 const {spawn}=require('node:child_process');const file=temp(t);
 const child=spawn(process.execPath,['-e','process.exit(0)']);const pid=child.pid;
 await new Promise(resolve=>child.on('exit',resolve));
 fs.mkdirSync(file+'.lock');fs.writeFileSync(path.join(file+'.lock','owner.json'),JSON.stringify({pid,token:'expired-owner'}));
 createRotatingLog(file,{maxBytes:100,backups:1}).append('recovered\n');
 assert.equal(fs.readFileSync(file,'utf8'),'recovered\n');assert.equal(fs.existsSync(file+'.lock'),false);
});
test('crash leftovers from the log manager are removed without deleting other temporary files',t=>{
 const file=temp(t),uuid='12345678-1234-1234-1234-123456789abc';
 fs.writeFileSync(file+'.tmp-'+uuid,'leftover');fs.writeFileSync(file+'.1.tmp-'+uuid,'leftover');
 fs.writeFileSync(file+'.tmp-not-owned','keep');
 createRotatingLog(file,{maxBytes:100,backups:2}).append('new\n');
 assert.equal(fs.existsSync(file+'.tmp-'+uuid),false);assert.equal(fs.existsSync(file+'.1.tmp-'+uuid),false);
 assert.equal(fs.readFileSync(file+'.tmp-not-owned','utf8'),'keep');
});
