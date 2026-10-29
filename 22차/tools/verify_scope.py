"""22차에서 허용한 실행 변경 외 기존 보안 로직과 UTF-8을 검사합니다."""
import hashlib
import json
from pathlib import Path
root = Path(__file__).resolve().parents[1]
source = root / '소스코드'
baseline = json.loads((root / 'tools/logic-baseline19.json').read_text(encoding='utf-8-sig'))
allowed = {'src/Models.cs', 'src/Runner.cs', 'src/UI/AutomationController.cs'}
for name, expected in baseline.items():
    if name not in allowed:
        assert hashlib.sha256((source / name).read_bytes()).hexdigest() == expected, name
count = 0
for path in source.rglob('*'):
    if path.suffix.lower() in {'.cs', '.ps1', '.md', '.iss'}:
        content = path.read_text(encoding='utf-8-sig', errors='strict')
        assert '\ufffd' not in content, str(path)
        count += 1
print('보안/기존로직 24개 해시 일치 / UTF-8 %d개 확인' % count)
