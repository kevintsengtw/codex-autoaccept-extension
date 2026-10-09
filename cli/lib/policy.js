// Regex rules are heuristics, not a shell parser or a security boundary.
const BUILTIN_DENY = [
  '\\b(Remove-Item|ri|del|erase|rmdir|rd)\\b',
  '\\b(Format-Volume|Clear-Disk|diskpart|format\\s+[a-z]:)\\b',
  '\\b(Invoke-Expression|iex)\\b',
  '\\brm\\b[^\\n]*\\s(-[a-z]*r[a-z]*|--recursive|--no-preserve-root)\\b',
  '\\bgit\\b.*\\b(reset\\b.*--hard|clean\\b.*-[a-z]*f|push\\b.*(--force|-[a-z]*f\\b|\\+\\S))',
  '\\b(drop\\s+(database|table|schema)|truncate\\s+table|delete\\s+from)\\b',
  '\\b(mkfs|shutdown|reboot)\\b', '\\bdd\\b.*\\bof=', '\\bchmod\\b.*\\b777\\b',
  '\\b(curl|wget)\\b[^|]*\\|\\s*(sudo\\s+)?(sh|bash|zsh|python\\d*|node|perl|ruby)\\b',
  '\\b(terraform\\s+destroy|kubectl\\s+delete|docker\\s+(system|volume)\\s+prune)\\b'
];
function evaluate(text, settings) {
  try {
    const denied = [...BUILTIN_DENY, ...settings.denyPatterns].map(s => new RegExp(s, 'i'));
    const allowed = settings.allowPatterns.map(s => new RegExp(s, 'i'));
    const lines = text.split('\n').map(s => s.trim());
    const candidates = [text, lines.join(' '), lines.join('')];
    if (denied.some(re => candidates.some(s => re.test(s)))) return {allow: false, reason: 'deny rule'};
    if (allowed.length && !allowed.some(re => re.test(text))) return {allow: false, reason: 'not allowlisted'};
    return {allow: true, reason: 'recognized approval'};
  } catch { return {allow: false, reason: 'invalid regular expression'}; }
}
module.exports = {evaluate, BUILTIN_DENY};
