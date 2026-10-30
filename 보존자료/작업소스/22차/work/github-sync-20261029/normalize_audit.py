import json,subprocess,hashlib,re,difflib
from pathlib import Path
root=Path(__file__).resolve().parent;repo=root/'repository';base=repo.parents[3]
# 실제 원본 프로젝트 위치를 명시하여 작업 사본을 원본으로 오인하지 않습니다.
base=Path(r'D:\2026\EXP 프로젝트(번장 상품 등록 매크로)')
def normalize(data):return data.decode('utf-8-sig').replace('\r\n','\n')
a=json.loads((root/'all-rounds-audit.json').read_text(encoding='utf-8'))
for row in a['rounds']:
 changed=[]
 for name in row['changed']:
  old=repo/row['round']/name;new=base/row['round']/name
  try:
   before=normalize(old.read_bytes());after=normalize(new.read_bytes())
   if before!=after:changed.append(name)
  except UnicodeError:changed.append(name)
 row['normalized_changed']=changed
 print(json.dumps({'round':row['round'],'remote':row['remote_files'],'source':row['source_candidates'],'missing_count':len(row['missing']),'changed_after_newline_bom':len(changed),'changed_names':changed[:8]},ensure_ascii=False))
tracked=subprocess.check_output(['git','-C',str(repo),'ls-files','-z']).decode().split('\0')
hashes=set()
for name in tracked:
 if not name.endswith('.cs'):continue
 try:hashes.add(hashlib.sha256(normalize((repo/name).read_bytes()).encode()).hexdigest())
 except UnicodeError:pass
unique=[];duplicate=0
for p in (base/'21차/work').rglob('*.cs'):
 try:
  text=normalize(p.read_bytes());text=re.sub(r'public const string Email\s*=\s*"[^"]+"\s*;', 'PUBLIC_CONTACT_REDACTED',text)
  h=hashlib.sha256(normalize(p.read_bytes()).encode()).hexdigest()
  if h in hashes:duplicate+=1
  else:unique.append(p.relative_to(base/'21차/work').as_posix())
 except UnicodeError:unique.append(p.relative_to(base/'21차/work').as_posix())
a['work21']['normalized_duplicate_count']=duplicate;a['work21']['normalized_unmatched_paths']=unique
(root/'normalized-audit.json').write_text(json.dumps(a,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'work21_normalized_duplicates':duplicate,'unmatched':len(unique),'sample':unique[:10]},ensure_ascii=False))
