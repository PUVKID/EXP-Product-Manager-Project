# -*- coding: utf-8 -*-
"""서버 보강 점검: SQL 주입, 경로 탐색, 본문 크기 제한, 동시 접속, 대량 전송 속도, TLS.
사용: python3 tests/hardening_checks.py   (임시 DB/포트를 스스로 만들고 지운다)"""
import json, os, ssl, subprocess, sys, tempfile, threading, time, urllib.request, urllib.error, uuid, socket

HERE = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TMP = tempfile.mkdtemp(prefix="bja_hard_")
env = dict(os.environ, BJA_DATA_DIR=TMP, BJA_INGEST_KEYS="hk", BJA_QUIET="1", BJA_ADMIN_PASSWORD="hardening-pass-123")
subprocess.check_call([sys.executable, "server.py", "init-admin", "--username", "admin"], cwd=HERE, env=env, stdout=subprocess.DEVNULL)

def free_port():
    s = socket.socket(); s.bind(("127.0.0.1", 0)); p = s.getsockname()[1]; s.close(); return p

res = []
def check(name, ok):
    res.append(ok); print(("  PASS " if ok else "  FAIL ") + name)

def req(base, method, path, body=None, headers=None, ctx=None, raw_body=None):
    data = raw_body if raw_body is not None else (json.dumps(body).encode() if body is not None else None)
    r = urllib.request.Request(base + path, data=data, method=method, headers=dict({"Content-Type": "application/json"}, **(headers or {})))
    try:
        with urllib.request.urlopen(r, timeout=15, context=ctx) as x:
            return x.status, x.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()

def start(port, extra=()):
    p = subprocess.Popen([sys.executable, "server.py", "serve", "--host", "127.0.0.1", "--port", str(port)] + list(extra), cwd=HERE, env=env, stderr=subprocess.DEVNULL)
    for _ in range(50):
        try: socket.create_connection(("127.0.0.1", port), 0.2).close(); break
        except OSError: time.sleep(0.1)
    return p

