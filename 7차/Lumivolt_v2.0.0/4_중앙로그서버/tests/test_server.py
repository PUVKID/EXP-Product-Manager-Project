# -*- coding: utf-8 -*-
import json, os, sys, tempfile, threading, unittest, uuid, urllib.request, urllib.error
from http.server import ThreadingHTTPServer

TMP = tempfile.mkdtemp(prefix="bja_srv_")
os.environ["BJA_DATA_DIR"] = TMP
os.environ["BJA_QUIET"] = "1"
os.environ["BJA_INGEST_KEYS"] = "test-ingest-key"
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
import server as S


def call(base, method, path, body=None, headers=None, raw=False):
    req = urllib.request.Request(base + path, method=method, data=json.dumps(body).encode() if body is not None else None,
                                 headers=dict({"Content-Type": "application/json"}, **(headers or {})))
    try:
        with urllib.request.urlopen(req, timeout=10) as r:
            data = r.read()
            return r.status, (data if raw else json.loads(data.decode() or "null")), r.headers
    except urllib.error.HTTPError as e:
        data = e.read()
        try:
            return e.code, json.loads(data.decode()), e.headers
        except Exception:
            return e.code, data, e.headers


class ServerTest(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        S.init_db()
        S.INGEST_KEYS[:] = S.load_ingest_keys()
        salt, h, it = S.hash_password("correct horse battery")
        with S.db() as con:
            con.execute("INSERT INTO admins VALUES('admin',?,?,?, 'ADMIN')", (salt, h, it))
            s2, h2, i2 = S.hash_password("userpassword123")
            con.execute("INSERT INTO admins VALUES('plain',?,?,?, 'USER')", (s2, h2, i2))
        cls.srv = ThreadingHTTPServer(("127.0.0.1", 0), S.Handler)
        cls.srv.daemon_threads = True
        threading.Thread(target=cls.srv.serve_forever, daemon=True).start()
        cls.base = "http://127.0.0.1:%d" % cls.srv.server_address[1]
        cls.cid = str(uuid.uuid4())
        st, j, _ = call(cls.base, "POST", "/api/v1/register", {"client_id": cls.cid, "info": {}}, {"X-Ingest-Key": "test-ingest-key"})
        cls.token = j["token"]

    @classmethod
    def tearDownClass(cls):
        cls.srv.shutdown()

    def ingest(self, events, info=None, cid=None, token=None):
        return call(self.base, "POST", "/api/v1/ingest", {"info": info or {}, "events": events},
                    {"X-Client-Id": cid or self.cid, "X-Client-Token": token or self.token})

    def admin(self):
        st, j, _ = call(self.base, "POST", "/api/v1/admin/login", {"username": "admin", "password": "correct horse battery"})
        self.assertEqual(st, 200); self.assertEqual(j["role"], "ADMIN")
        return {"Authorization": "Bearer " + j["token"]}

    def test_01_register_requires_key(self):
        st, _, _ = call(self.base, "POST", "/api/v1/register", {"client_id": str(uuid.uuid4())}, {"X-Ingest-Key": "wrong"})
        self.assertEqual(st, 401)
        st, _, _ = call(self.base, "POST", "/api/v1/register", {"client_id": "not-a-uuid"}, {"X-Ingest-Key": "test-ingest-key"})
        self.assertEqual(st, 400)

    def test_02_ingest_auth_and_dedupe(self):
        ev = lambda i, **k: dict({"id": "e-%s" % i, "ts": "2026-10-07T01:02:03.000Z", "level": "INFO", "module": "App", "action": "log", "message": "m"}, **k)
        st, j, _ = self.ingest([ev(1)], token="bad")
        self.assertEqual(st, 401)
        info = {"app_version": "2.0.0.0", "os": "Windows 11 23H2", "cpu": "Test CPU", "ram_mb": 16000, "gpu": "GPU", "screen": "1920x1080", "dpi": 144, "pc_id": "AAAA-BBBB-CCCC", "license_name": "tester"}
        st, j, _ = self.ingest([ev(1), ev(2, action="app.start")], info)
        self.assertEqual((st, j["accepted"]), (200, 2))
        st, j, _ = self.ingest([ev(1), ev(3)])           # 재전송된 1번은 무시
        self.assertEqual(j["accepted"], 1)

    def test_03_admin_auth(self):
        st, _, _ = call(self.base, "GET", "/api/v1/admin/overview")
        self.assertEqual(st, 401)
        st, _, _ = call(self.base, "POST", "/api/v1/admin/login", {"username": "plain", "password": "userpassword123"})
        self.assertEqual(st, 401)                         # USER 역할은 관리자 API 불가
        st, _, _ = call(self.base, "GET", "/api/v1/admin/overview", headers={"Authorization": "Bearer nope"})
        self.assertEqual(st, 401)

    def test_04_overview_users_detail(self):
        self.ingest([{"id": "p1", "level": "INFO", "module": "ProductUpload", "action": "product.register.start", "message": "[등록] a"},
                     {"id": "p2", "level": "INFO", "module": "ProductUpload", "action": "product.register.success", "message": "ok", "result": "OK"},
                     {"id": "p3", "level": "ERROR", "module": "ProductUpload", "action": "product.register.fail", "message": "이미지 업로드 실패", "result": "FAIL", "exc_type": "WebDriverException"}],
                    {"app_version": "2.0.0.0", "os": "Windows 11 23H2"})
        H = self.admin()
        st, o, _ = call(self.base, "GET", "/api/v1/admin/overview?tz=0", headers=H)
        self.assertEqual(st, 200); self.assertGreaterEqual(o["total_installs"], 1); self.assertGreaterEqual(o["online_users"], 1)
        st, u, _ = call(self.base, "GET", "/api/v1/admin/users", headers=H)
        me = [x for x in u["users"] if x["client_id"] == self.cid][0]
        self.assertTrue(me["online"]); self.assertEqual(me["launches"], 1); self.assertEqual(me["success"], 1); self.assertEqual(me["fail"], 1); self.assertEqual(me["errors"], 1)
        self.assertNotIn("token_hash", me)
        st, u2, _ = call(self.base, "GET", "/api/v1/admin/users?q=windows+11&online=1&has_error=1&version=2.0.0.0", headers=H)
        self.assertEqual(len(u2["users"]), 1)
        st, u3, _ = call(self.base, "GET", "/api/v1/admin/users?version=9.9", headers=H)
        self.assertEqual(len(u3["users"]), 0)
        st, d, _ = call(self.base, "GET", "/api/v1/admin/users/" + self.cid, headers=H)
        self.assertEqual(d["client"]["cpu"], "Test CPU"); self.assertEqual(d["client"]["dpi"], 144)
        self.assertTrue(len(d["recent_logs"]) >= 3); self.assertEqual(len(d["recent_errors"]), 1)

    def test_05_logs_filter_errors_export(self):
        self.ingest([{"id": "x1", "level": "WARN", "module": "M", "action": "log", "message": "=cmd|' /C calc'!A0"},
                     {"id": "x2", "level": "INFO", "module": "M", "action": "log", "message": "<script>alert(1)</script>"}])
        H = self.admin()
        st, r, _ = call(self.base, "GET", "/api/v1/admin/logs?level=WARN&q=cmd", headers=H)
        self.assertEqual(len(r["events"]), 1)
        st, r, _ = call(self.base, "GET", "/api/v1/admin/errors", headers=H)
        self.assertTrue(all(e["level"] in ("ERROR", "FATAL") for e in r["events"]))
        st, data, hd = call(self.base, "GET", "/api/v1/admin/logs?format=csv&level=WARN", headers=H, raw=True)
        text = data.decode("utf-8-sig")
        self.assertIn("'=cmd", text)                      # CSV 수식 주입 방어
        self.assertTrue(hd["Content-Disposition"].startswith("attachment"))
        st, data, _ = call(self.base, "GET", "/api/v1/admin/logs?format=json&download=1&client_id=" + self.cid, headers=H, raw=True)
        self.assertTrue(isinstance(json.loads(data.decode()), list))
        # since_id 증분 조회
        st, r, _ = call(self.base, "GET", "/api/v1/admin/logs?limit=1", headers=H)
        top = r["events"][0]["id"]
        self.ingest([{"id": "x3", "level": "INFO", "module": "M", "action": "log", "message": "new"}])
        st, r, _ = call(self.base, "GET", "/api/v1/admin/logs?since_id=%d" % top, headers=H)
        self.assertEqual([e["message"] for e in r["events"]], ["new"])

    def test_06_stats(self):
        H = self.admin()
        st, s, _ = call(self.base, "GET", "/api/v1/admin/stats?days=7&tz=540", headers=H)
        self.assertEqual(len(s["series"]), 7)
        self.assertTrue(any(v["name"] == "2.0.0.0" for v in s["versions"]))
        self.assertTrue(any(f["name"] == "product.register.success" for f in s["features"]))

    def test_07_static_and_headers(self):
        st, data, hd = call(self.base, "GET", "/", raw=True)
        self.assertEqual(st, 200); self.assertIn("Content-Security-Policy", hd)
        st, _, _ = call(self.base, "GET", "/../server.py", raw=True)
        self.assertEqual(st, 404)

    def test_99_login_rate_limit(self):
        for _ in range(5):
            call(self.base, "POST", "/api/v1/admin/login", {"username": "admin", "password": "wrong"})
        st, _, _ = call(self.base, "POST", "/api/v1/admin/login", {"username": "admin", "password": "correct horse battery"})
        self.assertEqual(st, 429)


if __name__ == "__main__":
    unittest.main(verbosity=2)
