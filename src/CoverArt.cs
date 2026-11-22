using System;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// Cover pictures for the user's own disc images, downloaded by disc code (SPEC-covers.md).
// Rules of this module:
//  - One fixed address on one fixed HTTPS host. There is no setting, variable or argument that changes it.
//  - The only thing sent is the disc code, as part of the address. It is checked against a strict pattern first.
//  - No redirect is followed. The answer must be a JPEG picture of at most MaxBytes.
//  - A picture is kept only in the launcher's data folder. It is never part of the program file, the source or a release.
//  - A code the collection does not have is remembered, so the launcher does not ask again at every start.
public static class CoverArt {
  public const string Host="raw.githubusercontent.com";
  const string Prefix="https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/";
  public const int MaxBytes=3*1024*1024,TimeoutMs=8000;
  public const string Saved="saved",Missing="missing",Failed="failed";
  public static string Folder {get{return Path.Combine(DeckPaths.Root,"covers");}}
  public static string SettingPath {get{return Path.Combine(DeckPaths.Root,"covers.txt");}}
  public static bool IsCode(string code){return code!=null&&Regex.IsMatch(code,"\\A[A-Z]{4}-[0-9]{5}\\z");} // \z, not $: a code with a line end after it is refused
  public static Uri Address(string code){
    if(!IsCode(code))throw new ArgumentException("Not a disc code.");
    var uri=new Uri(Prefix+code+".jpg");
    if(uri.Scheme!="https"||uri.Host!=Host||!uri.AbsolutePath.EndsWith("/"+code+".jpg",StringComparison.Ordinal))throw new ArgumentException("Not a cover address.");
    return uri;
  }
  public static string PicturePath(string folder,string code){if(!IsCode(code))throw new ArgumentException("Not a disc code.");return Path.Combine(folder,code+".jpg");}
  public static string MissPath(string folder,string code){if(!IsCode(code))throw new ArgumentException("Not a disc code.");return Path.Combine(folder,code+".none");}
  public static bool IsJpeg(byte[] data){return data!=null&&data.Length>=125&&data.Length<=MaxBytes&&data[0]==0xFF&&data[1]==0xD8&&data[2]==0xFF;}
  // The saved picture for this code, or "" when there is none.
  public static string Cached(string folder,string code){
    try{if(!IsCode(code))return "";string path=PicturePath(folder,code);if(!File.Exists(path))return "";long n=new FileInfo(path).Length;return n>=125&&n<=MaxBytes?path:"";}catch{return "";}
  }
  public static bool Missed(string folder,string code){try{return IsCode(code)&&File.Exists(MissPath(folder,code));}catch{return false;}}
  public static void ForgetMiss(string folder,string code){try{if(IsCode(code)&&File.Exists(MissPath(folder,code)))File.Delete(MissPath(folder,code));}catch{}}
  public static void RememberMiss(string folder,string code){Directory.CreateDirectory(folder);File.WriteAllText(MissPath(folder,code),"");}
  // Keeps downloaded bytes as the picture for this code. Bytes that are not a JPEG picture are refused.
  public static void Store(string folder,string code,byte[] data){
    if(!IsJpeg(data))throw new InvalidDataException("The download is not a JPEG picture.");
    string path=PicturePath(folder,code);Directory.CreateDirectory(folder);
    string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllBytes(temp,data);if(File.Exists(path))File.Delete(path);File.Move(temp,path);ForgetMiss(folder,code);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
  // The switch: two lines, the format number 1 and "download=1" or "download=0". No file, or a file that is not understood or cannot be read, means off (developer, 2026-11-18): nothing is sent until the user switches it on.
  public static bool LoadEnabled(string path){
    try{if(!File.Exists(path)||new FileInfo(path).Length>256)return false;string[] lines=File.ReadAllText(path,Encoding.UTF8).Replace("\r\n","\n").Split('\n');return lines.Length>1&&lines[0].Trim()=="1"&&lines[1].Trim()=="download=1";}catch{return false;}
  }
  public static void SaveEnabled(string path,bool on){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,"1\r\ndownload="+(on?"1":"0")+"\r\n",new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
  // Downloads the cover for this code into the folder. Returns Saved, Missing (the collection has no picture for the code;
  // remembered) or Failed (no connection, a refused answer, or not a picture; nothing is remembered, so a later start tries again).
  // Call it from a background thread.
  public static string Fetch(string folder,string code){
    try{
      Uri uri=Address(code);
      ServicePointManager.SecurityProtocol|=(SecurityProtocolType)3072; // TLS 1.2
      var req=(HttpWebRequest)WebRequest.Create(uri);req.AllowAutoRedirect=false;req.UseDefaultCredentials=false;req.Credentials=null;req.CookieContainer=null;req.Timeout=TimeoutMs;req.ReadWriteTimeout=TimeoutMs;req.UserAgent="FlutterDeck-Covers/1";req.Method="GET";
      using(var timer=new Timer(delegate{req.Abort();},null,TimeoutMs*2,Timeout.Infinite))using(var response=(HttpWebResponse)req.GetResponse()){
        if(response.StatusCode!=HttpStatusCode.OK||response.ContentLength>MaxBytes)return Failed;
        using(var stream=response.GetResponseStream())using(var memory=new MemoryStream()){
          var buffer=new byte[16384];int n;
          while((n=stream.Read(buffer,0,buffer.Length))>0){memory.Write(buffer,0,n);if(memory.Length>MaxBytes)return Failed;}
          byte[] data=memory.ToArray();if(!IsJpeg(data))return Failed;
          Store(folder,code,data);return Saved;
        }
      }
    }catch(WebException e){
      var answer=e.Response as HttpWebResponse;
      if(answer!=null&&answer.StatusCode==HttpStatusCode.NotFound){try{RememberMiss(folder,code);}catch{}return Missing;}
      return Failed;
    }catch{return Failed;}
  }
}
