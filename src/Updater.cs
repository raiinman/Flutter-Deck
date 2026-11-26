using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Security.Cryptography;
using System.Threading;

// Launcher self-update. No WPF here; the screen lives in Program.cs.
// Trust rule: nothing is offered or installed unless the manifest bytes carry a valid RSA signature
// from the embedded public key, and the downloaded file matches the signed length and SHA-256.
// The address, product, platform and key are compile-time constants on purpose: there is no file,
// environment variable or argument that can point a shipped launcher at another source or key.
// Tests reach the same code through the overloads that take a key and paths (see UpdaterPureTest.cs).
public sealed class UpdateInfo { public string Version="",Url="",Sha256="",Notes=""; public int Sequence; public long Length; }
public sealed class UpdatePrefs { public bool Auto=true; public int Hold=0; }
public sealed class UpdateCheck { public UpdateInfo Info; public byte[] Manifest,Signature; }

public static class Updater {
  // Release identity of this build. release.ps1 reads these and refuses a Sequence that did not go up.
  public const string Version="0.4.2";
  public const int Sequence=4;
  public const string Product="flutterdeck",Channel="stable",Platform="win-x64",FileName="FlutterDeck.exe";
  public const string ManifestUrl="https://raw.githubusercontent.com/raiinman/Flutter-Deck/main/update/manifest.xml";
  public const string SignatureUrl="https://raw.githubusercontent.com/raiinman/Flutter-Deck/main/update/manifest.sig";
  public const string DownloadPrefix="https://github.com/raiinman/Flutter-Deck/releases/download/";
  public const string PublicKey="<RSAKeyValue><Modulus>vvv3DsGpGkl+kU5JVFwBiDMHnM6OKv8yt6/7hC01hTrNBkpyN/m4ev9OhAMzsVPBp8V3H8DQh+ZfTsMqzT7AX3eYgAi52yArK+Jw+Rd23fIWORv2QLxCkj810Zhp/u3x9DGlKviZ1z6IeA7VSBU3rGXCPoVBnclh4FQKzQz3AENToylKwua/wIs0sJCoFXyQMKZVqGTsoECIp3ONwchFfg9B3+9+svFjM8QjYoW+aeQ5g1K1ljJBBmPycii3ySpiit1ACZ1XKfM3sszPAXM/4siEL4hfCkYzmGRUgZRvhFkHVSxGY1V24swLcMKV/uCEZ+GhLVBoGhpp1HcLgoqpXNzTvIPUpzC0KmFqrWsXTuB0sNj+lxTE4Y3Ry1dSvLehaFr/D6WI0JVr7WaTiBfd9Sx0Z89D1ipgP/b8dEBVuwDGYX5vIqL8jXoXLA1+ueAa1LwDyc0p2TYs55j56Oi65TrPSej5B9Doc9PEgVIOb00a2XF1i1+op5oMU19O0qb5</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";
  public const int MaxManifestBytes=16384,MaxSignatureBytes=2048,MaxNotes=2000;
  public const long MaxPayloadBytes=67108864;

  public static string PrefsPath {get{return Path.Combine(DeckPaths.Root,"updates.txt");}}

