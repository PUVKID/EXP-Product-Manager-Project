const fs=require('fs'),p=require('path');
const root=p.resolve('work/round13/source');
fs.cpSync(p.resolve('work/round11b/source'),root,{recursive:true,filter:f=>!['dist','bin','obj'].includes(p.basename(f))});
function edit(f,fn){let path=p.join(root,f);fs.writeFileSync(path,fn(fs.readFileSync(path,'utf8')));}
for(const f of ['src/Brand.cs','src/AssemblyInfo.cs','admin/AssemblyInfo.cs','installer/EXP.iss'])edit(f,s=>s.replaceAll('2.2.2','2.2.3'));
edit('build/Build.ps1',s=>s.replace("$compilerArgs += '/resource:resources\\fonts\\NanumGothicBold.ttf,font.NanumGothicBold.ttf'","$compilerArgs += '/resource:resources\\fonts\\NanumGothicBold.ttf,font.NanumGothicBold.ttf'\n    $compilerArgs += '/resource:resources\\app.ico,brand.app.ico'"));
edit('Lumivolt.csproj',s=>s.replace('<EmbeddedResource Include="resources\\fonts\\NanumGothicBold.ttf"','<EmbeddedResource Include="resources\\app.ico" LogicalName="brand.app.ico" />\n    <EmbeddedResource Include="resources\\fonts\\NanumGothicBold.ttf"'));
edit('admin/LicenseAdmin.cs',s=>s.replace('        Text = Brand.Master;','        using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("brand.app.ico")) if(stream!=null) Icon=new Icon(stream);\n        Text = Brand.Master;').replace('Font = Theme.FontBase() })','Font = Theme.FontBase(), Icon = this.Icon })'));
edit('admin/AdminWorkflow.cs',s=>s.replace('            foreach (var folder in folders)', '            try { folders.AddRange(Directory.GetDirectories(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EXP_MasterVault_*", SearchOption.TopDirectoryOnly)); } catch { }\n            foreach (var folder in folders)'));
console.log(root);
