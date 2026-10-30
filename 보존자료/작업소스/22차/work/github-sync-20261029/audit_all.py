import hashlib,json,subprocess
from pathlib import Path
base=Path(r"D:\2026\EXP 프로젝트(번장 상품 등록 매크로)")
repo=base/'22차/work/github-sync-20261029/repository'
tracked=subprocess.check_output(['git','-C',str(repo),'ls-files','-z']).decode('utf-8').split('\0')
tracked=[p for p in tracked if p]
code_ext={'.cs','.csproj','.ps1','.py','.js','.bat','.sh'}
remote_hashes={hashlib.sha256((repo/p).read_bytes()).hexdigest() for p in tracked if (repo/p).suffix in code_ext}
results=[]
for folder in sorted(base.iterdir(), key=lambda p:int(p.name[:-1]) if p.name.endswith('차') and p.name[:-1].isdigit() else 999):
 if not(folder.is_dir() and folder.name.endswith('차') and folder.name[:-1].isdigit()):continue
 present=[p for p in tracked if p.startswith(folder.name+'/')]
 candidates=[]
 for p in folder.rglob('*'):
  if not p.is_file() or p.suffix not in code_ext:continue
  rel=p.relative_to(folder)
  if any(x in {'work','evidence','.git','.codex','backups','dist','records','node_modules','__pycache__'} or 'backup' in x.lower() for x in rel.parts):continue
  target=folder.name+'/'+rel.as_posix()
  candidates.append({'path':rel.as_posix(),'in_remote':target in tracked,'same':target in tracked and (repo/target).read_bytes()==p.read_bytes()})
 results.append({'round':folder.name,'remote_files':len(present),'source_candidates':len(candidates),'missing':[p['path'] for p in candidates if not p['in_remote']],'changed':[p['path'] for p in candidates if p['in_remote'] and not p['same']]})
work21=base/'21차/work'
unique21=[];total21=0;duplicates21=0
for p in work21.rglob('*.cs'):
 total21+=1;h=hashlib.sha256(p.read_bytes()).hexdigest()
 if h in remote_hashes:duplicates21+=1
 else:unique21.append(str(p.relative_to(work21)).replace('\\','/'))
report={'rounds':results,'work21':{'cs_files':total21,'exact_existing_duplicates':duplicates21,'not_exactly_matching_count':len(unique21),'not_exactly_matching_paths':unique21},'outputs21_files':len(list((base/'21차/outputs').rglob('*')))}
(repo.parent/'all-rounds-audit.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf-8')
for row in results:print(json.dumps({'round':row['round'],'remote':row['remote_files'],'candidates':row['source_candidates'],'missing':len(row['missing']),'changed':row['changed']},ensure_ascii=False))
print(json.dumps({'work21':{'cs':total21,'duplicates':duplicates21,'not_exact':len(unique21),'sample':unique21[:12]},'outputs21_files':report['outputs21_files']},ensure_ascii=False))
