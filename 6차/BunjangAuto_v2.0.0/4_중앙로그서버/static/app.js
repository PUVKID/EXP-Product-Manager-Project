"use strict";
// BunjangAuto 관리자 센터 — 서버에서 받은 값은 전부 textContent 로만 넣는다(로그 내용은 클라이언트가 보낸 신뢰할 수 없는 값).
(function () {
  var TZ = -new Date().getTimezoneOffset();          // 브라우저 시간대(분)
  var token = sessionStorage.getItem("bja_token") || "";
  var live = { timer: null, lastId: 0, paused: false, rows: [] };
  var page = "dashboard", detailId = "";

  function $(id) { return document.getElementById(id); }
  function h(tag, attrs, kids) {
    var e = document.createElement(tag);
    for (var k in (attrs || {})) {
      if (k === "class") e.className = attrs[k];
      else if (k === "text") e.textContent = attrs[k];
      else if (k.indexOf("on") === 0) e.addEventListener(k.slice(2), attrs[k]);
      else e.setAttribute(k, attrs[k]);
    }
    (kids || []).forEach(function (c) { if (c != null) e.appendChild(typeof c === "string" ? document.createTextNode(c) : c); });
    return e;
  }
  function api(path, opts) {
    opts = opts || {};
    var headers = { "Content-Type": "application/json" };
    if (token) headers["Authorization"] = "Bearer " + token;
    return fetch(path, { method: opts.method || "GET", headers: headers, body: opts.body ? JSON.stringify(opts.body) : undefined })
      .then(function (r) {
        if (r.status === 401 && token) { logout(); throw new Error("세션 만료"); }
        return r;
      });
  }
  function getJson(path) { return api(path).then(function (r) { if (!r.ok) throw new Error("HTTP " + r.status); return r.json(); }); }
  function qs(o) { var p = []; for (var k in o) if (o[k] !== "" && o[k] != null) p.push(encodeURIComponent(k) + "=" + encodeURIComponent(o[k])); return p.join("&"); }
  function local(ts) {
    if (!ts) return "-";
    var d = new Date(ts); if (isNaN(d)) return ts;
    var p = function (n) { return (n < 10 ? "0" : "") + n; };
    return d.getFullYear() + "-" + p(d.getMonth() + 1) + "-" + p(d.getDate()) + " " + p(d.getHours()) + ":" + p(d.getMinutes()) + ":" + p(d.getSeconds());
  }
  function short(id) { return id ? id.slice(0, 8) : ""; }

  // ---------- 테마
  var themes = ["system", "light", "dark"], tnames = { system: "시스템", light: "라이트", dark: "다크" };
  function applyTheme(t) {
    document.documentElement.setAttribute("data-theme", t);
    $("theme").textContent = "테마: " + tnames[t];
    localStorage.setItem("bja_theme", t);
  }

  // ---------- 로그인
  function showLogin() { $("app").classList.add("hidden"); $("login").classList.remove("hidden"); }
  function logout() {
    if (token) api("/api/v1/admin/logout", { method: "POST", body: {} }).catch(function () {});
    token = ""; sessionStorage.removeItem("bja_token"); stopLive(); showLogin();
  }
  function doLogin() {
    $("lerr").textContent = "";
    fetch("/api/v1/admin/login", { method: "POST", headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ username: $("lu").value, password: $("lp").value }) })
      .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, j: j }; }); })
      .then(function (x) {
        if (!x.ok) { $("lerr").textContent = x.j.error || "로그인 실패"; return; }
        token = x.j.token; sessionStorage.setItem("bja_token", token); $("lp").value = "";
        $("login").classList.add("hidden"); $("app").classList.remove("hidden"); go("dashboard");
      }).catch(function () { $("lerr").textContent = "서버에 연결할 수 없습니다."; });
  }

  // ---------- 내비게이션
  var pages = [["dashboard", "Dashboard"], ["users", "Users"], ["live", "Live Logs"], ["errors", "Errors"], ["stats", "Statistics"]];
  function buildNav() {
    var nav = $("nav"); nav.textContent = "";
    pages.forEach(function (p) {
      nav.appendChild(h("button", { class: page === p[0] ? "on" : "", text: p[1], onclick: function () { go(p[0]); } }));
    });
  }
  function go(p, id) {
    stopLive(); page = p; detailId = id || "";
    buildNav();
    var t = (pages.filter(function (x) { return x[0] === p; })[0] || ["", "사용자 상세"])[1];
    $("title").textContent = p === "detail" ? "사용자 상세" : t;
    var v = $("view"); v.textContent = "";
    ({ dashboard: viewDashboard, users: viewUsers, live: viewLive, errors: viewErrors, stats: viewStats, detail: viewDetail }[p])(v);
  }

  // ---------- 공용 위젯
  function statCard(n, l) { return h("div", { class: "card stat" }, [h("div", { class: "n", text: String(n) }), h("div", { class: "l", text: l })]); }
  function table(cols, rows, onclick) {
    var thead = h("thead", {}, [h("tr", {}, cols.map(function (c) { return h("th", { text: c[0] }); }))]);
    var tb = h("tbody");
    rows.forEach(function (r) {
      var tr = h("tr", onclick ? { class: "click", onclick: function () { onclick(r); } } : {});
      cols.forEach(function (c) { var v = c[1](r); tr.appendChild(h("td", c[2] ? { class: c[2](r) } : {}, [v instanceof Node ? v : String(v == null ? "" : v)])); });
      tb.appendChild(tr);
    });
    return h("div", { class: "tw" }, [h("table", {}, [thead, tb])]);
  }
  var logCols = [
    ["시간", function (e) { return local(e.ts); }, function () { return "mono"; }],
    ["Client", function (e) { return short(e.client_id); }, function () { return "mono"; }],
    ["Level", function (e) { return e.level; }, function (e) { return "L-" + e.level; }],
    ["Module", function (e) { return e.module; }],
    ["Action", function (e) { return e.action; }, function () { return "mono"; }],
    ["Message", function (e) { return e.message; }, function () { return "msg"; }],
    ["Result", function (e) { return e.result; }]
  ];

  // ---------- 차트(SVG, 외부 라이브러리 없음)
  var NS = "http://www.w3.org/2000/svg";
  function se(tag, attrs, text) { var e = document.createElementNS(NS, tag); for (var k in attrs) e.setAttribute(k, attrs[k]); if (text != null) e.textContent = text; return e; }
  function barChart(series, keys, classes) {
    var W = 560, H = 190, L = 34, B = 24, T = 8;
    var svg = se("svg", { viewBox: "0 0 " + W + " " + H, width: "100%" });
    var max = 1; series.forEach(function (s) { keys.forEach(function (k) { max = Math.max(max, s[k]); }); });
    var gw = (W - L - 6) / series.length, bw = Math.max(4, gw / (keys.length + 1));
    [0, 0.5, 1].forEach(function (f) {
      var y = T + (H - T - B) * (1 - f);
      svg.appendChild(se("line", { x1: L, x2: W, y1: y, y2: y, stroke: "currentColor", "stroke-opacity": "0.12" }));
      svg.appendChild(se("text", { x: 2, y: y + 4 }, String(Math.round(max * f))));
    });
    series.forEach(function (s, i) {
      keys.forEach(function (k, j) {
        var hh = (H - T - B) * s[k] / max;
        svg.appendChild(se("rect", { class: classes[j], x: L + i * gw + j * bw + bw / 2, y: H - B - hh, width: bw - 1, height: hh }));
      });
      svg.appendChild(se("text", { x: L + i * gw + gw / 2 - 10, y: H - 8 }, s.day.slice(5)));
    });
    return svg;
  }
  function hbars(items) {
    var box = h("div"); var max = 1; items.forEach(function (i) { max = Math.max(max, i.n); });
    items.slice(0, 10).forEach(function (it) {
      var svg = se("svg", { viewBox: "0 0 560 22", width: "100%" });
      svg.appendChild(se("text", { x: 0, y: 15 }, it.name.length > 34 ? it.name.slice(0, 33) + "…" : it.name));
      svg.appendChild(se("rect", { class: "b1", x: 230, y: 4, width: Math.max(2, 270 * it.n / max), height: 14 }));
      svg.appendChild(se("text", { x: 236 + 270 * it.n / max, y: 15 }, String(it.n)));
      box.appendChild(svg);
    });
    if (!items.length) box.appendChild(h("div", { class: "muted", text: "데이터 없음" }));
    return box;
  }
  function legend(items) { return h("div", { class: "legend" }, items.map(function (i) { return h("span", { text: "■ " + i }); })); }

  // ---------- Dashboard
  function viewDashboard(v) {
    Promise.all([getJson("/api/v1/admin/overview?tz=" + TZ), getJson("/api/v1/admin/stats?days=7&tz=" + TZ)]).then(function (r) {
      var o = r[0], s = r[1];
      v.appendChild(h("div", { class: "cards" }, [
        statCard(o.total_installs, "전체 설치 수"), statCard(o.active_users, "활성 사용자(7일)"), statCard(o.online_users, "현재 온라인"),
        statCard(o.today_runs, "오늘 실행 수"), statCard(o.today_registrations, "오늘 자동등록 수"),
        statCard(o.today_success, "오늘 성공 수"), statCard(o.today_fail, "오늘 실패 수"), statCard(o.today_errors, "오늘 오류 수")]));
      v.appendChild(h("div", { class: "grid2" }, [
        h("div", { class: "card" }, [h("h3", { text: "최근 7일 실행량" }), barChart(s.series, ["runs"], ["b1"])]),
        h("div", { class: "card" }, [h("h3", { text: "최근 7일 등록 성공/실패" }), legend(["성공", "실패"]), barChart(s.series, ["success", "fail"], ["b3", "b2"])])]));
      v.appendChild(h("div", { class: "grid2" }, [
        h("div", { class: "card" }, [h("h3", { text: "최근 7일 오류" }), barChart(s.series, ["errors"], ["b2"])]),
        h("div", { class: "card" }, [h("h3", { text: "기능별 사용량" }), hbars(s.features)])]));
      v.appendChild(h("div", { class: "grid2" }, [
        h("div", { class: "card" }, [h("h3", { text: "버전별 사용자" }), hbars(s.versions)]),
        h("div", { class: "card" }, [h("h3", { text: "OS별 사용자" }), hbars(s.os)])]));
    }).catch(function (e) { v.appendChild(h("div", { class: "err", text: String(e.message) })); });
  }

  // ---------- Users
  function viewUsers(v) {
    var q = h("input", { placeholder: "Client ID / 버전 / OS / 이름 검색" });
    var ver = h("input", { placeholder: "버전 (예: 2.0.0.0)", size: "12" });
    var online = h("select", {}, [h("option", { value: "", text: "접속 상태: 전체" }), h("option", { value: "1", text: "온라인만" })]);
    var err = h("select", {}, [h("option", { value: "", text: "오류: 전체" }), h("option", { value: "1", text: "오류 발생" })]);
    var rec = h("select", {}, [["", "최근 접속: 전체"], ["1", "1시간 이내"], ["24", "24시간 이내"], ["168", "7일 이내"]].map(function (x) { return h("option", { value: x[0], text: x[1] }); }));
    var out = h("div");
    function load() {
      getJson("/api/v1/admin/users?" + qs({ q: q.value, version: ver.value, online: online.value, has_error: err.value, recent_hours: rec.value })).then(function (r) {
        out.textContent = "";
        out.appendChild(table([
          ["Client ID", function (u) { return u.client_id; }, function () { return "mono"; }],
          ["이름", function (u) { return u.license_name; }], ["버전", function (u) { return u.app_version; }], ["OS", function (u) { return u.os; }],
          ["상태", function (u) { return h("span", { class: "pill" + (u.online ? " on" : ""), text: u.online ? "온라인" : "오프라인" }); }],
          ["마지막 접속", function (u) { return local(u.last_seen); }],
          ["실행", function (u) { return u.launches; }], ["자동등록", function (u) { return u.auto_runs; }],
          ["성공", function (u) { return u.success; }], ["실패", function (u) { return u.fail; }], ["오류", function (u) { return u.errors; }]
        ], r.users, function (u) { go("detail", u.client_id); }));
        if (!r.users.length) out.appendChild(h("p", { class: "muted", text: "조건에 맞는 설치 환경이 없습니다." }));
      }).catch(function (e) { out.textContent = String(e.message); });
    }
    [q, ver, online, err, rec].forEach(function (c) { c.addEventListener("change", load); });
    q.addEventListener("keyup", function (e) { if (e.key === "Enter") load(); });
    v.appendChild(h("div", { class: "bar" }, [q, ver, online, err, rec, h("button", { text: "검색", onclick: load })]));
    v.appendChild(out); load();
  }

  // ---------- 로그 공용(Live / Errors / 사용자 상세) : 필터 + 내보내기
  function exportBtn(getFilters, fmt) {
    return h("button", { text: "내보내기 (" + fmt.toUpperCase() + ")", onclick: function () {
      api("/api/v1/admin/logs?" + qs(Object.assign({ format: fmt, download: "1", limit: 50000 }, getFilters()))).then(function (r) { return r.blob(); }).then(function (b) {
        var a = h("a", { href: URL.createObjectURL(b), download: "bja-logs." + fmt }); document.body.appendChild(a); a.click(); a.remove();
      });
    } });
  }
  function filterBar(opts) {
    var f = {
      q: h("input", { placeholder: "검색 (메시지/모듈/Client)" }),
      client_id: h("input", { placeholder: "Client ID 전체", size: "36" }),
      level: h("select", {}, [["", "Level: 전체"], ["ERROR,FATAL", "ERROR/FATAL"], ["WARN", "WARN"], ["INFO", "INFO"], ["DEBUG", "DEBUG"]].map(function (x) { return h("option", { value: x[0], text: x[1] }); })),
      module: h("input", { placeholder: "Module", size: "14" })
    };
    if (opts.fixedClient) { f.client_id.value = opts.fixedClient; }
    if (opts.fixedLevel) f.level.value = opts.fixedLevel;
    var bar = h("div", { class: "bar" }, [f.q].concat(opts.fixedClient ? [] : [f.client_id]).concat(opts.fixedLevel ? [] : [f.level]).concat([f.module]));
    bar.get = function () { return { q: f.q.value, client_id: f.client_id.value.trim(), level: f.level.value, module: f.module.value }; };
    bar.inputs = f; return bar;
  }
  function logView(v, opts) {
    var fb = filterBar(opts);
    var out = h("div");
    function load() {
      getJson("/api/v1/admin/logs?" + qs(Object.assign({ limit: opts.limit || 300 }, fb.get()))).then(function (r) {
        out.textContent = ""; out.appendChild(table(opts.cols || logCols, r.events));
      });
    }
    v.appendChild(fb);
    fb.appendChild(h("button", { text: "조회", onclick: load }));
    fb.appendChild(exportBtn(fb.get, "csv")); fb.appendChild(exportBtn(fb.get, "json"));
    v.appendChild(out); load();
  }

  // ---------- Live Logs (3초 폴링)
  function stopLive() { if (live.timer) { clearInterval(live.timer); live.timer = null; } }
  function viewLive(v) {
    live.lastId = 0; live.rows = []; live.paused = false;
    var fb = filterBar({}); var out = h("div"); var status = h("span", { class: "muted", text: "준실시간(3초 간격)" });
    var pause = h("button", { text: "일시정지", onclick: function () { live.paused = !live.paused; pause.textContent = live.paused ? "계속" : "일시정지"; } });
    var clear = h("button", { text: "초기화", onclick: function () { live.rows = []; live.lastId = 0; render(); poll(true); } });
    function render() { out.textContent = ""; out.appendChild(table(logCols, live.rows.slice(0, 500))); }
    function poll(first) {
      if (live.paused && !first) return;
      var f = fb.get();
      var params = Object.assign({ limit: first ? 100 : 500 }, f);
      if (live.lastId) params.since_id = live.lastId;
      api("/api/v1/admin/logs?" + qs(params)).then(function (r) { return r.json(); }).then(function (r) {
        var ev = r.events || [];
        if (!ev.length) return;
        // since_id 없을 땐 최신순(DESC), 있을 땐 DESC 로 받으므로 항상 앞에 붙인다
        live.rows = ev.concat(live.rows).slice(0, 1000);
        live.lastId = Math.max.apply(null, live.rows.map(function (e) { return e.id; }));
        status.textContent = "마지막 갱신 " + local(new Date().toISOString());
        render();
      }).catch(function () { status.textContent = "연결 오류"; });
    }
    fb.appendChild(pause); fb.appendChild(clear); fb.appendChild(exportBtn(fb.get, "csv")); fb.appendChild(exportBtn(fb.get, "json")); fb.appendChild(status);
    ["change"].forEach(function (e) { fb.inputs.level.addEventListener(e, function () { live.rows = []; live.lastId = 0; poll(true); }); });
    [fb.inputs.q, fb.inputs.client_id, fb.inputs.module].forEach(function (i) { i.addEventListener("change", function () { live.rows = []; live.lastId = 0; poll(true); }); });
    v.appendChild(fb); v.appendChild(out); poll(true); live.timer = setInterval(poll, 3000);
  }

  // ---------- Errors
  function viewErrors(v) {
    var cols = [
      ["발생시간", function (e) { return local(e.ts); }, function () { return "mono"; }],
      ["Client ID", function (e) { return e.client_id; }, function () { return "mono"; }],
      ["Version", function (e) { return e.version; }], ["Module", function (e) { return e.module; }],
      ["Exception Type", function (e) { return e.exc_type; }, function () { return "mono"; }],
      ["Message", function (e) { return e.message; }, function () { return "msg"; }]
    ];
    logView(v, { fixedLevel: "ERROR,FATAL", cols: cols });
  }

  // ---------- Statistics
  function viewStats(v) {
    var days = h("select", {}, [["7", "최근 7일"], ["14", "최근 14일"], ["30", "최근 30일"], ["90", "최근 90일"]].map(function (x) { return h("option", { value: x[0], text: x[1] }); }));
    var out = h("div");
    function load() {
      getJson("/api/v1/admin/stats?" + qs({ days: days.value, tz: TZ })).then(function (s) {
        out.textContent = "";
        out.appendChild(h("div", { class: "grid2" }, [
          h("div", { class: "card" }, [h("h3", { text: "실행량" }), barChart(s.series, ["runs"], ["b1"])]),
          h("div", { class: "card" }, [h("h3", { text: "자동등록 성공/실패" }), legend(["성공", "실패"]), barChart(s.series, ["success", "fail"], ["b3", "b2"])]),
          h("div", { class: "card" }, [h("h3", { text: "오류" }), barChart(s.series, ["errors"], ["b2"])]),
          h("div", { class: "card" }, [h("h3", { text: "기능별 사용량" }), hbars(s.features)]),
          h("div", { class: "card" }, [h("h3", { text: "버전별 사용자" }), hbars(s.versions)]),
          h("div", { class: "card" }, [h("h3", { text: "OS별 사용자" }), hbars(s.os)])]));
      });
    }
    days.addEventListener("change", load);
    v.appendChild(h("div", { class: "bar" }, [days])); v.appendChild(out); load();
  }

  // ---------- 사용자 상세
  function viewDetail(v) {
    getJson("/api/v1/admin/users/" + encodeURIComponent(detailId) + "?tz=" + TZ).then(function (r) {
      var c = r.client;
      var kv = [["Client ID", c.client_id], ["이름(라이선스)", c.license_name], ["PC 고유번호", c.pc_id], ["라이선스 만료", c.license_expires],
        ["앱 버전", c.app_version], ["Windows 버전", c.os], ["CPU", c.cpu], ["RAM", c.ram_mb ? c.ram_mb + " MB" : ""], ["GPU", c.gpu],
        ["해상도 / DPI", (c.screen || "") + " / " + (c.dpi || "")], ["마지막 실행", local(c.last_start)], ["마지막 접속", local(c.last_seen)],
        ["최초 접속", local(c.first_seen)], ["온라인 상태", c.online ? "온라인" : "오프라인"]];
      var info = h("div", { class: "kv" });
      kv.forEach(function (p) { info.appendChild(h("div", { text: p[0] })); info.appendChild(h("div", { class: "mono", text: String(p[1] == null ? "" : p[1]) })); });
      v.appendChild(h("div", { class: "bar" }, [h("button", { text: "← 목록", onclick: function () { go("users"); } })]));
      v.appendChild(h("div", { class: "cards" }, [statCard(c.launches, "프로그램 실행"), statCard(c.auto_runs, "자동등록"), statCard(c.success, "성공"), statCard(c.fail, "실패"), statCard(c.errors, "오류")]));
      v.appendChild(h("div", { class: "grid2" }, [
        h("div", { class: "card" }, [h("h3", { text: "시스템 정보" }), info]),
        h("div", { class: "card" }, [h("h3", { text: "최근 14일 (실행/성공/실패/오류)" }),
          barChart(r.daily.map(function (d) { return { day: d.day, runs: d.runs || 0, success: d.success || 0, fail: d.fail || 0, errors: d.errors || 0 }; }), ["runs", "success", "fail", "errors"], ["b1", "b3", "b2", "b2"])])]));
      v.appendChild(h("h3", { text: "최근 오류" }));
      v.appendChild(table(logCols, r.recent_errors.slice(0, 20)));
      v.appendChild(h("h3", { text: "이 사용자의 로그" }));
      var sub = h("div"); v.appendChild(sub); logView(sub, { fixedClient: detailId, limit: 300 });
    }).catch(function (e) { v.appendChild(h("div", { class: "err", text: String(e.message) })); });
  }

  // ---------- 시작
  $("lbtn").addEventListener("click", doLogin);
  $("lp").addEventListener("keyup", function (e) { if (e.key === "Enter") doLogin(); });
  $("logout").addEventListener("click", logout);
  $("theme").addEventListener("click", function () { applyTheme(themes[(themes.indexOf(localStorage.getItem("bja_theme") || "system") + 1) % 3]); });
  applyTheme(localStorage.getItem("bja_theme") || "system");
  setInterval(function () { $("clock").textContent = local(new Date().toISOString()); }, 1000);
  if (token) { api("/api/v1/admin/me").then(function (r) { if (r.ok) { $("app").classList.remove("hidden"); go("dashboard"); } else showLogin(); }).catch(showLogin); }
  else showLogin();
})();
