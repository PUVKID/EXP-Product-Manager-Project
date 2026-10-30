const fs=require('fs'),path=require('path'),crypto=require('crypto');
const project=path.resolve('work/Lumivolt_v2.1.0'), old=path.resolve('work/original/Lumivolt_v2.0.0/3_소스코드'),src=path.join(project,'3_소스코드');
function walk(d){return fs.readdirSync(d,{withFileTypes:true}).flatMap(e=> e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);}
function sha(f){return crypto.createHash('sha256').update(fs.readFileSync(f)).digest('hex');}
const before=walk(old).map(f=>path.relative(old,f));const after=walk(src).filter(f=>!/[\\/](dist|bin|obj)[\\/]/.test(f)).map(f=>path.relative(src,f));
const compare=[...new Set([...before,...after])].sort().map(p=>{
 if(/license_private\.xml/.test(p)) return {path:p,status:'removed-private-key',hashOmitted:true};
 const a=path.join(old,p),b=path.join(src,p),ha=fs.existsSync(a)?sha(a):null,hb=fs.existsSync(b)?sha(b):null;
 return {path:p,status:ha===hb?'unchanged':ha===null?'added':hb===null?'removed':'modified',beforeSha256:ha,afterSha256:hb};
});
fs.writeFileSync(path.join(project,'5_검증결과/SOURCE_COMPARISON.json'),JSON.stringify(compare,null,2));
const required=['src/Bot.cs','src/Storage.cs','src/Vault.cs','src/Schedule.cs','src/Selectors.cs','src/DriverManager.cs','src/MainForm.cs','src/UI/AutomationController.cs'];
for(const r of required){if(sha(path.join(old,r))!==sha(path.join(src,r)))throw new Error('Unexpected core change '+r);}
for(const f of walk(project)){if(/\.(ps1|iss|txt)$/.test(f) && !/security-results|regression-results|gate-results|production-results|ui-results/.test(f)) {const b=fs.readFileSync(f);if(b[0]!==0xff&&b[0]!==0xfe)fs.writeFileSync(f,'\ufeff'+b.toString('utf8').replace(/^\ufeff/,''));}}
const stage=path.resolve('work/final-stage/Lumivolt_v2.1.0');
for(const f of walk(project)){
 const rel=path.relative(project,f);
 if(/(^|[\\/])(dist|bin|obj|__pycache__)([\\/]|$)/.test(rel)) continue;
 if(/license_private|\.(dpapi|lvbackup|pfx|p12)$/i.test(rel)) throw new Error('Secret in package '+rel);
 const out=path.join(stage,rel);fs.mkdirSync(path.dirname(out),{recursive:true});fs.copyFileSync(f,out);
}
const files=walk(stage).filter(f=>path.relative(stage,f).replace(/\\/g,'/')!=='5_검증결과/FILES_SHA256.json').sort().map(f=>({path:path.relative(stage,f).replace(/\\/g,'/'),bytes:fs.statSync(f).size,sha256:sha(f)}));
fs.writeFileSync(path.join(stage,'5_검증결과/FILES_SHA256.json'),JSON.stringify(files,null,2));
console.log(JSON.stringify({files:files.length,sourceComparison:compare.reduce((a,r)=>(a[r.status]=(a[r.status]||0)+1,a),{}),coreUnchanged:required.length,stage},null,2));
