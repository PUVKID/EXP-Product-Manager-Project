using System; using System.IO; using System.Security.Cryptography;
class K { static void Main(){ var r=new RSACryptoServiceProvider(4096); File.WriteAllText("private.xml", r.ToXmlString(true)); File.WriteAllText("public.xml", r.ToXmlString(false)); } }
