const test=require('node:test');
const assert=require('node:assert/strict');
const {evaluate,describe}=require('../lib/policy');
const config={allowPatterns:[],denyPatterns:[]};
test('tool availability query does not mistake rm.exe and ErrorAction for recursive deletion',()=>{
  const query=`& { @('openfiles.exe', 'handle.exe', 'handle64.exe', 'rm.exe') |
    ForEach-Object { $cmd = Get-Command $_ -ErrorAction SilentlyContinue;
      [pscustomobject]@{ Name = $_; Available = ($null -ne $cmd) } }
    | ConvertTo-Json -Depth 5 }`;
  assert.equal(evaluate(query,config).allow,true);
  assert.equal(evaluate(`pwsh.exe -NoProfile -Command "${query}"`,config).allow,true);
  for(const cmd of ['rm -ErrorAction Stop','rm -f example; Write-Output -ErrorAction Stop',
    "Write-Output 'rm.exe' -ErrorAction Stop"]){
    assert.equal(evaluate(cmd,config).allow,true,cmd);
  }
  assert.deepEqual(evaluate(query,{...config,denyPatterns:['Get-Command']}).rules,
    [{id:'D001',category:'user-defined'}]);
});
test('recursive rm invocations retain B004 including wrappers and executable paths',()=>{
  for(const cmd of ['rm -r example','rm -rf example','rm -fr example','rm -R example',
    'rm -ivrf example','rm --recursive example','rm --no-preserve-root example',
    'rm -f example --recursive','sudo /bin/rm -rf example','rm.exe -rf example',
    '"C:\\tools\\rm.exe" -rf example',"'/bin/rm' -rf example",
    'pwsh -Command "rm -rf example"','sh -c \'rm -rf example\'', 'rm\n-rf example']){
    const outcome=evaluate(cmd,config);
    assert.equal(outcome.allow,false,cmd);
    assert.ok(outcome.rules.some(rule=>rule.id==='B004'),cmd);
  }
});
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
