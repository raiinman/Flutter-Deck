using System;
using System.IO;
using System.Text;
using System.Security.Cryptography;

// Tests for Updater.cs with a throwaway key and a scratch folder. No network, no real launcher file.
public static class UpdaterPureTest {
  static int failures=0;
  static void Check(bool ok,string name){if(!ok){failures++;Console.WriteLine("FAIL: "+name);}}
  static void Reject(Action action,string name){try{action();failures++;Console.WriteLine("FAIL (accepted): "+name);}catch(InvalidDataException){}catch(FileNotFoundException){}}
  const string Sha="0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF";
  static string Manifest(string product="flutterdeck",string channel="stable",string platform="win-x64",string schema="1",string version="0.3.1",string sequence="2",string name="FlutterDeck.exe",string length="1000",string sha=Sha,string url="https://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe",string notes="Fixes.",string extra=""){
    return "<update schema='"+schema+"' product='"+product+"' channel='"+channel+"' platform='"+platform+"'><version>"+version+"</version><sequence>"+sequence+"</sequence><file name='"+name+"' length='"+length+"' sha256='"+sha+"' url='"+url+"'/>"+extra+"<notes>"+notes+"</notes></update>";
  }
  static byte[] B(string s){return new UTF8Encoding(false).GetBytes(s);}
  static byte[] Sign(RSACryptoServiceProvider rsa,byte[] data){using(var sha=new SHA256CryptoServiceProvider())return rsa.SignData(data,sha);}

