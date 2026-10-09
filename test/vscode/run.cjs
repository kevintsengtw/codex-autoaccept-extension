// Real Extension Host test in an isolated profile; never changes the user's normal settings.
const vscode = require('vscode');
const fs = require('node:fs');
const assert = require('node:assert/strict');
async function run() {
  await vscode.extensions.getExtension('local-tools.codex-autoaccept').activate();
  const root = () => vscode.workspace.getConfiguration();
  const value = key => vscode.workspace.getConfiguration('codexAutoAccept').inspect(key)?.globalValue;
  const snapshots=[];
  try {
    await root().update('codexAutoAccept.enabled', false, vscode.ConfigurationTarget.Global);
    for (const next of [true,false]) {
      assert.equal(await vscode.commands.executeCommand('codexAutoAccept.toggle'), next);
      assert.equal(value('enabled'), next);
      snapshots.push({phase:'normal toggle',enabled:value('enabled')});
    }
    await root().update('codexAutoAccept.terminalRows', 0, vscode.ConfigurationTarget.Global);
    for (const next of [true,false]) {
      assert.equal(await vscode.commands.executeCommand('codexAutoAccept.toggle'), next);
      assert.equal(value('enabled'), next);
      snapshots.push({phase:'invalid dimension toggle',enabled:value('enabled')});
    }
    await Promise.all([vscode.commands.executeCommand('codexAutoAccept.toggle'),vscode.commands.executeCommand('codexAutoAccept.toggle')]);
    assert.equal(value('enabled'),false);
    snapshots.push({phase:'rapid double toggle',enabled:value('enabled')});
  } finally {
    await root().update('codexAutoAccept.enabled', false, vscode.ConfigurationTarget.Global);
    await root().update('codexAutoAccept.terminalRows', undefined, vscode.ConfigurationTarget.Global);
    fs.writeFileSync(process.env.CAA_PROBE_OUTPUT,JSON.stringify(snapshots,null,2));
  }
}
module.exports={run};