  // ---- trust and parsing (pure) ----
  public static bool Verify(byte[] manifest,byte[] signature,string publicKeyXml){
    if(manifest==null||signature==null||manifest.Length==0||signature.Length==0||String.IsNullOrEmpty(publicKeyXml))return false;
    try{using(var rsa=new RSACryptoServiceProvider()){rsa.PersistKeyInCsp=false;rsa.FromXmlString(publicKeyXml);using(var sha=new SHA256CryptoServiceProvider())return rsa.VerifyData(manifest,sha,signature);}}
    catch(Exception){return false;}
  }
  public static byte[] DecodeSignature(byte[] file){
    if(file==null||file.Length==0||file.Length>MaxSignatureBytes)throw new InvalidDataException("Signature file size");
    try{return Convert.FromBase64String(Encoding.ASCII.GetString(file).Trim());}catch(FormatException){throw new InvalidDataException("Signature encoding");}
  }
  static string Attr(XmlElement e,string name){if(!e.HasAttribute(name))throw new InvalidDataException("Missing "+name);return e.GetAttribute(name);}
  static string Child(XmlElement root,string name,int max,bool required){
    var list=root.GetElementsByTagName(name);if(list.Count==0){if(required)throw new InvalidDataException("Missing "+name);return "";}
    if(list.Count!=1||list[0].ParentNode!=root)throw new InvalidDataException("Repeated "+name);
    string text=list[0].InnerText.Trim();if(text.Length>max||(required&&text.Length==0))throw new InvalidDataException("Bad "+name);return text;
  }
  public static UpdateInfo Parse(byte[] manifest){
    if(manifest==null||manifest.Length==0||manifest.Length>MaxManifestBytes)throw new InvalidDataException("Manifest size");
    string xml;try{xml=new UTF8Encoding(false,true).GetString(manifest);}catch(ArgumentException){throw new InvalidDataException("Manifest encoding");}
    var doc=new XmlDocument{XmlResolver=null};
    try{using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))doc.Load(reader);}catch(XmlException){throw new InvalidDataException("Manifest XML");}
    var root=doc.DocumentElement;if(root==null||root.Name!="update")throw new InvalidDataException("Manifest root");
    if(Attr(root,"schema")!="1")throw new InvalidDataException("Unknown schema");
    if(Attr(root,"product")!=Product)throw new InvalidDataException("Wrong product");
    if(Attr(root,"channel")!=Channel)throw new InvalidDataException("Wrong channel");
    if(Attr(root,"platform")!=Platform)throw new InvalidDataException("Wrong platform");
    var info=new UpdateInfo();
    info.Version=Child(root,"version",16,true);if(!Regex.IsMatch(info.Version,@"\A[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}\z"))throw new InvalidDataException("Bad version");
    string seq=Child(root,"sequence",9,true);if(!Regex.IsMatch(seq,@"\A[1-9][0-9]{0,8}\z"))throw new InvalidDataException("Bad sequence");info.Sequence=Int32.Parse(seq);
    info.Notes=Child(root,"notes",MaxNotes,false);
    var files=root.GetElementsByTagName("file");if(files.Count!=1||files[0].ParentNode!=root)throw new InvalidDataException("Expected one file");
    var file=(XmlElement)files[0];
    if(Attr(file,"name")!=FileName)throw new InvalidDataException("Unexpected file name");
    string len=Attr(file,"length");if(!Regex.IsMatch(len,@"\A[1-9][0-9]{0,9}\z"))throw new InvalidDataException("Bad length");info.Length=Int64.Parse(len);if(info.Length>MaxPayloadBytes)throw new InvalidDataException("File too large");
    info.Sha256=Attr(file,"sha256").ToUpperInvariant();if(!Regex.IsMatch(info.Sha256,@"\A[0-9A-F]{64}\z"))throw new InvalidDataException("Bad digest");
    info.Url=Attr(file,"url");DownloadUri(info.Url);if(info.Url!=DownloadPrefix+"v"+info.Version+"/"+FileName)throw new InvalidDataException("Download address does not match the version");
    return info;
  }
  // Only a release asset of this repository, named exactly FlutterDeck.exe, under a plain tag folder.
  public static Uri DownloadUri(string text){
    Uri u;
    if(text==null||!text.StartsWith(DownloadPrefix,StringComparison.Ordinal)||!Uri.TryCreate(text,UriKind.Absolute,out u)||u.Scheme!="https"||!u.IsDefaultPort||u.UserInfo.Length>0||u.Query.Length>0||u.Fragment.Length>0||u.Host!="github.com")throw new InvalidDataException("Download address not allowed");
    string rest=text.Substring(DownloadPrefix.Length);
    if(!Regex.IsMatch(rest,@"\Av[0-9]{1,4}\.[0-9]{1,4}\.[0-9]{1,4}/FlutterDeck\.exe\z")||u.AbsoluteUri!=text)throw new InvalidDataException("Download address not allowed");
    return u;
  }
  public static bool RedirectAllowed(Uri u){return u!=null&&u.IsAbsoluteUri&&u.Scheme=="https"&&u.IsDefaultPort&&u.UserInfo.Length==0&&(u.Host=="github.com"||u.Host.EndsWith(".githubusercontent.com",StringComparison.Ordinal));}
  // Newer release sequence only. A release the user rolled back from is not offered again automatically.
  public static bool Offers(UpdateInfo info,int currentSequence,int hold,bool manual){return info!=null&&info.Sequence>currentSequence&&(manual||info.Sequence!=hold);}
  public static UpdateInfo Accept(byte[] manifest,byte[] signature,string publicKeyXml){if(!Verify(manifest,signature,publicKeyXml))throw new InvalidDataException("Signature check failed");return Parse(manifest);}

  // ---- files ----
  public static string FileSha256(string path){using(var sha=SHA256.Create())using(var s=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(s)).Replace("-","");}
  public static bool FileMatches(string path,UpdateInfo info){return File.Exists(path)&&new FileInfo(path).Length==info.Length&&String.Equals(FileSha256(path),info.Sha256,StringComparison.OrdinalIgnoreCase);}
  public static string StagedPath(string exePath){return exePath+".new";}
  public static string PreviousPath(string exePath){return exePath+".old";}
  public static string DiscardPath(string exePath){return exePath+".rolledback";}
  // The one place the launcher file is replaced. It re-checks signature, manifest and file itself.
  // The running file is renamed, never overwritten, so a failure part-way leaves a working launcher.
  public static void Install(string exePath,string stagedPath,byte[] manifest,byte[] signature,string publicKeyXml,int currentSequence){
    var info=Accept(manifest,signature,publicKeyXml);
    if(info.Sequence<=currentSequence)throw new InvalidDataException("Not newer than the installed release");
    if(!FileMatches(stagedPath,info))throw new InvalidDataException("Downloaded file does not match the signed release");
    string old=PreviousPath(exePath),keep=old+".keep";
    // Set the existing rollback copy aside first; it is deleted only after the swap has worked.
    Unprotect(keep);if(File.Exists(keep))File.Delete(keep);
    bool hadOld=File.Exists(old);if(hadOld)File.Move(old,keep);
    try{File.Move(exePath,old);}catch{if(hadOld)File.Move(keep,old);throw;}
    try{File.Move(stagedPath,exePath);}catch{File.Move(old,exePath);if(hadOld)File.Move(keep,old);throw;}
    Unprotect(old);Unprotect(exePath);
    try{Unprotect(keep);if(File.Exists(keep))File.Delete(keep);}catch(Exception){}
  }
  // A read-only flag carried over from the user's copy would block the next update or cleanup.
  static void Unprotect(string path){try{if(File.Exists(path))File.SetAttributes(path,File.GetAttributes(path)&~FileAttributes.ReadOnly);}catch(Exception){}}
  public static bool CanRollBack(string exePath){return File.Exists(PreviousPath(exePath));}
  public static void RollBack(string exePath){
    string old=PreviousPath(exePath),discard=DiscardPath(exePath);
    if(!File.Exists(old))throw new FileNotFoundException("No previous version kept");
    Unprotect(discard);if(File.Exists(discard))File.Delete(discard);
    File.Move(exePath,discard);
    try{File.Move(old,exePath);}catch{File.Move(discard,exePath);throw;}
  }
  // Leftovers from an interrupted download or a completed rollback. Best effort; never throws.
  public static void CleanUp(string exePath){foreach(string path in new[]{StagedPath(exePath),DiscardPath(exePath),PreviousPath(exePath)+".keep"}){try{Unprotect(path);if(File.Exists(path))File.Delete(path);}catch(Exception){}}}

  // Starts the launcher file again after an install or rollback. The caller then closes itself. arguments: what the
  // new copy needs to keep the same data folder (DeckPaths.RestartArguments); nothing else is ever passed.
  public static void Relaunch(string exePath,string arguments){
    if(arguments==null||(arguments.Length>0&&!arguments.StartsWith("--data-dir \"",StringComparison.Ordinal)))throw new ArgumentException("Unexpected restart option");if(!String.Equals(Path.GetFileName(exePath),FileName,StringComparison.OrdinalIgnoreCase)||!File.Exists(exePath))throw new FileNotFoundException("Launcher file not found");System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exePath,arguments){UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(exePath)}).Dispose();}

  // ---- preferences ----
  public static UpdatePrefs LoadPrefs(string path){
    var prefs=new UpdatePrefs();
    try{if(!File.Exists(path)||new FileInfo(path).Length>256)return prefs;foreach(string raw in File.ReadAllLines(path)){string line=raw.Trim();if(line=="auto=0")prefs.Auto=false;else if(line=="auto=1")prefs.Auto=true;else if(line.StartsWith("hold=",StringComparison.Ordinal)){int hold;if(Int32.TryParse(line.Substring(5),out hold)&&hold>=0)prefs.Hold=hold;}}}
    catch(Exception){return new UpdatePrefs();}
    return prefs;
  }
  public static void SavePrefs(string path,UpdatePrefs prefs){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,"auto="+(prefs.Auto?"1":"0")+"\nhold="+Math.Max(0,prefs.Hold)+"\n",new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }

  // ---- network ----
  static HttpWebRequest Request(Uri uri,int timeoutMs){
    ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072; // TLS 1.2, asked for by name so it never depends on a default. The exe declares target .NET Framework 4.8 (AppInfo.cs), where the default is "whatever Windows allows"; this line is kept so the updater still asks for TLS 1.2 even if that default or the target changes
    var req=(HttpWebRequest)WebRequest.Create(uri);req.AllowAutoRedirect=false;req.UseDefaultCredentials=false;req.Credentials=null;req.CookieContainer=null;req.Timeout=timeoutMs;req.ReadWriteTimeout=timeoutMs;req.UserAgent="FlutterDeck-Updater/1";return req;
  }
  static byte[] GetSmall(string url,int max){
    var req=Request(new Uri(url),8000);
    using(var timer=new Timer(delegate{req.Abort();},null,8000,Timeout.Infinite))using(var response=(HttpWebResponse)req.GetResponse()){
      if(response.StatusCode!=HttpStatusCode.OK)throw new WebException("Unexpected status "+(int)response.StatusCode);if(response.ContentLength>max)throw new InvalidDataException("Response too large");
      using(var stream=response.GetResponseStream())using(var m=new MemoryStream()){var b=new byte[4096];int n;while((n=stream.Read(b,0,b.Length))>0){if(m.Length+n>max)throw new InvalidDataException("Response too large");m.Write(b,0,n);}return m.ToArray();}
    }
  }
  // Fetches and accepts the published manifest. WebException means unreachable; InvalidDataException means not trusted.
  public static UpdateCheck Check(){
    var result=new UpdateCheck();result.Manifest=GetSmall(ManifestUrl,MaxManifestBytes);result.Signature=DecodeSignature(GetSmall(SignatureUrl,MaxSignatureBytes));
    result.Info=Accept(result.Manifest,result.Signature,PublicKey);return result;
  }
  // Downloads the release file beside the launcher. Follows at most four redirects, each to GitHub hosts only.
  // The file is kept only if its length and SHA-256 match the signed manifest.
  public static void Download(UpdateInfo info,string stagedPath){
    Uri uri=DownloadUri(info.Url);
    try{
      for(int hop=0;;hop++){
        var req=Request(uri,20000);
        using(var timer=new Timer(delegate{req.Abort();},null,600000,Timeout.Infinite))using(var response=(HttpWebResponse)req.GetResponse()){
          int code=(int)response.StatusCode;
          if(code==301||code==302||code==303||code==307||code==308){
            string location=response.Headers["Location"];Uri next;
            if(hop>=4||location==null||!Uri.TryCreate(uri,location,out next)||!RedirectAllowed(next))throw new InvalidDataException("Redirect not allowed");
            uri=next;continue;
          }
          if(code!=200||response.ContentLength>info.Length)throw new InvalidDataException("Unexpected download response");
          using(var stream=response.GetResponseStream())using(var file=new FileStream(stagedPath,FileMode.Create,FileAccess.Write,FileShare.None)){var b=new byte[65536];int n;long total=0;while((n=stream.Read(b,0,b.Length))>0){total+=n;if(total>info.Length)throw new InvalidDataException("Download larger than the signed length");file.Write(b,0,n);}}
          break;
        }
      }
      if(!FileMatches(stagedPath,info))throw new InvalidDataException("Downloaded file does not match the signed release");
    }catch{try{if(File.Exists(stagedPath))File.Delete(stagedPath);}catch(Exception){}throw;}
  }
}
