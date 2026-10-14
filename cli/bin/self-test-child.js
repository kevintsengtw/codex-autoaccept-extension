// These fixtures never execute the command displayed in the approval menu.
process.stdin.setRawMode(true);
process.stdin.setEncoding('utf8');
process.stdin.resume();
const expanded = process.argv.includes('--expanded');
const ordinary = 'Would you like to run the following command?\r\n\r\n  $ echo codex-auto-self-test\r\n\r\n› 1. Yes, proceed (y)\r\n  3. No, and tell Codex what to do differently (esc)\r\n\r\n  Press enter to confirm or esc to cancel';
const modal = ordinary.replace('  $ echo codex-auto-self-test', '  $ echo shown\r\n  +70 lines ctrl+a to view all');
const height = Math.max(4, (process.stdout.rows || 40) - 5);
const command = Array.from({length: height + Math.ceil(height / 2)}, (_, i) => `echo harmless-fixture-line-${i}`);
let phase = 'modal';
function draw(text) {process.stdout.write('\x1b[2J\x1b[H'+text);}
function pager(offset, percent) {
  draw(`/ E X E C / / /\r\n${command.slice(offset, offset + height).join('\r\n')}\r\n──────── ${percent}% ─\r\n ↑/↓ to scroll · pgup/pgdn to page · home/end to jump\r\n q close`);
}
setTimeout(() => draw(expanded ? modal : ordinary), 300);
const timer = setTimeout(() => {process.stdout.write('\r\nSELF-TEST TIMED OUT\r\n');process.exit(2);}, 20000);
process.stdin.on('data', data => {
  if (data.includes('\x03')) process.exit(130);
  if (expanded) {
    if (phase === 'modal' && data === '\x01') {phase = 'page1';process.stdout.write('\x1b[?1049h');pager(0,0);return;}
    if (phase === 'page1' && data === '\x1b[6~') {phase = 'page2';pager(command.length-height,100);return;}
    if (phase === 'page2' && data === 'q') {phase = 'returned';process.stdout.write('\x1b[?1049l');draw(modal);return;}
    if (phase !== 'returned' || data !== 'y') {clearTimeout(timer);process.stderr.write('\r\nSELF-TEST FAIL: unexpected input or premature approval\r\n');process.exit(3);}
  }
  if (data.includes('y')) {clearTimeout(timer);process.stdout.write(`\r\nSELF-TEST PASS: ${expanded ? 'expanded full command, returned and received y' : 'received y through PTY'}\r\n`);process.exit(0);}
  if (data.includes('\x1b')) process.exit(130);
});
