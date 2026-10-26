# -*- coding: utf-8 -*-
"""데모/검증용 샘플 데이터를 서버에 넣는다.  사용: python tests/seed_demo.py http://127.0.0.1:8080 <ingest-key>"""
import datetime, json, sys, urllib.request, uuid

base, key = sys.argv[1].rstrip("/"), sys.argv[2]

def call(path, body, headers):
    r = urllib.request.Request(base + path, data=json.dumps(body).encode(), method="POST",
                               headers=dict({"Content-Type": "application/json"}, **headers))
    return json.load(urllib.request.urlopen(r))

now = datetime.datetime.now(datetime.timezone.utc)
for i, (ver, os_) in enumerate([("2.0.0.0", "Windows 11 Pro 23H2"), ("2.0.0.0", "Windows 10 Pro 22H2"), ("1.9.0.0", "Windows 11 Home 24H2")]):
    cid = str(uuid.uuid4())
    tok = call("/api/v1/register", {"client_id": cid, "info": {}}, {"X-Ingest-Key": key})["token"]
    evs = []
    for d in range(6, -1, -1):
        ts = (now - datetime.timedelta(days=d)).strftime("%Y-%m-%dT%H:%M:%S.000Z")
        evs.append({"id": "%s-s%d" % (cid[:6], d), "ts": ts, "level": "INFO", "module": "App", "action": "app.start", "message": "start"})
        for k in range(3 + i):
            evs.append({"id": "%s-r%d-%d" % (cid[:6], d, k), "ts": ts, "level": "INFO", "module": "ProductUpload", "action": "product.register.start", "message": "[등록] 상품"})
            ok = (k + d) % 4 != 0
            evs.append({"id": "%s-o%d-%d" % (cid[:6], d, k), "ts": ts, "level": "INFO" if ok else "ERROR", "module": "ProductUpload",
                        "action": "product.register.success" if ok else "product.register.fail",
                        "message": "→ 등록 완료" if ok else "→ 실패: 이미지 업로드 실패", "result": "OK" if ok else "FAIL",
                        "exc_type": "" if ok else "System.IO.IOException"})
    call("/api/v1/ingest", {"info": {"app_version": ver, "os": os_, "cpu": "Ryzen 5 5600", "ram_mb": 16384, "gpu": "RTX 3060", "screen": "1920x1080",
                                      "dpi": 120, "license_name": "demo%d" % i}, "events": evs},
         {"X-Client-Id": cid, "X-Client-Token": tok})
print("seeded 3 demo clients")
