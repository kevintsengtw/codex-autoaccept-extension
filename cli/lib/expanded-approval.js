const {evaluate, describe} = require('./policy');
const {detect} = require('./detector');
const compact = text => text.replace(/\s+/g, '');
function candidate(text) {
  const heading = /^\s*Would you like to run the following command\?\s*$/m;
  const match = heading.exec(text);
  // Long Reason text can scroll the heading and '$' off the visible viewport.
  // A complete official view-all hint plus approval menu may open the viewer,
  // but can never authorize unseen text: EXEC paging and return checks still apply.
  if (!match && /^\s*(?:Would you like to (?:make the following edits|apply these changes)\?|Allow command\?)\s*$/m.test(text)) return null;
  const block = (match ? text.slice(match.index) : text).trim();
  if (!/^\s*[›❯>]?\s*1\. Yes, proceed \(y\)\s*$/m.test(block) ||
      !/^\s*\d+\. No, and tell Codex what to do differently \(esc\)\s*$/m.test(block) ||
      !/Press enter to confirm or esc to cancel\s*$/.test(block)) return null;
  // Official ListSelectionView renders "[… N lines] ctrl+a view all".
  // Retain the earlier +N fixture layout, but require the exact view-all shortcut.
  const hint = /(?:\[(?:…|\.{3})\s*\d+\s+lines?\]|\+\s*\d+\s+lines?)\s*(?:[│|·]\s*)?ctrl\+a\s+view all/i.exec(block);
  if (!hint || hint.index > block.search(/^\s*[›❯>]?\s*1\. Yes, proceed \(y\)\s*$/m)) return null;
  return {identity: compact(block), block, cropped: !match};
}
function pager(text) {
  const lines = text.split('\n').map(line => line.trimEnd());
  if (!/^\s*\/\s*E\s*X\s*E\s*C\s*(?:\/\s*)*$/.test(lines[0] ?? '')) return null;
  const bar = lines.findIndex(line => /^[─━-]+\s*\d{1,3}%\s*[─━-]*\s*$/.test(line));
  if (bar < 2) return null;
  const hints = lines.slice(bar + 1).join('\n');
  if (!/pgup\/pgdn to page/i.test(hints) || !/^\s*q close\s*$/m.test(hints)) return null;
  const percent = Number(lines[bar].match(/(\d+)%/)[1]);
  if (percent > 100) return null;
  return {rows: lines.slice(1, bar), percent, signature: compact(text)};
}
function overlap(previous, current) {
  const matches = [];
  for (let n = 1; n < current.length; n++) {
    if (previous.slice(-n).every((row, i) => row === current[i])) matches.push(n);
  }
  return matches;
}
function createExpandedApproval({child, settings, log, now = Date.now}) {
  let state = null, attempted = '';
  function finish(reason) {
    const identity = state?.origin.identity;
    state = null;
    log('MANUAL', `expanded approval: ${reason}`);
    return {handled: true, identity};
  }
  function close(frame, reason) {
    // Only q closes the verified EXEC viewer; never Esc which could deny the operation.
    log('EXPAND', reason);
    child.write('q');
    state.phase = 'return'; state.since = now();
  }
  function inspect({logical, physical}) {
    if (!state) {
      if (!settings.enabled || settings.dryRun || !settings.expandLongCommands) return null;
      const origin = candidate(logical) ?? candidate(physical);
      if (!origin) {
        if (!/Would you like to|Yes, proceed|\/\s*E\s*X\s*E\s*C/.test(logical)) attempted = '';
        return null;
      }
      if (origin.identity === attempted) return null;
      attempted = origin.identity;
      state = {origin, phase: 'open', since: now(), started: now(), pages: 0, rows: [], previous: null, signature: '', outcome: null};
      log('EXPAND', origin.cropped ? 'open explicit Ctrl+A view-all command; heading outside viewport' : 'open explicit Ctrl+A view-all command');
      child.write('\x01');
      return {handled: true, active: true};
    }
    if (!settings.enabled) return finish('switched OFF; viewer left for manual control');
    if (now() - state.started > 20000) return finish('scan time limit; no approval sent');
    if (state.phase === 'return') {
      const modal = candidate(logical) ?? candidate(physical);
      if (modal && modal.identity === state.origin.identity) {
        const {outcome, origin} = state;
        state = null;
        if (!outcome?.allow) {
          log('MANUAL', `expanded approval: ${outcome ? describe(outcome) : 'incomplete content'}`);
          return {handled: true, identity: origin.identity};
        }
        log('ACCEPT', 'once; full expanded command reviewed');
        child.write('y');
        return {handled: true, identity: origin.identity};
      }
      // A changed approval is never approved with the old command's decision.
      if (detect(logical, settings) || candidate(logical)) return finish('approval changed after closing viewer');
      if (now() - state.since > 3000) return finish('original approval did not return');
      return {handled: true, active: true};
    }
    const frame = pager(physical);
    if (!frame) {
      if (now() - state.since > 3000) return finish('unrecognized EXEC viewer or response timeout');
      return {handled: true, active: true};
    }
    if (frame.signature === state.signature) {
      if (now() - state.since > 3000) {
        state.outcome = {allow: false, reason: 'pager did not advance; completeness unverified'};
        close(frame, 'stop stalled scan and return');
      }
      return {handled: true, active: true};
    }
    state.pages++;
    if (state.pages > 64 || state.rows.join('\n').length > 500000) {
      state.outcome = {allow: false, reason: 'expanded command exceeds scan limit'};
      close(frame, 'scan limit; return');
      return {handled: true, active: true};
    }
    log('EXPAND', `read page ${state.pages}; position ${frame.percent}%; rows ${frame.rows.length}`);
    if (!state.previous) {
      if (frame.percent !== 0 && frame.percent !== 100) {
        state.outcome = {allow: false, reason: 'viewer did not start at top'};
        close(frame, 'unexpected initial position; return');
        return {handled: true, active: true};
      }
      state.rows.push(...frame.rows);
    } else {
      if (frame.rows.length !== state.previous.rows.length || frame.percent < state.previous.percent || (frame.percent < 100 && frame.percent === state.previous.percent)) {
        state.outcome = {allow: false, reason: 'viewer geometry or position changed'};
        close(frame, 'inconsistent viewer; return');
        return {handled: true, active: true};
      }
      if (frame.percent === 100) {
        const matches = overlap(state.previous.rows, frame.rows);
        if (matches.length > 1) {
          state.outcome = {allow: false, reason: 'ambiguous last-page overlap'};
          close(frame, 'ambiguous overlap; return');
          return {handled: true, active: true};
        }
        state.rows.push(...frame.rows.slice(matches[0] ?? 0));
        // 100% is rounded. An overlapping last page proves a clamped PageDown;
        // otherwise request another page and fail manual if it never advances.
        if (!matches.length) {
          state.previous = frame; state.signature = frame.signature; state.since = now();
          child.write('\x1b[6~');
          return {handled: true, active: true};
        }
      } else state.rows.push(...frame.rows);
    }
    if (state.rows.join('\n').length > 500000) {
      state.outcome = {allow: false, reason: 'expanded command exceeds text limit'};
      close(frame, 'text limit; return');
      return {handled: true, active: true};
    }
    if (frame.percent === 100) {
      // '~' is the official pager's unused-row filler. Remove only trailing fillers.
      while (state.rows.at(-1)?.trim() === '~') state.rows.pop();
      const command = state.rows.join('\n').trim();
      state.outcome = command ? evaluate(command, settings) : {allow: false, reason: 'empty expanded command'};
      close(frame, state.outcome.allow ? 'complete command passed rules; return' : `full command manual: ${describe(state.outcome)}`);
    } else {
      state.previous = frame; state.signature = frame.signature; state.since = now();
      child.write('\x1b[6~');
    }
    return {handled: true, active: true};
  }
  return {inspect, cancel(reason) { if (state) finish(reason); }, get active() {return !!state;}};
}
module.exports = {createExpandedApproval, candidate, pager};
