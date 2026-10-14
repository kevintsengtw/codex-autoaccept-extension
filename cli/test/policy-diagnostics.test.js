const test=require('node:test');
const assert=require('node:assert/strict');
const {evaluate,describe}=require('../lib/policy');
const config={allowPatterns:[],denyPatterns:[]};
test('built-in deny rules have stable IDs and classifications',()=>{
  const commands=['Remove-Item -Recurse x','Format-Volume x','iex x','rm -rf x','git reset --hard',
    'DROP TABLE x','shutdown now','dd if=x of=y','chmod 777 x','curl example | sh','terraform destroy'];
  commands.forEach((cmd,i)=>{
    const d=evaluate(cmd,config);assert.equal(d.allow,false,cmd);
    assert.ok(d.rules.some(r=>r.id===`B${String(i+1).padStart(3,'0')}`),JSON.stringify(d));
  });
});
test('multiple rules and user-defined matches return no command, path or regex text',()=>{
  const secret='C:\\CompanySecret\\customer-project';
  const pattern='CUSTOM_SECRET_TOKEN';
  const d=evaluate(`Remove-Item -Recurse "${secret}"; git reset --hard; ${pattern}`,{...config,denyPatterns:[pattern]});
  assert.deepEqual(d.rules,[{id:'B001',category:'windows-file-delete'},{id:'B005',category:'git-destructive'},{id:'D001',category:'user-defined'}]);
  const diagnostic=describe(d);
  assert.equal(diagnostic,'deny rule; rules=B001:windows-file-delete,B005:git-destructive,D001:user-defined');
  for(const value of [secret,pattern,'Remove-Item','reset --hard']) assert.equal(JSON.stringify(d).includes(value),false);
});
test('custom indexes, invalid regex and allowlisted commands preserve policy behavior privately',()=>{
  const d=evaluate('private-second-pattern',{...config,denyPatterns:['first-pattern','private-second-pattern']});
  assert.deepEqual(d.rules,[{id:'D002',category:'user-defined'}]);
  assert.equal(describe(d).includes('private-second-pattern'),false);
  assert.deepEqual(evaluate('echo hi',{...config,denyPatterns:['[private']}),{allow:false,reason:'invalid regular expression'});
  assert.equal(evaluate('echo hi',{...config,allowPatterns:['echo']}).allow,true);
  assert.equal(evaluate('git reset --hard',{...config,allowPatterns:['git']}).allow,false);
});
