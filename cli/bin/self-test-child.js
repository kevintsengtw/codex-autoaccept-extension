// This fixture never executes the command displayed in its approval menu.
process.stdin.setRawMode(true);
process.stdin.setEncoding('utf8');
process.stdin.resume();
setTimeout(() => {
  process.stdout.write('\x1b[2J\x1b[HWould you like to run the following command?\r\n\r\n  $ echo codex-auto-self-test\r\n\r\n› 1. Yes, proceed (y)\r\n  3. No, and tell Codex what to do differently (esc)\r\n\r\n  Press enter to confirm or esc to cancel');
}, 300);
const timer = setTimeout(() => {process.stdout.write('\r\nSELF-TEST TIMED OUT (no y received)\r\n');process.exit(2);}, 20000);
process.stdin.on('data', data => {
  if (data.includes('y')) {clearTimeout(timer); process.stdout.write('\r\nSELF-TEST PASS: received y through PTY\r\n');process.exit(0);}
  if (data.includes('\x03') || data.includes('\x1b')) process.exit(130);
});
