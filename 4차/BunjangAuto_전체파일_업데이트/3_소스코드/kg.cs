using System; using System.IO; using System.Security.Cryptography;
class K { static void Main(){ var r=new RSACryptoServiceProvider(3072); File.WriteAllText("private.xml", r.ToXmlString(true)); File.WriteAllText("public.xml", r.ToXmlString(false)); } }
