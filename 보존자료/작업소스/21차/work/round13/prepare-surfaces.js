const fs=require('fs'),p=require('path');const root=p.resolve('work/round13/source');
function change(file,fn){const path=p.join(root,file);fs.writeFileSync(path,fn(fs.readFileSync(path,'utf8')));}
for(const file of ['src/UI/UiKit.cs','src/UI/Pages/HelpPage.cs','src/UI/Pages/LicensePage.cs','src/UI/Pages/ProductsPage.cs','src/UI/Pages/SettingsPage.cs','src/UI/Pages/SchedulePage.cs','src/Terms.cs','admin/LicenseAdmin.cs'])change(file,s=>s.replace(/\bTextBox\b/g,'SoftInput').replace(/\bNumericUpDown\b/g,'SoftNumber').replace(/,?\s*BorderStyle\s*=\s*BorderStyle\.FixedSingle/g,''));
change('src/Design.cs',s=>s.replace(/public static TextBox Input/g,'public static SoftInput Input').replace('return new TextBox { Name = name','return new SoftInput { Name = name').replace(', BorderStyle = BorderStyle.FixedSingle','').replace('    // Shared, code-drawn artwork:',fs.readFileSync(p.resolve('work/round13/SurfaceControls.txt'),'utf8')+'\n    // Shared, code-drawn artwork:'));
change('src/Theme.cs',s=>s.replace('foreach (Control c in parent.Controls)','if(parent is SoftInput || parent is SoftNumber)return;\n            foreach (Control c in parent.Controls)').replace('g.RowHeadersDefaultCellStyle.BackColor = p.Card;','g.RowHeadersVisible=false;\n            g.RowHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;\n            g.RowHeadersDefaultCellStyle.BackColor = p.Card;').replace('g.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;','g.CellBorderStyle = DataGridViewCellBorderStyle.None;\n            g.AlternatingRowsDefaultCellStyle.BackColor=p.CardAlt;'));
change('src/UI/UiKit.cs',s=>s.replace('Minimum = min, Maximum = max, Width = width','Minimum = min, Maximum = max, Width = Math.Max(144,width)').replace('ScaleChildren(k, f);','if(!(k is SoftInput) && !(k is SoftNumber))ScaleChildren(k, f);'));
change('src/UI/Pages/HelpPage.cs',s=>s.replace('new Point(14, 46)','new Point(14, 60)').replace('new Point(8, 70)','new Point(8, 88)').replace('left.Height - Ui.S(80)','left.Height - Ui.S(98)'));
change('src/UI/Pages/SettingsPage.cs',s=>s.replace('new Point(24,74)','new Point(24,68)'));
change('src/UI/Pages/ProductsPage.cs',s=>s.replace('Height = 150','Height = 168').replace('new ComboBox {','new SoftCombo {').replace('new Point(414, 117)','new Point(414, 124)').replace('Padding = new Padding(1)','Padding = new Padding(12)'));
for(const file of ['src/Program.cs','src/Guard.cs','admin/LicenseAdmin.cs'])change(file,s=>s.replaceAll('MessageBox.Show(','AppDialog.Show('));
change('src/UI/FluentBox.cs',s=>`using System.Windows.Forms; namespace BunjangAuto { public static class FluentBox {
 public static DialogResult Show(string text,string caption){return AppDialog.Show(text,caption);}
 public static DialogResult Show(string text,string caption,MessageBoxButtons b,MessageBoxIcon i){return AppDialog.Show(text,caption,b,i);}
 public static DialogResult Show(IWin32Window owner,string text,string caption){return AppDialog.Show(owner,text,caption);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons b){return AppDialog.Show(owner,text,caption,b);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons b,MessageBoxIcon i){return AppDialog.Show(owner,text,caption,b,i);}
} }`);
for(const file of ['src/Brand.cs','src/AssemblyInfo.cs','admin/AssemblyInfo.cs','installer/EXP.iss'])change(file,s=>s.replaceAll('2.2.5','2.2.6'));
