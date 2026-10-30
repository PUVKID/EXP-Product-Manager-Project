using System;using System.IO;using System.Security.Cryptography;using Microsoft.Win32;using System.Text;using System.Security.Principal;
class Audit {
 [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref int n,StringBuilder s);
 static void Main(string[] args){using(var w=new StreamWriter(args[0])){try{
 int n=1024;var b=new StringBuilder(n);w.WriteLine("Package="+GetCurrentPackageFullName(ref n,b)+" "+b);w.WriteLine("Identity="+WindowsIdentity.GetCurrent().Name+" x64="+Environment.Is64BitProcess);
 var root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"BunjangAuto");w.WriteLine("Root="+root);
 foreach(var name in new[]{"usage.v1","license.key"}) {var p=Path.Combine(root,name); w.WriteLine(name+" exists="+File.Exists(p));if(File.Exists(p)){var data=File.ReadAllBytes(p);if(name=="usage.v1")w.WriteLine("File="+Encoding.UTF8.GetString(ProtectedData.Unprotect(data,Encoding.UTF8.GetBytes("EXP.Usage.v1"),DataProtectionScope.CurrentUser)));}}
 foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64})using(var hk=RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,view))using(var k=hk.OpenSubKey(@"Software\EXP\Usage")){w.WriteLine(view+" exists="+(k!=null));if(k!=null){var data=k.GetValue("State") as byte[];if(data!=null)w.WriteLine("Reg="+Encoding.UTF8.GetString(ProtectedData.Unprotect(data,Encoding.UTF8.GetBytes("EXP.Usage.v1"),DataProtectionScope.CurrentUser)));}}
 var provider=new CngProvider("Microsoft Platform Crypto Provider");foreach(var key in new[]{"Lumivolt.Device.v3","EXP.Usage.v1"}){w.WriteLine(key+" exists="+CngKey.Exists(key,provider));if(CngKey.Exists(key,provider))using(var k=CngKey.Open(key,provider))using(var sha=SHA256.Create())w.WriteLine("Fingerprint="+BitConverter.ToString(sha.ComputeHash(k.Export(CngKeyBlobFormat.EccPublicBlob))).Replace("-",""));}
 }catch(Exception ex){w.WriteLine(ex);}}}
}