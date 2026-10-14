// Regex rules are heuristics, not a shell parser or a security boundary.
const BUILTIN_DENY = [
  '\\b(Remove-Item|ri|del|erase|rmdir|rd)\\b',
  '\\b(Format-Volume|Clear-Disk|diskpart|format\\s+[a-z]:)\\b',
  '\\b(Invoke-Expression|iex)\\b',
  // Require an invocation-shaped rm token and complete known short options.
  // A quoted tool name in a list and PowerShell -ErrorAction are not rm -r.
  "\\brm(?:\\.exe)?[\"']?\\s+(?:[^\\n;|]*?\\s)?(?:-[dfirv]*r[dfirv]*|--recursive|--no-preserve-root)(?=$|[\\s;\"'])",
  '\\bgit\\b.*\\b(reset\\b.*--hard|clean\\b.*-[a-z]*f|push\\b.*(--force|-[a-z]*f\\b|\\+\\S))',
  '\\b(drop\\s+(database|table|schema)|truncate\\s+table|delete\\s+from)\\b',
  '\\b(mkfs|shutdown|reboot)\\b', '\\bdd\\b.*\\bof=', '\\bchmod\\b.*\\b777\\b',
  '\\b(curl|wget)\\b[^|]*\\|\\s*(sudo\\s+)?(sh|bash|zsh|python\\d*|node|perl|ruby)\\b',
  '\\b(terraform\\s+destroy|kubectl\\s+delete|docker\\s+(system|volume)\\s+prune)\\b'
];
const RULE_CATEGORIES = [
  'windows-file-delete', 'disk-format', 'dynamic-execution', 'recursive-delete',
  'git-destructive', 'destructive-sql', 'system-operation', 'raw-disk-write',
  'world-writable-permissions', 'remote-script-execution', 'infrastructure-delete'
];
function evaluate(text, settings) {
  try {
    const denied = [...BUILTIN_DENY, ...settings.denyPatterns].map((pattern, index) => ({
      re: new RegExp(pattern, 'i'),
      id: index < BUILTIN_DENY.length ? `B${String(index + 1).padStart(3, '0')}` : `D${String(index - BUILTIN_DENY.length + 1).padStart(3, '0')}`,
      category: index < BUILTIN_DENY.length ? RULE_CATEGORIES[index] : 'user-defined'
    }));
    const allowed = settings.allowPatterns.map(s => new RegExp(s, 'i'));
    const lines = text.split('\n').map(s => s.trim());
    const candidates = [text, lines.join(' '), lines.join('')];
    // Only static IDs/categories cross this boundary. Never return patterns,
    // matching text, paths, input snippets or RegExp match objects.
    const rules = denied.filter(rule => candidates.some(s => rule.re.test(s)))
      .map(({id, category}) => ({id, category}));
    if (rules.length) return {allow: false, reason: 'deny rule', rules};
    if (allowed.length && !allowed.some(re => re.test(text))) return {allow: false, reason: 'not allowlisted'};
    return {allow: true, reason: 'recognized approval'};
  } catch { return {allow: false, reason: 'invalid regular expression'}; }
}
function describe(outcome) {
  if (!outcome.rules?.length) return outcome.reason;
  return `${outcome.reason}; rules=${outcome.rules.map(rule => `${rule.id}:${rule.category}`).join(',')}`;
}
module.exports = {evaluate, describe, BUILTIN_DENY, RULE_CATEGORIES};
