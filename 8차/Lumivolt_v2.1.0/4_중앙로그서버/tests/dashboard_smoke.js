const { JSDOM } = require("jsdom");
// 사용: node dashboard_smoke.js http://127.0.0.1:8080   (npm i jsdom 필요. 관리자 비밀번호는 환경변수 BJA_ADMIN_PASSWORD)
const base = process.argv[2] || "http://127.0.0.1:8080";
(async () => {
  const dom = await JSDOM.fromURL(base + "/", { runScripts: "dangerously", resources: "usable", pretendToBeVisual: true });
  const w = dom.window;
  w.fetch = (u, o) => fetch(new URL(u, base), o);
  w.URL.createObjectURL = () => "blob:x";
  const errors = []; w.addEventListener("error", e => errors.push(e.message));
  await new Promise(r => setTimeout(r, 700));
  const $ = id => w.document.getElementById(id);
  console.log("login visible:", !$("login").classList.contains("hidden"));
  $("lu").value = "admin"; $("lp").value = "wrong"; $("lbtn").click();
  await new Promise(r => setTimeout(r, 600)); console.log("bad login msg:", $("lerr").textContent);
  $("lp").value = (process.env.BJA_ADMIN_PASSWORD || "correct horse battery"); $("lbtn").click();
  await new Promise(r => setTimeout(r, 1200));
  console.log("app visible:", !$("app").classList.contains("hidden"));
  const text = () => $("view").textContent.replace(/\s+/g, " ");
  console.log("DASH:", text().slice(0, 220));
  console.log("svg charts:", $("view").querySelectorAll("svg").length);
  const nav = [...$("nav").querySelectorAll("button")];
  for (const name of ["Users", "Errors", "Statistics", "Live Logs"]) {
    nav.find(b => b.textContent === name).click(); await new Promise(r => setTimeout(r, 900));
    console.log(name.toUpperCase() + ":", text().slice(0, 200), "| rows:", $("view").querySelectorAll("tbody tr").length);
  }
  // XSS 확인: 메시지의 <b> 가 태그로 해석되면 안 된다
  console.log("raw <b> element injected:", $("view").querySelectorAll("tbody b").length);
  // 사용자 상세
  nav.find(b => b.textContent === "Users").click(); await new Promise(r => setTimeout(r, 800));
  $("view").querySelector("tbody tr").click(); await new Promise(r => setTimeout(r, 1200));
  console.log("DETAIL:", $("title").textContent, "|", text().slice(0, 240));
  console.log("JS errors:", errors);
  process.exit(0);
})();
