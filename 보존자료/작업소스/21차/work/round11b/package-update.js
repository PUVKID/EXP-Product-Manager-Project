const fs=require('fs'),p=require('path');
const base=p.resolve('work/round11b');
for(const [from,to] of [['SecurityTests','security'],['RegressionTests','regression'],['ReleaseTests','release'],['GateIntegration','gate']])fs.copyFileSync(p.join(base,from+'-results.txt'),p.join(base,to+'-results.txt'));
let s=fs.readFileSync('work/round11/package.js','utf8').replaceAll("work/round11'","work/round11b'").replaceAll('2.2.1','2.2.2');
fs.writeFileSync(p.join(base,'package.js'),s);
