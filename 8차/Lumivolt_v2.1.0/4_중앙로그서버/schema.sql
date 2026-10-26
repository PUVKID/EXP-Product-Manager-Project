-- BunjangAuto 중앙 로그 서버 DB 스키마 (SQLite, WAL)
PRAGMA journal_mode=WAL;

CREATE TABLE IF NOT EXISTS clients (
    client_id     TEXT PRIMARY KEY,          -- 설치별 랜덤 UUID
    token_hash    TEXT NOT NULL,             -- 클라이언트 토큰의 SHA-256 (토큰 원문은 저장하지 않음)
    first_seen    TEXT NOT NULL,             -- UTC ISO-8601
    last_seen     TEXT NOT NULL,             -- 마지막 접속(서버가 배치를 받은 시각)
    last_start    TEXT,                      -- 마지막 프로그램 실행 시각
    app_version   TEXT,
    os            TEXT,
    cpu           TEXT,
    ram_mb        INTEGER,
    gpu           TEXT,
    screen        TEXT,                      -- 예: 1920x1080
    dpi           INTEGER,
    pc_id         TEXT,                      -- 라이선스용 PC 고유번호(XXXX-XXXX-XXXX)
    license_name  TEXT,                      -- 라이선스 발급 이름
    license_expires TEXT,
    ip            TEXT
);

CREATE TABLE IF NOT EXISTS events (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    uid        TEXT NOT NULL UNIQUE,         -- 클라이언트가 만든 이벤트 ID (재전송 시 중복 방지)
    client_id  TEXT NOT NULL,
    ts         TEXT NOT NULL,                -- 이벤트 발생 시각(UTC ISO-8601)
    received   TEXT NOT NULL,                -- 서버 수신 시각
    level      TEXT NOT NULL,                -- DEBUG/INFO/WARN/ERROR/FATAL
    module     TEXT NOT NULL,
    action     TEXT NOT NULL,
    message    TEXT NOT NULL,
    result     TEXT NOT NULL DEFAULT '',     -- OK / FAIL / ''
    exc_type   TEXT NOT NULL DEFAULT '',
    version    TEXT NOT NULL DEFAULT '',
    session    TEXT NOT NULL DEFAULT '',
    FOREIGN KEY (client_id) REFERENCES clients(client_id)
);
CREATE INDEX IF NOT EXISTS ix_events_ts      ON events(ts);
CREATE INDEX IF NOT EXISTS ix_events_client  ON events(client_id, ts);
CREATE INDEX IF NOT EXISTS ix_events_level   ON events(level, ts);
CREATE INDEX IF NOT EXISTS ix_events_action  ON events(action, ts);

CREATE TABLE IF NOT EXISTS admins (
    username   TEXT PRIMARY KEY,
    salt       TEXT NOT NULL,
    pw_hash    TEXT NOT NULL,                -- PBKDF2-HMAC-SHA256
    iterations INTEGER NOT NULL,
    role       TEXT NOT NULL DEFAULT 'ADMIN' CHECK (role IN ('USER','ADMIN'))
);

CREATE TABLE IF NOT EXISTS sessions (
    token_hash TEXT PRIMARY KEY,
    username   TEXT NOT NULL,
    role       TEXT NOT NULL,
    expires    TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS login_attempts (
    ip TEXT NOT NULL, ts REAL NOT NULL
);
