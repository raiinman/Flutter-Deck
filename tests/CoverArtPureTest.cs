using System;
using System.IO;

// Tests the parts of CoverArt that need no network: the code pattern, the address, the picture check, the kept files and the switch.
// Nothing is downloaded by this test.
static class CoverArtPureTest {
  static int failed;
  static void Check(bool ok,string what){if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}
  static bool Throws(Action a){try{a();}catch(ArgumentException){return true;}catch(InvalidDataException){return true;}return false;}
  static byte[] Jpeg(int length){var data=new byte[length];data[0]=0xFF;data[1]=0xD8;data[2]=0xFF;data[3]=0xE0;return data;}
  static int Main(string[] args){
    string root=args[0],folder=Path.Combine(root,"covers");Directory.CreateDirectory(root);
    // the code pattern: four capital letters, a hyphen, five digits, and nothing else
    foreach(string good in new[]{"SLUS-00603","SCES-12345","SLPS-01141"})Check(CoverArt.IsCode(good),"a disc code is accepted: "+good);
    foreach(string bad in new[]{"","slus-00603","SLUS_00603","SLUS-0060","SLUS-006030","SLUS-00603.jpg","SLUS-00603/..","../SLUS-00603","SLUS-00603?x=1","SLUS-00603#a","SLUS-00603%2F","SLUS-00603\n","SLU5-00603","SLUS-0O603"," SLUS-00603",null})Check(!CoverArt.IsCode(bad),"not a disc code: "+(bad??"null"));
    // the address: one host, one folder, the code and nothing else
    var uri=CoverArt.Address("SLUS-00603");
    Check(uri.AbsoluteUri=="https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/SLUS-00603.jpg"&&uri.Host==CoverArt.Host&&uri.Scheme=="https"&&uri.Query==""&&uri.Fragment==""&&uri.UserInfo=="","the address is the fixed one: "+uri.AbsoluteUri);
    foreach(string bad in new[]{"","SLUS-00603/../x","slus-00603","SLUS-00603@evil.example",null})Check(Throws(delegate{CoverArt.Address(bad);})&&Throws(delegate{CoverArt.PicturePath(folder,bad);})&&Throws(delegate{CoverArt.MissPath(folder,bad);}),"no address or file name is made from a bad code");
    // the picture check
    Check(CoverArt.IsJpeg(Jpeg(125))&&CoverArt.IsJpeg(Jpeg(CoverArt.MaxBytes)),"a JPEG picture of an allowed size is accepted");
    Check(!CoverArt.IsJpeg(null)&&!CoverArt.IsJpeg(new byte[0])&&!CoverArt.IsJpeg(Jpeg(124))&&!CoverArt.IsJpeg(Jpeg(CoverArt.MaxBytes+1))&&!CoverArt.IsJpeg(new byte[500])&&!CoverArt.IsJpeg(System.Text.Encoding.ASCII.GetBytes("<html>"+new string('x',300)+"</html>"))&&!CoverArt.IsJpeg(new byte[]{0x89,0x50,0x4E,0x47,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0}),"nothing, a tiny file, a huge file, zeros, a web page and a PNG are refused");
    // the kept files
    Check(CoverArt.Cached(folder,"SLUS-00603")==""&&!CoverArt.Missed(folder,"SLUS-00603"),"nothing is kept at first");
    CoverArt.Store(folder,"SLUS-00603",Jpeg(400));
    Check(CoverArt.Cached(folder,"SLUS-00603")==Path.Combine(folder,"SLUS-00603.jpg")&&new FileInfo(Path.Combine(folder,"SLUS-00603.jpg")).Length==400&&Directory.GetFiles(folder,"*.tmp").Length==0,"a picture is kept under its code and no temporary file is left");
    CoverArt.Store(folder,"SLUS-00603",Jpeg(500));Check(new FileInfo(CoverArt.Cached(folder,"SLUS-00603")).Length==500,"a second picture replaces the first");
    Check(Throws(delegate{CoverArt.Store(folder,"SLUS-01140",new byte[400]);})&&CoverArt.Cached(folder,"SLUS-01140")==""&&Throws(delegate{CoverArt.Store(folder,"../x",Jpeg(400));})&&Directory.GetFiles(root).Length==0,"bytes that are not a picture, or a bad code, keep nothing");
    File.WriteAllBytes(Path.Combine(folder,"SLUS-99999.jpg"),new byte[10]);Check(CoverArt.Cached(folder,"SLUS-99999")=="","a kept file that is too small is not used");
    CoverArt.RememberMiss(folder,"SLUS-01140");Check(CoverArt.Missed(folder,"SLUS-01140")&&!CoverArt.Missed(folder,"SLUS-00603"),"a code the collection does not have is remembered");
    CoverArt.ForgetMiss(folder,"SLUS-01140");Check(!CoverArt.Missed(folder,"SLUS-01140"),"and can be forgotten for a new try");
    CoverArt.RememberMiss(folder,"SLUS-01140");CoverArt.Store(folder,"SLUS-01140",Jpeg(300));Check(!CoverArt.Missed(folder,"SLUS-01140"),"a kept picture clears the remembered miss");
    Check(CoverArt.Cached(folder,"bad")==""&&!CoverArt.Missed(folder,"bad")&&CoverArt.Fetch(folder,"bad")==CoverArt.Failed&&CoverArt.Fetch(folder,"SLUS-00603/../x")==CoverArt.Failed,"a bad code is never looked up, on disk or on the network");
    // the switch
    string setting=Path.Combine(root,@"s\covers.txt");
    Check(!CoverArt.LoadEnabled(setting),"no setting means off");
    CoverArt.SaveEnabled(setting,false);Check(!CoverArt.LoadEnabled(setting)&&File.ReadAllText(setting)=="1\r\ndownload=0\r\n","off is saved and read back");
    CoverArt.SaveEnabled(setting,true);Check(CoverArt.LoadEnabled(setting)&&File.Exists(setting+".bak")&&Directory.GetFiles(Path.GetDirectoryName(setting),"*.tmp").Length==0,"on is saved; the file before is kept; no temporary file is left");
    foreach(string odd in new[]{"","2\r\ndownload=0\r\n","download=0\r\n","1\r\ndownload=no\r\n",new string('x',300)}){File.WriteAllText(setting,odd);Check(!CoverArt.LoadEnabled(setting),"a setting that is not understood means off");}
    if(failed>0){Console.WriteLine(failed+" check(s) failed");return 1;}
    Console.WriteLine("PASS: code pattern, fixed address, picture check, kept files, remembered miss, switch; nothing was downloaded");
    return 0;
  }
}
