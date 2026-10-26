"""검사된 EXE와 고객 배포 파일을 새 차수 안에서만 묶습니다."""
from pathlib import Path
import hashlib
import shutil
import zipfile

root = Path(__file__).resolve().parents[1]
source = root / "소스코드"
dist = source / "dist"
customer = root / "고객 배포"
owner = root / "관리자 전용"
customer.mkdir(exist_ok=True)
owner.mkdir(exist_ok=True)
for name in ("EXP.exe",):
    shutil.copy2(dist / name, customer / name)
shutil.copy2(dist / "installer" / "EXP_Setup_2.3.1.exe", customer / "EXP_Setup_2.3.1.exe")
for name in ("EXP-Master.exe", "EXP-Reset.exe"):
    shutil.copy2(dist / name, owner / name)
licenses = customer / "licenses"
licenses.mkdir(exist_ok=True)
for path in (source / "licenses").glob("*.txt"):
    shutil.copy2(path, licenses / path.name)
shutil.copy2(source / "resources" / "fonts" / "NanumGothic-OFL.txt", licenses / "NanumGothic-OFL.txt")
with zipfile.ZipFile(root / "EXP_2.3.1_고객배포.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(customer.rglob("*")):
        if path.is_file():
            archive.write(path, path.relative_to(customer))
files = sorted(customer.rglob("*.exe")) + sorted(owner.glob("*.exe")) + [root / "EXP_2.3.1_고객배포.zip"]
lines = [hashlib.sha256(path.read_bytes()).hexdigest() + "  " + path.relative_to(root).as_posix() for path in files]
(root / "SHA256SUMS.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
print("배포 파일 묶음 완료: " + str(len(files)) + "개 해시 기록")
