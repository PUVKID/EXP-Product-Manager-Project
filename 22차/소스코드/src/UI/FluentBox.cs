using System.Windows.Forms; namespace BunjangAuto { public static class FluentBox {
 public static DialogResult Show(string text,string caption){return AppDialog.Show(text,caption);}
 public static DialogResult Show(string text,string caption,MessageBoxButtons b,MessageBoxIcon i){return AppDialog.Show(text,caption,b,i);}
 public static DialogResult Show(IWin32Window owner,string text,string caption){return AppDialog.Show(owner,text,caption);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons b){return AppDialog.Show(owner,text,caption,b);}
 public static DialogResult Show(IWin32Window owner,string text,string caption,MessageBoxButtons b,MessageBoxIcon i){return AppDialog.Show(owner,text,caption,b,i);}
} }