const fs = require('node:fs');
const path = require('node:path');
const crypto = require('node:crypto');
const pause = ms => Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
function regular(file) {
  try {
    const stat = fs.lstatSync(file);
    if (!stat.isFile() || stat.isSymbolicLink()) throw Error('Log path must be a regular file.');
    return stat;
  } catch (err) {if (err.code === 'ENOENT') return null; throw err;}
}
function createRotatingLog(file, {maxBytes = 5 * 1024 * 1024, backups = 3} = {}) {
  if (!Number.isInteger(maxBytes) || maxBytes < 1 || !Number.isInteger(backups) || backups < 0 || backups > 20) throw Error('Invalid log retention settings.');
  file = path.resolve(file);
  const directory = path.dirname(file), base = path.basename(file), lock = file + '.lock';
  fs.mkdirSync(directory, {recursive:true});
  let initialized = false;
  function acquire() {
    const token = crypto.randomUUID(), until = Date.now() + 1000;
    while (true) {
      try {
        fs.mkdirSync(lock);
        try {fs.writeFileSync(path.join(lock,'owner.json'),JSON.stringify({pid:process.pid,token}),{flag:'wx',mode:0o600});}
        catch (err) {try {fs.rmdirSync(lock);} catch {} throw err;}
        return () => {
          const owner = JSON.parse(fs.readFileSync(path.join(lock,'owner.json'),'utf8'));
          if (owner.token !== token) throw Error('Log lock ownership changed.');
          fs.unlinkSync(path.join(lock,'owner.json'));fs.rmdirSync(lock);
        };
      } catch (err) {
        if (err.code !== 'EEXIST') throw err;
        // Reclaim only a valid lock whose owner is provably no longer alive.
        try {
          const ownerFile = path.join(lock,'owner.json');
          const raw = fs.readFileSync(ownerFile,'utf8'), owner = JSON.parse(raw);
          if (Number.isInteger(owner.pid) && owner.pid > 0) {
            try {process.kill(owner.pid,0);}
            catch (dead) {
              if (dead.code === 'ESRCH') {
                const reclaim = lock + '.reclaim';
                try {
                  fs.mkdirSync(reclaim);
                  try {
                    // Serialize stale-lock cleanup too: another writer may have
                    // acquired the original lock after a competing cleanup.
                    if (fs.readFileSync(ownerFile,'utf8') === raw) {
                      fs.unlinkSync(ownerFile);fs.rmdirSync(lock);
                    }
                  } finally {fs.rmdirSync(reclaim);}
                  continue;
                } catch {}
              }
            }
          }
        } catch {}
        if (Date.now() >= until) throw Error('Log is busy or has an unverified lock; no log files were overwritten.');
        pause(10);
      }
    }
  }
  function trim(fileToTrim) {
    const stat = regular(fileToTrim);
    if (!stat || stat.size <= maxBytes) return;
    // Read only the tail of an old unlimited log, never the entire file.
    const fd = fs.openSync(fileToTrim,'r');
    const tail = Buffer.alloc(maxBytes);
    try {
      let read = 0;
      while (read < tail.length) {
        const n = fs.readSync(fd,tail,read,tail.length-read,stat.size-tail.length+read);
        if (!n) throw Error('Log changed while reading its retained tail.');
        read += n;
      }
    } finally {fs.closeSync(fd);}
    const newline = tail.indexOf(10);
    const data = newline < 0 ? Buffer.alloc(0) : tail.subarray(newline+1);
    const temp = fileToTrim + '.tmp-' + crypto.randomUUID();
    try {fs.writeFileSync(temp,data,{mode:0o600,flag:'wx'});fs.renameSync(temp,fileToTrim);}
    finally {if (fs.existsSync(temp)) fs.unlinkSync(temp);}
  }
  function prune() {
    for (const entry of fs.readdirSync(directory)) {
      if (!entry.startsWith(base+'.')) continue;
      const suffix = entry.slice(base.length+1);
      // Under the file lock no cooperating writer can be using a trim temp.
      // Remove only this manager's exact UUID-named leftovers after a crash.
      if (/^(?:[1-9]\d*\.)?tmp-[a-f0-9]{8}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{4}-[a-f0-9]{12}$/i.test(suffix)) {
        const temp = path.join(directory,entry);
        regular(temp);fs.unlinkSync(temp);continue;
      }
      if (!/^[1-9]\d*$/.test(suffix)) continue;
      const archive = path.join(directory,entry);
      regular(archive);
      if (Number(suffix) > backups) fs.unlinkSync(archive);
      else trim(archive);
    }
  }
  function rotate() {
    prune();
    if (backups === 0) {fs.unlinkSync(file);return;}
    for (let i=backups;i>=1;i--) {
      const destination=file+'.'+i, source=i===1 ? file : file+'.'+(i-1);
      if (regular(destination)) fs.unlinkSync(destination);
      if (regular(source)) fs.renameSync(source,destination);
    }
  }
  return {
    append(line) {
      const data = Buffer.from(line,'utf8');
      if (data.length > maxBytes) throw Error('Log entry exceeds configured file limit.');
      const release = acquire();
      try {
        if (!initialized) {prune();trim(file);initialized=true;}
        const stat = regular(file);
        if (stat && stat.size + data.length > maxBytes) rotate();
        fs.appendFileSync(file,data,{mode:0o600});
      } finally {release();}
    }
  };
}
module.exports = {createRotatingLog};
