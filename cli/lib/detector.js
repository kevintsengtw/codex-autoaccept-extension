const {evaluate} = require('./policy');
// Match only the executable, never "echo codex" or text in arguments.
function isCodexCommand(line) {
  const words = line.match(/"[^"]*"|'[^']*'|[^\s]+/g) ?? [];
  while (words.length && /^[A-Za-z_][\w]*=/.test(words[0])) words.shift();
  const first = (words[0] ?? '').replace(/^["']|["']$/g, '').split(/[\\/]/).pop();
  return /^codex(?:\.exe)?$/.test(first ?? '');
}
function inspectMenu(screen) {
  const lines = screen.split('\n').map(l => l.trimEnd());
  const q = lines.findLastIndex(l => /^\s*(Would you like to (run the following command|make the following edits|apply these changes)\?|Allow command\?)\s*$/.test(l));
  if (q < 0) return {reason: 'missing complete approval heading'};
  const block = lines.slice(q).join('\n');
  const menu = lines.findIndex((line, i) => i > q && /^\s*[›❯>]?\s*1\. Yes, proceed \(y\)\s*$/.test(line));
  if (menu < 0) return {reason: 'missing complete Yes option'};
  if (!/^\s*\d+\. No, and tell Codex what to do differently \(esc\)\s*$/m.test(block)) return {reason: 'missing complete No option'};
  if (!/Press enter to confirm or esc to cancel\s*$/.test(block)) return {reason: 'missing confirmation footer at end of screen'};
  const file = /make the following edits|apply these changes/.test(lines[q]);
  // A prompt marker can occupy its own physical row after wrapping/redraw.
  // Keep the explicit '$' requirement; never infer execution from Reason text.
  const command = file ? -1 : lines.findIndex((line, i) => i > q && i < menu &&
    (/^\s*\$\s+\S/.test(line) || (/^\s*\$\s*$/.test(line) &&
      i + 1 < menu && lines[i + 1].trim() && !/^\s*(Reason:|Environment:|Permission rule:)/.test(lines[i + 1]))));
  if (!file && command < 0) return {reason: 'missing command start ($ prefix); command may be outside visible screen'};
  return {lines, q, block, menu, file, command};
}
function diagnose(screen) { return inspectMenu(screen).reason ?? ''; }
function detect(screen, settings) {
  const parsed = inspectMenu(screen);
  if (parsed.reason) return null;
  const {lines, q, block, menu, file, command} = parsed;
  const identity = block.replace(/\s+/g, ' ').trim();
  if (file && !settings.approveFileChanges) return {allow: false, reason: 'file approvals disabled', identity};
  let content;
  if (file) content = lines.slice(q + 1, menu).join('\n');
  else {
    // Reason can repeat/summarize the pwsh script using ellipses. Only the
    // displayed execution block is relevant to clipping and command policy.
    content = lines.slice(command, menu).join('\n').replace(/^\s*\$\s+/, '').trim();
  }
  // Truncated execution content still remains manual; do not approve unseen commands.
  if (/[…]|\.{3}|\bctrl\+o\b|command.*clipped|\+\s*\d+\s+lines?\b[^\n]*ctrl\+a|ctrl\+a[^\n]*view all/i.test(content)) return {allow: false, reason: 'clipped content', identity};
  return {...evaluate(content, settings), identity, key: 'y'};
}
module.exports = {isCodexCommand, detect, diagnose};
