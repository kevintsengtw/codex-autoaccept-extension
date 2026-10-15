// Regex rules are heuristics, not a shell parser or a security boundary.
const GIT_ARG = `(?:"[^"\\r\\n]*"|'[^'\\r\\n]*'|[^\\s;|&"'(){}]+)`;
const GIT_GLOBAL = `(?:(?:-[Cc]|--(?:git-dir|work-tree|namespace|config-env))\\s+${GIT_ARG}|-[Cc][^\\s;|&"']+|--(?:git-dir|work-tree|namespace|config-env)=[^\\s;|&"']+|--(?:no-pager|paginate|bare|no-replace-objects|literal-pathspecs|glob-pathspecs|noglob-pathspecs|icase-pathspecs))\\s+`;
const GIT_END = `(?=$|[\\s;|&"'(){}])`;
const GIT_FLAGS = `(?:reset\\s+(?:${GIT_ARG}\\s+)*?["']?--hard["']?${GIT_END}|clean\\s+(?:${GIT_ARG}\\s+)*?["']?(?:-[dfinqxe]*f[dfinqxe]*|--force)["']?${GIT_END}|push\\s+(?:${GIT_ARG}\\s+)*?["']?(?:-[vfnuq]*f[vfnuq]*|--force(?:-with-lease|-if-includes)?(?:=[^\\s;|&"']+)?|\\+[^\\s;|&"']+)["']?${GIT_END})`;
const BUILTIN_DENY = [
  '\\b(Remove-Item|ri|del|erase|rmdir|rd)\\b',
  '\\b(Format-Volume|Clear-Disk|diskpart|format\\s+[a-z]:)\\b',
  '\\b(Invoke-Expression|iex)\\b',
  // Require an invocation-shaped rm token and complete known short options.
  // A quoted tool name in a list and PowerShell -ErrorAction are not rm -r.
  "\\brm(?:\\.exe)?[\"']?\\s+(?:[^\\n;|]*?\\s)?(?:-[dfirv]*r[dfirv]*|--recursive|--no-preserve-root)(?=$|[\\s;\"'])",
  // Require git's subcommand after its global options and a complete flag
  // token in that argument sequence, not arbitrary words later in a report.
  `\\bgit(?:\\.exe)?["']?\\s+(?:${GIT_GLOBAL})*${GIT_FLAGS}`,
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
const WINDOWS_DELETE_DETAILS = ['Remove-Item','ri','del','erase','rmdir','rd'].map((name,index)=>({
  id:`B001.${String(index+1).padStart(2,'0')}`, re:new RegExp(`\\b${name}\\b`,'i')
}));
function withoutPowerShellVariables(text) {
  // B001 lists command aliases (rd, ri, del...), not variable names. Preserve
  // the original text for all other rules and user-defined patterns.
  return text.replace(/\$(?:\{(?:[A-Za-z_]\w*:)?[A-Za-z_][\w-]*\}|(?:[A-Za-z_]\w*:)?[A-Za-z_][\w-]*)/g, match => ' '.repeat(match.length));
}
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
    const commandCandidates = candidates.map(withoutPowerShellVariables);
    // Only static IDs/categories cross this boundary. Never return patterns,
    // matching text, paths, input snippets or RegExp match objects.
    const rules = denied.filter(rule => (rule.id === 'B001' ? commandCandidates : candidates).some(s => rule.re.test(s)))
      .map(({id, category}) => ({id, category}));
    if (rules.length) {
      const details = rules.some(rule=>rule.id==='B001') ? WINDOWS_DELETE_DETAILS
        .filter(detail=>commandCandidates.some(candidate=>detail.re.test(candidate))).map(detail=>detail.id) : [];
      return {allow:false,reason:'deny rule',rules,...(details.length ? {details} : {})};
    }
    if (allowed.length && !allowed.some(re => re.test(text))) return {allow: false, reason: 'not allowlisted'};
    return {allow: true, reason: 'recognized approval'};
  } catch { return {allow: false, reason: 'invalid regular expression'}; }
}
function describe(outcome) {
  if (!outcome.rules?.length) return outcome.reason;
  return `${outcome.reason}; rules=${outcome.rules.map(rule => `${rule.id}:${rule.category}`).join(',')}${outcome.details?.length ? `; details=${outcome.details.join(',')}` : ''}`;
}
module.exports = {evaluate, describe, BUILTIN_DENY, RULE_CATEGORIES};
