const test=require('node:test');
const assert=require('node:assert/strict');
const {evaluate,describe}=require('../lib/policy');
const config={allowPatterns:[],denyPatterns:[]};
test('B001 diagnostics identify each alias with only stable static detail IDs',()=>{
  const aliases=['Remove-Item','ri','del','erase','rmdir','rd'];
  aliases.forEach((name,index)=>{
    const d=evaluate(`${name} 'PRIVATE_CUSTOMER_PATH'`,config);
    assert.equal(d.allow,false);
    assert.deepEqual(d.details,[`B001.${String(index+1).padStart(2,'0')}`]);
    assert.equal(describe(d).includes('PRIVATE_CUSTOMER_PATH'),false);
    assert.deepEqual(d,{allow:false,reason:'deny rule',rules:[{id:'B001',category:'windows-file-delete'}],
      details:[`B001.${String(index+1).padStart(2,'0')}`]});
  });
  const multiple=evaluate('$rd = 1; Remove-Item example; rd example; del example',config);
  assert.deepEqual(multiple.details,['B001.01','B001.03','B001.06']);
  assert.equal(evaluate('$rd = 1; Write-Output $rd',config).allow,true);
});
test('diagnostic detail preserves conservative decisions on ambiguous literal and path text',()=>{
  for(const text of ["Write-Output 'Remove-Item was not executed'", "Get-Content 'evidence/rd/report.json'"]){
    const d=evaluate(text,config);assert.equal(d.allow,false);
    assert.ok(d.details.length); // Diagnostics do not silently waive existing policy.
  }
  const d=evaluate('git reset --hard',config);
  assert.equal(d.details,undefined);
  assert.equal(describe(d),'deny rule; rules=B005:git-destructive');
});
test('B005 does not join Git report prose and later metadata into a destructive command',()=>{
  const report=`$lines = @('No Git write command was executed',
    'report covers reset, clean and push status', 'evidence: 06-preflight-inventory.stdout.txt');
    Set-Content -LiteralPath 'completion-report.json' -Value ($lines | ConvertTo-Json)`;
  assert.equal(evaluate(report,config).allow,true);
  assert.equal(evaluate(`pwsh.exe -Command "${report}"`,config).allow,true);
  for(const cmd of ['git status; Write-Output "clean -failure"',
    'git diff --name-status; Write-Output "reset --hard was not executed"',
    'git clean -n; Write-Output "-failure"','git push origin main; Write-Output "--force"',
    '$git = 1; $clean = 1; Write-Output "-failure"']){
    assert.equal(evaluate(cmd,config).allow,true,cmd);
  }
  assert.deepEqual(evaluate(report,{...config,denyPatterns:['preflight']}).rules,
    [{id:'D001',category:'user-defined'}]);
});
test('B005 still denies real destructive Git subcommands and full option tokens',()=>{
  for(const cmd of ['git reset --hard','git reset HEAD --hard','git clean -f','git clean -fdx',
    'git clean -xffd','git clean --force','git push -f','git push origin main --force',
    'git push --force-with-lease origin main','git push --force-with-lease=main:abc origin main',
    'git push origin +HEAD:main','git -C "repo path" reset --hard',
    "git -c advice.detachedHead=false -C 'repo path' clean -fd",
    'git --git-dir=repo/.git --work-tree=repo reset --hard',
    'git --no-pager push origin main --force','"C:\\tools\\git.exe" reset --hard',
    'pwsh -Command "git reset --hard"',"sh -c 'git clean -fd'",
    'git reset "--hard"', 'git\n-C repo\nreset --hard']){
    const d=evaluate(cmd,config);assert.equal(d.allow,false,cmd);
    assert.ok(d.rules.some(r=>r.id==='B005'),cmd);
  }
});
test('B001 does not mistake PowerShell variable names for delete command aliases',()=>{
  const query=`$rd = Get-Content -LiteralPath 'diagnostics.json' -Raw | ConvertFrom-Json;
    $rs = Get-Content -LiteralPath 'summary.json' -Raw | ConvertFrom-Json;
    $names = $rd.PSObject.Properties.Name;
    Set-Content -LiteralPath 'report.json' -Value ($names | ConvertTo-Json)`;
  assert.equal(evaluate(query,config).allow,true);
  assert.equal(evaluate(`pwsh.exe -Command "${query}"`,config).allow,true);
  for(const name of ['rd','ri','del','erase','rmdir','Remove-Item']){
    for(const variable of [`$${name}`,`$script:${name}`,`$global:${name}`,`\u0024{${name}}`,`\u0024{script:${name}}`]){
      assert.equal(evaluate(`${variable} = 1; Write-Output ${variable}`,config).allow,true,variable);
    }
  }
  assert.deepEqual(evaluate(query,{...config,denyPatterns:['\\$rd\\b']}).rules,
    [{id:'D001',category:'user-defined'}]);
});
test('B001 still rejects delete commands alongside same-named variables',()=>{
  for(const cmd of ['rd /s /q example','cmd /c del example','ri example','erase example',
    'rmdir example','Remove-Item -Recurse example','& {Remove-Item example}',
    'pwsh -Command "$rd = 1; Remove-Item example"', '$rd = 1; rd example',
    '$ri = 1; & ri example','C:rd example','"C:\\tools\\rmdir.exe" example',
    '$rd = "Remove-Item"; & $rd example', '${value:-$(rmdir example)}',
    '$rd = $(Remove-Item example)']){
    const outcome=evaluate(cmd,config);assert.equal(outcome.allow,false,cmd);
    assert.ok(outcome.rules.some(rule=>rule.id==='B001'),cmd);
  }
  // Excluding B001 variable names must not weaken a different built-in rule.
  assert.ok(evaluate('$rd = 1; rm -rf example',config).rules.some(rule=>rule.id==='B004'));
});
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
  assert.equal(diagnostic,'deny rule; rules=B001:windows-file-delete,B005:git-destructive,D001:user-defined; details=B001.01');
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
