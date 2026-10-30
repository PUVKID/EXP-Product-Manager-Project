import hashlib, json, pathlib, re, subprocess, sys, urllib.parse

ROOT = pathlib.Path(__file__).resolve().parent.parent
GIT = r'C:\Users\PUVKID\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\git\cmd\git.exe'
BASE = '6847ab775c71f5012aaf78cfa5d32d44132701b8'

def git(*args):
    return subprocess.check_output([GIT, *args], cwd=ROOT)

def fail(message):
    raise SystemExit(message)

manifest = json.loads((ROOT / 'work/public-sync-manifest.json').read_text(encoding='utf-8'))
entries = {x['target']: x for x in manifest}
if len(entries) != len(manifest): fail('중복 대상')
changed = git('diff', '--cached', '--name-only', '-z').decode('utf-8').split('\0')[:-1]
allowed_docs = {'README.md','CHANGELOG.md','docs/VERSIONS.md','docs/SANITIZATION.md','docs/ALL_ROUNDS_AUDIT.md'} | {f'{n}차/README.md' for n in (1,10,21,22)} | {f'docs/versions/{n}차.md' for n in range(1,23)}
if set(changed) != set(entries) | allowed_docs: fail('스테이지 경로와 명세 불일치')
existing = set(git('ls-tree','-r','--name-only','-z',BASE).decode('utf-8').split('\0')[:-1])
if (set(changed) & existing) - ({'README.md','CHANGELOG.md','docs/VERSIONS.md','docs/SANITIZATION.md'} | {f'docs/versions/{n}차.md' for n in range(2,21) if n != 10}): fail('기존 공개 파일 변경')
batch = subprocess.Popen([GIT, 'cat-file', '--batch'], cwd=ROOT, stdin=subprocess.PIPE, stdout=subprocess.PIPE)
requests = ''.join(':'+path+'\n' for path in changed).encode('utf-8')
out, _ = batch.communicate(requests)
if batch.returncode: fail('인덱스 읽기 실패')
old_notice = '현재 공개본에는 DLL·아이콘·글꼴·도움말·브랜드 이미지 등 빌드 입력이 빠져 있으며, 최신 build.bat의 위임 대상 build/Build.ps1도 포함되어 있지 않습니다.'
new_notice = '공개본에는 DLL·아이콘·글꼴·브랜드 이미지 등 일부 바이너리 빌드 입력이 빠져 있습니다. 각 차수의 빌드 스크립트와 리소스 구성을 확인하세요.'

blobs = {}
pos = 0
for path in changed:
    end = out.index(b'\n', pos)
    header = out[pos:end].split()
    if len(header) != 3 or header[1] != b'blob': fail('인덱스 blob 오류: '+path)
    size = int(header[2]); pos = end + 1
    blobs[path] = out[pos:pos+size]; pos += size + 1
for n in list(range(2,10)) + list(range(11,21)):
    path = f'docs/versions/{n}차.md'
    original = git('show', BASE+':'+path).decode('utf-8').replace('\r\n','\n')
    if original.count(old_notice) != 1: fail('기존 차수 안내 기준 불일치: '+path)
    expected = original.replace(old_notice, new_notice).replace(new_notice+' \n',new_notice+'\n')
    actual = blobs[path].decode('utf-8').replace('\r\n','\n')
    if actual != expected: fail('기존 차수 문서의 범위 외 변경: '+path)
for path in changed:
    data = blobs[path]
    try: text = data.decode('utf-8-sig')
    except UnicodeDecodeError: fail('UTF-8 오류: '+path)
    if '\ufffd' in text: fail('대체 문자: '+path)
    if re.search(r'(?i)(expmasterxx00@gmail\.com|lukeyun@kyonggi\.ac\.kr|[a-z]:\\users\\puvkid|<P>[^<]{20,}</P>|ghp_[A-Za-z0-9]{20,}|sk-proj-[A-Za-z0-9_-]{20,})',text): fail('민감 정보: '+path)
    if path in entries:
        e = entries[path]; source = pathlib.Path(e['source']).read_bytes()
        if hashlib.sha256(source).hexdigest() != e['source_sha256']: fail('원본 변경: '+path)
        work = (ROOT/path).read_bytes()
        if hashlib.sha256(work).hexdigest() != e['target_sha256']: fail('공개 사본 변경: '+path)
        if data.replace(b'\r\n',b'\n') != work.replace(b'\r\n',b'\n'): fail('인덱스 내용 불일치: '+path)
        if not e['transforms'] and data.replace(b'\r\n',b'\n') != source.replace(b'\r\n',b'\n'): fail('복사 내용 불일치: '+path)
        if e['transforms']:
            original = source.decode('utf-8-sig').replace('\r\n','\n')
            if 'Brand.Email 환경변수' in e['transforms']:
                ref=(ROOT/'20차/소스코드/src/Brand.cs').read_text(encoding='utf-8-sig')
                block=re.search(r'        public static string Email\n        \{.*?\n        \}', ref, re.S).group()
                original,count=re.subn(r'        public const string Email = "[^"\r\n]+";',block,original)
                if count != 1: fail('Brand 치환 불일치: '+path)
            for mail in ('expmasterxx00@gmail.com','lukeyun@kyonggi.ac.kr'):
                original=original.replace(mail,'contact@example.invalid')
            if '행 끝 공백 정규화' in e['transforms']:
                original = '\n'.join(line.rstrip(' \t') for line in original.split('\n'))
            if '끝 빈 줄 정규화' in e['transforms']:
                original = original.rstrip('\n') + '\n'
            if data.replace(b'\r\n',b'\n') != original.encode('utf-8'): fail('허용 처리 외 변경: '+path)
    if path.endswith('.md'):
        for target in re.findall(r'(?<!!)\[[^]]*\]\(([^)]+)\)',text):
            target=urllib.parse.unquote(target.split('#',1)[0]).strip('<>')
            if not target or '://' in target or target.startswith('mailto:'): continue
            if not (ROOT/path).parent.joinpath(target).exists(): fail('링크 누락: '+path+' -> '+target)
