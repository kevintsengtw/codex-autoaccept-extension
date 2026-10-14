#!/usr/bin/env node
const path = require('node:path');
const os = require('node:os');
const {parse, settings: loadSettings} = require('../lib/options');
const {resolveCodex} = require('../lib/resolve');
const {createSession} = require('../lib/session');
const {createRotatingLog} = require('../lib/rotating-log');
const HELP = `Codex Auto-Accept CLI 0.1.13
Usage: codex-auto [wrapper options] -- [Codex arguments]
  --auto          Start auto-accept ON (default OFF)
  --off           Start OFF, overriding config
  --dry-run       Detect and log; never send automatic approval keys
  --config PATH   Load explicit JSON configuration
  --codex PATH    Codex executable or official bin/codex.js
  --log PATH      Metadata-only log file
  --self-test-expanded  Exercise expansion, paging and return with a local fixture
  --self-test     Exercise PTY approval with a harmless local fixture
  --help          Show this help
During a session: Ctrl+] toggles ON/OFF. Ctrl+C is passed to Codex.
Examples:
  codex-auto --auto
  codex-auto --auto -- resume
  codex-auto --auto --self-test
`;
function main() {
  const options = parse(process.argv.slice(2));
  if (options.help) {process.stdout.write(HELP);return;}
  if (!process.stdin.isTTY || !process.stdout.isTTY || !process.stdin.setRawMode) throw Error('Use an interactive terminal (Windows Terminal); redirected stdin/stdout is not supported.');
  const settings = loadSettings(options);
  const target = options.selfTest ? {file: process.execPath, args: [path.join(__dirname, 'self-test-child.js'), ...(options.expandedSelfTest ? ['--expanded'] : [])]} : resolveCodex(options.codex);
  const logPath = options.log ? path.resolve(options.log) : path.join(process.env.LOCALAPPDATA ?? path.join(os.homedir(), '.local', 'state'), 'CodexAutoAccept', 'cli.log');
  const logger = createRotatingLog(logPath,{maxBytes:settings.logMaxBytes,backups:settings.logBackupCount});
  // A log failure stops approvals rather than silently losing the decision record.
  logger.append(`${new Date().toISOString()} START CLI=0.1.13 ${settings.enabled ? 'ON' : 'OFF'}${settings.dryRun ? ' DRY_RUN' : ''}\n`);
  process.stderr.write(`[codex-auto] ${settings.enabled ? 'ON' : 'OFF'}${settings.dryRun ? ' (dry run)' : ''} | Ctrl+] toggles | log: ${logPath}\r\n`);
  let pty;
  try {pty = require('node-pty');} catch (err) {throw Error(`Cannot load node-pty. Run npm ci in this package on the target Windows computer using native Windows Node.js. ${err.message}`);}
  const size = () => ({cols: Math.max(20, process.stdout.columns || 120), rows: Math.max(10, process.stdout.rows || 40)});
  const dimensions = size();
  const child = pty.spawn(target.file, [...target.args, ...options.codexArgs], {
    name: 'xterm-256color', ...dimensions, cwd: process.cwd(), env: {...process.env, TERM:'xterm-256color'}, useConpty: true
  });
  let finished = false, session;
  const wasRaw = process.stdin.isRaw ?? false;
  const onInput = data => {try {session.input(data);} catch (err) {fail(err);}};
  const onResize = () => {try {const {cols, rows} = size();session.resize(cols, rows);} catch (err) {fail(err);}};
  function cleanup() {
    if (finished) return;
    finished = true; session?.stop();
    process.stdin.off('data', onInput);process.stdout.off('resize', onResize);
    process.off('SIGTERM', onTerminate);process.off('SIGHUP', onTerminate);process.off('SIGINT', onTerminate);
    process.stdin.setRawMode(wasRaw);process.stdin.pause();
  }
  function fail(err) {cleanup();try {child.kill();} catch {} process.stderr.write(`\r\n[codex-auto] ${err.message}\r\n`);process.exitCode=1;}
  function onTerminate() {cleanup();try {child.kill();} catch {} process.exitCode=130;}
  session = createSession({child, settings, ...dimensions, onError: fail, log: (kind, reason) => {
    logger.append(`${new Date().toISOString()} ${kind} ${reason}\n`);
    if (kind === 'MODE') process.stderr.write(`\r\n[codex-auto] ${reason}${settings.dryRun ? ' (dry run)' : ''}\r\n`);
  }});
  child.onData(data => {
    if (finished) return;
    process.stdout.write(data);
    try {session.feed(data);} catch (err) {fail(err);}
  });
  child.onExit(({exitCode}) => {if (!finished) {cleanup();process.exitCode=exitCode;}});
  process.stdin.setEncoding('utf8');process.stdin.setRawMode(true);process.stdin.resume();
  process.stdin.on('data', onInput);process.stdout.on('resize', onResize);
  process.on('SIGTERM', onTerminate);process.on('SIGHUP', onTerminate);process.on('SIGINT', onTerminate);
}
if (require.main === module) {try {main();} catch (err) {process.stderr.write(`[codex-auto] ${err.message}\n`);process.exitCode=1;}}
module.exports = {main};
