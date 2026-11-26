using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

// Tests for InstallCore.cs on a scratch folder. No registry, no shortcuts, no real install folder.
public static class InstallPureTest {
  static int failures=0;
  static void Check(bool ok,string name){if(!ok){failures++;Console.WriteLine("FAIL: "+name);}}
  static void Throws<T>(Action action,string name) where T:Exception {try{action();failures++;Console.WriteLine("FAIL (no error): "+name);}catch(T){}}
  static Stream S(string text){return new MemoryStream(Encoding.UTF8.GetBytes(text));}
  // A zip in memory: name, text, name, text, ...
  static byte[] Zip(params string[] pairs){var m=new MemoryStream();using(var z=new ZipArchive(m,ZipArchiveMode.Create,true)){for(int i=0;i<pairs.Length;i+=2){if(pairs[i].EndsWith("/")){z.CreateEntry(pairs[i]);continue;}using(var w=new StreamWriter(z.CreateEntry(pairs[i]).Open()))w.Write(pairs[i+1]);}}return m.ToArray();}
  static BuilderPayload Tools(string version,byte[] extraKit){
    var p=new BuilderPayload();
    byte[] kit=extraKit??Zip("build_game.py","kit "+version,"runtime/a.c","a "+version);
    byte[] zig=Zig(version);byte[] py=Zip("python.exe","python "+version,"LICENSE.txt","psf");
    p.Parts.Add(new BuilderPart{Folder="kit",Open=delegate{return new MemoryStream(kit);}});
    p.Parts.Add(new BuilderPart{Folder="python",Open=delegate{return new MemoryStream(py);}});
    p.Parts.Add(new BuilderPart{Folder="zig",StripTopFolder=true,Open=delegate{return new MemoryStream(zig);}});
    p.Files.Add(new BuilderFile{Path="NOTICES.txt",Open=delegate{return S("notices "+version);}});
    p.Files.Add(new BuilderFile{Path=@"zig\THIRD-PARTY-NOTICES.txt",Open=delegate{return S("zig notices");}});
    return p;
  }
  static byte[] Zig(string version){return Zip("zig-x86_64-windows-0.17.1/","","zig-x86_64-windows-0.17.1/zig.exe","zig "+version,"zig-x86_64-windows-0.17.1/LICENSE","mit","zig-x86_64-windows-0.17.1/lib/libc/mingw/COPYING","zpl");}
  public static int Main(string[] args){
    string root=Path.GetFullPath(args[0]);
    if(Directory.Exists(root)){foreach(string old in Directory.GetFiles(root,"*",SearchOption.AllDirectories))File.SetAttributes(old,FileAttributes.Normal);Directory.Delete(root,true);}
    Directory.CreateDirectory(root);
    string folder=Path.Combine(root,"Programs","Flutter Deck"),source=Path.Combine(root,"setup-source.exe");File.WriteAllText(source,"setup program");
    Func<int,Stream> v1=delegate(int i){return S("v1 file "+i);},v2=delegate(int i){return S("v2 file "+i);};

    // fresh install
    Check(Installer.CanInstallInto(folder)&&!Installer.IsInstallFolder(folder),"missing folder is installable, not yet an install");
    Installer.Install(folder,v1,source);
    for(int i=0;i<Installer.Payload.Length;i++)Check(File.ReadAllText(Path.Combine(folder,Installer.Payload[i]))=="v1 file "+i,"payload "+Installer.Payload[i]);
    Check(File.ReadAllText(Path.Combine(folder,Installer.UninstallerName))=="setup program"&&Installer.IsInstallFolder(folder),"uninstaller copied");
    Check(Directory.GetFiles(folder,"*.setup-tmp",SearchOption.AllDirectories).Length==0,"no staging files left");

    // replace an existing install; stale updater leftovers go, the user's own file stays
    foreach(string name in Installer.Leftovers)File.WriteAllText(Path.Combine(folder,name),"stale");
    File.SetAttributes(Path.Combine(folder,Installer.ExeName),FileAttributes.ReadOnly);
    string mine=Path.Combine(folder,"my notes.txt");File.WriteAllText(mine,"keep me");
    Installer.Install(folder,v2,source);
    Check(File.ReadAllText(Path.Combine(folder,Installer.ExeName))=="v2 file 0","replace over a read-only launcher");
    foreach(string name in Installer.Leftovers)Check(!File.Exists(Path.Combine(folder,name)),"stale leftover removed: "+name);
    Check(File.ReadAllText(mine)=="keep me","install leaves the user's own file");

    // a failed copy leaves the existing install untouched
    Throws<FileNotFoundException>(delegate{Installer.Install(folder,delegate(int i){return i==2?null:S("v3 file "+i);},source);},"missing payload");
    Throws<IOException>(delegate{Installer.Install(folder,delegate(int i){if(i==1)throw new IOException("disk full");return S("v3 file "+i);},source);},"copy failure");
    for(int i=0;i<Installer.Payload.Length;i++)Check(File.ReadAllText(Path.Combine(folder,Installer.Payload[i]))=="v2 file "+i,"failed install kept "+Installer.Payload[i]);
    Check(Directory.GetFiles(folder,"*.setup-tmp",SearchOption.AllDirectories).Length==0,"failed install left no staging files");

    // a failure part-way through the swap (another program holding one file) puts everything back
    using(var hold=new FileStream(Path.Combine(folder,"LICENSE"),FileMode.Open,FileAccess.Read,FileShare.Read)){
      Throws<IOException>(delegate{Installer.Install(folder,delegate(int i){return S("v4 file "+i);},source);},"swap failure on a held file");
    }
    for(int i=0;i<Installer.Payload.Length;i++)Check(File.ReadAllText(Path.Combine(folder,Installer.Payload[i]))=="v2 file "+i,"failed swap put back "+Installer.Payload[i]);
    Check(File.ReadAllText(Path.Combine(folder,Installer.UninstallerName))=="setup program"&&Directory.GetFiles(folder,"*.setup-*",SearchOption.AllDirectories).Length==0,"failed swap left the uninstaller and no setup leftovers");
    // a first-time install that fails part-way leaves no half install behind
    string fresh=Path.Combine(root,"Fresh");Directory.CreateDirectory(Path.Combine(fresh,"NOTICE.md"));Directory.Delete(Path.Combine(fresh,"NOTICE.md"));
    Throws<IOException>(delegate{Installer.Install(fresh,delegate(int i){if(i==3)throw new IOException("unreadable");return S("x");},source);},"first install copy failure");
    Check(!File.Exists(Path.Combine(fresh,Installer.ExeName))&&!Installer.IsInstallFolder(fresh),"failed first install leaves no launcher");
    Check(!Directory.Exists(fresh),"failed first install leaves no empty folders");
    // leftovers of ours alone never block a retry; anything else does
    Directory.CreateDirectory(Path.Combine(fresh,"assets"));File.WriteAllText(Path.Combine(fresh,"LICENSE.setup-old"),"stale");File.WriteAllText(Path.Combine(fresh,"FlutterDeck.exe.setup-tmp"),"stale");File.SetAttributes(Path.Combine(fresh,"FlutterDeck.exe.setup-tmp"),FileAttributes.ReadOnly);
    Check(Installer.CanInstallInto(fresh),"a folder holding only our leftovers can be installed into");
    Installer.Install(fresh,v1,source);Check(File.ReadAllText(Path.Combine(fresh,Installer.ExeName))=="v1 file 0"&&Directory.GetFiles(fresh,"*.setup-*",SearchOption.AllDirectories).Length==0,"retry after leftovers installs cleanly");
    File.Delete(Path.Combine(fresh,Installer.ExeName));Check(Installer.CanInstallInto(fresh),"an install missing its launcher can be repaired");
    Check(Installer.Uninstall(fresh).Count==0&&!Directory.Exists(fresh),"an install missing its launcher can still be uninstalled");

    // a launcher still running under an updater leftover name (after a self-update, before restart) is detected
    File.WriteAllText(Path.Combine(folder,"FlutterDeck.exe.old"),"running old build");
    using(var hold=new FileStream(Path.Combine(folder,"FlutterDeck.exe.old"),FileMode.Open,FileAccess.Read,FileShare.Read)){
      Check(Installer.LauncherInUse(folder),"launcher running as .old detected");
      Throws<IOException>(delegate{Installer.Uninstall(folder);},"uninstall while the old build runs");
    }
    File.Delete(Path.Combine(folder,"FlutterDeck.exe.old"));

    // a folder that merely contains a launcher (portable copy, source checkout) is not an install and is never cleaned
    string dev=Path.Combine(root,"dev checkout");Directory.CreateDirectory(Path.Combine(dev,"assets"));Directory.CreateDirectory(Path.Combine(dev,"codes"));
    foreach(string name in Installer.Payload)File.WriteAllText(Path.Combine(dev,name),"source file");
    Check(!Installer.IsInstallFolder(dev),"a folder without the uninstaller is not an install");
    Throws<InvalidOperationException>(delegate{Installer.Uninstall(dev);},"uninstall of a source checkout");
    foreach(string name in Installer.Payload)Check(File.ReadAllText(Path.Combine(dev,name))=="source file","checkout file kept: "+name);
    foreach(string bad in new[]{"C:foo",@"\foo",@"\\server\share\Flutter Deck",@"\\?\C:\x\y","C:/x/y"}){string b=bad;Throws<ArgumentException>(delegate{Installer.Uninstall(b);},"path form "+b);Check(!Installer.IsInstallFolder(b),"not an install folder: "+b);}

    // a running launcher (file held open) blocks install and uninstall, and nothing is removed
    using(var hold=new FileStream(Path.Combine(folder,Installer.ExeName),FileMode.Open,FileAccess.Read,FileShare.Read)){
      Check(Installer.LauncherInUse(folder),"held launcher detected");
      Throws<IOException>(delegate{Installer.Install(folder,v1,source);},"install while running");
      Throws<IOException>(delegate{Installer.Uninstall(folder);},"uninstall while running");
    }
    Check(!Installer.LauncherInUse(folder)&&File.ReadAllText(Path.Combine(folder,"LICENSE"))=="v2 file 2","nothing removed while running");

    // folders Setup must refuse
    string other=Path.Combine(root,"Documents");Directory.CreateDirectory(other);File.WriteAllText(Path.Combine(other,"thesis.docx"),"important");
    Check(!Installer.CanInstallInto(other)&&!Installer.IsInstallFolder(other),"a folder with other files is refused");
    Throws<InvalidOperationException>(delegate{Installer.Install(other,v1,source);},"install into another folder");
    Throws<InvalidOperationException>(delegate{Installer.Uninstall(other);},"uninstall of another folder");
    Check(File.ReadAllText(Path.Combine(other,"thesis.docx"))=="important","other folder untouched");
    Throws<ArgumentException>(delegate{Installer.Install("relative\\path",v1,source);},"relative path");
    Throws<ArgumentException>(delegate{Installer.Uninstall(Path.GetPathRoot(root));},"drive root");
    Check(!Installer.IsInstallFolder("")&&!Installer.IsInstallFolder(null)&&!Installer.IsInstallFolder(Path.GetPathRoot(root)),"empty, null and root are not install folders");
    string empty=Path.Combine(root,"Empty");Directory.CreateDirectory(empty);Check(Installer.CanInstallInto(empty),"empty folder is installable");

    // uninstall removes only known files and keeps the folder when the user's file remains
    foreach(string name in Installer.Leftovers)File.WriteAllText(Path.Combine(folder,name),"leftover");
    File.SetAttributes(Path.Combine(folder,"NOTICE.md"),FileAttributes.ReadOnly);
    var failed=Installer.Uninstall(folder);
    Check(failed.Count==0,"uninstall reported no failures");
    Check(File.ReadAllText(mine)=="keep me"&&Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Length==1,"uninstall left only the user's own file");
    Check(!Directory.Exists(Path.Combine(folder,"assets")),"empty assets folder removed");
    Check(!Directory.Exists(Path.Combine(folder,"codes")),"empty codes folder removed");
    File.Delete(mine);Installer.Install(folder,v1,source);failed=Installer.Uninstall(folder);
    Check(failed.Count==0&&!Directory.Exists(folder)&&Directory.Exists(Path.Combine(root,"Programs")),"clean uninstall removes the folder but not its parent");

    // settings removal is by name only
    string settings=Path.Combine(root,"FlutterDeck");Directory.CreateDirectory(Path.Combine(settings,"news","ABC"));
    Directory.CreateDirectory(Path.Combine(settings,"covers"));foreach(string name in new[]{"window.txt","text-size.txt","setup.xml","setup.xml.bak","updates.txt","choices.txt","choices.txt.bak","overlay.txt","library.txt","library.txt.bak","game-folder.txt","game-folder.txt.bak","covers.txt","library-view.txt","library-view.txt.bak",@"covers\SLUS-00603.jpg",@"covers\SLUS-01140.none",@"news\ABC\cache.xml",@"news\ABC\read.txt"})File.WriteAllText(Path.Combine(settings,name),"x");
    Check(Installer.SettingsRemain(settings),"settings present before removal");
    Installer.RemoveSettings(settings);Check(!Directory.Exists(settings)&&!Installer.SettingsRemain(settings),"known settings removed and empty folder removed");
    Directory.CreateDirectory(settings);File.WriteAllText(Path.Combine(settings,"window.txt"),"x");File.WriteAllText(Path.Combine(settings,"my save.sav"),"precious");
    Installer.RemoveSettings(settings);Check(!File.Exists(Path.Combine(settings,"window.txt"))&&!File.Exists(Path.Combine(settings,"overlay.txt"))&&File.ReadAllText(Path.Combine(settings,"my save.sav"))=="precious","unknown file in the settings folder is kept");
    Installer.RemoveSettings(Path.Combine(root,"missing"));
    // removing settings takes the game-program setting but never the memory card, key map or session files
    string s3=Path.Combine(root,"FlutterDeck3"),mml=Path.Combine(s3,"games","mml1");Directory.CreateDirectory(Path.Combine(mml,"sessions"));
    File.WriteAllText(Path.Combine(s3,"runtime.xml"),"x");File.WriteAllText(Path.Combine(s3,"runtime.xml.bak"),"x");File.WriteAllText(Path.Combine(mml,"card1.mcd"),"progress");File.WriteAllText(Path.Combine(mml,"keys.ini"),"keys");File.WriteAllText(Path.Combine(mml,"sessions","a.card-at-start.mcd"),"backup");
    Installer.RemoveSettings(s3);
    Check(!File.Exists(Path.Combine(s3,"runtime.xml"))&&!File.Exists(Path.Combine(s3,"runtime.xml.bak"))&&!Installer.SettingsRemain(s3),"game-program setting removed with the other settings");
    Check(File.ReadAllText(Path.Combine(mml,"card1.mcd"))=="progress"&&File.Exists(Path.Combine(mml,"keys.ini"))&&File.Exists(Path.Combine(mml,"sessions","a.card-at-start.mcd")),"memory card, key map and session backups survive settings removal");
    // read-only settings files, backups and save-time temporary copies are removed too, and "remain" sees them
    string s2=Path.Combine(root,"FlutterDeck2");Directory.CreateDirectory(Path.Combine(s2,"news","A"));
    foreach(string name in new[]{"setup.xml.bak","updates.txt.0123abcd.tmp",@"news\A\cache.xml"}){File.WriteAllText(Path.Combine(s2,name),"x");File.SetAttributes(Path.Combine(s2,name),FileAttributes.ReadOnly);}
    Check(Installer.SettingsRemain(s2),"backups and temporary copies count as settings that remain");
    Installer.RemoveSettings(s2);Check(!Installer.SettingsRemain(s2)&&!Directory.Exists(s2),"read-only settings, backups and temporary copies removed");


    // ---- the build tools (builder folder) ----
    string tf=Path.Combine(root,"Tools Test","Flutter Deck"),tb=Path.Combine(tf,"builder");
    long last=-1,lastTotal=-1;
    Installer.Install(tf,v1,source,Tools("v1",null),delegate(long d,long t){last=d;lastTotal=t;});
    Check(File.ReadAllText(Path.Combine(tb,"kit","build_game.py"))=="kit v1"&&File.ReadAllText(Path.Combine(tb,"kit","runtime","a.c"))=="a v1","kit unpacked into builder\\kit");
    Check(File.ReadAllText(Path.Combine(tb,"zig","zig.exe"))=="zig v1"&&File.Exists(Path.Combine(tb,"zig","lib","libc","mingw","COPYING"))&&!Directory.Exists(Path.Combine(tb,"zig","zig-x86_64-windows-0.17.1")),"zig unpacked without its top folder, licence files kept");
    Check(File.ReadAllText(Path.Combine(tb,"python","python.exe"))=="python v1"&&File.ReadAllText(Path.Combine(tb,"zig","THIRD-PARTY-NOTICES.txt"))=="zig notices"&&File.ReadAllText(Path.Combine(tb,"NOTICES.txt"))=="notices v1","python, notices and the Zig notices placed");
    Check(last==lastTotal&&lastTotal==Installer.BuilderBytes(Tools("v1",null)),"progress ends at the total");
    Check(File.ReadAllLines(Path.Combine(tb,Installer.BuilderList)).Length==9&&!Directory.Exists(Path.Combine(tf,"builder.setup-tmp"))&&!Directory.Exists(Path.Combine(tf,"builder.setup-old")),"list of 9 files written, no staging left");
    // replace: new tools in, files of the old version that the new one does not have are gone, a user's own file stays
    File.WriteAllText(Path.Combine(tb,"kit","old-only.txt"),"x");var lines=new List<string>(File.ReadAllLines(Path.Combine(tb,Installer.BuilderList)));lines.Add(@"kit\old-only.txt");File.WriteAllLines(Path.Combine(tb,Installer.BuilderList),lines.ToArray());
    File.SetAttributes(Path.Combine(tb,"python","python.exe"),FileAttributes.ReadOnly);
    Installer.Install(tf,v2,source,Tools("v2",null),null);
    Check(File.ReadAllText(Path.Combine(tb,"zig","zig.exe"))=="zig v2"&&!File.Exists(Path.Combine(tb,"kit","old-only.txt"))&&!Directory.Exists(Path.Combine(tf,"builder.setup-old")),"replace swaps the tools in and clears the old ones by their list");
    // a build running from the tools holds a file: the swap fails and the previous launcher and tools stay
    using(var hold=new FileStream(Path.Combine(tb,"zig","zig.exe"),FileMode.Open,FileAccess.Read,FileShare.Read)){
      Throws<IOException>(delegate{Installer.Install(tf,delegate(int i){return S("v3 file "+i);},source,Tools("v3",null),null);},"replace while a tool file is in use");
    }
    Check(File.ReadAllText(Path.Combine(tb,"zig","zig.exe"))=="zig v2"&&File.ReadAllText(Path.Combine(tf,Installer.ExeName))=="v2 file 0"&&!Directory.Exists(Path.Combine(tf,"builder.setup-tmp"))&&!Directory.Exists(Path.Combine(tf,"builder.setup-old")),"held tool file: previous launcher and tools kept, no staging left");
    // a damaged payload (unpacking fails part-way) changes nothing
    var broken=Tools("v4",null);broken.Parts.Add(new BuilderPart{Folder="kit2",Open=delegate{return new MemoryStream(new byte[]{1,2,3});}});
    Throws<InvalidDataException>(delegate{Installer.Install(tf,delegate(int i){return S("v4 file "+i);},source,broken,null);},"damaged tool zip");
    Check(File.ReadAllText(Path.Combine(tb,"zig","zig.exe"))=="zig v2"&&File.ReadAllText(Path.Combine(tf,Installer.ExeName))=="v2 file 0"&&!Directory.Exists(Path.Combine(tf,"builder.setup-tmp")),"damaged payload: nothing changed");
    // names that would land outside the builder folder are refused before anything is written
    foreach(string evil in new[]{"../evil.txt","..\\evil.txt","a/../../evil.txt","C:/evil.txt","/evil.txt","a/b./c","con:x","a//b"}){
      string e=evil;Throws<InvalidDataException>(delegate{Installer.Install(tf,v2,source,Tools("v5",Zip("ok.txt","x",e,"evil")),null);},"unsafe name "+e);
    }
    Check(!File.Exists(Path.Combine(tf,"evil.txt"))&&!File.Exists(Path.Combine(root,"Tools Test","evil.txt"))&&!Directory.Exists(Path.Combine(tf,"builder.setup-tmp")),"no file written outside, no staging left");
    Check(Installer.CleanRelative(@"kit\a.c")==@"kit\a.c"&&Installer.CleanRelative("kit/a.c")==@"kit\a.c"&&Installer.CleanRelative("..")==null&&Installer.CleanRelative(@"kit\..\..\x")==null&&Installer.CleanRelative(@"\\server\x")==null&&Installer.CleanRelative("a\\b ")==null,"relative-name rules");
    // a nearly full drive: refused before anything is written
    var realFree=Installer.FreeBytes;Installer.FreeBytes=delegate(string f){return 1000;};
    try{Throws<NotEnoughSpaceException>(delegate{Installer.Install(tf,delegate(int i){return S("v6 file "+i);},source,Tools("v6",null),null);},"not enough space");}finally{Installer.FreeBytes=realFree;}
    Check(File.ReadAllText(Path.Combine(tf,Installer.ExeName))=="v2 file 0"&&!Directory.Exists(Path.Combine(tf,"builder.setup-tmp")),"full drive: nothing changed");
    // a first install that fails leaves no builder folders behind, and a staging folder left by a crash is cleared next time
    string tfresh=Path.Combine(root,"Tools Fresh");
    Throws<IOException>(delegate{Installer.Install(tfresh,delegate(int i){if(i==2)throw new IOException("unreadable");return S("x");},source,Tools("v1",null),null);},"first install with tools, copy failure");
    Check(!Directory.Exists(tfresh),"failed first install with tools leaves nothing");
    Directory.CreateDirectory(Path.Combine(tfresh,"builder.setup-tmp","kit"));File.WriteAllText(Path.Combine(tfresh,"builder.setup-tmp","kit","build_game.py"),"half");File.WriteAllLines(Path.Combine(tfresh,"builder.setup-tmp",Installer.BuilderList),new[]{@"kit\build_game.py"});
    Check(Installer.CanInstallInto(tfresh),"a folder holding only a crashed staging folder can be installed into");
    Installer.Install(tfresh,v1,source,Tools("v1",null),null);Check(File.ReadAllText(Path.Combine(tfresh,"builder","kit","build_game.py"))=="kit v1"&&!Directory.Exists(Path.Combine(tfresh,"builder.setup-tmp")),"crashed staging cleared and the install completed");
    // uninstall: a tool file in use keeps the uninstaller for a retry; then everything listed goes, a user's file stays
    using(var hold=new FileStream(Path.Combine(tb,"python","python.exe"),FileMode.Open,FileAccess.Read,FileShare.Read)){
      var partial=Installer.Uninstall(tf);Check(partial.Count==1&&partial[0]==@"builder\python\python.exe"&&Installer.IsInstallFolder(tf),"uninstall with a tool file in use is partial and keeps the uninstaller");
    }
    Directory.CreateDirectory(Path.Combine(tb,"kit"));File.WriteAllText(Path.Combine(tb,"kit","my build notes.txt"),"keep me");
    Check(Installer.Uninstall(tf).Count==0,"uninstall with tools reported no failures");
    Check(File.ReadAllText(Path.Combine(tb,"kit","my build notes.txt"))=="keep me"&&Directory.GetFiles(tf,"*",SearchOption.AllDirectories).Length==1,"uninstall removed every listed tool file and kept the user's own file");
    File.Delete(Path.Combine(tb,"kit","my build notes.txt"));Installer.Install(tf,v1,source,Tools("v1",null),null);
    Check(Installer.Uninstall(tf).Count==0&&!Directory.Exists(tf),"clean uninstall with tools removes the whole folder");
    // a list naming files outside the builder folder is not followed
    Installer.Install(tf,v1,source,Tools("v1",null),null);string outside=Path.Combine(root,"Tools Test","outside.txt");File.WriteAllText(outside,"precious");
    File.AppendAllText(Path.Combine(tb,Installer.BuilderList),"..\\..\\outside.txt\r\n"+outside+"\r\n");
    Installer.Uninstall(tf);Check(File.ReadAllText(outside)=="precious","uninstall never follows a list entry outside the builder folder");
    // an old tools folder holding a file no list names is moved aside, never deleted, and never blocks the install
    Installer.Install(tf,v1,source,Tools("v1",null),null);File.Delete(Path.Combine(tb,Installer.BuilderList));Directory.CreateDirectory(Path.Combine(tb,"empty","deeper"));File.WriteAllText(Path.Combine(tb,"mine.txt"),"mine");
    Installer.Install(tf,v2,source,Tools("v2",null),null);
    var keptDirs=Directory.GetDirectories(tf,"builder.kept-*");
    Check(keptDirs.Length==1&&File.ReadAllText(Path.Combine(keptDirs[0],"mine.txt"))=="mine"&&File.ReadAllText(Path.Combine(tb,"zig","zig.exe"))=="zig v2"&&!Directory.Exists(Path.Combine(tf,"builder.setup-old")),"unlisted old tools kept aside, new tools installed");
    Check(Installer.Uninstall(tf).Count==0&&Directory.Exists(keptDirs[0])&&!Directory.Exists(tb),"uninstall keeps the kept-aside folder");
    Check(Installer.CanInstallInto(tf),"a folder holding only a kept-aside tools folder can be installed into again");
    Installer.Install(tf,v1,source,Tools("v1",null),null);
    // a list line too long to be a path is skipped; replace and uninstall still work
    File.AppendAllText(Path.Combine(tb,Installer.BuilderList),new string('a',150)+"\\"+new string('b',148)+"\r\n");
    Installer.Install(tf,v2,source,Tools("v2",null),null);Check(File.ReadAllText(Path.Combine(tb,"zig","zig.exe"))=="zig v2"&&!Directory.Exists(Path.Combine(tf,"builder.setup-old")),"a too-long list line does not stop a replace");
    File.AppendAllText(Path.Combine(tb,Installer.BuilderList),new string('a',150)+"\\"+new string('b',148)+"\r\n");
    Check(Installer.Uninstall(tf).Count==0&&!Directory.Exists(tb),"a too-long list line does not stop uninstall");

    // version comparison for the downgrade warning
    Check(Installer.CompareVersions("0.3.0","0.3.1")<0&&Installer.CompareVersions("0.10.0","0.9.9")>0&&Installer.CompareVersions("0.3.0","0.3.0")==0&&Installer.CompareVersions("0.3.0.0","0.3.0")==0&&Installer.CompareVersions("","0.3.0")<0&&Installer.CompareVersions("junk","")==0,"version comparison");
    Check(Installer.DefaultFolder.EndsWith(@"\Programs\Flutter Deck",StringComparison.Ordinal)&&!String.Equals(Installer.DefaultFolder,Installer.SettingsFolder,StringComparison.OrdinalIgnoreCase)&&!Installer.SettingsFolder.StartsWith(Installer.DefaultFolder,StringComparison.OrdinalIgnoreCase),"install folder is separate from the settings folder");

    if(failures>0){Console.WriteLine(failures+" installer check(s) failed");return 1;}
    Console.WriteLine("PASS: install, replace, failed-copy safety, running-launcher refusal, foreign-folder refusal, fixed-list uninstall, by-name settings removal, version comparison. Scratch folder only: no registry, shortcut or real install folder touched.");return 0;
  }
}
