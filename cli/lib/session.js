const {Terminal} = require('@xterm/headless');
const {detect} = require('./detector');
function createSession({child, settings, cols, rows, log, onError = err => {throw err;}, setTimer = setTimeout, clearTimer = clearTimeout}) {
  const screen = new Terminal({cols, rows, scrollback: 0, allowProposedApi: true});
  let ended = false, timer, last = '', generation = 0, writing = 0;
  function inspect() {
    if (ended || writing || !settings.enabled) return;
    const buffer = screen.buffer.active;
    const text = Array.from({length: screen.rows}, (_, i) => buffer.getLine(buffer.viewportY + i)?.translateToString(true) ?? '').join('\n').trimEnd();
    const result = detect(text, settings);
    if (!result) { last = ''; return; }
    if (last === result.identity) return;
    last = result.identity;
    if (!result.allow || !('key' in result)) { log('MANUAL', result.reason); return; }
    if (settings.dryRun) { log('DRY_RUN', 'would approve once'); return; }
    log('ACCEPT', 'once');
    child.write(result.key);
  }
  function schedule() { clearTimer(timer); timer = setTimer(() => {try {inspect();} catch (err) {onError(err);}}, 250); }
  return {
    feed(chunk) {
      if (ended) return;
      generation++; const current = generation;
      clearTimer(timer); writing++;
      screen.write(chunk, () => {
        writing--;
        if (!ended && current === generation) schedule();
      });
    },
    input(data) {
      if (ended) return;
      // Ctrl+] is reserved by the wrapper. Everything else, including Ctrl+C, is forwarded.
      for (const part of data.split(/(\x1d)/)) {
        if (part === '\x1d') {
          settings.enabled = !settings.enabled; last = ''; clearTimer(timer);
          log('MODE', settings.enabled ? 'ON' : 'OFF');
          if (settings.enabled) schedule();
        } else if (part) child.write(part);
      }
    },
    resize(cols, rows) {
      if (ended) return;
      clearTimer(timer); generation++;
      child.resize(cols, rows); screen.resize(cols, rows); last = ''; schedule();
    },
    stop() { if (ended) return; ended = true; clearTimer(timer); screen.dispose(); }
  };
}
module.exports = {createSession};