for n in range(1,23):
    if not (ROOT/f'docs/versions/{n}차.md').exists(): fail('차수 문서 누락')
for path in entries:
    if pathlib.PurePosixPath(path).suffix.lower() in ('.exe','.dll','.zip','.xml','.csv','.dpapi'): fail('금지 확장자: '+path)
# 독립 검증이 지적한 후보와 추가 탐색 범위를 원본에서 다시 대조합니다.
candidates = json.loads((ROOT/'evidence/missing-public-text-candidates.json').read_text(encoding='utf-8'))['files']
classification = json.loads((ROOT/'work/second-classification.json').read_text(encoding='utf-8'))
if len(candidates) != 293 or len({x['target'] for x in candidates}) != 293: fail('누락 후보 목록 불일치')
for x in candidates:
    if x['target'] not in entries or entries[x['target']]['source'] != x['source']: fail('후보 미분류: '+x['target'])
for name in ('package_release.py','verify_scope.py','logic-baseline19.json'):
    if '22차/tools/'+name not in entries: fail('22차 도구 누락: '+name)
excluded = {x['target'] for x in classification['excluded'] if x['reason'] == '개인키 XML'}
if len(excluded) != 7: fail('개인키 제외 목록 불일치')
source_base = pathlib.Path(r'D:\2026\EXP 프로젝트(번장 상품 등록 매크로)')
source_roots = []
for n in list(range(2,10))+list(range(11,21)):
    base = source_base / f'{n}차'
    source_roots.extend((p, pathlib.PurePosixPath(f'{n}차') / pathlib.PurePosixPath(p.relative_to(base).as_posix())) for p in base.rglob('*') if p.is_dir() and p.name in ('3_소스코드','소스코드','BunjangAuto_소스코드_강화'))
source_roots.extend([(source_base/'21차/work/round13/delivery/EXP_v2.2.5/3_소스코드',pathlib.PurePosixPath('21차/보관자료/EXP_v2.2.5/3_소스코드')), (source_base/'22차/소스코드',pathlib.PurePosixPath('22차/소스코드'))])
text_ext = {'.cs','.csproj','.ps1','.sh','.bat','.iss','.md','.txt','.manifest','.json','.py','.config','.xml','.csv'}
found_excluded = set()
for source_root, target_root in source_roots:
    for p in source_root.rglob('*'):
        if not p.is_file() or p.suffix.lower() not in text_ext or any(part.lower() in ('dist','lib') for part in p.relative_to(source_root).parts): continue
        target = (target_root / pathlib.PurePosixPath(p.relative_to(source_root).as_posix())).as_posix()
        if target in excluded:
            if p.name != 'license_private.xml': fail('제외 사유 불일치: '+target)
            found_excluded.add(target)
        elif target not in entries and target not in existing: fail('원본 텍스트 누락: '+target)
if found_excluded != excluded: fail('원본 개인키 제외 목록 불일치')
if blobs['22차/소스코드/src/Security/TrustedKeys.cs'] != git('show',BASE+':20차/소스코드/src/Security/TrustedKeys.cs') or blobs['21차/보관자료/EXP_v2.2.5/3_소스코드/src/Security/TrustedKeys.cs'] != blobs['22차/소스코드/src/Security/TrustedKeys.cs']: fail('공개키 불일치')
print(f'통과: 스테이지 {len(changed)}개, 원본 복제 {len(entries)}개, 1~22차 상태')
