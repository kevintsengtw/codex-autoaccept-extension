"""Package source plus npm-provided Windows runtime modules; no macOS binaries."""
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import hashlib
import json

root = Path(__file__).resolve().parents[1]
source = root / 'cli'
version = json.loads((source / 'package.json').read_text())['version']
output = root / f'codex-autoaccept-cli-windows-{version}.zip'
prefix = f'codex-autoaccept-cli-{version}'
with ZipFile(output, 'w', ZIP_DEFLATED) as archive:
    for file in sorted(source.rglob('*')):
        if not file.is_file():
            continue
        rel = file.relative_to(source)
        if 'node_modules' in rel.parts:
            if file.name == '.package-lock.json':
                continue
            if 'prebuilds' in rel.parts:
                target = rel.parts[rel.parts.index('prebuilds') + 1]
                if target not in ('win32-x64', 'win32-arm64'):
                    continue
            if 'build' in rel.parts or file.suffix == '.pdb':
                continue
        data = file.read_bytes()
        if file.suffix == '.cmd':
            data = data.replace(b'\r\n', b'\n').replace(b'\n', b'\r\n')
        archive.writestr(f'{prefix}/{rel.as_posix()}', data)
with ZipFile(output) as archive:
    entries = archive.namelist()
    for arch in ('win32-x64', 'win32-arm64'):
        assert any(f'prebuilds/{arch}/conpty.node' in n for n in entries)
        assert any(f'prebuilds/{arch}/conpty/' in n and n.endswith('OpenConsole.exe') for n in entries)
    assert any(n.endswith('/package-lock.json') for n in entries)
    assert not any('darwin-' in n or n.endswith('.pdb') for n in entries)
checksum = hashlib.sha256(output.read_bytes()).hexdigest()
output.with_suffix('.zip.sha256').write_text(f'{checksum}  {output.name}\n')
print(f'{output}\n{output.stat().st_size} bytes\nSHA-256 {checksum}')
