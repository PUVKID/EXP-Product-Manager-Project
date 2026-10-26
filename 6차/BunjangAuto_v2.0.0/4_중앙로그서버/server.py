#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
BunjangAuto 중앙 로그 서버  (Copyright (c) 음악천재)

- 외부 패키지 없이 Python 3.8+ 표준 라이브러리만 사용한다. (http.server + sqlite3)
- 클라이언트(BunjangAuto.exe)  → POST /api/v1/register, /api/v1/ingest
- 관리자(웹 대시보드 / EXE 관리자 센터) → /api/v1/admin/*  (ADMIN 역할만 허용)
- HTTPS: --cert/--key 를 주면 이 프로세스가 직접 TLS 를 처리하고,
         nginx/Caddy 같은 리버스 프록시 뒤에 둘 때는 생략한다. (운영 환경에서는 반드시 HTTPS 사용)

사용법
    python server.py init-admin --username admin          # 관리자 계정 생성(비밀번호 입력 프롬프트)
    python server.py serve --port 8443 --cert c.pem --key k.pem
    python server.py serve --port 8080                    # 개발/프록시 뒤
"""
import argparse
import csv
import getpass
import hashlib
import hmac
import io
import json
import mimetypes
import os
import re
import secrets
import sqlite3
import ssl
import sys
import threading
import time
from datetime import datetime, timedelta, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

HERE = os.path.dirname(os.path.abspath(__file__))
STATIC_DIR = os.path.join(HERE, "static")

DATA_DIR = os.environ.get("BJA_DATA_DIR", os.path.join(HERE, "data"))
DB_PATH = os.environ.get("BJA_DB", os.path.join(DATA_DIR, "bja_logs.db"))

ONLINE_SECONDS = 180                 # 마지막 접속이 이 시간 이내면 '온라인'
MAX_BODY = 1_000_000                 # 요청 본문 최대 1MB
MAX_EVENTS_PER_BATCH = 500
MAX_MESSAGE = 4000
SESSION_HOURS = 8
PBKDF2_ITER = 200_000

LEVELS = ("DEBUG", "INFO", "WARN", "ERROR", "FATAL")
UUID_RE = re.compile(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")

_rate_lock = threading.Lock()
_client_hits = {}                    # client_id -> [timestamps]


# --------------------------------------------------------------------------- 유틸
def utcnow():
    return datetime.now(timezone.utc)


def iso(dt):
    return dt.strftime("%Y-%m-%dT%H:%M:%S.") + "%03dZ" % (dt.microsecond // 1000)


def now_iso():
    return iso(utcnow())


def sha256(s):
    return hashlib.sha256(s.encode("utf-8")).hexdigest()


def load_ingest_keys():
    """클라이언트 등록용 키. 환경변수 BJA_INGEST_KEYS(쉼표 구분) 또는 data/ingest_keys.txt.
    이 키는 EXE 안에 들어가므로 '비밀'이 아니라 스팸 방지용 문턱일 뿐이다.
    (관리자 자격은 이 키와 완전히 별개이며 서버에서만 검증한다.)"""
    keys = [k.strip() for k in os.environ.get("BJA_INGEST_KEYS", "").split(",") if k.strip()]
    p = os.path.join(DATA_DIR, "ingest_keys.txt")
    if not keys and os.path.exists(p):
        keys = [l.strip() for l in open(p, encoding="utf-8") if l.strip() and not l.startswith("#")]
    if not keys:
        os.makedirs(DATA_DIR, exist_ok=True)
        k = secrets.token_urlsafe(24)
        with open(p, "w", encoding="utf-8") as f:
            f.write("# 클라이언트 등록 키(EXE 빌드 시 BJA_INGEST_KEY 로 넣는다). 한 줄에 하나.\n" + k + "\n")
        keys = [k]
        print("[서버] 새 ingest 키를 만들었습니다: %s  (%s)" % (k, p), file=sys.stderr)
    return keys


def db():
    os.makedirs(os.path.dirname(DB_PATH), exist_ok=True)
    con = sqlite3.connect(DB_PATH, timeout=15)
    con.row_factory = sqlite3.Row
    con.execute("PRAGMA foreign_keys=ON")
    return con


def init_db():
    with db() as con:
        con.executescript(open(os.path.join(HERE, "schema.sql"), encoding="utf-8").read())


def hash_password(password, salt=None, iterations=PBKDF2_ITER):
    salt = salt or secrets.token_hex(16)
    h = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), bytes.fromhex(salt), iterations).hex()
    return salt, h, iterations


def clip(v, n):
    return ("" if v is None else str(v))[:n]


# --------------------------------------------------------------------------- 쿼리 도우미
def event_filters(q):
    """쿼리스트링 → (WHERE 절, 파라미터). 관리자 로그 화면/내보내기가 같은 필터를 쓴다."""
    where, args = [], []
    cid = q.get("client_id", [""])[0]
    if cid:
        where.append("e.client_id = ?"); args.append(cid)
    lv = q.get("level", [""])[0]
    if lv:
        lvs = [x for x in lv.upper().split(",") if x in LEVELS]
        if lvs:
            where.append("e.level IN (%s)" % ",".join("?" * len(lvs))); args += lvs
    mod = q.get("module", [""])[0]
    if mod:
        where.append("e.module = ?"); args.append(mod)
    act = q.get("action", [""])[0]
    if act:
        where.append("e.action LIKE ?"); args.append(act.replace("%", "") + "%")
    res = q.get("result", [""])[0]
    if res:
        where.append("e.result = ?"); args.append(res)
    text = q.get("q", [""])[0]
    if text:
        like = "%" + text.replace("%", "").replace("_", "") + "%"
        where.append("(e.message LIKE ? OR e.module LIKE ? OR e.action LIKE ? OR e.exc_type LIKE ? OR e.client_id LIKE ?)")
        args += [like] * 5
    frm = q.get("from", [""])[0]
    if frm:
        where.append("e.ts >= ?"); args.append(frm)
    to = q.get("to", [""])[0]
    if to:
        where.append("e.ts <= ?"); args.append(to)
    since = q.get("since_id", [""])[0]
    if since.isdigit():
        where.append("e.id > ?"); args.append(int(since))
    return (" WHERE " + " AND ".join(where)) if where else "", args


EVENT_COLS = ("id", "ts", "client_id", "version", "level", "module", "action", "message", "result", "exc_type")


def query_events(con, q, default_limit=200, max_limit=50000, order="DESC"):
    where, args = event_filters(q)
    try:
        limit = int(q.get("limit", [str(default_limit)])[0])
    except ValueError:
        limit = default_limit
    limit = max(1, min(limit, max_limit))
    sql = "SELECT %s FROM events e%s ORDER BY e.id %s LIMIT ?" % (",".join("e." + c for c in EVENT_COLS), where, order)
    return [dict(r) for r in con.execute(sql, args + [limit]).fetchall()]


def client_counters(con, ids=None):
    """클라이언트별 실행/자동등록/성공/실패/오류 횟수."""
    sql = """SELECT client_id,
        SUM(action='app.start') AS launches,
        SUM(action='product.register.start') AS auto_runs,
        SUM(action='product.register.success') AS success,
        SUM(action='product.register.fail') AS fail,
        SUM(level IN ('ERROR','FATAL')) AS errors
        FROM events GROUP BY client_id"""
    out = {}
    for r in con.execute(sql):
        out[r["client_id"]] = {k: (r[k] or 0) for k in ("launches", "auto_runs", "success", "fail", "errors")}
    return out


def is_online(last_seen):
    try:
        dt = datetime.strptime(last_seen, "%Y-%m-%dT%H:%M:%S.%fZ").replace(tzinfo=timezone.utc)
    except Exception:
        return False
    return (utcnow() - dt).total_seconds() <= ONLINE_SECONDS


def tz_expr(tz):
    # SQLite datetime() 수정자: '+540 minutes'
    return "%+d minutes" % tz


def parse_tz(q):
    try:
        return max(-720, min(840, int(q.get("tz", ["0"])[0])))
    except ValueError:
        return 0


# --------------------------------------------------------------------------- HTTP
class Handler(BaseHTTPRequestHandler):
    server_version = "BJALog/2.0"
    protocol_version = "HTTP/1.1"

    # ---- 공통
    def log_message(self, fmt, *args):
        if os.environ.get("BJA_QUIET") != "1":
            sys.stderr.write("[%s] %s %s\n" % (self.log_date_time_string(), self.client_address[0], fmt % args))

    def ip(self):
        if os.environ.get("BJA_TRUST_PROXY") == "1":
            xf = self.headers.get("X-Forwarded-For", "")
            if xf:
                return xf.split(",")[0].strip()
        return self.client_address[0]

    def send_json(self, obj, code=200):
        body = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        self.security_headers()
        self.end_headers()
        self.wfile.write(body)

    def security_headers(self):
        self.send_header("X-Content-Type-Options", "nosniff")
        self.send_header("X-Frame-Options", "DENY")
        self.send_header("Referrer-Policy", "no-referrer")
        self.send_header("Content-Security-Policy",
                         "default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; frame-ancestors 'none'")

    def err(self, code, msg):
        self.send_json({"error": msg}, code)

    def read_json(self):
        try:
            n = int(self.headers.get("Content-Length", "0"))
        except ValueError:
            n = 0
        if n <= 0 or n > MAX_BODY:
            raise ValueError("본문 크기가 올바르지 않습니다.")
        return json.loads(self.rfile.read(n).decode("utf-8"))

    # ---- 라우팅
    def do_GET(self):
        u = urlparse(self.path)
        path, q = u.path, parse_qs(u.query)
        try:
            if path == "/healthz":
                return self.send_json({"ok": True, "time": now_iso()})
            if path.startswith("/api/v1/admin/"):
                return self.admin_get(path[len("/api/v1/admin/"):], q)
            if path.startswith("/api/"):
                return self.err(404, "not found")
            return self.static(path)
        except Exception as ex:  # 서버 오류가 연결을 끊지 않도록
            self.log_message("GET 오류: %r", ex)
            try:
                self.err(500, "서버 오류")
            except Exception:
                pass

    def do_POST(self):
        u = urlparse(self.path)
        path = u.path
        try:
            if path == "/api/v1/register":
                return self.register()
            if path == "/api/v1/ingest":
                return self.ingest()
            if path == "/api/v1/admin/login":
                return self.admin_login()
            if path == "/api/v1/admin/logout":
                return self.admin_logout()
            return self.err(404, "not found")
        except ValueError as ex:
            return self.err(400, str(ex))
        except Exception as ex:
            self.log_message("POST 오류: %r", ex)
            try:
                self.err(500, "서버 오류")
            except Exception:
                pass

    # ---- 정적 파일(대시보드)
    def static(self, path):
        if path in ("/", ""):
            path = "/index.html"
        rel = os.path.normpath(path.lstrip("/"))
        full = os.path.join(STATIC_DIR, rel)
        if not os.path.abspath(full).startswith(os.path.abspath(STATIC_DIR) + os.sep) or not os.path.isfile(full):
            return self.err(404, "not found")
        ctype = mimetypes.guess_type(full)[0] or "application/octet-stream"
        data = open(full, "rb").read()
        self.send_response(200)
        self.send_header("Content-Type", ctype + ("; charset=utf-8" if ctype.startswith("text/") or ctype.endswith("javascript") else ""))
        self.send_header("Content-Length", str(len(data)))
        self.security_headers()
        self.end_headers()
        self.wfile.write(data)

    # ================================================================= 클라이언트 API
    def register(self):
        key = self.headers.get("X-Ingest-Key", "")
        if not any(hmac.compare_digest(key, k) for k in INGEST_KEYS):
            return self.err(401, "invalid ingest key")
        body = self.read_json()
        cid = str(body.get("client_id", ""))
        if not UUID_RE.match(cid):
            return self.err(400, "client_id 형식 오류")
        token = secrets.token_urlsafe(32)
        info = body.get("info") or {}
        now = now_iso()
        with db() as con:
            row = con.execute("SELECT 1 FROM clients WHERE client_id=?", (cid,)).fetchone()
            if row:
                con.execute("UPDATE clients SET token_hash=?, last_seen=? WHERE client_id=?", (sha256(token), now, cid))
            else:
                con.execute("INSERT INTO clients(client_id, token_hash, first_seen, last_seen) VALUES(?,?,?,?)",
                            (cid, sha256(token), now, now))
            self.update_client(con, cid, info)
        return self.send_json({"token": token})

    def update_client(self, con, cid, info):
        if not isinstance(info, dict):
            return
        fields = {
            "app_version": clip(info.get("app_version"), 40), "os": clip(info.get("os"), 200),
            "cpu": clip(info.get("cpu"), 200), "gpu": clip(info.get("gpu"), 200),
            "screen": clip(info.get("screen"), 40), "pc_id": clip(info.get("pc_id"), 20),
            "license_name": clip(info.get("license_name"), 100),
            "license_expires": clip(info.get("license_expires"), 40), "ip": self.ip(),
        }
        for k in ("ram_mb", "dpi"):
            try:
                fields[k] = int(info.get(k) or 0)
            except (TypeError, ValueError):
                fields[k] = 0
        # 일부 값만 담긴 요청이 이미 저장된 정보를 빈 값으로 덮어쓰지 않도록, 값이 있는 항목만 갱신한다
        fields = {k: v for k, v in fields.items() if k == "ip" or v not in ("", 0)}
        sets = ", ".join("%s=?" % k for k in fields)
        con.execute("UPDATE clients SET %s WHERE client_id=?" % sets, list(fields.values()) + [cid])

    def rate_ok(self, cid, limit=120, window=60):
        now = time.time()
        with _rate_lock:
            hits = [t for t in _client_hits.get(cid, []) if now - t < window]
            if len(hits) >= limit:
                _client_hits[cid] = hits
                return False
            hits.append(now)
            _client_hits[cid] = hits
        return True

    def ingest(self):
        cid = self.headers.get("X-Client-Id", "")
        token = self.headers.get("X-Client-Token", "")
        if not UUID_RE.match(cid) or not token:
            return self.err(401, "unauthorized")
        if not self.rate_ok(cid):
            return self.err(429, "too many requests")
        body = self.read_json()
        events = body.get("events") or []
        if not isinstance(events, list) or len(events) > MAX_EVENTS_PER_BATCH:
            return self.err(400, "events 개수가 올바르지 않습니다.")
        now = now_iso()
        accepted = 0
        with db() as con:
            row = con.execute("SELECT token_hash FROM clients WHERE client_id=?", (cid,)).fetchone()
            if not row or not hmac.compare_digest(row["token_hash"], sha256(token)):
                return self.err(401, "unauthorized")
            con.execute("UPDATE clients SET last_seen=? WHERE client_id=?", (now, cid))
            self.update_client(con, cid, body.get("info"))
            for ev in events:
                if not isinstance(ev, dict):
                    continue
                uid = clip(ev.get("id"), 64)
                if not uid:
                    continue
                level = clip(ev.get("level"), 8).upper()
                if level not in LEVELS:
                    level = "INFO"
                ts = clip(ev.get("ts"), 40) or now
                if not re.match(r"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}", ts):
                    ts = now
                try:
                    cur = con.execute(
                        """INSERT OR IGNORE INTO events(uid, client_id, ts, received, level, module, action,
                           message, result, exc_type, version, session) VALUES(?,?,?,?,?,?,?,?,?,?,?,?)""",
                        (uid, cid, ts, now, level, clip(ev.get("module"), 60) or "App", clip(ev.get("action"), 80) or "log",
                         clip(ev.get("message"), MAX_MESSAGE), clip(ev.get("result"), 8), clip(ev.get("exc_type"), 120),
                         clip(ev.get("version"), 40), clip(ev.get("session"), 40)))
                    accepted += cur.rowcount
                    if ev.get("action") == "app.start":
                        con.execute("UPDATE clients SET last_start=? WHERE client_id=?", (ts, cid))
                except sqlite3.Error:
                    continue
        return self.send_json({"accepted": accepted})

    # ================================================================= 관리자 인증
    def admin_login(self):
        ip = self.ip()
        now = time.time()
        with db() as con:
            con.execute("DELETE FROM login_attempts WHERE ts < ?", (now - 600,))
            n = con.execute("SELECT COUNT(*) FROM login_attempts WHERE ip=?", (ip,)).fetchone()[0]
            if n >= 5:
                return self.err(429, "로그인 시도가 너무 많습니다. 10분 뒤 다시 시도하세요.")
            body = self.read_json()
            user, pw = clip(body.get("username"), 64), str(body.get("password") or "")
            row = con.execute("SELECT * FROM admins WHERE username=?", (user,)).fetchone()
            # 존재하지 않는 계정이어도 같은 시간이 걸리도록 항상 해시를 계산한다
            salt = row["salt"] if row else "00" * 16
            it = row["iterations"] if row else PBKDF2_ITER
            _, h, _ = hash_password(pw, salt, it)
            if not row or not hmac.compare_digest(h, row["pw_hash"]) or row["role"] != "ADMIN":
                con.execute("INSERT INTO login_attempts(ip, ts) VALUES(?,?)", (ip, now))
                return self.err(401, "아이디 또는 비밀번호가 올바르지 않습니다.")
            token = secrets.token_urlsafe(32)
            exp = iso(utcnow() + timedelta(hours=SESSION_HOURS))
            con.execute("DELETE FROM sessions WHERE expires < ?", (now_iso(),))
            con.execute("INSERT INTO sessions(token_hash, username, role, expires) VALUES(?,?,?,?)",
                        (sha256(token), user, row["role"], exp))
        return self.send_json({"token": token, "role": row["role"], "expires": exp})

    def admin_logout(self):
        tok = self.bearer()
        if tok:
            with db() as con:
                con.execute("DELETE FROM sessions WHERE token_hash=?", (sha256(tok),))
        return self.send_json({"ok": True})

    def bearer(self):
        a = self.headers.get("Authorization", "")
        return a[7:].strip() if a.lower().startswith("bearer ") else ""

    def require_admin(self):
        tok = self.bearer()
        if not tok:
            self.err(401, "로그인이 필요합니다.")
            return False
        with db() as con:
            row = con.execute("SELECT role, expires FROM sessions WHERE token_hash=?", (sha256(tok),)).fetchone()
        if not row or row["expires"] < now_iso():
            self.err(401, "세션이 만료되었습니다.")
            return False
        if row["role"] != "ADMIN":
            self.err(403, "관리자 권한이 필요합니다.")
            return False
        return True

    # ================================================================= 관리자 API
    def admin_get(self, route, q):
        if not self.require_admin():
            return
        with db() as con:
            if route == "me":
                return self.send_json({"role": "ADMIN"})
            if route == "overview":
                return self.send_json(self.overview(con, parse_tz(q)))
            if route == "users":
                return self.send_json({"users": self.users(con, q)})
            if route.startswith("users/"):
                return self.user_detail(con, route[6:], q)
            if route == "logs":
                return self.logs(con, q)
            if route == "errors":
                q = dict(q); q["level"] = ["ERROR,FATAL"]
                return self.send_json({"events": query_events(con, q, 200)})
            if route == "stats":
                return self.send_json(self.stats(con, q))
        return self.err(404, "not found")

    def overview(self, con, tz):
        now = utcnow()
        online_cut = iso(now - timedelta(seconds=ONLINE_SECONDS))
        week_cut = iso(now - timedelta(days=7))
        today = con.execute("SELECT date('now', ?)", (tz_expr(tz),)).fetchone()[0]
        day = "date(datetime(ts, '%s')) = ?" % tz_expr(tz)
        r = con.execute("""SELECT
            SUM(action='app.start') AS runs, SUM(action='product.register.start') AS reg,
            SUM(action='product.register.success') AS ok, SUM(action='product.register.fail') AS fail,
            SUM(level IN ('ERROR','FATAL')) AS err FROM events WHERE %s""" % day, (today,)).fetchone()
        return {
            "total_installs": con.execute("SELECT COUNT(*) FROM clients").fetchone()[0],
            "active_users": con.execute("SELECT COUNT(*) FROM clients WHERE last_seen >= ?", (week_cut,)).fetchone()[0],
            "online_users": con.execute("SELECT COUNT(*) FROM clients WHERE last_seen >= ?", (online_cut,)).fetchone()[0],
            "today_runs": r["runs"] or 0, "today_registrations": r["reg"] or 0,
            "today_success": r["ok"] or 0, "today_fail": r["fail"] or 0, "today_errors": r["err"] or 0,
            "server_time": iso(now),
        }

    def users(self, con, q):
        rows = [dict(r) for r in con.execute("SELECT * FROM clients ORDER BY last_seen DESC")]
        cnt = client_counters(con)
        text = q.get("q", [""])[0].lower()
        ver = q.get("version", [""])[0]
        want_online = q.get("online", [""])[0] == "1"
        want_err = q.get("has_error", [""])[0] == "1"
        try:
            recent_h = float(q.get("recent_hours", ["0"])[0])
        except ValueError:
            recent_h = 0
        recent_cut = iso(utcnow() - timedelta(hours=recent_h)) if recent_h > 0 else ""
        out = []
        for r in rows:
            c = cnt.get(r["client_id"], dict(launches=0, auto_runs=0, success=0, fail=0, errors=0))
            r.update(c)
            r["online"] = is_online(r["last_seen"])
            r.pop("token_hash", None)
            if text and text not in (r["client_id"] + " " + (r["app_version"] or "") + " " + (r["os"] or "") + " " +
                                     (r["license_name"] or "") + " " + (r["pc_id"] or "")).lower():
                continue
            if ver and r["app_version"] != ver:
                continue
            if want_online and not r["online"]:
                continue
            if want_err and not c["errors"]:
                continue
            if recent_cut and r["last_seen"] < recent_cut:
                continue
            out.append(r)
        return out

    def user_detail(self, con, cid, q):
        row = con.execute("SELECT * FROM clients WHERE client_id=?", (cid,)).fetchone()
        if not row:
            return self.err(404, "client not found")
        d = dict(row); d.pop("token_hash", None)
        d.update(client_counters(con).get(cid, dict(launches=0, auto_runs=0, success=0, fail=0, errors=0)))
        d["online"] = is_online(d["last_seen"])
        q1 = {"client_id": [cid]}
        recent = query_events(con, q1, 100)
        errs = query_events(con, {"client_id": [cid], "level": ["ERROR,FATAL"]}, 50)
        tz = parse_tz(q)
        daily = [dict(r) for r in con.execute(
            """SELECT date(datetime(ts, ?)) AS day, SUM(action='app.start') AS runs,
               SUM(action='product.register.success') AS success, SUM(action='product.register.fail') AS fail,
               SUM(level IN ('ERROR','FATAL')) AS errors FROM events WHERE client_id=?
               AND ts >= datetime('now','-14 days') GROUP BY day ORDER BY day""", (tz_expr(tz), cid))]
        return self.send_json({"client": d, "recent_logs": recent, "recent_errors": errs, "daily": daily})

    def logs(self, con, q):
        fmt = q.get("format", ["json"])[0]
        if fmt in ("csv", "json"):
            events = query_events(con, q, default_limit=200, max_limit=50000)
            if q.get("download", ["0"])[0] == "1" or fmt == "csv":
                stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
                if fmt == "csv":
                    buf = io.StringIO()
                    w = csv.writer(buf)
                    w.writerow(EVENT_COLS)
                    for e in events:
                        w.writerow([self.csv_safe(e[c]) for c in EVENT_COLS])
                    data = ("\ufeff" + buf.getvalue()).encode("utf-8")
                    ctype, name = "text/csv; charset=utf-8", "bja-logs-%s.csv" % stamp
                else:
                    data = json.dumps(events, ensure_ascii=False, indent=1).encode("utf-8")
                    ctype, name = "application/json; charset=utf-8", "bja-logs-%s.json" % stamp
                self.send_response(200)
                self.send_header("Content-Type", ctype)
                self.send_header("Content-Disposition", 'attachment; filename="%s"' % name)
                self.send_header("Content-Length", str(len(data)))
                self.security_headers()
                self.end_headers()
                self.wfile.write(data)
                return
            return self.send_json({"events": events})
        return self.err(400, "format 은 json 또는 csv")

    @staticmethod
    def csv_safe(v):
        # 엑셀 수식 주입 방지: = + - @ 로 시작하는 문자열 앞에 ' 를 붙인다
        s = "" if v is None else str(v)
        return "'" + s if s[:1] in ("=", "+", "-", "@") else s

    def stats(self, con, q):
        tz = parse_tz(q)
        try:
            days = max(1, min(90, int(q.get("days", ["7"])[0])))
        except ValueError:
            days = 7
        modifier = tz_expr(tz)
        start = (utcnow() + timedelta(minutes=tz) - timedelta(days=days - 1)).strftime("%Y-%m-%d")
        rows = {r["day"]: r for r in con.execute(
            """SELECT date(datetime(ts, ?)) AS day, SUM(action='app.start') AS runs,
               SUM(action='product.register.start') AS reg, SUM(action='product.register.success') AS success,
               SUM(action='product.register.fail') AS fail, SUM(level IN ('ERROR','FATAL')) AS errors
               FROM events WHERE date(datetime(ts, ?)) >= ? GROUP BY day""", (modifier, modifier, start))}
        series = []
        d0 = datetime.strptime(start, "%Y-%m-%d")
        for i in range(days):
            day = (d0 + timedelta(days=i)).strftime("%Y-%m-%d")
            r = rows.get(day)
            series.append({"day": day, **{k: ((r[k] or 0) if r else 0) for k in ("runs", "reg", "success", "fail", "errors")}})
        versions = [dict(r) for r in con.execute(
            "SELECT COALESCE(NULLIF(app_version,''),'(알 수 없음)') AS name, COUNT(*) AS n FROM clients GROUP BY name ORDER BY n DESC")]
        oss = [dict(r) for r in con.execute(
            "SELECT COALESCE(NULLIF(os,''),'(알 수 없음)') AS name, COUNT(*) AS n FROM clients GROUP BY name ORDER BY n DESC")]
        feats = [dict(r) for r in con.execute(
            """SELECT action AS name, COUNT(*) AS n FROM events
               WHERE action NOT IN ('log','heartbeat') AND date(datetime(ts, ?)) >= ?
               GROUP BY action ORDER BY n DESC LIMIT 20""", (modifier, start))]
        return {"days": days, "series": series, "versions": versions, "os": oss, "features": feats}


INGEST_KEYS = []


# --------------------------------------------------------------------------- CLI
def cmd_init_admin(args):
    init_db()
    pw = os.environ.get("BJA_ADMIN_PASSWORD") or getpass.getpass("관리자 비밀번호(12자 이상): ")
    if len(pw) < 12:
        print("비밀번호는 12자 이상이어야 합니다.", file=sys.stderr)
        return 2
    salt, h, it = hash_password(pw)
    with db() as con:
        con.execute("INSERT OR REPLACE INTO admins(username, salt, pw_hash, iterations, role) VALUES(?,?,?,?,'ADMIN')",
                    (args.username, salt, h, it))
    print("관리자 계정 '%s' 을(를) 저장했습니다." % args.username)
    return 0


def cmd_serve(args):
    global INGEST_KEYS
    init_db()
    INGEST_KEYS = load_ingest_keys()
    srv = ThreadingHTTPServer((args.host, args.port), Handler)
    srv.daemon_threads = True
    scheme = "http"
    if args.cert and args.key:
        ctx = ssl.SSLContext(ssl.PROTOCOL_TLS_SERVER)
        ctx.minimum_version = ssl.TLSVersion.TLSv1_2
        ctx.load_cert_chain(args.cert, args.key)
        srv.socket = ctx.wrap_socket(srv.socket, server_side=True)
        scheme = "https"
    print("[서버] %s://%s:%d 에서 대기 중 (DB: %s)" % (scheme, args.host, args.port, DB_PATH), file=sys.stderr)
    try:
        srv.serve_forever()
    except KeyboardInterrupt:
        pass
    return 0


def main():
    ap = argparse.ArgumentParser(description="BunjangAuto 중앙 로그 서버")
    sub = ap.add_subparsers(dest="cmd", required=True)
    a = sub.add_parser("init-admin", help="관리자 계정 생성/비밀번호 변경")
    a.add_argument("--username", default="admin")
    s = sub.add_parser("serve", help="서버 시작")
    s.add_argument("--host", default="0.0.0.0")
    s.add_argument("--port", type=int, default=8443)
    s.add_argument("--cert")
    s.add_argument("--key")
    args = ap.parse_args()
    sys.exit(cmd_init_admin(args) if args.cmd == "init-admin" else cmd_serve(args))


if __name__ == "__main__":
    main()
