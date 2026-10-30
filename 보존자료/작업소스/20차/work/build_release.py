"""릴리스 빌드의 원본 출력과 한글 표시용 로그를 분리 보존합니다."""
import json
import subprocess
from pathlib import Path
root = Path(__file__).resolve().parents[1]
source = root / '소스코드'
for target in ('All', 'Reset'):
    argv = [r'C:\Users\PUVKID\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe', '-NoProfile', '-File', str(source / 'build/Build.ps1'), '-Target', target]
    result = subprocess.run(argv, cwd=source, capture_output=True, timeout=60)
    raw = result.stdout + result.stderr
    (root / ('work/build-' + target + '.raw')).write_bytes(raw)
    record = {'argv': argv, 'cwd': str(source), 'exit_code': result.returncode, 'bytes': len(raw), 'encoding': '행별 UTF-8 또는 CP949'}
    (root / ('work/build-' + target + '.json')).write_text(json.dumps(record, ensure_ascii=False, indent=2), encoding='utf-8')
    lines = []
    for line in raw.split(b'\n'):
        try:
            lines.append(line.decode('utf-8', errors='strict'))
        except UnicodeDecodeError:
            lines.append(line.decode('cp949', errors='strict'))
    (root / ('work/build-' + target + '.log')).write_text('\n'.join(lines), encoding='utf-8')
    print(json.dumps(record, ensure_ascii=False))
    if result.returncode:
        raise SystemExit(result.returncode)