port = free_port(); base = "http://127.0.0.1:%d" % port; srv = start(port)
try:
    st, b = req(base, "POST", "/api/v1/admin/login", {"username": "admin", "password": "hardening-pass-123"})
    H = {"Authorization": "Bearer " + json.loads(b)["token"]}
    cid = str(uuid.uuid4()); st, b = req(base, "POST", "/api/v1/register", {"client_id": cid, "info": {}}, {"X-Ingest-Key": "hk"}); tok = json.loads(b)["token"]
    IH = {"X-Client-Id": cid, "X-Client-Token": tok}

    # --- SQL 주입 시도: 서버가 죽거나 전체 데이터를 돌려주면 안 됨
    req(base, "POST", "/api/v1/ingest", {"events": [{"id": "s1", "level": "INFO", "module": "M", "action": "log", "message": "정상 메시지"}]}, IH)
    for payload in ["' OR '1'='1", "'; DROP TABLE events;--", "%' UNION SELECT token_hash,1,1,1,1,1,1,1,1,1 FROM clients--", "\" OR 1=1 --"]:
        for field in ("q", "client_id", "module", "action", "level", "since_id", "from", "to"):
            st, b = req(base, "GET", "/api/v1/admin/logs?%s=%s" % (field, urllib.request.quote(payload)), headers=H)
            if st not in (200, 400): check("SQL 주입 응답 코드 이상 (%s)" % field, False); break
    st, b = req(base, "GET", "/api/v1/admin/logs?q=" + urllib.request.quote("' OR '1'='1"), headers=H)
    check("SQL 주입 문자열이 일반 검색어로만 처리됨(결과 0건)", st == 200 and json.loads(b)["events"] == [])
    st, b = req(base, "GET", "/api/v1/admin/logs", headers=H)
    check("주입 시도 후에도 events 테이블/데이터 정상", st == 200 and len(json.loads(b)["events"]) == 1)
    st, b = req(base, "GET", "/api/v1/admin/users", headers=H)
    check("사용자 목록 응답에 token_hash 가 없음", b"token_hash" not in b)
    # 로그인 정보에 주입
    st, b = req(base, "POST", "/api/v1/admin/login", {"username": "admin' --", "password": "x"})
    check("로그인 SQL 주입 → 401", st == 401)

    # --- 경로 탐색 / 이상한 요청
    check("정적 경로 탐색 차단", all(req(base, "GET", p)[0] == 404 for p in ("/../server.py", "/%2e%2e/server.py", "/..%2fserver.py", "/static/../server.py", "/data/bja_logs.db")))
    big = b'{"events":[' + b'{"id":"x","message":"' + b"a" * 1_100_000 + b'"}]}'
    try: big_ok = req(base, "POST", "/api/v1/ingest", headers=IH, raw_body=big)[0] == 400
    except (ConnectionError, urllib.error.URLError, OSError): big_ok = True      # 서버가 본문을 다 받기 전에 연결을 닫아 거절한 경우도 거부로 본다
    check("1MB 초과 본문 거부(400 또는 연결 종료)", big_ok and req(base, "GET", "/healthz")[0] == 200)
    check("깨진 JSON 은 400, 서버는 계속 동작", req(base, "POST", "/api/v1/ingest", headers=IH, raw_body=b"{not json")[0] == 400 and req(base, "GET", "/healthz")[0] == 200)
    check("이벤트 501건 초과 거부", req(base, "POST", "/api/v1/ingest", {"events": [{"id": "e%d" % i} for i in range(501)]}, IH)[0] == 400)
    check("토큰 없이/틀린 토큰으로 전송 거부", req(base, "POST", "/api/v1/ingest", {"events": []}, {"X-Client-Id": cid, "X-Client-Token": "bad"})[0] == 401)
    check("다른 클라이언트 ID 로 내 토큰 사용 불가", req(base, "POST", "/api/v1/ingest", {"events": []}, {"X-Client-Id": str(uuid.uuid4()), "X-Client-Token": tok})[0] == 401)
    st, b = req(base, "POST", "/api/v1/ingest", {"events": [{"id": "x1", "level": "INFO", "module": "M" * 500, "action": "a" * 500, "message": "m" * 100000, "exc_type": "e" * 500}]}, IH)
    st2, b2 = req(base, "GET", "/api/v1/admin/logs?q=mmmm&limit=1", headers=H)
    ev = json.loads(b2)["events"][0]
    check("필드 길이 상한(모듈 60/액션 80/메시지 4000자)으로 잘려 저장됨", len(ev["module"]) <= 60 and len(ev["action"]) <= 80 and len(ev["message"]) <= 4000)

    # --- 동시 접속: 20개 클라이언트가 동시에 100건씩
    errs = []
    def worker(i):
        try:
            c = str(uuid.uuid4()); s, b = req(base, "POST", "/api/v1/register", {"client_id": c, "info": {}}, {"X-Ingest-Key": "hk"}); t = json.loads(b)["token"]
            for k in range(2):
                evs = [{"id": "%s-%d-%d" % (c[:6], k, j), "level": "INFO", "module": "M", "action": "app.start" if j == 0 else "log", "message": "x"} for j in range(50)]
                s, b = req(base, "POST", "/api/v1/ingest", {"events": evs}, {"X-Client-Id": c, "X-Client-Token": t})
                if s != 200: errs.append(s)
        except Exception as ex: errs.append(repr(ex))
    t0 = time.time(); th = [threading.Thread(target=worker, args=(i,)) for i in range(60)]
    [t.start() for t in th]; [t.join() for t in th]; dt = time.time() - t0
    st, b = req(base, "GET", "/api/v1/admin/overview", headers=H); o = json.loads(b)
    print("    동시 전송 오류:", errs[:5]); check("동시 60개 클라이언트 × 100건 전송 오류 없음 (%.1f초)" % dt, not errs)
    check("동시 전송 후 설치 수 61, 실행 이벤트 합계 정확(120)", o["total_installs"] == 61 and o["today_runs"] == 120)
    st, b = req(base, "GET", "/api/v1/admin/logs?limit=50000&format=json", headers=H)
    check("저장된 이벤트 수 정확(1+1+2000=6002)", len(json.loads(b)["events"]) == 6002)
    t0 = time.time(); evs = [{"id": "p%d" % i, "level": "INFO", "module": "M", "action": "log", "message": "대량 전송 속도 시험"} for i in range(500)]
    s, b = req(base, "POST", "/api/v1/ingest", {"events": evs}, IH); dt = time.time() - t0
    check("500건 한 번에 저장이 1초 이내 (%.2f초)" % dt, s == 200 and dt < 1.0)
    # --- 세션: 로그아웃 후 토큰 무효
    req(base, "POST", "/api/v1/admin/logout", {}, H)
    check("로그아웃한 토큰은 즉시 무효(401)", req(base, "GET", "/api/v1/admin/overview", headers=H)[0] == 401)
finally:
    srv.terminate()

# --- TLS
if subprocess.call(["which", "openssl"], stdout=subprocess.DEVNULL) == 0:
    key, crt = os.path.join(TMP, "k.pem"), os.path.join(TMP, "c.pem")
    subprocess.check_call(["openssl", "req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", key, "-out", crt, "-days", "2", "-subj", "/CN=localhost", "-addext", "subjectAltName=DNS:localhost,IP:127.0.0.1"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    tport = free_port(); tsrv = start(tport, ["--cert", crt, "--key", key])
    try:
        ctx = ssl.create_default_context(cafile=crt)
        st, b = req("https://127.0.0.1:%d" % tport, "GET", "/healthz", ctx=ctx)
        check("HTTPS 직접 제공(--cert/--key), 인증서 검증 통과", st == 200)
        try: req("http://127.0.0.1:%d" % tport, "GET", "/healthz"); plain = True
        except Exception: plain = False
        check("같은 포트에 평문 HTTP 로는 접속 불가", not plain)
        old = ssl.SSLContext(ssl.PROTOCOL_TLS_CLIENT); old.check_hostname = False; old.verify_mode = ssl.CERT_NONE
        try: old.maximum_version = ssl.TLSVersion.TLSv1_1; s = socket.create_connection(("127.0.0.1", tport)); old.wrap_socket(s); lowtls = True
        except Exception: lowtls = False
        check("TLS 1.1 이하는 거부(최소 TLS 1.2)", not lowtls)
    finally:
        tsrv.terminate()
else:
    print("  (건너뜀) openssl 없음 → TLS 점검")
print("RESULT: %d/%d" % (sum(res), len(res))); sys.exit(0 if all(res) else 1)
