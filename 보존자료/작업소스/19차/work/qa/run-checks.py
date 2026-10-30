from pathlib import Path
import subprocess,json,datetime
base=Path('../work/qa')
def run(argv,name):
 r=subprocess.run(argv,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,timeout=100);lines=[]
 for line in r.stdout.split(b'\n'):
  try:lines.append(line.decode('utf-8'))
  except UnicodeDecodeError:lines.append(line.decode('cp949',errors='replace'))
 out='\n'.join(lines);base.joinpath(name+'.raw').write_bytes(r.stdout);base.joinpath(name+'.log').write_text(out,encoding='utf-8');print(name,'exit',r.returncode,'원본 bytes',len(r.stdout),'아래 마지막 800자 발췌',out[-800:],flush=True)
 with base.joinpath('check-runs.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps(dict(time_kst=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).isoformat(),argv=argv,cwd=str(Path.cwd()),exit_code=r.returncode,original_bytes=len(r.stdout),captured_bytes=len(r.stdout),log=name+'.log'),ensure_ascii=False)+'\n')
 if r.returncode:
  with base.joinpath('build-failures.jsonl').open('a',encoding='utf-8') as f:f.write(json.dumps(dict(time_kst=datetime.datetime.now(datetime.timezone(datetime.timedelta(hours=9))).isoformat(),session='01a130b3-d834-7820-ada5-c6857c0ab621',task='exp-monochrome-19-20261026',argv=argv,cwd=str(Path.cwd()),exit_code=r.returncode,error_original=out,changes='흑백 팔레트와 큰 현황 패널 및 원형 진행 구현',repair_used=0,input_fix_used=1),ensure_ascii=False)+'\n')
 return r.returncode
for target in ['All','Reset','Tests']:
 if run(['powershell','-NoProfile','-ExecutionPolicy','Bypass','-File','build/Build.ps1','-Target',target],'final-build-'+target):raise SystemExit(1)
if run([str(Path('dist/RedesignUiTests.exe').resolve()),str((base/'redesign').resolve())],'redesign'):raise SystemExit(1)
for scale in ['1','1.25','1.5','1.75','2']:
 if run([str(Path('dist/DpiLayoutTests.exe').resolve()),str((base/('dpi-'+scale)).resolve()),scale],'dpi-'+scale):raise SystemExit(1)
