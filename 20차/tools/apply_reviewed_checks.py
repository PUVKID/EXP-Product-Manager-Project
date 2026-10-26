"""새로운 반복 UI 검사까지 포함한 명세를 검증한 뒤 적용합니다."""
import json
import shutil
import sys
from pathlib import Path
sys.path.insert(0, r'C:\Users\PUVKID\.codex\hooks')
from routing_stop import validate_checks
root = Path(__file__).resolve().parents[1]
new_path = root / 'tools/checks-final-reviewed.json'
spec = json.loads(new_path.read_text(encoding='utf-8-sig'))
checks, errors = validate_checks(root, spec)
assert not errors, errors
old_path = root / '.codex/routing-checks.json'
backup = root / 'work/initial-routing-checks.json'
if not backup.exists():
    shutil.copy2(old_path, backup)
old_path.write_text(json.dumps(spec, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
print('추가 UI 요구에 맞는 검사 명세 %d개 적용' % len(checks))
