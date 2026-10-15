"""Package the independent 0.2.x console repair; never rebuild or replace R11."""
from pathlib import Path
import hashlib
import json
import zipfile

root = Path(__file__).resolve().parent.parent
source = root / "console-probe"
name = "codex-uia-probe-windows-0.2.2"
baseline = json.loads((source / "BASELINE.json").read_text())
baseline_zip = root / "artifacts" / baseline["package"]
assert hashlib.sha256(baseline_zip.read_bytes()).hexdigest() == baseline["sha256"]

files = {
    "CodexUiaProbe.exe": source / "bin/Release/net48/CodexUiaProbe.exe",
    "CodexUiaProbe.exe.config": source / "bin/Release/net48/CodexUiaProbe.exe.config",
    "CodexUiaManualInvoke.exe": source / "manual/bin/Release/net48/CodexUiaManualInvoke.exe",
    "CodexUiaManualInvoke.exe.config": source / "manual/bin/Release/net48/CodexUiaManualInvoke.exe.config",
}
for filename in ("README.md", "VERIFICATION.md", "BASELINE.json", "LICENSE",
                 "Run-Auto-Accept.cmd", "Run-Manual-Once.cmd", "Run-Probe.cmd"):
    files[filename] = source / filename
for path in source.rglob("*"):
    relative = path.relative_to(source)
    if not path.is_file() or any(p in {"bin", "obj"} for p in relative.parts):
        continue
    if path.suffix in {".cs", ".csproj", ".json", ".manifest", ".ps1", ".props", ".cmd"}:
        files["source/" + relative.as_posix()] = path

contents = {}
for entry, path in files.items():
    data = path.read_bytes()
    if entry.endswith(".cmd"):
        data = data.replace(b"\r\n", b"\n").replace(b"\n", b"\r\n")
    contents[entry] = data
contents["SHA256SUMS"] = "".join(
    f"{hashlib.sha256(data).hexdigest()}  {entry}\n"
    for entry, data in sorted(contents.items())
).encode()

output = root / "artifacts" / (name + ".zip")
with zipfile.ZipFile(output, "w", zipfile.ZIP_DEFLATED) as archive:
    for entry, data in contents.items():
        archive.writestr(name + "/" + entry, data)
with zipfile.ZipFile(output) as archive:
    assert archive.testzip() is None
    assert set(archive.namelist()) == {name + "/" + entry for entry in contents}
    for entry, data in contents.items():
        assert archive.read(name + "/" + entry) == data

checksums = root / "artifacts/SHA256SUMS"
previous = checksums.read_text().splitlines()
previous = [line for line in previous if line.split()[-1] != output.name]
previous.append(hashlib.sha256(output.read_bytes()).hexdigest() + "  " + output.name)
checksums.write_text("\n".join(previous) + "\n")
print(f"Verified console archive: {output} ({output.stat().st_size} bytes)")
