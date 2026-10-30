using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Security.Cryptography;
using BunjangAuto;
class RestoreProducts {
 static string directory;
 static int Main(string[] args){
  directory=Path.GetDirectoryName(Path.GetFullPath(args[0]));
  AppDomain.CurrentDomain.AssemblyResolve+=delegate(object sender,ResolveEventArgs e){var app=Assembly.LoadFile(Path.Combine(directory,"EXP.exe"));using(var stream=app.GetManifestResourceStream("lib."+new AssemblyName(e.Name).Name+".dll")){if(stream==null)return null;using(var memory=new MemoryStream()){stream.CopyTo(memory);return Assembly.Load(memory.ToArray());}}};
  return Run(args);
 }
 [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
 static int Run(string[] args){
  var items=CsvIO.Read(args[1]);
  if(items.Count!=6 || items.Any(x=>!x.IsValid))throw new Exception("Unexpected original product data");
  foreach(var item in items)foreach(var photo in item.Images.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries))if(!File.Exists(photo))throw new Exception("Original photo is missing");
  Directory.CreateDirectory(Path.Combine(Paths.Root,"photos"));
  foreach(var item in items){
   var photos=item.Images.Split(new[]{';'},StringSplitOptions.RemoveEmptyEntries).Select(photo=>{
    string hash;using(var sha=SHA256.Create())using(var stream=File.OpenRead(photo))hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
    var target=Path.Combine(Paths.Root,"photos","restored-"+hash+Path.GetExtension(photo));
    if(!File.Exists(target))File.Copy(photo,target);
    return target;
   }).ToArray();item.Images=string.Join(";",photos);
  }
  Storage.SaveProducts(items);
  var read=Storage.LoadProducts();
  if(read.Count!=6 || read.Where((item,i)=>item.Title!=items[i].Title||item.Description!=items[i].Description||item.Price!=items[i].Price||item.Images!=items[i].Images).Any())throw new Exception("Restoration verification failed");
  Console.WriteLine("Restored and read back 6 products; all image files exist. Posting history is not present in the source.");
  return 0;
 }
}
