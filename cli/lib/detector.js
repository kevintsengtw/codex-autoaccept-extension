const {evaluate} = require('./policy');
// Match only the executable, never "echo codex" or text in arguments.
function isCodexCommand(line) {
  const words = line.match(/"[^"]*"|'[^']*'|[^\s]+/g) ?? [];
  while (words.length && /^[A-Za-z_][\w]*=/.test(words[0])) words.shift();
  const first = (words[0] ?? '').replace(/^["']|["']$/g, '').split(/[\\/]/).pop();
  return /^codex(?:\.exe)?$/.test(first ?? '');
}
function detect(screen, settings) {
  const lines = screen.split('\n').map(l => l.trimEnd());
  const q = lines.findLastIndex(l => /^\s*(Would you like to (run the following command|make the following edits|apply these changes)\?|Allow command\?)\s*$/.test(l));
  if (q < 0) return null;
  const block = lines.slice(q).join('\n');
  // Require the full completed menu and footer, so quoted prose/partial redraws don't qualify.
  if (!/^\s*[›❯>]?\s*1\. Yes, proceed \(y\)\s*$/m.test(block) ||
      !/^\s*\d+\. No, and tell Codex what to do differently \(esc\)\s*$/m.test(block) ||
      !/Press enter to confirm or esc to cancel\s*$/.test(block)) return null;
  const file = /make the following edits|apply these changes/.test(lines[q]);
  const identity = block.replace(/\s+/g, ' ').trim();
  if (file && !settings.approveFileChanges) return {allow: false, reason: 'file approvals disabled', identity};
  const menu = lines.findIndex((line, i) => i > q && /^\s*[›❯>]?\s*1\. Yes, proceed \(y\)\s*$/.test(line));
  let content;
  if (file) content = lines.slice(q + 1, menu).join('\n');
  else {
    const command = lines.findIndex((line, i) => i > q && i < menu && /^\s*\$\s+\S/.test(line));
    if (command < 0) return null;
    // Reason can repeat/summarize the pwsh script using ellipses. Only the
    // displayed execution block is relevant to clipping and command policy.
    content = lines.slice(command, menu).join('\n').replace(/^\s*\$\s+/, '').trim();
  }
  // Truncated execution content still remains manual; do not approve unseen commands.
  if (/[…]|\.{3}|\bctrl\+o\b|command.*clipped/i.test(content)) return {allow: false, reason: 'clipped content', identity};
  return {...evaluate(content, settings), identity, key: 'y'};
}
module.exports = {isCodexCommand, detect};
