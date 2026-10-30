using System;using System.IO;using System.Reflection;using BunjangAuto;using Microsoft.Win32;using System.Security.Cryptography;using Lumivolt.Security;
class UsageAudit {
 [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref int length,System.Text.StringBuilder name);
 static string directory;
 static int Main(string[] args){directory=Path.GetDirectoryName(Path.GetFullPath(args[0]));AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){var app=Assembly.LoadFile(Path.Combine(directory,"EXP.exe"));using(var s=app.GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(s==null)return null;using(var m=new MemoryStream()){s.CopyTo(m);return Assembly.Load(m.ToArray());}}};return Run(args);}
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static int Run(string[] args){
 if(args.Length>1)Console.SetOut(new StreamWriter(args[1]){AutoFlush=true});
 int length=1024;var package=new System.Text.StringBuilder(length);int packageResult=GetCurrentPackageFullName(ref length,package);Console.WriteLine("Package="+packageResult+" "+package);
 Console.WriteLine("Root="+Paths.Root+"; 64bit="+Environment.Is64BitProcess);
 var li=License.Verify(File.ReadAllText(Path.Combine(Paths.Root,"license.key")),"");Console.WriteLine("LicenseValid="+li.Valid+" Trial="+li.Trial);
 Console.WriteLine("FileExists="+File.Exists(Path.Combine(Paths.Root,"usage.v1")));
 using(var key=Registry.CurrentUser.OpenSubKey(@"Software\EXP\Usage")){Console.WriteLine("RegistryExists="+(key!=null));if(key!=null){var bytes=key.GetValue("State") as byte[];Console.WriteLine("RegistryBytes="+(bytes==null?0:bytes.Length));}}
 try{var read=typeof(UsageQuota).GetMethod("Read",BindingFlags.Static|BindingFlags.NonPublic);var s=read.Invoke(null,new object[]{li.MachineId,DateTime.UtcNow});Console.WriteLine("ReadResult="+s.ToString());}catch(Exception ex){Console.WriteLine(ex.ToString());return 1;}
 return 0;
 }
}
