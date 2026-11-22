using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

// Tests for BuildCore.cs and BuildRun.cs on a scratch folder. No real build tool runs: this test program is its own
// stand-in. Copied to "python.exe" it plays the builder kit's command and does what the file plan.txt beside it says.
static class BuildPureTest {
  static int checks,failed;
  static void Check(bool ok,string what){checks++;if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}

  // ---- the stand-in for the packed Python ----
  static int StandIn(string[] args){
    Console.OutputEncoding=new UTF8Encoding(false);
    string here=Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName);
    string outFolder="";for(int i=0;i+1<args.Length;i++)if(args[i]=="--out")outFolder=args[i+1];
    File.WriteAllLines(Path.Combine(here,"received-args.txt"),args);
    var seen=new List<string>();foreach(System.Collections.DictionaryEntry e in Environment.GetEnvironmentVariables()){string k=(string)e.Key;if(k.StartsWith("RMX_",StringComparison.OrdinalIgnoreCase)||k.StartsWith("PYTHON",StringComparison.OrdinalIgnoreCase))seen.Add(k);}
    File.WriteAllLines(Path.Combine(here,"received-env.txt"),seen.ToArray());
    string program=Path.Combine(outFolder,"build","game","rock_neo_rt_kit.exe");
    foreach(string raw in File.ReadAllLines(Path.Combine(here,"plan.txt"))){
      int sp=raw.IndexOf(' ');string verb=sp<0?raw:raw.Substring(0,sp),rest=sp<0?"":raw.Substring(sp+1);
      if(verb=="say")Console.WriteLine(rest);
      else if(verb=="err")Console.Error.WriteLine(rest);
      else if(verb=="sleep")Thread.Sleep(Int32.Parse(rest));
      else if(verb=="program"){Directory.CreateDirectory(Path.GetDirectoryName(program));File.WriteAllText(program,rest);}
      else if(verb=="marker"){Directory.CreateDirectory(outFolder);File.WriteAllText(Path.Combine(outFolder,BuildCore.MarkerName),rest.Replace("{sha}",File.Exists(program)?BuildCore.Sha256Of(program).ToLowerInvariant():""));}
      else if(verb=="child"){
        var info=new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,"--stand-in-child"){UseShellExecute=false,CreateNoWindow=true};
        using(var c=Process.Start(info))File.WriteAllText(Path.Combine(here,"child-pid.txt"),c.Id.ToString());
      }
      else if(verb=="stdin"){int n=0;var input=Console.OpenStandardInput();while(input.ReadByte()>=0)n++;File.WriteAllText(Path.Combine(here,"stdin-end.txt"),"ended after "+n+" bytes");}   // at most a byte order mark, which .NET sends by itself under a UTF-8 console
      else if(verb=="exit"){Console.Out.Flush();return Int32.Parse(rest);}
    }
    return 0;
  }
  static bool Alive(int id){try{using(var p=Process.GetProcessById(id))return !p.HasExited;}catch(Exception){return false;}}

  static readonly BuildGame G1=BuildCore.Game(1),G2=BuildCore.Game(2),G3=BuildCore.Game(3);
  const string GoodMarker="{\n \"kit_version\": 13,\n \"kit\": \"commit f4c9dfd18fc7\",\n \"state\": \"ready\",\n \"finished\": \"2026-11-19 05:00:00\",\n \"exe\": \"build/game/rock_neo_rt_kit.exe\",\n \"exe_sha256\": \"{sha}\",\n \"selftest\": \"passed\",\n \"selftest_ram_sha256\": \"fcff12e8625877299c5f1ecb6e08c2e0d6ea6ee4721c17a081e61b5ce7e1ffb0\",\n \"minutes\": 1.5\n}";

  static int Main(string[] args){
    string self=Process.GetCurrentProcess().MainModule.FileName;
    if(args.Length==1&&args[0]=="--stand-in-child"){Thread.Sleep(120000);return 0;}
    if(String.Equals(Path.GetFileNameWithoutExtension(self),"python",StringComparison.OrdinalIgnoreCase))return StandIn(args);
    if(args.Length!=1){Console.WriteLine("usage: BuildPureTest <scratch folder>");return 2;}
    string root=Path.GetFullPath(args[0]);
    if(Directory.Exists(root))Directory.Delete(root,true);
    Directory.CreateDirectory(root);
    try{Core(root);Games(Path.Combine(root,"games"));Run(root,self);}
    catch(Exception e){failed++;Console.WriteLine("FAIL: a test threw: "+e);}
    Console.WriteLine(failed==0?"PASS: "+checks+" builder checks":"FAILED: "+failed+" of "+checks+" builder checks");
    return failed==0?0:1;
  }

  // ---- BuildCore ----
  static void Core(string root){
    // the lines of the build
    var l=BuildCore.ParseLine("@RMX kit 12");Check(l.Kind=="kit"&&l.A==12,"kit line");
    l=BuildCore.ParseLine("@RMX step 4 5 build");Check(l.Kind=="step"&&l.A==4&&l.B==5&&l.Word=="build","step line");
    l=BuildCore.ParseLine("@RMX progress compile 17 412");Check(l.Kind=="progress"&&l.Word=="compile"&&l.A==17&&l.B==412,"progress line");
    l=BuildCore.ParseLine("@RMX note track-differs");Check(l.Kind=="note"&&l.Word=="track-differs","note line");
    l=BuildCore.ParseLine("@RMX stop 21 wrong-disc");Check(l.Kind=="stop"&&l.A==21&&l.Word=="wrong-disc","stop line");
    l=BuildCore.ParseLine("@RMX done build/game/rock_neo_rt_kit.exe");Check(l.Kind=="done"&&l.Word=="build/game/rock_neo_rt_kit.exe","done line");
    Check(BuildCore.ParseLine("4/5 build").Kind=="text"&&BuildCore.ParseLine("").Kind=="text"&&BuildCore.ParseLine(null).Kind=="text"&&BuildCore.ParseLine(" @RMX kit 12").Kind=="text"&&BuildCore.ParseLine("@RMXkit 12").Kind=="text","a line for a person is text");
    foreach(string odd in new[]{"@RMX ","@RMX later-word 1 2","@RMX kit","@RMX kit twelve","@RMX kit -1","@RMX step 6 5 build","@RMX step 0 5 tools","@RMX step 4 5 BUILD","@RMX progress compile 5 4","@RMX progress compile x 4","@RMX stop wrong-disc 21","@RMX note two words","@RMX kit 99999999999","@RMX done"})
      Check(BuildCore.ParseLine(odd).Kind=="unknown","an unknown or broken launcher line changes nothing: "+odd);
    // a whole build, as the kit prints it
    var p=new BuildProgress();double last=-1;bool rising=true;
    foreach(string line in new[]{"@RMX kit 12","Mega Man Legends builder","@RMX step 1 5 tools","1/5 tools","@RMX step 2 5 read-disc","@RMX step 3 5 check-disc","@RMX note track-differs","@RMX step 4 5 build","@RMX progress translate 1 40","@RMX progress translate 40 40","@RMX progress compile 0 400","@RMX progress compile 200 400","@RMX later-word 7","@RMX progress compile 400 400","@RMX progress link 1 1","@RMX step 5 5 self-test","    self-test passed","@RMX done build/game/rock_neo_rt_kit.exe"}){
      BuildCore.Apply(p,line);double f=BuildCore.Fraction(p);if(f<last||f<0||f>1)rising=false;last=f;
    }
    Check(rising&&last==1,"the progress bar only goes forward and ends full");
    Check(p.KitVersion==12&&p.Step==5&&p.TrackDiffers&&!p.ZigUnproven&&!p.ChildrenNotTied&&p.StopCode==-1&&p.DonePath=="build/game/rock_neo_rt_kit.exe","the state after a good build");
    Check(p.Game=="","the first kit names no game");
    p=new BuildProgress();BuildCore.Apply(p,"@RMX step 4 5 build");BuildCore.Apply(p,"@RMX progress compile 100 400");
    Check(BuildCore.StepText(p)=="Step 4 of 5: building the game (compiling, 100 of 400)."&&BuildCore.StepText(new BuildProgress())=="The build is starting.","the step sentence");
    double half=BuildCore.Fraction(p);Check(half>0.3&&half<0.6,"compiling is most of the bar");
    BuildCore.Apply(p,"@RMX step 2 5 read-disc");Check(p.Step==4,"a step never goes back");
    p=new BuildProgress();BuildCore.Apply(p,"");BuildCore.Apply(p,"STOPPED: this is another game.");BuildCore.Apply(p,"@RMX stop 21 wrong-disc");
    Check(p.StopCode==21&&p.StopWord=="wrong-disc"&&p.StopReason=="this is another game.","a stop keeps its code, its word and its reason");
    BuildCore.Apply(p,"@RMX note zig-version-unproven");BuildCore.Apply(p,"@RMX note children-not-tied");BuildCore.Apply(p,"@RMX note some-later-note");Check(p.ZigUnproven&&p.ChildrenNotTied,"notes");
    var texts=new HashSet<string>();foreach(int code in new[]{0,2,10,11,12,13,20,21,30,31,32,33})texts.Add(BuildCore.ExitText(code,G1));
    Check(texts.Count==12&&BuildCore.ExitText(21,G1).Contains("SLUS-00603")&&BuildCore.ExitText(-1,G1).Contains("ended or crashed")&&BuildCore.ExitText(1,G1).Contains("code 1")&&BuildCore.ExitText(-1073741510,G1).Contains("ended or crashed"),"each exit code has its own sentence, and any other code says ended or crashed");
    // the command
    Check(BuildCore.Quote("C:\\a b\\c")=="\"C:\\a b\\c\""&&BuildCore.Quote("C:\\a\\")=="\"C:\\a\\\\\"","quoting, also a path that ends in a backslash");
    foreach(string bad in new[]{"","C:\\a\"b","C:\\a\nb"}){bool threw=false;try{BuildCore.Quote(bad);}catch(ArgumentException){threw=true;}Check(threw,"a path with a quote sign or a line break is refused");}
    var t=new BuildTools{Kit="C:\\fd\\builder\\kit",Python="C:\\fd\\builder\\python\\python.exe",Zig="C:\\fd\\builder\\zig\\zig.exe"};
    Check(BuildCore.Arguments(t,"D:\\my games\\Mega Man Legends (USA).cue","C:\\data\\built\\mml1")=="-B \"C:\\fd\\builder\\kit\\build_game.py\" --cue \"D:\\my games\\Mega Man Legends (USA).cue\" --out \"C:\\data\\built\\mml1\" --zig \"C:\\fd\\builder\\zig\\zig.exe\"","the command line is the contract's, with -B, and never --skip-selftest");
    // the kit's small JSON files
    var d=BuildCore.Flat(GoodMarker);Check(d!=null&&d["state"]=="ready"&&d["kit_version"]=="13"&&d["minutes"]=="1.5"&&d["exe"]=="build/game/rock_neo_rt_kit.exe","a real marker is read");
    Check(BuildCore.Flat("{}")!=null&&BuildCore.Flat("\uFEFF{\"a\":1}")!=null,"an empty object and a byte order mark are fine");
    foreach(string bad in new[]{null,"","[]","{\"a\":{\"b\":1}}","{\"a\":[1]}","{\"a\":\"x\\\\y\"}","{\"a\":\"x\\\"y\"}","{\"a\":1,\"a\":2}","{\"a\":true}","{\"a\":1,}","{\"a\":1} x","{\"a\":1}{\"b\":2}","{\"state\":\"building\",\"note\":\"x\", \"state\" : \"ready\"}",new string(' ',70000)})
      Check(BuildCore.Flat(bad)==null,"a file of another shape is not used");
    // may a built program start?
    string outFolder=Path.Combine(root,"built","mml1"),program=Path.Combine(outFolder,"build","game","rock_neo_rt_kit.exe"),marker=Path.Combine(outFolder,BuildCore.MarkerName);
    string exe,hash;
    Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash)=="The game is not built yet.","no folder: not built yet");
    Directory.CreateDirectory(Path.GetDirectoryName(program));File.WriteAllText(program,"program one");
    string sha=BuildCore.Sha256Of(program);
    Check(BuildCore.StartProblem(program,G1,root).Length>0,"a program in the output folder without a marker does not start");
    File.WriteAllText(marker,GoodMarker.Replace("{sha}",sha.ToLowerInvariant()));
    Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash)==""&&exe==program&&hash==sha,"a finished, tested build is accepted");
    Check(BuildCore.StartProblem(program,G1,root)==""&&BuildCore.StartProblem(program.ToUpperInvariant(),G1,root)==""&&BuildCore.StartProblem(program,G1,Path.Combine(root,"elsewhere"))=="","and it starts, also when the launcher's own folder is another one");
    Check(BuildCore.BuildFolderOf(program)==outFolder&&BuildCore.BuildFolderOf(Path.Combine(root,"x.exe"))==null,"the build folder is found by its marker");
    string other=Path.Combine(root,"other","rock_neo_rt_mp11.exe");Directory.CreateDirectory(Path.GetDirectoryName(other));File.WriteAllText(other,"x");
    Check(BuildCore.StartProblem(other,G1,root)==""&&BuildCore.StartProblem("",G1,root)==""&&BuildCore.StartProblem(Path.Combine(root,"missing.exe"),G1,root)=="","a program that no build made is not this check's matter");
    string second=Path.Combine(outFolder,"build","game","other.exe");File.WriteAllText(second,"program one");
    Check(BuildCore.StartProblem(second,G1,root).Contains("not the program that the build finished"),"another file in the build folder does not start, also with the same content");
    File.WriteAllText(program,"program two");
    Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash).Contains("not the one that passed its test")&&exe==""&&BuildCore.StartProblem(program,G1,root).Contains("not the one that passed its test")&&BuildCore.StartProblem(program,G1,root,delegate(string path){return sha;})==""&&BuildCore.StartProblem(program,G1,root,delegate(string path){throw new IOException();}).Contains("could not be read"),"a program that changed after the build does not start (a build killed between its two last steps)");
    File.WriteAllText(program,"program one");
    // a second marker nearer to the program cannot stand in for the real one
    string near=Path.Combine(outFolder,"build","game",BuildCore.MarkerName);
    File.WriteAllText(near,GoodMarker.Replace("build/game/rock_neo_rt_kit.exe","rock_neo_rt_kit.exe").Replace("{sha}",sha));
    Check(BuildCore.StartProblem(program,G1,root)==""&&BuildCore.BuildFoldersOf(program).Count==2,"two markers that both prove the program: it starts");
    File.WriteAllText(marker,GoodMarker.Replace("{sha}",new string('0',64)));
    Check(BuildCore.StartProblem(program,G1,root).Contains("not the one that passed its test")&&BuildCore.StartProblem(program,G1,Path.Combine(root,"elsewhere")).Length>0,"a nearer marker does not help when the real marker does not prove the program");
    File.Delete(near);File.WriteAllText(marker,GoodMarker.Replace("{sha}",sha.ToLowerInvariant()));
    Check(!BuildCore.BuildRunning(root,G1)&&!BuildCore.BuildRunning(Path.Combine(root,"nowhere"),G1)&&!BuildCore.AnyBuildRunning(root),"no gate file: no build runs");
    foreach(var change in new[]{new[]{"\"state\": \"ready\"","\"state\": \"building\"","not finished"},new[]{"\"selftest\": \"passed\"","\"selftest\": \"skipped\"","did not pass its test"},new[]{"\"selftest\": \"passed\",","","did not pass its test"},new[]{"\"state\": \"ready\",","","not finished"},
        new[]{"\"exe_sha256\": \"{sha}\"","\"exe_sha256\": \"abc\"","cannot be read"},new[]{"build/game/rock_neo_rt_kit.exe","../../other/rock_neo_rt_mp11.exe","outside its folder"},new[]{"build/game/rock_neo_rt_kit.exe","build/../build/game/rock_neo_rt_kit.exe","outside its folder"},
        new[]{"build/game/rock_neo_rt_kit.exe","/build/game/rock_neo_rt_kit.exe","outside its folder"},new[]{"build/game/rock_neo_rt_kit.exe","C:/Windows/notepad.exe","outside its folder"},new[]{"build/game/rock_neo_rt_kit.exe","build/game/rock_neo_rt_kit.exe:x.exe","outside its folder"},
        new[]{"build/game/rock_neo_rt_kit.exe","build/game/rock_neo_rt_kit.cmd","outside its folder"},new[]{"build/game/rock_neo_rt_kit.exe","build/game/gone.exe","is missing"},new[]{"\"minutes\": 1.5","\"minutes\": [1]","cannot be read"}}){
      File.WriteAllText(marker,GoodMarker.Replace(change[0],change[1]).Replace("{sha}",sha.ToLowerInvariant()));
      string said=BuildCore.BuiltProblem(outFolder,G1,out exe,out hash);
      Check(said.Contains(change[2])&&exe==""&&hash==""&&BuildCore.StartProblem(program,G1,root).Length>0,"a marker that does not prove the program: "+change[1]+" -> "+said);
    }
    File.WriteAllText(marker,GoodMarker.Replace("{sha}",sha));Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash)=="","the hash may be in capital letters");
    var read=BuildCore.ReadMarker(outFolder);Check(read!=null&&read.KitVersion==13&&read.State=="ready"&&read.Selftest=="passed","the marker's kit version is read");
    Check(BuildCore.Inside("C:\\a\\b\\c.exe","C:\\a\\b")&&BuildCore.Inside("C:\\A\\B\\c.exe","c:\\a\\b\\")&&!BuildCore.Inside("C:\\a\\bc\\d.exe","C:\\a\\b")&&!BuildCore.Inside("C:\\a\\b","C:\\a\\b")&&!BuildCore.Inside("C:\\a\\b\\..\\c.exe","C:\\a\\b"),"inside a folder");
    Directory.Delete(outFolder,true);
    // the build tools
    string baseDir=Path.Combine(root,"launcher"),data=Path.Combine(root,"data");Directory.CreateDirectory(baseDir);Directory.CreateDirectory(data);
    var found=BuildCore.Find(baseDir,data,G1);Check(!found.Offered&&found.Problem=="","no builder folder and no pointer file: the build is not offered");
    string kit=Path.Combine(baseDir,"builder","kit"),py=Path.Combine(baseDir,"builder","python","python.exe"),zig=Path.Combine(baseDir,"builder","zig","zig.exe");
    Directory.CreateDirectory(kit);
    found=BuildCore.Find(baseDir,data,G1);Check(found.Offered&&found.Problem.Contains("builder kit is missing or not complete"),"a builder folder without a kit: offered, with the problem said");
    foreach(string name in new[]{"build_game.py","kit-manifest.json"})File.WriteAllText(Path.Combine(kit,name),"x");
    File.WriteAllText(Path.Combine(kit,"expected.json"),"{\n \"kit\": \"commit f4c9dfd18fc7\",\n \"kit_version\": 13,\n \"zig_version\": \"0.17.1\",\n \"selftest_frames\": 1500\n}");
    found=BuildCore.Find(baseDir,data,G1);Check(found.Problem.Contains("Python is missing"),"no Python");
    Directory.CreateDirectory(Path.GetDirectoryName(py));File.WriteAllText(py,"x");
    found=BuildCore.Find(baseDir,data,G1);Check(found.Problem.Contains("Zig is missing"),"no Zig");
    Directory.CreateDirectory(Path.GetDirectoryName(zig));File.WriteAllText(zig,"x");
    found=BuildCore.Find(baseDir,data,G1);Check(found.Offered&&found.Problem==""&&found.KitVersion==13&&found.Kit==kit&&found.Python==py&&found.Zig==zig,"the shipped builder folder is found, with its kit version");
    File.WriteAllText(Path.Combine(kit,"expected.json"),"{\"kit_version\": 12}");Check(BuildCore.Find(baseDir,data,G1).Problem.Contains("too old"),"a kit older than 13 is refused");
    File.WriteAllText(Path.Combine(kit,"expected.json"),"not json");Check(BuildCore.Find(baseDir,data,G1).Problem.Contains("too old"),"a kit without a readable version is refused");
    File.WriteAllText(Path.Combine(kit,"expected.json"),"{\"kit_version\": 13}");
    string dev=Path.Combine(root,"dev");Directory.CreateDirectory(dev);
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"# places of the build tools on this PC","kit = "+kit,"python="+py,"zig="+zig,"","something else"});
    found=BuildCore.Find(dev,data,G1);Check(found.Offered&&found.Problem==""&&found.Kit==kit&&found.KitVersion==13,"without a builder folder, the pointer file in the data folder names the tools");
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"kit=builder\\kit","python="+py,"zig="+zig});Check(BuildCore.Find(dev,data,G1).Problem.Contains("no usable place"),"a place that is not a full path is refused");
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"kit="+kit,"python="+Path.Combine(root,"dev","other.exe"),"zig="+zig});File.WriteAllText(Path.Combine(root,"dev","other.exe"),"x");Check(BuildCore.Find(dev,data,G1).Problem.Contains("Python is missing"),"another program in the place of Python is refused");
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"kit="+kit});Check(BuildCore.Find(dev,data,G1).Problem.Contains("no usable place"),"a pointer file that does not name all three is refused");
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"kit="+Path.Combine(root,"nowhere"),"python="+py,"zig="+zig});Check(BuildCore.Find(baseDir,data,G1).Problem=="","the builder folder beside the launcher wins over the pointer file");
    File.Delete(Path.Combine(data,BuildCore.ToolsFileName));
    // a built game never leaves this PC
    var none=new List<string>();
    Check(BuildCore.SyncProblem("C:\\Users\\someone\\AppData\\Local\\FlutterDeck\\built\\mml1",none)==""&&BuildCore.SyncProblem("D:\\Portable\\OneDriveNotes\\built\\mml1",none)=="","an ordinary folder is fine");
    foreach(string synced in new[]{"C:\\Users\\someone\\OneDrive\\FlutterDeck\\built\\mml1","C:\\Users\\someone\\OneDrive - Some Company\\fd\\built\\mml1","D:\\Dropbox\\fd\\built\\mml1","D:\\Google Drive\\fd\\built\\mml1","E:\\onedrive\\x"})
      Check(BuildCore.SyncProblem(synced,none).Contains("must stay on this PC"),"a folder that a cloud service copies away is refused: "+synced);
    Check(BuildCore.SyncProblem("D:\\Cloud\\fd\\built\\mml1",new List<string>{"D:\\Cloud"}).Length>0&&BuildCore.SyncProblem("D:\\Cloud",new List<string>{"D:\\Cloud\\"}).Length>0&&BuildCore.SyncProblem("D:\\Cloudy\\fd",new List<string>{"D:\\Cloud",""})=="","a sync folder that Windows names is refused, by whatever name");
    Check(BuildCore.SyncProblem("C:\\Users\\Mega\\AppData\\Local\\FlutterDeck\\built\\mml1",none,"C:\\Users\\Mega")==""&&BuildCore.SyncProblem("C:\\Users\\nextcloud\\AppData\\Local\\FlutterDeck\\built\\mml1",none,"C:\\Users\\nextcloud\\")==""&&BuildCore.SyncProblem("C:\\Users\\Mega\\MEGA\\fd\\built\\mml1",none,"C:\\Users\\Mega").Length>0&&BuildCore.SyncProblem("C:\\Users\\OneDrive\\OneDrive\\fd",none,"C:\\Users\\OneDrive").Length>0,"the name of the Windows account is not taken for a cloud folder");
    Check(BuildCore.SyncProblem("G:\\My Drive\\fd\\built\\mml1",none).Length>0&&BuildCore.SyncProblem("C:\\Users\\someone\\Box\\fd\\built\\mml1",none,"C:\\Users\\someone").Length>0&&BuildCore.SyncProblem("D:\\Boxes\\fd",none)=="","more cloud folders by name");
    Check(BuildCore.OutputFolder("C:\\d",G1)=="C:\\d\\built\\mml1"&&!BuildCore.Inside(BuildCore.LockPath("C:\\d",G1),BuildCore.OutputFolder("C:\\d",G1))&&!BuildCore.Inside(BuildCore.LogPath("C:\\d",G1),BuildCore.OutputFolder("C:\\d",G1)),"the launcher's own two files are beside the output folder, not in it");
  }

  // ---- more than one game: each built program starts only as the game its marker names ----
  const string Marker2="{\n \"kit_version\": 1,\n \"kit\": \"commit c3c8b759e1ff\",\n \"game\": \"SLUS-01140\",\n \"state\": \"ready\",\n \"finished\": \"2026-11-19 13:30:11\",\n \"exe\": \"build/game/rock_neo_rt_kit.exe\",\n \"exe_sha256\": \"{sha}\",\n \"selftest\": \"passed\",\n \"selftest_ram_sha256\": \"4389d64ac39f0724b9cf24287c0757d157aa2be5a4d39278772eb850a1cf3210\",\n \"minutes\": 4.6\n}";
  static void Games(string root){
    Directory.CreateDirectory(root);
    Check(G1!=null&&G2!=null&&G3!=null&&G1.Id==1&&G2.Id==2&&G3.Id==3&&BuildCore.Game(0)==null&&BuildCore.Game(4)==null&&BuildCore.Game(-1)==null&&BuildCore.Games.Length==3,"three games can be built, and no other number is a game");
    Check(G3.Slug=="tron"&&G3.Code=="SLUS-00896"&&G3.KitKey=="kit3"&&G3.MarkerNamesGame&&G3.Slug!=G1.Slug&&G3.Slug!=G2.Slug&&G3.KitKey!=G2.KitKey&&G3.Code!=G2.Code&&BuildCore.OutputFolder("C:\\d",G3)=="C:\\d\\built\\tron"&&BuildCore.LockPath("C:\\d",G3)!=BuildCore.LockPath("C:\\d",G2)&&BuildCore.ExitText(21,G3).Contains("SLUS-00896")&&BuildCore.ExitText(21,G3).Contains("Tron Bonne"),"the third game shares no folder, no kit and no code with the others");
    Check(G1.Slug!=G2.Slug&&G1.KitKey!=G2.KitKey&&G1.Code!=G2.Code&&!G1.MarkerNamesGame&&G2.MarkerNamesGame,"the two games share no folder, no kit and no code");
    var games=BuildCore.Games;games[0]=null;Check(BuildCore.Game(1)!=null&&BuildCore.Games[0]!=null,"the list of games is not shared");
    Check(BuildCore.OutputFolder("C:\\d",G2)=="C:\\d\\built\\mml2"&&BuildCore.LockPath("C:\\d",G2)!=BuildCore.LockPath("C:\\d",G1)&&BuildCore.LogPath("C:\\d",G2)!=BuildCore.LogPath("C:\\d",G1)&&!BuildCore.Inside(BuildCore.LockPath("C:\\d",G2),BuildCore.OutputFolder("C:\\d",G2))&&!BuildCore.Inside(BuildCore.OutputFolder("C:\\d",G2),BuildCore.OutputFolder("C:\\d",G1)),"each game has its own output folder, gate file and kept text");
    Check(BuildCore.ExitText(21,G2).Contains("SLUS-01140")&&BuildCore.ExitText(21,G2).Contains("Mega Man Legends 2")&&!BuildCore.ExitText(21,G1).Contains("Legends 2")&&BuildCore.ExitText(20,G2).Contains("plain cue sheet"),"the wrong-disc sentence names the game of the build");
    var l=BuildCore.ParseLine("@RMX game SLUS-01140");Check(l.Kind=="game"&&l.Word=="SLUS-01140","game line");
    foreach(string odd in new[]{"@RMX game","@RMX game slus-01140","@RMX game SLUS-01140 x","@RMX game SLUS01140","@RMX game ../x"})Check(BuildCore.ParseLine(odd).Kind=="unknown","a broken game line changes nothing: "+odd);
    var p=new BuildProgress();BuildCore.Apply(p,"@RMX kit 1");BuildCore.Apply(p,"@RMX game SLUS-01140");Check(p.Game=="SLUS-01140"&&p.KitVersion==1,"the game line is kept in the state");

    // two built games side by side in the launcher's folder for built games
    string data=Path.Combine(root,"data"),out1=BuildCore.OutputFolder(data,G1),out2=BuildCore.OutputFolder(data,G2);
    string p1=Path.Combine(out1,"build","game","rock_neo_rt_kit.exe"),p2=Path.Combine(out2,"build","game","mml2_rt_kit.exe");
    Directory.CreateDirectory(Path.GetDirectoryName(p1));Directory.CreateDirectory(Path.GetDirectoryName(p2));File.WriteAllText(p1,"game one");File.WriteAllText(p2,"game two");
    string m1=Path.Combine(out1,BuildCore.MarkerName),m2=Path.Combine(out2,BuildCore.MarkerName);
    string good1=GoodMarker.Replace("{sha}",BuildCore.Sha256Of(p1)),good2=Marker2.Replace("rock_neo_rt_kit.exe","mml2_rt_kit.exe").Replace("{sha}",BuildCore.Sha256Of(p2));
    File.WriteAllText(m1,good1);File.WriteAllText(m2,good2);
    string exe,hash;
    Check(BuildCore.StartProblem(p1,G1,data)==""&&BuildCore.StartProblem(p2,G2,data)=="","each built game starts as itself");
    Check(BuildCore.StartProblem(p1,G2,data).Contains("another game")&&BuildCore.StartProblem(p2,G1,data).Contains("another game"),"a built game does not start as the other game");
    Check(BuildCore.BuiltProblem(out1,G1,out exe,out hash)==""&&BuildCore.BuiltProblem(out2,G2,out exe,out hash)==""&&BuildCore.BuiltProblem(out1,G2,out exe,out hash).Contains("another game")&&exe==""&&hash==""&&BuildCore.BuiltProblem(out2,G1,out exe,out hash).Contains("another game")&&exe=="","a folder that holds the other game's build is not this game's build");
    Check(BuildCore.StartProblem(p2,null,data).Length>0&&BuildCore.BuiltProblem(out2,null,out exe,out hash).Length>0,"no game: nothing starts");
    var read=BuildCore.ReadMarker(out2);Check(read!=null&&read.HasGame&&read.Game=="SLUS-01140"&&BuildCore.MarkerIsFor(read,G2)&&!BuildCore.MarkerIsFor(read,G1)&&!BuildCore.MarkerIsFor(null,G1)&&!BuildCore.MarkerIsFor(read,null),"the marker's game is read");
    read=BuildCore.ReadMarker(out1);Check(read!=null&&!read.HasGame&&BuildCore.MarkerIsFor(read,G1)&&!BuildCore.MarkerIsFor(read,G2),"a marker that names no game is the first game's only");
    // what the marker may say about its game
    foreach(var c in new[]{new[]{"SLUS-00603","0","0"},new[]{"SLUS-01140","0","1"},new[]{"SLUS-00896","0","0"},new[]{"","0","0"},new[]{"slus-01140","0","0"},new[]{"SLUS-01140 ","0","0"},new[]{"Mega Man Legends 2 (USA) SLUS-01140","0","0"}}){
      File.WriteAllText(m2,good2.Replace("\"game\": \"SLUS-01140\"","\"game\": \""+c[0]+"\""));
      Check((BuildCore.StartProblem(p2,G1,data)=="")==(c[1]=="1")&&(BuildCore.StartProblem(p2,G2,data)=="")==(c[2]=="1"),"the marker's game decides which game the program starts as: '"+c[0]+"'");
    }
    File.WriteAllText(m2,good2.Replace("\"game\": \"SLUS-01140\",","\"game\": \"SLUS-01140\",\n \"game\": \"SLUS-00603\","));
    Check(BuildCore.StartProblem(p2,G1,data).Length>0&&BuildCore.StartProblem(p2,G2,data).Length>0,"a marker that names two games proves nothing");
    File.WriteAllText(m2,good2.Replace("\"game\": \"SLUS-01140\",","\"game\": 1140,"));
    Check(BuildCore.StartProblem(p2,G1,data).Length>0&&BuildCore.StartProblem(p2,G2,data).Length>0,"a game that is a number is no game");
    // the marker is gone: a file inside the folder for built games never runs unproven, as any game
    File.Delete(m2);
    Check(BuildCore.BuildFoldersOf(p2).Count==0&&BuildCore.StartProblem(p2,G1,data).Length>0&&BuildCore.StartProblem(p2,G2,data).Length>0,"a built program whose marker is gone does not start, also not as the other game");
    string loose=Path.Combine(BuildCore.BuiltRoot(data),"loose.exe"),odd2=Path.Combine(BuildCore.BuiltRoot(data),"other","x.exe");
    File.WriteAllText(loose,"x");Directory.CreateDirectory(Path.GetDirectoryName(odd2));File.WriteAllText(odd2,"x");
    Check(BuildCore.StartProblem(loose,G1,data).Length>0&&BuildCore.StartProblem(odd2,G1,data).Length>0&&BuildCore.StartProblem(odd2,G2,data).Length>0&&BuildCore.StartProblem(Path.Combine(BuildCore.BuiltRoot(data),"MML2","build","game","mml2_rt_kit.exe").ToUpperInvariant(),G2,data).Length>0,"any file inside the folder for built games needs a marker");
    Check(BuildCore.StartProblem(Path.Combine(data,"elsewhere","rock_neo_rt_mp11.exe"),G1,data)==""&&BuildCore.StartProblem(Path.Combine(data,"builtx","a","b.exe"),G2,data)=="","a program outside it that no build made is not this check's matter");
    // the first game's marker in the second game's folder, and the other way round
    File.WriteAllText(m2,good1.Replace("build/game/rock_neo_rt_kit.exe","build/game/mml2_rt_kit.exe").Replace(BuildCore.Sha256Of(p1),BuildCore.Sha256Of(p2)));
    Check(BuildCore.StartProblem(p2,G2,data).Contains("another game")&&BuildCore.StartProblem(p2,G1,data).Contains("folder of another game"),"a first-kit marker in the second game's folder: the program starts as neither game");
    string moved=Path.Combine(root,"moved","build","game","mml2_rt_kit.exe");Directory.CreateDirectory(Path.GetDirectoryName(moved));File.Copy(p2,moved);File.Copy(m2,Path.Combine(root,"moved",BuildCore.MarkerName));
    Check(BuildCore.StartProblem(moved,G1,data)==""&&BuildCore.StartProblem(moved,G2,data).Contains("another game"),"outside the folder for built games such a hand-made marker makes it a first-game program, like any file the player chose");
    File.WriteAllText(m2,good2);
    // a copy of a build folder elsewhere keeps its game
    string away=Path.Combine(root,"copy of mml2");Directory.CreateDirectory(Path.Combine(away,"build","game"));
    string pa=Path.Combine(away,"build","game","mml2_rt_kit.exe");File.Copy(p2,pa);File.Copy(m2,Path.Combine(away,BuildCore.MarkerName));
    Check(BuildCore.StartProblem(pa,G2,data)==""&&BuildCore.StartProblem(pa,G1,data).Contains("another game"),"a copied build folder still starts as its own game only");
    // a nearer marker of the right game does not help when the folder's own marker is the other game's
    File.WriteAllText(Path.Combine(out2,"build","game",BuildCore.MarkerName),good1.Replace("build/game/rock_neo_rt_kit.exe","mml2_rt_kit.exe").Replace(BuildCore.Sha256Of(p1),BuildCore.Sha256Of(p2)));
    Check(BuildCore.StartProblem(p2,G1,data).Contains("another game")&&BuildCore.StartProblem(p2,G2,data).Contains("another game"),"two markers that name different games: the program starts as neither");
    File.Delete(Path.Combine(out2,"build","game",BuildCore.MarkerName));

    // three built games side by side: all nine combinations of built program and game to start
    string out3=BuildCore.OutputFolder(data,G3),p3=Path.Combine(out3,"build","game","tron_rt_kit.exe"),m3=Path.Combine(out3,BuildCore.MarkerName);
    Directory.CreateDirectory(Path.GetDirectoryName(p3));File.WriteAllText(p3,"game three");
    string good3=Marker2.Replace("rock_neo_rt_kit.exe","tron_rt_kit.exe").Replace("SLUS-01140","SLUS-00896").Replace("{sha}",BuildCore.Sha256Of(p3));File.WriteAllText(m3,good3);
    var programs3=new[]{p1,p2,p3};var games3=new[]{G1,G2,G3};
    for(int a=0;a<3;a++)for(int b=0;b<3;b++)
      Check((BuildCore.StartProblem(programs3[a],games3[b],data)=="")==(a==b)&&(BuildCore.StartProblem(programs3[a],games3[b],data,null,GameBuild.RealPath,GameBuild.FileId)=="")==(a==b),"built game "+(a+1)+" started as game "+(b+1)+": "+(a==b?"starts":"refused"));
    Check(BuildCore.BuiltProblem(out3,G3,out exe,out hash)==""&&exe==p3&&BuildCore.BuiltProblem(out3,G1,out exe,out hash).Contains("another game")&&BuildCore.BuiltProblem(out3,G2,out exe,out hash).Contains("another game")&&BuildCore.BuiltProblem(out1,G3,out exe,out hash).Contains("another game")&&BuildCore.BuiltProblem(out2,G3,out exe,out hash).Contains("another game"),"the third game's build folder is the third game's only");
    File.WriteAllText(m3,good2.Replace("mml2_rt_kit.exe","tron_rt_kit.exe").Replace(BuildCore.Sha256Of(p2),BuildCore.Sha256Of(p3)));
    Check(BuildCore.StartProblem(p3,G3,data).Length>0&&BuildCore.StartProblem(p3,G2,data).Length>0&&BuildCore.StartProblem(p3,G1,data).Length>0,"a Legends 2 marker in the Tron Bonne folder: the program starts as no game");
    File.WriteAllText(m3,good1.Replace("build/game/rock_neo_rt_kit.exe","build/game/tron_rt_kit.exe").Replace(BuildCore.Sha256Of(p1),BuildCore.Sha256Of(p3)));
    Check(BuildCore.StartProblem(p3,G3,data).Length>0&&BuildCore.StartProblem(p3,G1,data).Length>0,"a first-kit marker (no game) in the Tron Bonne folder: the program starts as no game");
    File.Delete(m3);Check(BuildCore.StartProblem(p3,G3,data).Length>0&&BuildCore.StartProblem(p3,G1,data).Length>0&&BuildCore.FilesFolder(data,G3)=="","the Tron Bonne program without its marker starts as no game, and has no Files folder");
    File.WriteAllText(m3,good3);Check(BuildCore.FilesFolder(data,G3)==out3&&BuildCore.StartProblem(p3,null,data).Length>0&&BuildCore.StartProblem(p3,BuildCore.Game(4),data).Length>0,"with its marker back it has one; a number that is no game starts nothing");
    // the Files button: the build folder of the game, and only when a build of this game is there
    Check(BuildCore.FilesFolder(data,G1)==out1&&BuildCore.FilesFolder(data,G2)==out2&&BuildCore.FilesFolder(data,null)==""&&BuildCore.FilesFolder(Path.Combine(root,"no data"),G1)=="","the Files folder of a built game is its own build folder; no build, no folder");
    File.WriteAllText(m2,good2.Replace("\"state\": \"ready\"","\"state\": \"building\"").Replace("\"exe\": \"build/game/mml2_rt_kit.exe\",","\"exe\": \"C:/Windows/notepad.exe\","));
    Check(BuildCore.FilesFolder(data,G2)==out2,"a build that was begun has its folder too, and a path in the marker is never what is opened");
    File.WriteAllText(m2,good1);Check(BuildCore.FilesFolder(data,G2)=="","a folder with another game's marker is not this game's Files folder");
    File.Delete(m2);Check(BuildCore.FilesFolder(data,G2)=="","a folder without a marker is not shown");
    File.WriteAllText(m2,"not json");Check(BuildCore.FilesFolder(data,G2)=="","a marker that cannot be read: no folder");
    File.WriteAllText(m2,good2);
    Check(GameFilesOpen.Open("")=="The folder was not found."&&GameFilesOpen.Open("relative\\folder")=="The folder was not found."&&GameFilesOpen.Open(Path.Combine(root,"no such folder"))=="The folder was not found."&&GameFilesOpen.Open(p2)=="The folder was not found."&&GameFilesOpen.Open(out2+"\\..\\mml2")=="The folder was not found.","only a folder that exists, by its full path, is ever opened; a file is not");
    string named=Path.Combine(root,"folder.cmd");Directory.CreateDirectory(named);Check(GameFilesOpen.Open(named)=="The folder was not found."&&GameFilesOpen.Open(named+"\\")=="The folder was not found.","a folder whose name has an extension is never given to Windows");
    // a marker planted deep below the real one does not stand in for it
    string deep=Path.Combine(out2,"a","b","c","d","x.exe");Directory.CreateDirectory(Path.GetDirectoryName(deep));File.WriteAllText(deep,"planted");
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(deep),BuildCore.MarkerName),good2.Replace("build/game/mml2_rt_kit.exe","x.exe").Replace(BuildCore.Sha256Of(p2),BuildCore.Sha256Of(deep)));
    Check(BuildCore.BuildFoldersOf(deep).Count==1&&BuildCore.StartProblem(deep,G2,data).Contains("not the program that the build finished")&&BuildCore.StartProblem(deep,G1,data).Length>0,"a file deep inside a build folder with its own marker does not start: the folder's own marker decides");
    // a game that an older kit built still starts (the window asks for a new build); only new builds need the new kit
    File.WriteAllText(m1,good1.Replace("\"kit_version\": 13","\"kit_version\": 12"));
    Check(BuildCore.StartProblem(p1,G1,data)==""&&BuildCore.BuiltProblem(out1,G1,out exe,out hash)==""&&BuildCore.ReadMarker(out1).KitVersion==12&&BuildCore.ReadMarker(out1).KitVersion<G1.OldestKit,"a game that kit 12 built still starts, and its marker shows the older kit");
    File.WriteAllText(m1,good1);
    // another name of the folder for built games (a share of this PC, a mapped drive): found by what the folders are
    Check(GameBuild.FileId(out2)!=null&&GameBuild.FileId(out2)==GameBuild.FileId(out2+"\\")&&GameBuild.FileId(out2)!=GameBuild.FileId(out1)&&GameBuild.FileId(p2)!=GameBuild.FileId(p1)&&GameBuild.FileId(Path.Combine(root,"no such folder"))==null,"one folder has one number under every name; two folders have two");
    string alias=Path.Combine(root,"another name of built");Check(Junction(alias,BuildCore.BuiltRoot(data)),"a second name for the folder for built games");
    try{
      Check(GameBuild.FileId(alias)==GameBuild.FileId(BuildCore.BuiltRoot(data)),"the second name is the same folder");
      string a2=Path.Combine(alias,"mml2","build","game","mml2_rt_kit.exe"),a1=Path.Combine(alias,"mml1","build","game","rock_neo_rt_kit.exe");
      // the path is taken as written here (no true-place lookup), as it is for a share path that Windows does not resolve
      Check(BuildCore.StartProblem(a2,G1,data,null,null,GameBuild.FileId).Contains("another game")&&BuildCore.StartProblem(a1,G2,data,null,null,GameBuild.FileId).Contains("another game")&&BuildCore.StartProblem(a2,G2,data,null,null,GameBuild.FileId)==""&&BuildCore.StartProblem(a1,G1,data,null,null,GameBuild.FileId)=="","under another name of the folder each built game still starts as itself only");
      string aLoose=Path.Combine(alias,"loose.exe"),aDeep=Path.Combine(alias,"mml2","a","b","c","d","x.exe"),aCache=Path.Combine(alias,"mml2","build","zig-cache","o","abc","mml2_rt.exe");
      Directory.CreateDirectory(Path.GetDirectoryName(aCache));File.WriteAllText(aCache,"x");
      foreach(string hidden in new[]{aLoose,aDeep,aCache}){
        Check(File.Exists(hidden)&&BuildCore.StartProblem(hidden,G1,data,null,null,GameBuild.FileId).Length>0&&BuildCore.StartProblem(hidden,G2,data,null,null,GameBuild.FileId).Length>0,"under another name a file inside the folder for built games that no marker proves starts as no game: "+Path.GetFileName(hidden));
        Check(BuildCore.StartProblem(hidden,G1,data)==""||BuildCore.BuildFoldersOf(hidden).Count>0,"(without that lookup the same file would pass: the lookup is what stops it)");
      }
      File.Delete(m1);
      Check(BuildCore.StartProblem(a1,G1,data,null,null,GameBuild.FileId).Length>0&&BuildCore.StartProblem(a1,G2,data,null,null,GameBuild.FileId).Length>0,"under another name a built program whose marker is gone starts as no game");
      File.WriteAllText(m1,good1);
      Check(BuildCore.StartProblem(a2,G2,data,null,null,delegate(string x){return null;}).Contains("cannot be checked")&&BuildCore.StartProblem(a2,G2,data,null,null,delegate(string x){throw new IOException();}).Contains("cannot be checked"),"when the folder for built games cannot be told apart, nothing under another name starts");
      Check(BuildCore.StartProblem(Path.Combine(root,"moved","build","game","mml2_rt_kit.exe"),G1,data,null,GameBuild.RealPath,GameBuild.FileId)==""&&BuildCore.StartProblem(p2,G2,data,null,GameBuild.RealPath,GameBuild.FileId)==""&&BuildCore.StartProblem(p2,G1,data,null,GameBuild.RealPath,GameBuild.FileId).Length>0,"with both lookups, as the launcher asks: a file outside is still not this check's matter, and the built game starts as itself");
    }finally{if(Directory.Exists(alias))Directory.Delete(alias);}
    // a name whose top is BELOW the folder for built games (a share of ...\mml2\build): the walk up never meets that
    // folder, so the program is known by what the file is. The same goes for a hard link, which is one file with two names.
    string below2=Path.Combine(root,"name below built");Check(Junction(below2,Path.Combine(out2,"build")),"a name whose top is below the folder for built games");
    string hard=Path.Combine(root,"hard link","second name.exe");Directory.CreateDirectory(Path.GetDirectoryName(hard));
    try{
      string b2=Path.Combine(below2,"game","mml2_rt_kit.exe");
      Check(File.Exists(b2)&&BuildCore.BuildFoldersOf(b2).Count==0&&GameBuild.FileId(b2)==GameBuild.FileId(p2)&&BuildCore.StartProblem(b2,G1,data)=="","(seen as a path only, that name has no marker above it and would pass)");
      Check(BuildCore.StartProblem(b2,G1,data,null,null,GameBuild.FileId).Contains("another game")&&BuildCore.StartProblem(b2,G2,data,null,null,GameBuild.FileId)=="","a built program under a name whose top is below the folder for built games starts as itself only");
      File.Delete(m2);
      Check(BuildCore.StartProblem(b2,G1,data,null,null,GameBuild.FileId).Length>0&&BuildCore.StartProblem(b2,G2,data,null,null,GameBuild.FileId).Length>0,"and as no game when its marker is gone");
      File.WriteAllText(m2,good2);
      Check(HardLink(hard,p2)&&GameBuild.FileId(hard)==GameBuild.FileId(p2),"a hard link is the same file under a second name");
      Check(BuildCore.StartProblem(hard,G1,data,null,GameBuild.RealPath,GameBuild.FileId).Contains("another game")&&BuildCore.StartProblem(hard,G2,data,null,GameBuild.RealPath,GameBuild.FileId)==""&&BuildCore.StartProblem(hard,G1,data)=="","a hard link to a built program starts as that program's game only (as a path only it would pass)");
      File.WriteAllText(m2,good2.Replace(BuildCore.Sha256Of(p2),new string('0',64)));
      Check(BuildCore.StartProblem(hard,G2,data,null,GameBuild.RealPath,GameBuild.FileId).Length>0,"and not at all when the marker does not prove the program");
      File.WriteAllText(m2,good2);
      string copy=Path.Combine(root,"hard link","a copy.exe");File.Copy(p2,copy);
      Check(BuildCore.StartProblem(copy,G1,data,null,GameBuild.RealPath,GameBuild.FileId)=="","a copy is another file: it is the player's own choice, as before");
    }finally{if(Directory.Exists(below2))Directory.Delete(below2);if(File.Exists(hard))File.Delete(hard);}
    Check(File.Exists(p2)&&BuildCore.StartProblem(p2,G2,data,null,GameBuild.RealPath,GameBuild.FileId)=="","the built program is still there and still starts as itself");
    // the real thing where this PC lets the test reach its own disk as a share
    string share=p2.Length>3&&p2[1]==':'?"\\\\localhost\\"+p2.Substring(0,1)+"$"+p2.Substring(2):null;
    if(share!=null&&File.Exists(share)){
      string shareLoose=share.Substring(0,share.Length-("mml2\\build\\game\\mml2_rt_kit.exe").Length)+"loose.exe";
      Check(BuildCore.StartProblem(share,G1,data,null,GameBuild.RealPath,GameBuild.FileId).Contains("another game")&&BuildCore.StartProblem(share,G2,data,null,GameBuild.RealPath,GameBuild.FileId)==""&&File.Exists(shareLoose)&&BuildCore.StartProblem(shareLoose,G1,data,null,GameBuild.RealPath,GameBuild.FileId).Length>0,"through a share of this PC the built second game starts as the second game only, and a loose file there as no game");
    }else Console.WriteLine("NOTE: this PC does not let the test reach its disk as a share; the share case was tested through a second folder name only");
    // a folder link (junction) into a build folder: the launcher's check goes by the true place of the file
    Check(String.Equals(GameBuild.RealPath(p2),p2,StringComparison.OrdinalIgnoreCase)&&String.Equals(GameBuild.RealPath(out2),out2,StringComparison.OrdinalIgnoreCase)&&GameBuild.RealPath(Path.Combine(root,"no such file.exe"))==null,"the true place of a plain file is the file; a missing file has none");
    string link=Path.Combine(root,"link to build"),linkGame=Path.Combine(root,"link to game"),linkAll=Path.Combine(root,"link to mml2");
    Check(Junction(link,Path.Combine(out2,"build"))&&Junction(linkGame,Path.Combine(out2,"build","game"))&&Junction(linkAll,out2),"junctions can be made for the test");
    try{
      foreach(string through in new[]{Path.Combine(link,"game","mml2_rt_kit.exe"),Path.Combine(linkGame,"mml2_rt_kit.exe"),Path.Combine(linkAll,"build","game","mml2_rt_kit.exe")}){
        Check(File.Exists(through)&&String.Equals(GameBuild.RealPath(through),p2,StringComparison.OrdinalIgnoreCase),"the true place behind a folder link is found");
        Check(BuildCore.StartProblem(through,G1,data,null,GameBuild.RealPath).Contains("another game")&&BuildCore.StartProblem(through,G2,data,null,GameBuild.RealPath)=="","through a folder link the built second game still starts as the second game only");
      }
      File.Delete(m2);
      foreach(string through in new[]{Path.Combine(link,"game","mml2_rt_kit.exe"),Path.Combine(linkGame,"mml2_rt_kit.exe"),Path.Combine(linkAll,"build","game","mml2_rt_kit.exe")})
        Check(BuildCore.StartProblem(through,G1,data,null,GameBuild.RealPath).Length>0&&BuildCore.StartProblem(through,G2,data,null,GameBuild.RealPath).Length>0,"through a folder link a built program whose marker is gone starts as no game");
      File.WriteAllText(m2,good2);
      // the data folder itself reached through a link: the folder for built games is found by its true place too
      string dataLink=Path.Combine(root,"link to data");Check(Junction(dataLink,data),"a junction to the data folder");
      try{Check(BuildCore.StartProblem(p2,G1,dataLink,null,GameBuild.RealPath).Contains("another game")&&BuildCore.StartProblem(Path.Combine(dataLink,"built","mml2","build","game","mml2_rt_kit.exe"),G2,dataLink,null,GameBuild.RealPath)==""&&BuildCore.StartProblem(p1,G1,dataLink,null,GameBuild.RealPath)=="","a data folder that is reached through a link is the same data folder");}
      finally{Directory.Delete(dataLink);}
    }finally{foreach(string j in new[]{link,linkGame,linkAll})if(Directory.Exists(j))Directory.Delete(j);}
    Check(File.Exists(p2)&&File.Exists(m2),"removing the links removed nothing behind them");
    Check(BuildCore.StartProblem(p2,G2,data,null,delegate(string x){return null;}).Contains("cannot be checked")&&BuildCore.StartProblem(p2,G2,data,null,delegate(string x){throw new IOException();}).Contains("cannot be checked")&&BuildCore.StartProblem(Path.Combine(root,"no such file.exe"),G1,data,null,GameBuild.RealPath)=="","a program whose true place cannot be found does not start; a missing file is not this check's matter");
    Check(BuildCore.StartProblem(p2,G2,data,null,delegate(string x){return String.Equals(x,p2,StringComparison.OrdinalIgnoreCase)?x:null;}).Contains("cannot be checked"),"a folder for built games whose true place cannot be found stops the start");

    // the kits
    string dev=Path.Combine(root,"dev"),kitA=Path.Combine(root,"tools","kit one"),kitB=Path.Combine(root,"tools","kit two"),py=Path.Combine(root,"tools","python","python.exe"),zig=Path.Combine(root,"tools","zig","zig.exe");
    foreach(string dir in new[]{dev,kitA,kitB,Path.GetDirectoryName(py),Path.GetDirectoryName(zig)})Directory.CreateDirectory(dir);
    File.WriteAllText(py,"x");File.WriteAllText(zig,"x");
    foreach(string kit in new[]{kitA,kitB})foreach(string name in new[]{"build_game.py","kit-manifest.json"})File.WriteAllText(Path.Combine(kit,name),"list of "+Path.GetFileName(kit));
    const string e1="{\n \"kit\": \"commit f4c9dfd18fc7\",\n \"kit_version\": 13,\n \"zig_version\": \"0.17.1\",\n \"game\": \"Mega Man Legends (USA) SLUS-00603\",\n \"selftest_frames\": 1500\n}";
    const string e2="{\n \"kit\": \"commit 5fa6b7b1f72f\",\n \"kit_version\": 1,\n \"zig_version\": \"0.17.1\",\n \"game\": \"Mega Man Legends 2 (USA) SLUS-01140\",\n \"selftest_frames\": 6000,\n \"missing_ok\": [\n  \"ZNULL.DAT\"\n ]\n}";
    File.WriteAllText(Path.Combine(kitA,"expected.json"),e1);File.WriteAllText(Path.Combine(kitB,"expected.json"),e2);
    string line;
    Check(BuildCore.KitInfo(kitA,out line)==13&&line=="Mega Man Legends (USA) SLUS-00603"&&BuildCore.KitInfo(kitB,out line)==1&&line=="Mega Man Legends 2 (USA) SLUS-01140"&&BuildCore.KitVersionOf(kitB)==1,"the real shape of both kits' expected.json is read, also with a list in it");
    foreach(string bad in new[]{"","not json","[1]","{\"kit_version\": 1,\"kit_version\": 2}","{\"meta\":{\"kit_version\": 99}}","{\"kit_version\": 1,\"a\":[1,2}","{\"kit_version\": 1,\"a\":\"x}","{\"kit_version\": 13}{\"game\": \"Mega Man Legends 2 (USA) SLUS-01140\"}","{\"kit_version\": 13} {}","{}{\"kit_version\": 13}","{\"kit_version\": \"1\"}","{\"kit_version\": 1.5}","{\"kit_version\": -1}","{\"kit_version\": 1,\"game\": \"a\",\"game\": \"b\"}","{\"kit_version\": 1,\"game\": 5}","{\"kit_version\": 1,\"game\": \"a\\\\b\"}","{\"kit_version\": 1} x"}){
      File.WriteAllText(Path.Combine(kitB,"expected.json"),bad);Check(BuildCore.KitInfo(kitB,out line)==0&&line==null,"an expected.json of another shape gives no version: "+bad);
    }
    File.WriteAllText(Path.Combine(kitB,"expected.json"),"{\"a\":{\"kit_version\": 9},\"kit_version\": 12,\"meta\":{\"game\":\"Mega Man Legends 2 (USA) SLUS-01140\"},\"list\":[\"game\",\"kit_version\"]}");
    Check(BuildCore.KitInfo(kitB,out line)==12&&line==null,"only the top level of expected.json counts: a version or a game inside an inner object or a list is not read");
    File.WriteAllText(Path.Combine(kitB,"expected.json"),e2);
    string pointer=Path.Combine(data,BuildCore.ToolsFileName);
    File.WriteAllLines(pointer,new[]{"kit="+kitA,"python="+py,"zig="+zig});
    var f1=BuildCore.Find(dev,data,G1);var f2=BuildCore.Find(dev,data,G2);
    Check(f1.Offered&&f1.Problem==""&&f1.KitVersion==13&&!f2.Offered&&f2.Problem==""&&f2.Kit=="","a pointer file that names the first kit only: the second game's build is not offered");
    File.WriteAllLines(pointer,new[]{"kit2="+kitB,"python="+py,"zig="+zig});
    f1=BuildCore.Find(dev,data,G1);f2=BuildCore.Find(dev,data,G2);
    Check(!f1.Offered&&f2.Offered&&f2.Problem==""&&f2.KitVersion==1&&f2.Kit==kitB&&f2.Python==py&&f2.Zig==zig,"and the other way round; Python and Zig are named once for all kits");
    File.WriteAllLines(pointer,new[]{"kit="+kitB,"kit2="+kitA,"python="+py,"zig="+zig});
    Check(BuildCore.Find(dev,data,G1).Problem.Contains("for another game")&&BuildCore.Find(dev,data,G2).Problem.Contains("for another game"),"a kit in the place of the other game's kit is refused");
    File.WriteAllText(Path.Combine(kitB,"expected.json"),"{\"kit_version\": 1}");File.WriteAllLines(pointer,new[]{"kit="+kitA,"kit2="+kitB,"python="+py,"zig="+zig});
    Check(BuildCore.Find(dev,data,G2).Problem.Contains("for another game")&&BuildCore.Find(dev,data,G1).Problem=="","a second-game kit must say which game it builds");
    File.WriteAllText(Path.Combine(kitB,"expected.json"),e2.Replace("\"kit_version\": 1","\"kit_version\": 0"));Check(BuildCore.Find(dev,data,G2).Problem.Contains("too old"),"a kit below the oldest version of its game is refused");
    File.WriteAllText(Path.Combine(kitB,"expected.json"),e2);
    string pin=BuildCore.Sha256Of(Path.Combine(kitB,"kit-manifest.json"));
    File.WriteAllLines(pointer,new[]{"kit="+kitA,"kit2="+kitB,"kit2-manifest-sha256="+pin.ToLowerInvariant(),"python="+py,"zig="+zig});
    f2=BuildCore.Find(dev,data,G2);Check(f2.Problem==""&&f2.ManifestPin.Length==64&&BuildCore.Find(dev,data,G1).ManifestPin==""&&BuildCore.Find(dev,data,G1).Problem=="","a pinned kit that is unchanged is accepted; the pin is the second kit's only");
    File.WriteAllText(Path.Combine(kitB,"kit-manifest.json"),"another list");
    Check(BuildCore.Find(dev,data,G2).Problem.Contains("not the one that was checked")&&BuildCore.Find(dev,data,G1).Problem=="","a pinned kit whose list changed is refused");
    File.WriteAllLines(pointer,new[]{"kit2="+kitB,"kit2-manifest-sha256=abc","python="+py,"zig="+zig});Check(BuildCore.Find(dev,data,G2).Problem.Contains("could not be read"),"a pin that is not a SHA-256 is refused");
    File.WriteAllLines(pointer,new[]{"kit2="+kitB,"kit-manifest-sha256="+new string('0',64),"python="+py,"zig="+zig});Check(BuildCore.Find(dev,data,G2).Problem=="","the first kit's pin does not touch the second kit");
    // the third kit
    string kitC=Path.Combine(root,"tools","kit three");Directory.CreateDirectory(kitC);foreach(string name in new[]{"build_game.py","kit-manifest.json"})File.WriteAllText(Path.Combine(kitC,name),"list of kit three");
    const string e3="{\n \"kit\": \"commit 67091845d85d\",\n \"kit_version\": 1,\n \"zig_version\": \"0.17.1\",\n \"game\": \"The Misadventures of Tron Bonne (USA) SLUS-00896\",\n \"selftest_frames\": 12000\n}";
    File.WriteAllText(Path.Combine(kitC,"expected.json"),e3);File.WriteAllText(Path.Combine(kitB,"kit-manifest.json"),"list of kit two");
    File.WriteAllLines(pointer,new[]{"kit="+kitA,"kit2="+kitB,"kit3="+kitC,"kit3-manifest-sha256="+BuildCore.Sha256Of(Path.Combine(kitC,"kit-manifest.json")),"python="+py,"zig="+zig});
    var f3=BuildCore.Find(dev,data,G3);
    Check(f3.Offered&&f3.Problem==""&&f3.Kit==kitC&&f3.KitVersion==1&&f3.ManifestPin.Length==64&&BuildCore.Find(dev,data,G1).Kit==kitA&&BuildCore.Find(dev,data,G2).Kit==kitB&&BuildCore.Find(dev,data,G2).ManifestPin=="","three kits side by side: each game finds its own, and a pin is one kit's only");
    File.WriteAllLines(pointer,new[]{"kit="+kitC,"kit2="+kitC,"kit3="+kitB,"python="+py,"zig="+zig});
    Check(BuildCore.Find(dev,data,G1).Problem.Contains("for another game")&&BuildCore.Find(dev,data,G2).Problem.Contains("for another game")&&BuildCore.Find(dev,data,G3).Problem.Contains("for another game"),"the Tron Bonne kit is refused for the other games, and another kit for Tron Bonne");
    File.WriteAllLines(pointer,new[]{"kit="+kitA,"kit2="+kitB,"python="+py,"zig="+zig});Check(!BuildCore.Find(dev,data,G3).Offered,"no kit3 line: the build of the third game is not offered");
    File.Delete(pointer);
    // shipped: one kit folder for each game under "builder"
    string shipped=Path.Combine(root,"launcher"),sb=Path.Combine(shipped,"builder");
    Directory.CreateDirectory(Path.Combine(sb,"python"));Directory.CreateDirectory(Path.Combine(sb,"zig"));File.WriteAllText(Path.Combine(sb,"python","python.exe"),"x");File.WriteAllText(Path.Combine(sb,"zig","zig.exe"),"x");
    Check(!BuildCore.Find(shipped,data,G1).Offered&&!BuildCore.Find(shipped,data,G2).Offered,"a builder folder without a kit folder offers no build");
    Directory.CreateDirectory(Path.Combine(sb,"kit2"));foreach(string name in new[]{"build_game.py","kit-manifest.json"})File.WriteAllText(Path.Combine(sb,"kit2",name),"x");File.WriteAllText(Path.Combine(sb,"kit2","expected.json"),e2);
    f1=BuildCore.Find(shipped,data,G1);f2=BuildCore.Find(shipped,data,G2);
    Check(!f1.Offered&&f2.Offered&&f2.Problem==""&&f2.Kit==Path.Combine(sb,"kit2"),"the shipped folder offers the build of each game that has its kit folder");
  }

  static bool HardLink(string link,string target){
    try{
      var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/c mklink /H \""+link+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
      using(var p=Process.Start(info)){p.StandardOutput.ReadToEnd();p.StandardError.ReadToEnd();p.WaitForExit(10000);return p.ExitCode==0&&File.Exists(link);}
    }catch(Exception){return false;}
  }
  static bool Junction(string link,string target){
    try{
      var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cmd.exe"),"/c mklink /J \""+link+"\" \""+target+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
      using(var p=Process.Start(info)){p.StandardOutput.ReadToEnd();p.StandardError.ReadToEnd();p.WaitForExit(10000);return p.ExitCode==0&&Directory.Exists(link);}
    }catch(Exception){return false;}
  }

  // ---- BuildRun, with the stand-in ----
  sealed class Seen { public List<string> Lines=new List<string>();public int Code=-999;public ManualResetEvent Ended=new ManualResetEvent(false); }
  static GameBuild Go(BuildTools tools,string cue,string data,Seen seen,BuildGame game=null){
    return GameBuild.Start(tools,cue,data,game??G1,delegate(string line){lock(seen.Lines)seen.Lines.Add(line);},delegate(int code){seen.Code=code;seen.Ended.Set();});
  }
  static void Run(string root,string self){
    string toolsDir=Path.Combine(root,"run tools"),data=Path.Combine(root,"run data"),kit=Path.Combine(toolsDir,"kit"),py=Path.Combine(toolsDir,"python","python.exe"),zig=Path.Combine(toolsDir,"zig","zig.exe");
    Directory.CreateDirectory(kit);Directory.CreateDirectory(Path.GetDirectoryName(py));Directory.CreateDirectory(Path.GetDirectoryName(zig));Directory.CreateDirectory(data);
    File.Copy(self,py);File.WriteAllText(zig,"x");
    foreach(string name in new[]{"build_game.py","kit-manifest.json"})File.WriteAllText(Path.Combine(kit,name),"x");
    File.WriteAllText(Path.Combine(kit,"expected.json"),"{\"kit_version\": 13}");
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"kit="+kit,"python="+py,"zig="+zig});
    var tools=BuildCore.Find(Path.Combine(root,"dev"),data,G1);Check(tools.Problem==""&&tools.Offered,"the stand-in tools are found");
    string here=Path.GetDirectoryName(py),plan=Path.Combine(here,"plan.txt"),cue=Path.Combine(root,"my disc","Mega Man Legends (USA).cue"),outFolder=BuildCore.OutputFolder(data,G1);
    var kitBefore=new List<string>();foreach(string f in Directory.GetFiles(kit))kitBefore.Add(Path.GetFileName(f)+"|"+File.ReadAllText(f));

    // a good build
    Environment.SetEnvironmentVariable("RMX_MP","host:47400:00112233445566778899AABBCCDDEEFF:any");Environment.SetEnvironmentVariable("RMX_MP_STATUS","C:\\x");Environment.SetEnvironmentVariable("PYTHONPATH","C:\\planted");Environment.SetEnvironmentVariable("PYTHONSTARTUP","C:\\planted.py");
    File.WriteAllLines(plan,new[]{"stdin","say @RMX kit 12","say Mega Man Legends builder \u00e9\u00e8 \u2713","say @RMX step 1 5 tools","err a line on the error stream","say @RMX step 4 5 build","say @RMX progress compile 3 4","program the new game program","say @RMX step 5 5 self-test","marker "+GoodMarker.Replace("\n"," "),"say @RMX done build/game/rock_neo_rt_kit.exe","exit 0"},new UTF8Encoding(false));
    var seen=new Seen();var run=Go(tools,cue,data,seen);
    Check(seen.Ended.WaitOne(30000)&&seen.Code==0,"a good build ends with code 0");
    Environment.SetEnvironmentVariable("RMX_MP",null);Environment.SetEnvironmentVariable("RMX_MP_STATUS",null);Environment.SetEnvironmentVariable("PYTHONPATH",null);Environment.SetEnvironmentVariable("PYTHONSTARTUP",null);
    var p=new BuildProgress();lock(seen.Lines)foreach(string line in seen.Lines)BuildCore.Apply(p,line);
    Check(p.KitVersion==12&&p.Step==5&&p.DonePath=="build/game/rock_neo_rt_kit.exe"&&seen.Lines.Contains("Mega Man Legends builder \u00e9\u00e8 \u2713")&&seen.Lines.Contains("a line on the error stream"),"every line arrives, also letters outside ASCII and the error stream");
    string[] got=File.ReadAllLines(Path.Combine(here,"received-args.txt"));
    Check(String.Join("|",got)=="-B|"+Path.Combine(kit,"build_game.py")+"|--cue|"+cue+"|--out|"+outFolder+"|--zig|"+zig,"the stand-in received exactly the contract's command, with paths that hold spaces and brackets");
    Check(File.ReadAllLines(Path.Combine(here,"received-env.txt")).Length==0,"no RMX_ and no PYTHON setting of this launcher reaches the build");
    string typed=File.ReadAllText(Path.Combine(here,"stdin-end.txt"));Check(typed=="ended after 0 bytes"||typed=="ended after 3 bytes","the build cannot wait for typed input: its input ends at once ("+typed+")");
    string exe,hash;Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash)==""&&BuildCore.StartProblem(exe,G1,data)=="","the result of the good build passes the start check");
    Check(!run.Running&&!run.Cancelled&&!File.Exists(BuildCore.LockPath(data,G1))&&!BuildCore.BuildRunning(data,G1),"after the build the gate file is gone");
    run.Cancel();Check(!run.Cancelled,"cancel after a build has ended does not call it cancelled");
    string kept=File.ReadAllText(BuildCore.LogPath(data,G1));Check(kept.Contains("@RMX done")&&kept.Contains("a line on the error stream")&&kept.Contains("\u2713"),"the text of the last build is kept for a report");

    // a build that the kit stops
    File.WriteAllLines(plan,new[]{"say @RMX kit 12","say @RMX step 3 5 check-disc","say ","say STOPPED: this disc is another game (SLPS-01141).","say @RMX stop 21 wrong-disc","exit 21"});
    seen=new Seen();Go(tools,cue,data,seen);Check(seen.Ended.WaitOne(30000)&&seen.Code==21,"a stopped build ends with the kit's code");
    p=new BuildProgress();lock(seen.Lines)foreach(string line in seen.Lines)BuildCore.Apply(p,line);
    Check(p.StopCode==21&&p.StopReason.Contains("another game")&&p.DonePath=="","and its reason is kept");
    Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash)=="","the earlier good build is still accepted (the stand-in changed nothing)");
    Check(!File.ReadAllText(BuildCore.LogPath(data,G1)).Contains("@RMX done"),"the kept text is of the last build only");

    // two builds at once, and cancel with a child process
    File.WriteAllLines(plan,new[]{"say @RMX kit 12","child","say @RMX step 4 5 build","sleep 60000","exit 0"});
    seen=new Seen();run=Go(tools,cue,data,seen);
    string pidFile=Path.Combine(here,"child-pid.txt");if(File.Exists(pidFile))File.Delete(pidFile);
    for(int i=0;i<100&&!File.Exists(pidFile);i++)Thread.Sleep(100);
    Thread.Sleep(300);int child=Int32.Parse(File.ReadAllText(pidFile));
    Check(run.Running&&Alive(child)&&run.Tied&&BuildCore.BuildRunning(data,G1),"the build and its child process run, tied to this program, and the gate file says so");
    bool refused=false;try{Go(tools,cue,data,new Seen());}catch(InvalidOperationException e){refused=e.Message.Contains("Another build");}
    Check(refused,"a second build into the same folder is refused while the first runs");
    var watch=Stopwatch.StartNew();run.Cancel();
    Check(seen.Ended.WaitOne(20000)&&watch.ElapsedMilliseconds<20000,"cancel ends the build");
    for(int i=0;i<50&&Alive(child);i++)Thread.Sleep(100);
    Check(!Alive(child)&&!run.Running&&run.Cancelled,"cancel also ends the child process, without the kit's own tie");
    Check(seen.Code!=0&&BuildCore.ExitText(seen.Code,G1).Contains("ended or crashed"),"a cancelled build has a code that says ended");
    for(int i=0;i<30&&File.Exists(BuildCore.LockPath(data,G1));i++)Thread.Sleep(100);
    Check(!File.Exists(BuildCore.LockPath(data,G1)),"and the gate file is gone, so a new build can start");
    run.Cancel();Check(true,"cancel after the end does nothing");

    // the build ends, but a child process lives on and holds its output: the end is not kept waiting for it
    File.Delete(pidFile);File.WriteAllLines(plan,new[]{"say @RMX kit 12","child","sleep 400","say @RMX done x","exit 0"});
    seen=new Seen();watch=Stopwatch.StartNew();run=Go(tools,cue,data,seen);
    Check(seen.Ended.WaitOne(30000)&&seen.Code==0&&watch.ElapsedMilliseconds<15000,"a child that outlives the build does not hold up its end ("+watch.ElapsedMilliseconds+" ms)");
    child=Int32.Parse(File.ReadAllText(pidFile));for(int i=0;i<50&&Alive(child);i++)Thread.Sleep(100);
    Check(!Alive(child)&&!File.Exists(BuildCore.LockPath(data,G1)),"and it is ended with the build, by the tie");
    // a tool that does not run
    var broken=new BuildTools{Kit=kit,Python=Path.Combine(here,"gone","python.exe"),Zig=zig};
    refused=false;try{Go(broken,cue,data,new Seen());}catch(InvalidOperationException e){refused=e.Message.Contains("could not be started");}
    Check(refused&&!File.Exists(BuildCore.LockPath(data,G1)),"a Python that does not start gives a sentence, and the gate file is gone");
    File.WriteAllLines(plan,new[]{"say @RMX kit 12","exit 0"});seen=new Seen();Go(tools,cue,data,seen);Check(seen.Ended.WaitOne(30000)&&seen.Code==0,"and the next build starts");

    // the second game: its own kit, its own folder, and a marker that names the game
    string kit2=Path.Combine(toolsDir,"kit2");Directory.CreateDirectory(kit2);
    foreach(string name in new[]{"build_game.py","kit-manifest.json"})File.WriteAllText(Path.Combine(kit2,name),"x");
    File.WriteAllText(Path.Combine(kit2,"expected.json"),"{\n \"kit_version\": 1,\n \"game\": \"Mega Man Legends 2 (USA) SLUS-01140\",\n \"missing_ok\": [\n  \"ZNULL.DAT\"\n ]\n}");
    File.WriteAllLines(Path.Combine(data,BuildCore.ToolsFileName),new[]{"kit="+kit,"kit2="+kit2,"python="+py,"zig="+zig});
    var tools2=BuildCore.Find(Path.Combine(root,"dev"),data,G2);Check(tools2.Problem==""&&tools2.Offered&&tools2.Kit==kit2&&tools2.KitVersion==1&&BuildCore.Find(Path.Combine(root,"dev"),data,G1).Kit==kit,"each game finds its own kit");
    string out2=BuildCore.OutputFolder(data,G2);
    File.WriteAllLines(plan,new[]{"say @RMX kit 1","say @RMX game SLUS-01140","say @RMX step 4 5 build","child","program the second game","marker "+Marker2.Replace("\n"," "),"sleep 1500","say @RMX done build/game/rock_neo_rt_kit.exe","exit 0"},new UTF8Encoding(false));
    if(File.Exists(pidFile))File.Delete(pidFile);
    seen=new Seen();run=Go(tools2,cue,data,seen,G2);
    for(int i=0;i<100&&!File.Exists(pidFile);i++)Thread.Sleep(100);
    Check(BuildCore.BuildRunning(data,G2)&&!BuildCore.BuildRunning(data,G1)&&BuildCore.AnyBuildRunning(data),"while the second game is built, its own gate file is held, and any build counts");
    Check(seen.Ended.WaitOne(30000)&&seen.Code==0,"a good build of the second game ends with code 0");
    got=File.ReadAllLines(Path.Combine(here,"received-args.txt"));
    Check(String.Join("|",got)=="-B|"+Path.Combine(kit2,"build_game.py")+"|--cue|"+cue+"|--out|"+out2+"|--zig|"+zig,"the second game's command names its own kit and its own output folder");
    p=new BuildProgress();lock(seen.Lines)foreach(string line in seen.Lines)BuildCore.Apply(p,line);
    Check(p.Game=="SLUS-01140"&&p.KitVersion==1,"the kit's game line is kept");
    string exe2,hash2;
    Check(BuildCore.BuiltProblem(out2,G2,out exe2,out hash2)==""&&BuildCore.StartProblem(exe2,G2,data)==""&&BuildCore.StartProblem(exe2,G1,data).Contains("another game")&&BuildCore.BuiltProblem(out2,G1,out exe,out hash).Contains("another game"),"the built second game starts as the second game only");
    Check(BuildCore.BuiltProblem(outFolder,G1,out exe,out hash)==""&&BuildCore.StartProblem(exe,G2,data).Contains("another game")&&BuildCore.StartProblem(exe,G1,data)=="","and the first game's build is untouched and starts as the first game only");
    Check(!BuildCore.AnyBuildRunning(data)&&File.ReadAllText(BuildCore.LogPath(data,G2)).Contains("@RMX game SLUS-01140")&&!File.ReadAllText(BuildCore.LogPath(data,G1)).Contains("SLUS-01140"),"each game keeps the text of its own last build");
    foreach(string f in Directory.GetFiles(kit2))Check(Path.GetFileName(f)=="build_game.py"||Path.GetFileName(f)=="kit-manifest.json"||Path.GetFileName(f)=="expected.json","nothing was written into the second kit");

    var kitAfter=new List<string>();foreach(string f in Directory.GetFiles(kit))kitAfter.Add(Path.GetFileName(f)+"|"+File.ReadAllText(f));
    Check(String.Join("\n",kitBefore.ToArray())==String.Join("\n",kitAfter.ToArray())&&Directory.GetDirectories(kit).Length==0,"the launcher wrote nothing into the kit folder");
    var beside=new List<string>();foreach(string f in Directory.GetFileSystemEntries(Path.Combine(data,"built")))beside.Add(Path.GetFileName(f));beside.Sort(StringComparer.Ordinal);
    Check(String.Join(",",beside.ToArray())=="mml1,mml1-last-build.txt,mml2,mml2-last-build.txt","beside the output folders there is only the text of each game's last build");
  }
}