  public static int Main(string[] args){
    string dir=Path.GetFullPath(args[0]);if(Directory.Exists(dir))Directory.Delete(dir,true);Directory.CreateDirectory(dir);
    using(var rsa=new RSACryptoServiceProvider(2048))using(var other=new RSACryptoServiceProvider(2048)){
      rsa.PersistKeyInCsp=false;other.PersistKeyInCsp=false;string pub=rsa.ToXmlString(false),otherPub=other.ToXmlString(false);

      // signature
      byte[] good=B(Manifest()),sig=Sign(rsa,good);
      Check(Updater.Verify(good,sig,pub),"valid signature accepted");
      byte[] tampered=(byte[])good.Clone();tampered[tampered.Length/2]^=1;Check(!Updater.Verify(tampered,sig,pub),"tampered manifest rejected");
      byte[] badSig=(byte[])sig.Clone();badSig[5]^=1;Check(!Updater.Verify(good,badSig,pub),"tampered signature rejected");
      Check(!Updater.Verify(good,sig,otherPub),"wrong signer rejected");
      Check(!Updater.Verify(good,new byte[0],pub)&&!Updater.Verify(good,null,pub)&&!Updater.Verify(good,sig,"")&&!Updater.Verify(good,sig,"<nonsense/>"),"empty or malformed inputs rejected");
      Check(!Updater.Verify(good,sig,Updater.PublicKey),"test key is not the shipped key");
      using(var shipped=new RSACryptoServiceProvider()){shipped.PersistKeyInCsp=false;shipped.FromXmlString(Updater.PublicKey);Check(shipped.KeySize==3072&&shipped.PublicOnly,"shipped public key loads, 3072-bit, public only");}
      Check(Updater.Verify(good,Updater.DecodeSignature(B(Convert.ToBase64String(sig)+"\r\n")),pub),"base64 signature file decodes");
      Reject(delegate{Updater.DecodeSignature(B("not base64 !!"));},"bad signature encoding");
      Reject(delegate{Updater.DecodeSignature(new byte[Updater.MaxSignatureBytes+1]);},"oversize signature file");
      Reject(delegate{Updater.Accept(good,badSig,pub);},"Accept without valid signature");

      // manifest rules
      var info=Updater.Parse(good);
      Check(info.Version=="0.3.1"&&info.Sequence==2&&info.Length==1000&&info.Sha256==Sha&&info.Notes=="Fixes."&&info.Url.EndsWith("/v0.3.1/FlutterDeck.exe"),"manifest fields");
      Check(Updater.Parse(B(Manifest(sha:Sha.ToLowerInvariant()))).Sha256==Sha,"digest case normalised");
      Reject(delegate{Updater.Parse(B(Manifest(product:"other")));},"wrong product");
      Reject(delegate{Updater.Parse(B(Manifest(channel:"beta")));},"wrong channel");
      Reject(delegate{Updater.Parse(B(Manifest(platform:"win-arm64")));},"wrong platform");
      Reject(delegate{Updater.Parse(B(Manifest(schema:"2")));},"unknown schema");
      Reject(delegate{Updater.Parse(B(Manifest(version:"1.2")));},"bad version");
      Reject(delegate{Updater.Parse(B(Manifest(sequence:"0")));},"zero sequence");
      Reject(delegate{Updater.Parse(B(Manifest(sequence:"-3")));},"negative sequence");
      Reject(delegate{Updater.Parse(B(Manifest(name:"setup.exe")));},"unexpected file name");
      Reject(delegate{Updater.Parse(B(Manifest(length:"0")));},"zero length");
      Reject(delegate{Updater.Parse(B(Manifest(length:"67108865")));},"over size limit");
      Reject(delegate{Updater.Parse(B(Manifest(sha:"ABC")));},"short digest");
      foreach(string url in new[]{"http://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe","https://github.com/someone/else/releases/download/v0.3.1/FlutterDeck.exe","https://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe?x=1","https://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/../../../../evil/FlutterDeck.exe","https://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/other.exe","https://github.com.evil.example/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe","https://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe#f","https://user@github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe"}){string u=url;Reject(delegate{Updater.Parse(B(Manifest(url:u)));},"download address "+u);}
      Reject(delegate{Updater.Parse(B(Manifest(sequence:"1٢")));},"non-ASCII digit in sequence");
      Reject(delegate{Updater.Parse(B(Manifest(version:"0.3.١")));},"non-ASCII digit in version");
      Reject(delegate{Updater.Parse(B(Manifest(length:"1000&#10;")));},"newline after length");
      Reject(delegate{Updater.Parse(B(Manifest(sha:Sha+"&#10;")));},"newline after digest");
      Reject(delegate{Updater.Parse(B(Manifest(version:"0.3.2")));},"download tag differs from version");
      Reject(delegate{Updater.Parse(B(Manifest(extra:"<file name='FlutterDeck.exe' length='1' sha256='"+Sha+"' url='https://github.com/raiinman/Flutter-Deck/releases/download/v0.3.1/FlutterDeck.exe'/>")));},"two files");
      Reject(delegate{Updater.Parse(B(Manifest(notes:new string('n',Updater.MaxNotes+1))));},"notes too long");
      Reject(delegate{Updater.Parse(B("<!DOCTYPE update [<!ENTITY a 'b'>]>"+Manifest()));},"DTD");
      Reject(delegate{Updater.Parse(B("<other/>"));},"wrong root");
      Reject(delegate{Updater.Parse(B("not xml"));},"not XML");
      Reject(delegate{Updater.Parse(new byte[Updater.MaxManifestBytes+1]);},"oversize manifest");
      Reject(delegate{Updater.Parse(new byte[]{0x3C,0xFF,0xFE});},"invalid UTF-8");

      // offer rule
      Check(Updater.Offers(info,1,0,false)&&!Updater.Offers(info,2,0,false)&&!Updater.Offers(info,3,0,true)&&!Updater.Offers(info,1,2,false)&&Updater.Offers(info,1,2,true)&&!Updater.Offers(null,1,0,true),"offer rule: newer only, held release needs a manual check");
      Check(Updater.RedirectAllowed(new Uri("https://objects.githubusercontent.com/x"))&&Updater.RedirectAllowed(new Uri("https://github.com/x"))&&!Updater.RedirectAllowed(new Uri("http://objects.githubusercontent.com/x"))&&!Updater.RedirectAllowed(new Uri("https://githubusercontent.com.evil.example/x"))&&!Updater.RedirectAllowed(new Uri("https://evilgithubusercontent.com/x"))&&!Updater.RedirectAllowed(new Uri("https://objects.githubusercontent.com:8443/x"))&&!Updater.RedirectAllowed(null),"redirect allowlist");

      // install and rollback on scratch files
      string exe=Path.Combine(dir,"FlutterDeck.exe"),staged=Updater.StagedPath(exe);
      byte[] oldBytes=B("old launcher"),newBytes=B("new launcher build");
      File.WriteAllBytes(exe,oldBytes);File.WriteAllBytes(staged,newBytes);
      string digest;using(var sha=SHA256.Create())digest=BitConverter.ToString(sha.ComputeHash(newBytes)).Replace("-","");
      byte[] real=B(Manifest(length:newBytes.Length.ToString(),sha:digest)),realSig=Sign(rsa,real);
      Reject(delegate{Updater.Install(exe,staged,real,realSig,otherPub,1);},"install with wrong signer");
      Reject(delegate{Updater.Install(exe,staged,real,realSig,pub,2);},"install of a release that is not newer (replay)");
      Reject(delegate{Updater.Install(exe,staged,good,sig,pub,1);},"install when the file does not match the signed digest");
      File.WriteAllBytes(staged,B("new launcher buile"));Reject(delegate{Updater.Install(exe,staged,real,realSig,pub,1);},"install of an altered download of the right length");
      Check(File.ReadAllText(exe)=="old launcher"&&!File.Exists(Updater.PreviousPath(exe)),"refused installs leave the launcher untouched");
      File.WriteAllBytes(staged,newBytes);Check(Updater.FileMatches(staged,Updater.Parse(real)),"staged file matches");
      Check(!Updater.CanRollBack(exe),"nothing to roll back before the first install");Reject(delegate{Updater.RollBack(exe);},"rollback with no previous version");
      Updater.Install(exe,staged,real,realSig,pub,1);
      Check(File.ReadAllText(exe)=="new launcher build"&&File.ReadAllText(Updater.PreviousPath(exe))=="old launcher"&&!File.Exists(staged),"install swaps files and keeps the previous version");
      Check(Updater.CanRollBack(exe),"rollback offered after install");
      Updater.RollBack(exe);
      Check(File.ReadAllText(exe)=="old launcher"&&File.ReadAllText(Updater.DiscardPath(exe))=="new launcher build"&&!Updater.CanRollBack(exe),"rollback restores the previous version");
      File.WriteAllBytes(staged,B("partial"));Updater.CleanUp(exe);Check(!File.Exists(staged)&&!File.Exists(Updater.DiscardPath(exe))&&File.ReadAllText(exe)=="old launcher","cleanup removes leftovers only");
      // a locked previous-version file must not cost the user the working launcher
      File.WriteAllBytes(staged,newBytes);File.WriteAllBytes(Updater.PreviousPath(exe),B("stale"));
      using(var hold=new FileStream(Updater.PreviousPath(exe),FileMode.Open,FileAccess.Read,FileShare.None)){try{Updater.Install(exe,staged,real,realSig,pub,1);failures++;Console.WriteLine("FAIL: install with a locked previous file should fail");}catch(IOException){}catch(UnauthorizedAccessException){}}
      Check(File.ReadAllText(exe)=="old launcher","failed install leaves a working launcher");

      // a launcher file held open by another program: the earlier rollback copy must survive the failed install
      File.WriteAllBytes(staged,newBytes);File.WriteAllBytes(Updater.PreviousPath(exe),B("earlier rollback copy"));
      using(var hold=new FileStream(exe,FileMode.Open,FileAccess.Read,FileShare.Read)){try{Updater.Install(exe,staged,real,realSig,pub,1);failures++;Console.WriteLine("FAIL: install with the launcher file held open should fail");}catch(IOException){}catch(UnauthorizedAccessException){}}
      Check(File.ReadAllText(exe)=="old launcher"&&File.ReadAllText(Updater.PreviousPath(exe))=="earlier rollback copy"&&!File.Exists(Updater.PreviousPath(exe)+".keep"),"failed swap keeps both the launcher and the earlier rollback copy");
      // read-only flags must not block this update, the next one, or cleanup
      File.SetAttributes(exe,FileAttributes.ReadOnly);File.SetAttributes(Updater.PreviousPath(exe),FileAttributes.ReadOnly);
      try{Updater.Install(exe,staged,real,realSig,pub,1);}catch(Exception e){failures++;Console.WriteLine("FAIL: install over read-only files: "+e.GetType().Name);}
      Check(File.ReadAllText(exe)=="new launcher build"&&File.ReadAllText(Updater.PreviousPath(exe))=="old launcher"&&(File.GetAttributes(Updater.PreviousPath(exe))&FileAttributes.ReadOnly)==0&&!File.Exists(Updater.PreviousPath(exe)+".keep"),"install over read-only files, flags cleared");
      Updater.RollBack(exe);File.SetAttributes(Updater.DiscardPath(exe),FileAttributes.ReadOnly);Updater.CleanUp(exe);Check(!File.Exists(Updater.DiscardPath(exe))&&File.ReadAllText(exe)=="old launcher","cleanup removes a read-only leftover");

      // preferences
      string prefs=Path.Combine(dir,"updates.txt");
      var p=Updater.LoadPrefs(prefs);Check(p.Auto&&p.Hold==0,"defaults: automatic check on");
      p.Auto=false;p.Hold=7;Updater.SavePrefs(prefs,p);p=Updater.LoadPrefs(prefs);Check(!p.Auto&&p.Hold==7,"preferences round trip");
      p.Auto=true;p.Hold=0;Updater.SavePrefs(prefs,p);p=Updater.LoadPrefs(prefs);Check(p.Auto&&p.Hold==0,"preferences overwrite");
      File.WriteAllText(prefs,"garbage\nhold=-4\nauto=maybe");p=Updater.LoadPrefs(prefs);Check(p.Auto&&p.Hold==0,"unreadable preferences fall back to defaults");
      File.WriteAllText(prefs,new string('x',400));p=Updater.LoadPrefs(prefs);Check(p.Auto&&p.Hold==0,"oversize preferences ignored");
    }
    if(failures>0){Console.WriteLine(failures+" updater check(s) failed");return 1;}
    Console.WriteLine("PASS: signature accept/reject, manifest rules, download-address and redirect allowlists, offer rule, install/rollback/cleanup on scratch files, preferences. No network request and no real launcher file touched.");return 0;
  }
}
