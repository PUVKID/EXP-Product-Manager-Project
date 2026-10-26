"""디자인 작업에서 핵심 로직과 한글 소스 보존을 확인합니다."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / '소스코드'
baseline = json.loads((root / 'tools' / 'logic-baseline.json').read_text(encoding='utf-8-sig'))
for name, expected in baseline.items():
    actual = hashlib.sha256((source / name).read_bytes()).hexdigest()
    if actual != expected:
        raise SystemExit('핵심 로직 변경: ' + name)
count = 0
for path in source.rglob('*'):
    if path.suffix.lower() in {'.cs', '.ps1', '.md', '.iss'}:
        text = path.read_text(encoding='utf-8-sig', errors='strict')
        if '\ufffd' in text:
            raise SystemExit('대체 문자 발견: ' + str(path))
        count += 1
print('핵심 로직 보존 %d개 / UTF-8 확인 %d개' % (len(baseline), count))
