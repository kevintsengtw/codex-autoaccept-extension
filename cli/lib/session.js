const {Terminal} = require('@xterm/headless');
const {detect, diagnose} = require('./detector');
function createSession({child, settings, cols, rows, log, onError = err => {throw err;}, setTimer = setTimeout, clearTimer = clearTimeout}) {
  const screen = new Terminal({cols, rows, scrollback: 0, allowProposedApi: true});
  let ended = false, timer, last = '', diagnostic = '', writing = 0;
  function snapshot() {
    const buffer = screen.buffer.active, lines = [];
    for (let i = 0; i < screen.rows; i++) {
      const line = buffer.getLine(buffer.viewportY + i);
      if (!line) continue;
      // Join only terminal-generated soft wraps; preserve explicit command newlines.
      const text = line.translateToString(!buffer.getLine(buffer.viewportY + i + 1)?.isWrapped);
      // Cursor-addressed redraws can retain an old isWrapped flag. Approval
      // UI boundaries must stay on their own line even on such reused rows.
      const boundary = /^\s*(?:Would you like to |Allow command\?|Environment:|Reason:|Permission rule:|\$\s|[›❯>]?\s*\d+\. (?:Yes,|No,)|Press enter to confirm)/.test(text);
      if (line.isWrapped && lines.length && !boundary) lines[lines.length - 1] += text;
      else lines.push(text);
    }
    return lines.join('\n').trimEnd();
  }
  function inspect() {
    if (ended || writing || !settings.enabled) return;
    const text = snapshot();
    const result = detect(text, settings);
    if (!result) {
      if (text.trim() && !/Would you like to|Allow command\?|Yes, proceed|No, and tell Codex/.test(text)) last = '';
      const reason = /Would you like to|Allow command\?/.test(text) ? `${diagnose(text)}; terminal=${screen.cols}x${screen.rows}` : '';
      if (reason && reason !== diagnostic) log('UNRECOGNIZED', reason);
      diagnostic = reason;
      return;
    }
    diagnostic = '';
    if (last === result.identity) return;
    last = result.identity;
    if (!result.allow || !('key' in result)) { log('MANUAL', result.reason); return; }
    if (settings.dryRun) { log('DRY_RUN', 'would approve once'); return; }
    log('ACCEPT', 'once');
    child.write(result.key);
  }
  function schedule() {
    if (ended || timer !== undefined) return;
    // Fixed deadline, not an idle debounce: redraws must not postpone inspection forever.
    timer = setTimer(() => {
      timer = undefined;
      if (ended) return;
      if (writing) {schedule(); return;}
      try {inspect();} catch (err) {onError(err);}
    }, 250);
  }
  return {
    feed(chunk) {
      if (ended) return;
      writing++;
      screen.write(chunk, () => {
        writing--;
        if (!ended) {
          // Observe prompt disappearance even when the next identical prompt arrives
          // before the deadline. Only an intact menu can be approved by inspect().
          const text = snapshot();
          if (text.trim() && !/Would you like to|Allow command\?|Yes, proceed|No, and tell Codex/.test(text)) last = '';
          schedule();
        }
      });
    },
    input(data) {
      if (ended) return;
      // Ctrl+] is reserved by the wrapper. Everything else, including Ctrl+C, is forwarded.
      for (const part of data.split(/(\x1d)/)) {
        if (part === '\x1d') {
          settings.enabled = !settings.enabled; last = ''; clearTimer(timer); timer = undefined;
          log('MODE', settings.enabled ? 'ON' : 'OFF');
          if (settings.enabled) schedule();
        } else if (part) child.write(part);
      }
    },
    resize(cols, rows) {
      if (ended) return;
      clearTimer(timer); timer = undefined;
      child.resize(cols, rows); screen.resize(cols, rows); schedule();
    },
    stop() { if (ended) return; ended = true; clearTimer(timer); screen.dispose(); }
  };
}
module.exports = {createSession};
