using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

// Tests for GameRuntime.cs on a scratch folder with a stand-in program. This test program is its own stand-in:
// run with "--identity" it answers like a contract 2.0 game program; run with "--live" as its first argument it
// records the arguments it was given and exits with a chosen code.
// No real game program, disc, card or launcher setting is touched.
public static class GameRuntimePureTest {
  static int failures=0;
  static void Check(bool ok,string name){if(!ok){failures++;Console.WriteLine("FAIL: "+name);}}
  static string TakeLikeGame(string f){File.Move(f,f+".taken");string txt=File.ReadAllText(f+".taken");File.Delete(f+".taken");return txt;}   // what the game does with the control file
  static bool WaitForFile(string f,int ms){for(int t=0;t<ms;t+=20){if(File.Exists(f))return true;System.Threading.Thread.Sleep(20);}return File.Exists(f);}
  static void Throws<T>(Action action,string name) where T:Exception {try{action();failures++;Console.WriteLine("FAIL (no error): "+name);}catch(T){}}
  static string Here {get{return Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);}}

  // Stand-in for "--identity": files beside the stand-in choose what kind of build it pretends to be.
  static int StandInIdentity(){
    File.AppendAllText(Path.Combine(Here,"identity-asked.txt"),"asked\r\n"); // the trace the tests use to see whether this program was run at all
    if(File.Exists(Path.Combine(Here,"identity-refuses.txt"))){Console.Error.WriteLine("unknown argument --identity");return 2;} // like the 1.x program
    string contract=File.Exists(Path.Combine(Here,"identity-contract.txt"))?File.ReadAllText(Path.Combine(Here,"identity-contract.txt")).Trim():"2.0";
    string features=File.Exists(Path.Combine(Here,"identity-features.txt"))?File.ReadAllText(Path.Combine(Here,"identity-features.txt")).Trim():"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume";
    string programSays=File.Exists(Path.Combine(Here,"identity-program.txt"))?File.ReadAllText(Path.Combine(Here,"identity-program.txt")).Trim():"rock_neo_rt",gameSays=File.Exists(Path.Combine(Here,"identity-game.txt"))?File.ReadAllText(Path.Combine(Here,"identity-game.txt")).Trim():"Mega Man Legends (USA)";
    Console.WriteLine("program="+programSays);Console.WriteLine("contract="+contract);Console.WriteLine("build=2026-11-16 10:00:00");Console.WriteLine("game="+gameSays);Console.WriteLine("features="+features);
    return 0;
  }
  // Stand-in for play mode: writes what it received beside itself, a session log in --run-dir, and exits with the code in stand-in-exit.txt.
  static int StandIn(string[] args){
    string here=Here;
    File.WriteAllLines(Path.Combine(here,"received.txt"),args);File.WriteAllLines(Path.Combine(here,"received-env.txt"),new[]{Environment.GetEnvironmentVariable("RMX_MP")??"(none)",Environment.GetEnvironmentVariable("RMX_MP_STATUS")??"(none)"});
    int code=0;string plan=Path.Combine(here,"stand-in-exit.txt");
    if(File.Exists(plan)){string text=File.ReadAllText(plan).Trim();if(text=="sleep"){Thread.Sleep(5000);return 0;}code=Int32.Parse(text);}
    string run=null,fail=null;for(int i=0;i<args.Length-1;i++){if(args[i]=="--run-dir")run=args[i+1];if(args[i]=="--fail-file")fail=args[i+1];}
    Console.Error.WriteLine("RUN END and a state report always go to standard error, even on exit 0");
    if(code==2){Console.Error.WriteLine("unknown argument --example");if(fail!=null)File.WriteAllText(fail,"exit=2\r\nreason=unknown argument, or an option without its value: '--example'\r\n");return 2;}
    if(run!=null&&Directory.Exists(run)){
      var log=new StringBuilder("build: rock_neo_rt, contract 2.0\r\nwindow open, 320x240 x3\r\n");
      if(code==3)log.Append("TRAP: cannot open disc image\r\n");
      log.Append("state report line\r\nRUN END: "+(code==0?"window closed":"stopped")+" (exit code "+code+")\r\nsession summary follows\r\nlast line\r\n");
      File.WriteAllText(Path.Combine(run,"live_test.log"),log.ToString());
    }
    return code;
  }
  static string Expected(string cue,string data,string cardOption,int scale,int volume,bool mute){
    return "--live --exe-from-disc ROCK_NEO.EXE --run-dir \""+GameRuntime.SessionsPath(data)+"\" --cue \""+cue+"\" --card0 \""+GameRuntime.CardPath(data)+"\" "+cardOption+" --keys \""+GameRuntime.KeysPath(data)+"\" --scale "+scale+" --volume "+volume+(mute?" --mute":"")+" --fail-file \""+GameRuntime.FailFilePath(data)+"\"";
  }

    // ---- Mega Man Legends 2 (the game side's contract L2-1): the same code with its own program, folder, card and start line ----
  static void LegendsTwo(string root,string folder,string program,string cue,string data,string known,RuntimeSettings s){
      var k2=GameRuntime.Legends2;string folder2=Path.Combine(root,"my game 2 folder");Directory.CreateDirectory(folder2);
      string program2=Path.Combine(folder2,"mml2_rt_014.exe");File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location,program2);using(var add=new FileStream(program2,FileMode.Append))add.WriteByte(7); // one more byte: a different hash, so it is asked for its own identity
      File.WriteAllText(Path.Combine(folder2,"identity-contract.txt"),"2.6");File.WriteAllText(Path.Combine(folder2,"identity-program.txt"),"mml2_rt");File.WriteAllText(Path.Combine(folder2,"identity-game.txt"),"Mega Man Legends 2 (USA) SLUS-01140");
      File.WriteAllText(Path.Combine(folder2,"identity-features.txt"),"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid"); // the features= line of contract L2-1
      // the Legends 1 stand-in answers plainly again, whatever the tests above left beside it
      foreach(string left in new[]{"identity-refuses.txt","identity-features.txt","identity-program.txt","identity-game.txt"})if(File.Exists(Path.Combine(folder,left)))File.Delete(Path.Combine(folder,left));
      File.WriteAllText(Path.Combine(folder,"identity-contract.txt"),"2.6");GameRuntime.ForgetHashes();
      string known2=GameRuntime.FileSha256(program2);Check(known2!=known,"the two stand-in programs have different hashes");
      string cue2=Path.Combine(folder2,"Mega Man Legends 2 (USA).cue");File.WriteAllText(cue2,"FILE \"track.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n");File.WriteAllBytes(Path.Combine(folder2,"track.bin"),new byte[16]);
      string dataTwo=Path.Combine(root,"games","mml2");
      var s2=new RuntimeSettings{Program=program2,WindowHeight=720,Volume=80,OriginalPicture=true};
      var r2=GameRuntime.Readiness(s2,cue2,dataTwo,known2,true,k2);
      Check(r2.Ready&&r2.KnownBuild&&r2.HasWindowHeight&&r2.HasPictureOption,"Legends 2 is ready with its own program and disc image: "+r2.Problem);
      Check(!r2.HasMovementOption&&!r2.HasThirdPerson&&!r2.HasPadMap&&!r2.HasLookOptions&&!r2.HasRumble&&!r2.HasZTarget,"Legends 2 reports none of the Legends 1 game-code features");
      string line2=GameRuntime.BuildArguments(s2,cue2,dataTwo,r2.HasPictureOption,r2.HasWindowHeight,r2.HasMovementOption,r2.HasThirdPerson,r2.HasPadMap,r2.HasLookOptions,r2.HasRumble,r2.HasZTarget,k2);
      string want2="--live --exe-from-disc SLUS_011.40 --run-dir \""+GameRuntime.SessionsPath(dataTwo)+"\" --cue \""+cue2+"\" --card0 \""+GameRuntime.CardPath(dataTwo)+"\" --card-new --keys \""+GameRuntime.KeysPath(dataTwo)+"\" --window-height 720 --volume 80 --fail-file \""+GameRuntime.FailFilePath(dataTwo)+"\" --original-picture";
      Check(line2==want2,"the Legends 2 start line is exactly the one of contract L2-1 section 3: "+line2);
      // every choice that belongs to Legends 1 game code stays off the line, whatever the settings say
      var loud=new RuntimeSettings{Program=program2,WindowHeight=1080,Volume=50,Mute=true,OriginalMovement=true,OriginalControls=true,NoRumble=true,RumbleStrength=40,InvertLookY=true,LookSensitivity=200,ZTargetOff=true,AimStyleCamera=true,Scale=5};
      string loudLine=GameRuntime.BuildArguments(loud,cue2,dataTwo,r2.HasPictureOption,r2.HasWindowHeight,r2.HasMovementOption,r2.HasThirdPerson,r2.HasPadMap,r2.HasLookOptions,r2.HasRumble,r2.HasZTarget,k2);
      foreach(string never in new[]{"--movement","--controls","--pad-map","--rumble","--no-rumble","--look-","--lockon","--aim-style","--scale","--settings","--present","--internal","--card-from-gme","ROCK_NEO","mml1"})Check(!loudLine.Contains(never),"the Legends 2 start line never has "+never+": "+loudLine);
      Check(loudLine.Contains(" --window-height 1080 --volume 50 --mute "),"size, volume and mute are passed for Legends 2: "+loudLine);
      // a .gme save is never used for Legends 2, even when one lies in its folder, and cannot be copied in
      Directory.CreateDirectory(dataTwo);File.WriteAllBytes(GameRuntime.ImportPath(dataTwo),new byte[GameRuntime.GmeBytes]);
      string withGme=GameRuntime.BuildArguments(s2,cue2,dataTwo,true,true,false,false,false,false,false,false,k2);Check(!withGme.Contains("--card-from-gme")&&withGme.Contains(" --card-new "),"a .gme in the Legends 2 folder is not used: "+withGme);
      File.Delete(GameRuntime.ImportPath(dataTwo));
      string gme2=Path.Combine(folder2,"legends1-save.gme");File.WriteAllBytes(gme2,new byte[GameRuntime.GmeBytes]);
      Throws<InvalidDataException>(delegate{GameRuntime.AdoptCard(gme2,dataTwo,false);},"a .gme save cannot be copied in for Legends 2");Check(!GameRuntime.HasImport(dataTwo)&&!GameRuntime.HasCard(dataTwo),"the refused .gme left nothing in the Legends 2 folder");
      // each slot starts only its own game
      var wrong1=GameRuntime.Readiness(new RuntimeSettings{Program=program2,AcceptedHash=known2},cue,data,known,true);
      Check(!wrong1.Ready&&wrong1.Problem.Contains("This is the Mega Man Legends 2 game program"),"the Legends 1 slot refuses a program that says it is Legends 2: "+wrong1.Problem);
      var wrong2=GameRuntime.Readiness(new RuntimeSettings{Program=program,AcceptedHash=known},cue2,dataTwo,known2,true,k2);
      Check(!wrong2.Ready&&wrong2.Problem.Contains("This is not the Mega Man Legends 2 game program"),"the Legends 2 slot refuses a program that does not say it is Legends 2: "+wrong2.Problem);
      Throws<InvalidOperationException>(delegate{GameRuntime.Start(new RuntimeSettings{Program=program,AcceptedHash=known},cue2,dataTwo,known2,k2,null);},"Start refuses the wrong program for the Legends 2 slot");
      var half=new System.Collections.Generic.Dictionary<string,string>{{"game","Mega Man Legends 2 (USA) SLUS-01140"},{"program","rock_neo_rt"}};Check(GameRuntime.CheckSlot(k2,half).Length>0&&GameRuntime.CheckSlot(GameRuntime.Legends1,half).Length>0,"a program whose two answers disagree is refused by both slots");
      Check(GameRuntime.CheckSlot(GameRuntime.Legends1,new System.Collections.Generic.Dictionary<string,string>()).Length==0&&GameRuntime.CheckSlot(k2,new System.Collections.Generic.Dictionary<string,string>()).Length>0,"an older Legends 1 build that names no game still fits slot 1; slot 2 needs the exact answer");
      var unknown2=GameRuntime.Readiness(s2,cue2,dataTwo,"00",true,k2);Check(!unknown2.Ready&&unknown2.NeedsAcknowledgement,"an unknown Legends 2 build needs the user's confirmation, like Legends 1");
      Check(GameRuntime.Readiness(new RuntimeSettings(),cue2,dataTwo,known2,true,k2).Problem.Contains("mml2_rt_018.exe"),"the Legends 2 slot asks for the Legends 2 program by name");
      Check(GameRuntime.Readiness(new RuntimeSettings(),cue,data,known,true).Problem=="Choose your game program (rock_neo_rt_hc2.exe or rock_neo_rt_fd.exe).","the Legends 1 sentence is unchanged");
      // the two games never share a folder, a card, a key map, a session folder or a marker
      Check(GameRuntime.DataFolderFor(1)==GameRuntime.DataFolder&&GameRuntime.DataFolderFor(2)!=GameRuntime.DataFolder&&GameRuntime.DataFolderFor(2).EndsWith(@"games\mml2",StringComparison.Ordinal)&&GameRuntime.DataFolderFor(7)==GameRuntime.DataFolder,"Legends 2 has its own data folder; any other number is Legends 1");
      foreach(string mine in new[]{GameRuntime.CardPath(dataTwo),GameRuntime.KeysPath(dataTwo),GameRuntime.SessionsPath(dataTwo),GameRuntime.LockPath(dataTwo),GameRuntime.FailFilePath(dataTwo),GameRuntime.ImportPath(dataTwo)})Check(!mine.StartsWith(data+"\\",StringComparison.OrdinalIgnoreCase),"no Legends 2 file is in the Legends 1 folder: "+mine);
      Check(GameRuntime.GateName(dataTwo)!=GameRuntime.GateName(data),"each game has its own card gate");
      // settings: one entry for each game; saving one never loses the other
      string twoPath=Path.Combine(root,"runtime-two.xml");var a1=new RuntimeSettings{Program=program,Volume=40};GameRuntime.Save(twoPath,a1);
      string alone=File.ReadAllText(twoPath);Check(alone.Contains("id=\"1\"")&&!alone.Contains("id=\"2\""),"saving Legends 1 alone writes no Legends 2 entry");
      Check(GameRuntime.Load(twoPath,2).Program=="","no Legends 2 entry means default Legends 2 settings");
      GameRuntime.Save(twoPath,s2,2);var b1=GameRuntime.Load(twoPath);var b2=GameRuntime.Load(twoPath,2);
      Check(b1.Program==program&&b1.Volume==40&&b2.Program==program2&&b2.Volume==80&&b2.OriginalPicture,"saving Legends 2 keeps the Legends 1 entry");
      a1.Volume=55;GameRuntime.Save(twoPath,a1);Check(GameRuntime.Load(twoPath,2).Program==program2&&GameRuntime.Load(twoPath,2).Volume==80&&GameRuntime.Load(twoPath).Volume==55,"saving Legends 1 keeps the Legends 2 entry");
      File.WriteAllText(twoPath,"<other/>");Throws<InvalidDataException>(delegate{GameRuntime.Save(twoPath,s2,2);},"Legends 2 settings are not saved over a file whose Legends 1 entry cannot be read");Check(File.ReadAllText(twoPath)=="<other/>","that file was left as it was");
      string onlyTwo=Path.Combine(root,"runtime-only-two.xml");GameRuntime.Save(onlyTwo,s2,2);Check(GameRuntime.Load(onlyTwo,2).Program==program2&&GameRuntime.Load(onlyTwo).Program=="","Legends 2 can be saved first");
      // a stand-in start of Legends 2: its own folder is prepared, no gamepad map template is written, the line is the tested one
      File.WriteAllText(Path.Combine(folder2,"stand-in-exit.txt"),"0");
      var g2=GameRuntime.Start(s2,cue2,dataTwo,known2,k2,new[]{data});g2.Process.WaitForExit();
      Check(g2.Arguments==want2&&g2.Process.ExitCode==0,"Legends 2 started with the tested line: "+g2.Arguments);
      string[] got2=File.ReadAllLines(Path.Combine(folder2,"received.txt"));Check(Array.IndexOf(got2,"SLUS_011.40")==2&&Array.IndexOf(got2,"--card-new")>0&&Array.IndexOf(got2,"ROCK_NEO.EXE")<0,"the Legends 2 stand-in received its own disc program name");
      Check(File.Exists(GameRuntime.KeysPath(dataTwo))&&Directory.Exists(GameRuntime.SessionsPath(dataTwo))&&!File.Exists(GameRuntime.PadMapPath(dataTwo)),"Legends 2 gets a key map and a session folder, and no gamepad map template");
      GameRuntime.ReleaseLock(dataTwo);Check(!GameRuntime.CardInUse(dataTwo),"the Legends 2 card is free after its game ended");
      // one game at a time across both
      File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"sleep");var hold=GameRuntime.Start(s,cue,data,known);Thread.Sleep(300);
      Throws<InvalidOperationException>(delegate{GameRuntime.Start(s2,cue2,dataTwo,known2,k2,new[]{data});},"Legends 2 is not started while Legends 1 runs");
      Check(!GameRuntime.CardInUse(dataTwo),"the refused Legends 2 start left no marker");
      hold.Process.WaitForExit();GameRuntime.ReleaseLock(data);File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");
      // two launchers pressing Play on the two games at the same moment: only one game starts
      File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"sleep");File.WriteAllText(Path.Combine(folder2,"stand-in-exit.txt"),"sleep");
      for(int round=0;round<3;round++){
        GameSession one=null,two=null;var go=new ManualResetEvent(false);
        var first=new Thread(delegate(){go.WaitOne();try{one=GameRuntime.Start(s,cue,data,known,null,new[]{dataTwo});}catch(InvalidOperationException){}});
        var second=new Thread(delegate(){go.WaitOne();try{two=GameRuntime.Start(s2,cue2,dataTwo,known2,k2,new[]{data});}catch(InvalidOperationException){}});
        first.Start();second.Start();go.Set();first.Join();second.Join();
        Check((one==null)!=(two==null),"two starts at the same moment: exactly one of the two games starts (round "+round+")");
        if(one!=null)one.Process.WaitForExit();if(two!=null)two.Process.WaitForExit();
        GameRuntime.ReleaseLock(data);GameRuntime.ReleaseLock(dataTwo);
      }
      File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");File.WriteAllText(Path.Combine(folder2,"stand-in-exit.txt"),"0");
      // the analog pad switch (contract L2-2): passed only for Legends 2, only when ticked, only to a program that reports pad-type
      var stick=new RuntimeSettings{Program=program2,WindowHeight=720,Volume=80,OriginalPicture=true,AnalogPad=true};
      Check(GameRuntime.BuildArguments(stick,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,false,true)==want2+" --pad-type analog","the analog pad choice adds exactly --pad-type analog at the end");
      Check(!GameRuntime.BuildArguments(stick,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,false,false).Contains("--pad-type"),"no --pad-type for a program that does not report it (build 014)");
      Check(GameRuntime.BuildArguments(s2,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,false,true)==want2,"with the switch off nothing is passed: the program keeps its digital pad");
      Check(!GameRuntime.BuildArguments(stick,cue,data,true,true,false,false,false,false,false,false,null,false,true).Contains("--pad-type"),"--pad-type is never passed to Legends 1");
      Check(!r2.HasPadType,"a program whose features lack pad-type does not offer the switch");
      // the free camera of Legends 2 (contract L2-4): passed only for Legends 2, only when ticked, only to a program that reports free-camera
      var fcam=new RuntimeSettings{Program=program2,WindowHeight=720,Volume=80,OriginalPicture=true,FreeCamera=true,LookSensitivity=150,InvertLookY=true};
      Check(GameRuntime.BuildArguments(fcam,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,freeCameraOption:true)==want2+" --mv2-free --mv2-look-speed 150 --mv2-look-invert 1","the free camera choice adds exactly --mv2-free, the speed and the invert choice at the end");
      Check(!GameRuntime.BuildArguments(fcam,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,freeCameraOption:false).Contains("--mv2"),"no --mv2 option for a program that does not report free-camera");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings{Program=program2,WindowHeight=720,Volume=80,OriginalPicture=true,LookSensitivity=150},cue2,dataTwo,true,true,false,false,false,false,false,false,k2,freeCameraOption:true).Contains("--mv2"),"with the switch off nothing is passed");
      Check(!GameRuntime.BuildArguments(fcam,cue,data,true,true,false,false,false,false,false,false,null,freeCameraOption:true).Contains("--mv2"),"--mv2 options are never passed to Legends 1");
      var fdist=new RuntimeSettings{Program=program2,WindowHeight=720,Volume=80,OriginalPicture=true,FreeCamera=true,LookSensitivity=150,InvertLookY=true,CameraDistance=130};
      Check(GameRuntime.BuildArguments(fdist,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,freeCameraOption:true,freeCameraDistanceOption:true)==want2+" --mv2-free --mv2-look-speed 150 --mv2-look-invert 1 --mv2-camera-distance 130","the free camera distance is added only for a program that reports free-camera-distance");
      Check(!GameRuntime.BuildArguments(fdist,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,freeCameraOption:true,freeCameraDistanceOption:false).Contains("--mv2-camera-distance"),"a program with free-camera but not free-camera-distance (build 023) is not given the option");
      // the Legends 2 look of the lock-on marker (contract 2.29, L16): Legends 1 only, only when ticked, only for a program that reports lock-marker-2
      var lm2=new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,OriginalPicture=true,LockMarker2=true};
      Check(GameRuntime.BuildArguments(lm2,cue,data,true,true,false,false,false,false,false,false,null,lockMarker2Option:true).EndsWith(" --lock-marker 2"),"the marker choice adds --lock-marker 2 at the end for Legends 1");
      Check(!GameRuntime.BuildArguments(lm2,cue,data,true,true,false,false,false,false,false,false,null,lockMarker2Option:false).Contains("--lock-marker"),"no --lock-marker for a program that does not report lock-marker-2");
      Check(new RuntimeSettings().LockMarker2,"the Legends 2 marker is on by default (owner, 2026-11-22)");
      {string lmPath=Path.Combine(root,"runtime-lockmarker.xml");var off=new RuntimeSettings{Program=program,LockMarker2=false};GameRuntime.Save(lmPath,off);
       Check(!GameRuntime.Load(lmPath).LockMarker2,"a player who switched the Legends 2 marker off keeps it off");
       GameRuntime.Save(lmPath,new RuntimeSettings{Program=program});Check(GameRuntime.Load(lmPath).LockMarker2,"switched on, it stays on");
       string old=File.ReadAllText(lmPath);old=System.Text.RegularExpressions.Regex.Replace(old," lockMarker=\"[a-z]+\"","");old=old.Replace("lockMarker2=\"1\"","lockMarker2=\"0\"");File.WriteAllText(lmPath,old);
       Check(!old.Contains("lockMarker=")&&old.Contains("lockMarker2=\"0\"")&&GameRuntime.Load(lmPath).LockMarker2,"a settings file from 0.4.0 (lockMarker2=\"0\" written for a row nobody saw) gets the marker on");}
      Check(!GameRuntime.BuildArguments(new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,OriginalPicture=true,LockMarker2=false},cue,data,true,true,false,false,false,false,false,false,null,lockMarker2Option:true).Contains("--lock-marker"),"with the choice off nothing is passed: the program keeps its single arrow");
      Check(!GameRuntime.BuildArguments(lm2,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,lockMarker2Option:true).Contains("--lock-marker"),"--lock-marker is never passed to Legends 2");
      var nf=new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,OriginalPicture=true,NoLockFlick=true};
      Check(GameRuntime.BuildArguments(nf,cue,data,true,true,false,false,false,false,false,false,null,lockFlickOption:true).EndsWith(" --lock-flick 0"),"only the off choice of the flick is passed (on is the program's default)");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,OriginalPicture=true},cue,data,true,true,false,false,false,false,false,false,null,lockFlickOption:true).Contains("--lock-flick"),"with the flick on nothing is passed");
      Check(!GameRuntime.BuildArguments(nf,cue,data,true,true,false,false,false,false,false,false,null,lockFlickOption:false).Contains("--lock-flick"),"no --lock-flick for a program that does not report it");
      Check(!GameRuntime.BuildArguments(nf,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,lockFlickOption:true).Contains("--lock-flick"),"--lock-flick is never passed to Legends 2");
      Check(GameRuntime.BuildArguments(nf,cue,data,true,true,false,false,false,false,false,false,null,liveControlOption:true).Contains(" --control-file \""+GameRuntime.ControlFilePath(data)+"\""),"live control: --control-file with the command file path is passed to a program that reports it");
      Check(!GameRuntime.BuildArguments(nf,cue,data,true,true,false,false,false,false,false,false,null,liveControlOption:false).Contains("--control-file"),"live control: nothing is passed to a program that does not report it");
      Check(!GameRuntime.BuildArguments(nf,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,liveControlOption:true).Contains("--control-file"),"live control: --control-file is never passed to Legends 2");
      {
        string lcDir=Path.Combine(Path.GetTempPath(),"fd-live-control-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(lcDir);string lcFile=Path.Combine(lcDir,"live-control.txt");
        try{
          Check(LiveControl.Send(lcFile,"pause on")&&File.ReadAllText(lcFile)=="pause on\n"&&!File.Exists(lcFile+".tmp"),"live control: a command is written as a file and no temp file is left");
          LiveControl.Send(lcFile,"mute on");
          Check(File.ReadAllText(lcFile)=="pause on\n","live control: while the game has not taken the file, a new command waits in memory and the file is not rewritten (so nothing runs twice)");
          Check(TakeLikeGame(lcFile)=="pause on\n"&&WaitForFile(lcFile,3000)&&File.ReadAllText(lcFile)=="mute on\n","live control: once the game took the file, the waiting command is written as the next file");
          TakeLikeGame(lcFile);
          Check(!LiveControl.Send(lcFile,"")&&!LiveControl.Send(lcFile,"bad\nline")&&!LiveControl.Send(lcFile,new string('x',41))&&!LiveControl.Send(null,"pause on")&&!File.Exists(lcFile),"live control: an empty line, a line with a line break, a long line or no path is refused and nothing is written");
          LiveControl.Send(lcFile,"volume 10");LiveControl.Send(lcFile,"volume 20");LiveControl.Send(lcFile,"volume 30");LiveControl.Send(lcFile,"pause on");LiveControl.Send(lcFile,"pause off");LiveControl.Send(lcFile,"screenshot");LiveControl.Send(lcFile,"screenshot");
          Check(TakeLikeGame(lcFile)=="volume 10\n"&&WaitForFile(lcFile,3000)&&TakeLikeGame(lcFile)=="volume 30\npause off\nscreenshot\nscreenshot\n","live control: a newer volume, pause, mute or speed line replaces an older one that still waits; two screenshots stay two");
          {
            var got=new System.Collections.Generic.List<string>();var sizes=new System.Collections.Generic.List<int>();bool stopTaker=false;
            var taker=new System.Threading.Thread(delegate(){while(!stopTaker){try{if(File.Exists(lcFile)){string txt=TakeLikeGame(lcFile);lock(got){foreach(string l in txt.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries))got.Add(l);sizes.Add(Encoding.ASCII.GetByteCount(txt));}}}catch(IOException){}System.Threading.Thread.Sleep(60);}});
            taker.Start();
            for(int i=0;i<300;i++){LiveControl.Send(lcFile,"x"+i);System.Threading.Thread.Sleep(2);}
            for(int w=0;w<100;w++){lock(got){if(got.Count>=300)break;}System.Threading.Thread.Sleep(50);}
            stopTaker=true;taker.Join();
            lock(got){
              bool inOrder=got.Count==300;for(int i=0;i<got.Count&&inOrder;i++)if(got[i]!="x"+i)inOrder=false;
              int big=0;foreach(int sz in sizes)if(sz>=1024)big++;
              Check(inOrder,"live control: 300 commands sent quickly while a stand-in game takes the file every 60 ms all arrive once and in order ("+got.Count+" of 300)");
              Check(big==0&&sizes.Count>1,"live control: no file is over 1024 bytes ("+sizes.Count+" files)");
            }
          }
          {int cnt;var qq=new System.Collections.Generic.List<string>();for(int i=0;i<60;i++)LiveControl.Add(qq,"line"+(i%10)+" abcdefghijklmnopqrstuvwxyz");string ft=LiveControl.FileText(qq,out cnt);Check(cnt<=30&&Encoding.ASCII.GetByteCount(ft)<=900&&cnt==ft.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length&&qq.Count==60,"live control: one file holds at most 30 lines and 900 bytes, and building it removes nothing");}
          {int cnt;var qs=new System.Collections.Generic.List<string>();for(int i=0;i<150;i++)LiveControl.Add(qs,"s"+i);string fs=LiveControl.FileText(qs,out cnt);Check(cnt==30&&fs.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries).Length==30,"live control: short lines are held to 30 per file as well (the game reads 32)");}
          {string rf=Path.Combine(lcDir,"reset.txt");LiveControl.Send(rf,"pause on");LiveControl.Send(rf,"mute on");LiveControl.Reset(rf);File.Delete(rf);System.Threading.Thread.Sleep(250);Check(!File.Exists(rf),"live control: a new game starts clean, commands that waited for the old game are never written for the new one");}
          {string ff=Path.Combine(lcDir,"full.txt");LiveControl.Send(ff,"fullscreen toggle");int accepted=0;for(int i=0;i<250;i++)if(LiveControl.Send(ff,"screenshot"))accepted++;LiveControl.Reset(ff);File.Delete(ff);Check(accepted==200,"live control: when 200 commands wait, the next ones are refused (so the launcher can log it), not dropped quietly ("+accepted+" accepted)");}
          File.WriteAllText(lcFile+".state","paused 1\r\nvolume 35\r\nmute 0\r\nfullscreen 1\r\nlast_screenshot C:\\runs\\a b\\shot_f000012.bmp\r\n");
          var lcState=LiveControl.ReadState(lcFile);
          Check(lcState!=null&&lcState.Paused&&lcState.Volume==35&&!lcState.Mute&&lcState.Fullscreen&&lcState.LastShot==@"C:\runs\a b\shot_f000012.bmp","live control: the state file is read (CR LF, a picture path with spaces)");
          File.WriteAllText(lcFile+".state","paused 0\nvolume 250\nmute 1\nfullscreen 0\nlast_screenshot \n");
          lcState=LiveControl.ReadState(lcFile);Check(lcState!=null&&!lcState.Paused&&lcState.Volume==100&&lcState.Mute&&!lcState.Fullscreen&&lcState.LastShot=="","live control: a volume over 100 is held at 100 and an empty screenshot line is empty");
          File.WriteAllText(lcFile+".state","paused 0\nvolume 50\nmute 0\nfullscreen 0\nlast_screenshot \nunlimited_speed 1\n");
          Check(LiveControl.ReadState(lcFile).Unlimited,"live control: the unlimited speed line is read");
          File.WriteAllText(lcFile+".state","paused 0\nvolume 50\n");Check(!LiveControl.ReadState(lcFile).Unlimited,"live control: a state file without the speed line means normal speed");
          File.Delete(lcFile+".state");Check(LiveControl.ReadState(lcFile)==null,"live control: no state file gives nothing");
          File.WriteAllText(lcFile+".state","nonsense\n");Check(LiveControl.ReadState(lcFile)==null&&LiveControl.ParseState("")==null&&LiveControl.ParseState(null)==null&&LiveControl.ParseState("paused 1\n")==null,"live control: a damaged or half-written state file gives nothing");
          using(var lcStateHold=new FileStream(lcFile+".state",FileMode.Create,FileAccess.Write,FileShare.ReadWrite)){var hbytes=Encoding.ASCII.GetBytes("paused 0\nvolume 5\n");lcStateHold.Write(hbytes,0,hbytes.Length);lcStateHold.Flush();var lcStateRead=LiveControl.ReadState(lcFile);Check(lcStateRead!=null&&lcStateRead.Volume==5,"live control: a state file the game still has open is read");}
          {int cnt;var qq=new System.Collections.Generic.List<string>();LiveControl.Add(qq,"fullscreen toggle");Check(LiveControl.FileText(qq,out cnt)=="fullscreen toggle\n"&&cnt==1,"live control: the file text is one line per command");}
        }finally{try{Directory.Delete(lcDir,true);}catch(Exception){}}
      }
      {   // the Jet Skates of Legends 1 (contracts 2.31 to 2.33): on by default, only the off choice is passed, never to another game
        var sk=new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,NoSkateShoot=true,NoSkateLock=true,NoSkateToggle=true};
        string skl=GameRuntime.BuildArguments(sk,cue,data,true,true,false,false,false,false,false,false,null,skateShootOption:true,skateLockOption:true,skateToggleOption:true);
        Check(skl.Contains(" --skate-shoot 0")&&skl.Contains(" --skate-lock 0")&&skl.Contains(" --skate-toggle 0"),"the three skates choices are passed when switched off: "+skl);
        string skOn=GameRuntime.BuildArguments(new RuntimeSettings{Program=program,WindowHeight=720,Volume=80},cue,data,true,true,false,false,false,false,false,false,null,skateShootOption:true,skateLockOption:true,skateToggleOption:true);
        Check(!skOn.Contains("--skate-")&&!new RuntimeSettings().NoSkateShoot&&!new RuntimeSettings().NoSkateLock&&!new RuntimeSettings().NoSkateToggle,"all three on by default, and on passes nothing");
        Check(!GameRuntime.BuildArguments(sk,cue,data,true,true,false,false,false,false,false,false,null).Contains("--skate-"),"nothing for a program that does not report the words");
        Check(!GameRuntime.BuildArguments(sk,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,skateShootOption:true,skateLockOption:true,skateToggleOption:true).Contains("--skate-"),"never passed to Legends 2");
        string skPath=Path.Combine(root,"runtime-skates.xml");GameRuntime.Save(skPath,sk);var skBack=GameRuntime.Load(skPath);
        Check(skBack.NoSkateShoot&&skBack.NoSkateLock&&skBack.NoSkateToggle,"the three choices are saved and read back");
        // lock-on by health (game contract 2.44): on by default, passed only to a Legends 1 program that reports the word
        var lh=new RuntimeSettings{Program=program,WindowHeight=720,Volume=80};
        Check(GameRuntime.BuildArguments(lh,cue,data,true,true,false,false,false,false,false,false,null,lockHealthOption:true).Contains(" --lock-health 1"),"lock-on by health is passed when the program reports the word");
        Check(!GameRuntime.BuildArguments(lh,cue,data,true,true,false,false,false,false,false,false,null).Contains("--lock-health"),"nothing for a program that does not report the word");
        Check(!GameRuntime.BuildArguments(new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,NoLockHealth=true},cue,data,true,true,false,false,false,false,false,false,null,lockHealthOption:true).Contains("--lock-health"),"switched off passes nothing (the program default is off)");
        Check(!GameRuntime.BuildArguments(lh,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,lockHealthOption:true).Contains("--lock-health"),"never passed to Legends 2");
        Check(!GameRuntime.BuildArguments(lh,cue,data,true,true,false,false,false,false,false,false,GameRuntime.Tron,lockHealthOption:true).Contains("--lock-health"),"never passed to Tron Bonne");
        // backup target picker (game contract 2.43): the launcher always turns it off, only for a Legends 1 program that reports the word
        Check(GameRuntime.BuildArguments(lh,cue,data,true,true,false,false,false,false,false,false,null,lockFallbackOption:true).Contains(" --lock-fallback 0"),"the backup target picker is turned off when the program reports the word");
        Check(!GameRuntime.BuildArguments(lh,cue,data,true,true,false,false,false,false,false,false,null).Contains("--lock-fallback"),"nothing for a program that does not report the word");
        Check(!GameRuntime.BuildArguments(lh,cue2,dataTwo,true,true,false,false,false,false,false,false,k2,lockFallbackOption:true).Contains("--lock-fallback"),"backup picker option never passed to Legends 2");
        Check(!GameRuntime.BuildArguments(lh,cue,data,true,true,false,false,false,false,false,false,GameRuntime.Tron,lockFallbackOption:true).Contains("--lock-fallback"),"backup picker option never passed to Tron Bonne");
        string lhPath=Path.Combine(root,"runtime-lockhealth.xml");GameRuntime.Save(lhPath,new RuntimeSettings{NoLockHealth=true});
        Check(GameRuntime.Load(lhPath).NoLockHealth&&!new RuntimeSettings().NoLockHealth,"the choice is saved and read back, and it is on by default");
        var dg=new RuntimeSettings{Program=program,WindowHeight=720,Volume=80,NoDiagnostics=true};
        Check(GameRuntime.BuildArguments(dg,cue,data,true,true,false,false,false,false,false,false,null,diagnosticsOption:true).Contains(" --diagnostics 0"),"the diagnostics opt-out is passed when the player switched the recorder off");
        Check(!GameRuntime.BuildArguments(new RuntimeSettings{Program=program,WindowHeight=720,Volume=80},cue,data,true,true,false,false,false,false,false,false,null,diagnosticsOption:true).Contains("--diagnostics")&&!new RuntimeSettings().NoDiagnostics,"the recorder is on by default, and on passes nothing");
        Check(!GameRuntime.BuildArguments(dg,cue,data,true,true,false,false,false,false,false,false,null).Contains("--diagnostics"),"the opt-out is not passed to a program that does not report the word");
        string dgPath=Path.Combine(root,"runtime-diagnostics.xml");GameRuntime.Save(dgPath,dg);Check(GameRuntime.Load(dgPath).NoDiagnostics,"the diagnostics choice is saved and read back");
        {   // the guide "How to use the new features"
          var all=new RuntimeCheck{Ready=true,HasRoll=true,HasLiveCodes=true,HasTeleport=true,HasZTarget=true,HasLockFlick=true,HasShotAim=true,HasLockHealth=true};
          Check(GameRuntime.GuideTopicsFor(all,true)==GameRuntime.GuideAll&&GameRuntime.GuideAll==31&&GameRuntime.GuideTopicsFor(all,false)==30&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true},false)==0&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true},true)==GameRuntime.GuideBuild,"guide topics follow the features of the game program and the builder");
          Check(GameRuntime.GuideTopicsFor(new RuntimeCheck{HasRoll=true,HasTeleport=true},true)==0&&GameRuntime.GuideTopicsFor(null,true)==0&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasRoll=true},false)==GameRuntime.GuideRoll&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasLiveCodes=true},false)==GameRuntime.GuideCodes&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasTeleport=true},false)==GameRuntime.GuideTeleport,"no guide for a game that is not ready, and each feature brings only its own topic");
          Check(GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasZTarget=true,HasLockFlick=true,HasShotAim=true,HasLockHealth=true},false)==GameRuntime.GuideLock&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasZTarget=true,HasLockFlick=true},false)==0&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasZTarget=true,HasLockFlick=true,HasShotAim=true},false)==0&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasZTarget=true,HasLockFlick=true,HasLockHealth=true},false)==0&&GameRuntime.GuideTopicsFor(new RuntimeCheck{Ready=true,HasLockFlick=true,HasShotAim=true,HasLockHealth=true},false)==0,"the lock-on topic names four switches, so it is only for a program that has all four");
          var gs=new RuntimeSettings{Program=program};Check(GameRuntime.GuideDue(gs,all,false,27)==30&&GameRuntime.GuideDue(null,all,false,27)==0,"every topic is due at first");
          GameRuntime.GuideShown(gs,GameRuntime.GuideTeleport|GameRuntime.GuideLock,27);Check(gs.GuideSeen==24&&gs.GuideBuildSeen==0&&GameRuntime.GuideDue(gs,all,false,27)==(GameRuntime.GuideRoll|GameRuntime.GuideCodes)&&GameRuntime.GuideDue(gs,all,true,27)==(GameRuntime.GuideBuild|GameRuntime.GuideRoll|GameRuntime.GuideCodes),"a topic that was shown is not due again, and a topic that arrives later is");
          GameRuntime.GuideShown(gs,GameRuntime.GuideAll|64,27);Check(gs.GuideSeen==30&&gs.GuideBuildSeen==27&&GameRuntime.GuideDue(gs,all,true,27)==0,"after every topic was shown nothing is due");
          Check(GameRuntime.GuideDue(gs,all,true,28)==GameRuntime.GuideBuild&&GameRuntime.GuideDue(gs,all,false,28)==0&&GameRuntime.GuideDue(gs,all,true,26)==0,"the build-again topic comes back for a newer builder, and only when the builder is newer than the game's");
          GameRuntime.GuideShown(gs,GameRuntime.GuideBuild,26);Check(gs.GuideBuildSeen==27,"an older builder never lowers the build-again mark");
          string gPath=Path.Combine(root,"runtime-guide.xml");GameRuntime.Save(gPath,new RuntimeSettings{Program=program,GuideSeen=22,GuideBuildSeen=27});Check(GameRuntime.Load(gPath).GuideSeen==22&&GameRuntime.Load(gPath).GuideBuildSeen==27,"the guide topics already shown are saved and read back");
          File.WriteAllText(gPath,File.ReadAllText(gPath).Replace(" guideSeen=\"22\"","").Replace(" guideBuildSeen=\"27\"",""));Check(GameRuntime.Load(gPath).GuideSeen==0&&GameRuntime.Load(gPath).GuideBuildSeen==0,"settings saved by an older launcher: no topic was shown yet");
          File.WriteAllText(gPath,File.ReadAllText(gPath).Replace(" rumbleStrength="," guideSeen=\"-5\" guideBuildSeen=\"x\" rumbleStrength="));Check(GameRuntime.Load(gPath).GuideSeen==0&&GameRuntime.Load(gPath).GuideBuildSeen==0,"a damaged guide mark is read as nothing shown");
          Check(GameRuntime.GuideParts(0).Length==0&&GameRuntime.GuideParts(GameRuntime.GuideAll).Length==5&&GameRuntime.GuideParts(GameRuntime.GuideRoll|GameRuntime.GuideCodes).Length==2&&GameRuntime.GuideParts(GameRuntime.GuideCodes)[0][0]=="Cheat codes","the guide shows only the chosen topics, in order");
          bool gWhole=true;foreach(var gp in GameRuntime.GuideParts(GameRuntime.GuideAll))gWhole=gWhole&&gp.Length==2&&gp[0].Length>0&&gp[0].Length<=60&&gp[1].Length>=80&&gp[1].IndexOf('\n')<0;Check(gWhole,"every guide topic has a short heading and a text");
          string roll=GameRuntime.GuideParts(GameRuntime.GuideRoll)[0][1],codes=GameRuntime.GuideParts(GameRuntime.GuideCodes)[0][1],tele=GameRuntime.GuideParts(GameRuntime.GuideTeleport)[0][1],build=GameRuntime.GuideParts(GameRuntime.GuideBuild)[0][1],lockText=GameRuntime.GuideParts(GameRuntime.GuideLock)[0][1];
          Check(roll.Contains("does NOT start the Jet Skates")&&roll.Contains("walk switch")&&roll.Contains("keyboard")&&codes.Contains("Ctrl+Alt+O")&&codes.Contains("does not undo")&&codes.Contains("back up your memory card")&&codes.Contains("before you press Play")&&tele.Contains("back up your memory card")&&tele.Contains("skips the story")&&build.Contains("The game was not started now")&&lockText.Contains("needs a gamepad"),"the guide gives the warnings that matter: skates with quick roll, codes and Teleport against a saved game, no start after build again, a gamepad for lock-on");
        }
        var dn=new RuntimeSettings{Program=program};var dnC=new RuntimeCheck{Ready=true,HasDiagnostics=true};
        Check(GameRuntime.DiagnosticsNoticeDue(dn,dnC),"the diagnostics notice is due before the first session of a program that records");
        Check(!GameRuntime.DiagnosticsNoticeDue(dn,new RuntimeCheck{Ready=true})&&!GameRuntime.DiagnosticsNoticeDue(dn,new RuntimeCheck{HasDiagnostics=true})&&!GameRuntime.DiagnosticsNoticeDue(dn,null),"no notice for a program without the recorder, for a game that is not ready, or without a check");
        Check(!GameRuntime.DiagnosticsNoticeDue(new RuntimeSettings{Program=program,NoDiagnostics=true},dnC),"no notice for a player who already switched the recorder off in Settings: nothing can switch it back on");
        string dnOld=Path.Combine(root,"runtime-diagnostics-old.xml");GameRuntime.Save(dnOld,new RuntimeSettings{Program=program});File.WriteAllText(dnOld,File.ReadAllText(dnOld).Replace(" noDiagnostics=\"0\"","").Replace(" diagnosticsNoticeSeen=\"0\"",""));
        var dnOldBack=GameRuntime.Load(dnOld);Check(!File.ReadAllText(dnOld).Contains("iagnostics")&&!dnOldBack.NoDiagnostics&&!dnOldBack.DiagnosticsNoticeSeen&&GameRuntime.DiagnosticsNoticeDue(dnOldBack,dnC),"a settings file written before this version (no diagnostics attributes) reads as recorder on, notice not yet seen");
        GameRuntime.DiagnosticsNoticeAnswer(dn,false);Check(dn.NoDiagnostics&&dn.DiagnosticsNoticeSeen&&!GameRuntime.DiagnosticsNoticeDue(dn,dnC),"the answer No turns the recorder off and the notice is not shown again");
        Check(GameRuntime.BuildArguments(dn,cue,data,true,true,false,false,false,false,false,false,null,diagnosticsOption:true).Contains(" --diagnostics 0"),"after the answer No the opt-out is passed to the game");
        var dy=new RuntimeSettings{Program=program};GameRuntime.DiagnosticsNoticeAnswer(dy,true);Check(!dy.NoDiagnostics&&dy.DiagnosticsNoticeSeen&&!GameRuntime.BuildArguments(dy,cue,data,true,true,false,false,false,false,false,false,null,diagnosticsOption:true).Contains("--diagnostics"),"the answer Yes keeps the recorder on (nothing is passed) and the notice is not shown again");
        string dnPath=Path.Combine(root,"runtime-diagnostics-notice.xml");GameRuntime.Save(dnPath,dn);var dnBack=GameRuntime.Load(dnPath);Check(dnBack.DiagnosticsNoticeSeen&&dnBack.NoDiagnostics&&!new RuntimeSettings().DiagnosticsNoticeSeen,"the seen mark and the choice are saved and read back; a new settings file has not seen the notice");
        Check(GameRuntime.DiagnosticsNotice.Contains("never sent anywhere")&&GameRuntime.DiagnosticsNotice.Contains("sessions folder on this PC")&&GameRuntime.DiagnosticsNotice.Contains("memory, its log and the buttons you press")&&GameRuntime.DiagnosticsNotice.Contains("while no game is running")&&GameRuntime.DiagnosticsNotice.Contains("F12")&&GameRuntime.DiagnosticsNotice.Contains("Settings")&&GameRuntime.DiagnosticsNotice.Contains("Diagnostics recording is ON. Please read this, then choose.")&&GameRuntime.DiagnosticsNotice.Contains("Off: no new record is written. Records already written stay on this PC until you delete them")&&GameRuntime.DiagnosticsNotice.Contains("can show your Windows user name")&&GameRuntime.DiagnosticsNotice.Contains("last 5 minutes")&&GameRuntime.DiagnosticsNotice.Contains("about 4 GB")&&GameRuntime.DiagnosticsNotice.Contains("_diag")&&GameRuntime.DiagnosticsNotice.Contains("do not post it or share it")&&GameRuntime.DiagnosticsNotice.Contains("read it again"),"the notice says what is recorded, where the record stays, not to share it, the F12 key, the disk space, the opt-out and where to change it");
        {bool whole=GameRuntime.DiagnosticsNoticeParts.Length==7;foreach(var part in GameRuntime.DiagnosticsNoticeParts)whole=whole&&part.Length==2&&part[0].Length>0&&part[0].Length<=40&&part[1].Length>=40&&part[1].IndexOf('\n')<0;Check(whole,"every part of the notice has a short heading and a text");}
        {   // the two buttons: the first always means recording on, and the button that keeps the present state has the keyboard
          string[] f=GameRuntime.DiagnosticsNoticeButtons(false,true),rOn=GameRuntime.DiagnosticsNoticeButtons(true,true),rOff=GameRuntime.DiagnosticsNoticeButtons(true,false);
          Check(f.Length==2&&f[0]=="Keep it on"&&f[1]=="Turn it off"&&rOn[0]=="Keep it on"&&rOn[1]=="Turn it off"&&rOff[0]=="Turn it on"&&rOff[1]=="Keep it off","the first button is always recording on, the second always off");
          Check(GameRuntime.DiagnosticsNoticeFirstFocused(false,true)&&GameRuntime.DiagnosticsNoticeFirstFocused(true,true)&&!GameRuntime.DiagnosticsNoticeFirstFocused(true,false),"Enter by reflex keeps the present state");
          Check(GameRuntime.DiagnosticsNoticeLead(true,false).Contains("is OFF")&&!GameRuntime.DiagnosticsNoticeLead(true,false).Contains("is ON")&&GameRuntime.DiagnosticsNoticeLead(true,true).Contains("is ON")&&GameRuntime.DiagnosticsNoticeLead(false,true).Contains("is ON"),"a notice read again says the true state");
          var none=new RuntimeSettings{NoDiagnostics=true};Check(!GameRuntime.DiagnosticsNoticeApply(none,null)&&none.NoDiagnostics&&!none.DiagnosticsNoticeSeen&&!GameRuntime.DiagnosticsNoticeApply(null,true),"a notice closed without an answer changes nothing and is not counted as seen");
          var yes=new RuntimeSettings{NoDiagnostics=true};var no=new RuntimeSettings();Check(GameRuntime.DiagnosticsNoticeApply(yes,true)&&!yes.NoDiagnostics&&yes.DiagnosticsNoticeSeen&&GameRuntime.DiagnosticsNoticeApply(no,false)&&no.NoDiagnostics&&no.DiagnosticsNoticeSeen,"the first button turns the recorder on, the second turns it off, and both count as seen");
        }
      }
      // ---- the third game: The Misadventures of Tron Bonne (contract T-2) ----
      var k3=GameRuntime.Tron;string data3=Path.Combine(root,"games","tron data");
      Check(GameRuntime.Game(3)==k3&&GameRuntime.Game(2)==k2&&GameRuntime.Game(1)==GameRuntime.Legends1&&GameRuntime.GameCount==3&&GameRuntime.IsGame(1)&&GameRuntime.IsGame(3)&&!GameRuntime.IsGame(0)&&!GameRuntime.IsGame(4)&&!GameRuntime.IsGame(-1),"three games, and no other number is one");
      Check(k3.Id==3&&k3.DiscProgram=="TRON.EXE"&&k3.ProgramName=="tron_rt"&&k3.GameLine=="The Misadventures of Tron Bonne (USA) SLUS-00896"&&!k3.GmeImport&&k3.KnownHash.Length==64,"Tron Bonne is read from the disc as TRON.EXE and never takes a .gme save");
      Check(GameRuntime.DataFolderFor(3)!=GameRuntime.DataFolderFor(1)&&GameRuntime.DataFolderFor(3)!=GameRuntime.DataFolderFor(2)&&GameRuntime.DataFolderFor(3).EndsWith("\\games\\tron")&&GameRuntime.CardPath(GameRuntime.DataFolderFor(3))!=GameRuntime.CardPath(GameRuntime.DataFolderFor(1)),"Tron Bonne has its own data folder and its own card file");
      // the start line: exactly the one of contract T-2 section 3, whatever the saved choices of the other games' options are
      var s3=new RuntimeSettings{Program=program2,WindowHeight=720,Volume=80,OriginalPicture=true,AnalogPad=true,NoRumble=true,NoIntroMovie=true,OriginalMovement=true,OriginalControls=true,Teleport=true};
      string want3="--live --exe-from-disc TRON.EXE --run-dir \""+GameRuntime.SessionsPath(data3)+"\" --cue \""+cue2+"\" --card0 \""+GameRuntime.CardPath(data3)+"\" --card-new --keys \""+GameRuntime.KeysPath(data3)+"\" --window-height 720 --volume 80 --fail-file \""+GameRuntime.FailFilePath(data3)+"\" --original-picture";
      string line3=GameRuntime.BuildArguments(s3,cue2,data3,true,true,false,false,false,false,false,false,k3,false,false);
      Check(line3==want3,"the Tron Bonne start line is exactly the one of contract T-2 section 3: "+line3);
      Check(GameRuntime.BuildArguments(s3,cue2,data3,true,true,false,false,false,false,false,false,k3,false,true)==want3,"--pad-type is never passed to Tron Bonne, also if a program reported it");
      foreach(string never in new[]{"--rumble","--no-rumble","--controls","--lockon","--aim-style","--no-intro-movie","--force-intro-movie","--pad-type","--movement","--card-from-gme","--pad-map","--look-","--teleport","--scale"})
        Check(!line3.Contains(never),"the Tron Bonne start line has no option that its program refuses: "+never);
      Directory.CreateDirectory(data3);File.WriteAllText(GameRuntime.ImportPath(data3),"a save of another game");
      Check(GameRuntime.BuildArguments(s3,cue2,data3,true,true,false,false,false,false,false,false,k3,false,false)==want3,"a .gme file in the Tron Bonne folder is never given to the game");
      File.Delete(GameRuntime.ImportPath(data3));
      // each slot starts only its own game
      var id3=new System.Collections.Generic.Dictionary<string,string>{{"game","The Misadventures of Tron Bonne (USA) SLUS-00896"},{"program","tron_rt"}};
      var id2=new System.Collections.Generic.Dictionary<string,string>{{"game","Mega Man Legends 2 (USA) SLUS-01140"},{"program","mml2_rt"}};
      var id1=new System.Collections.Generic.Dictionary<string,string>{{"game","Mega Man Legends (USA) SLUS-00603"},{"program","rock_neo_rt"}};
      Check(GameRuntime.CheckSlot(k3,id3)==""&&GameRuntime.CheckSlot(k2,id2)==""&&GameRuntime.CheckSlot(GameRuntime.Legends1,id1)=="","each game's own program is accepted by its slot");
      Check(GameRuntime.CheckSlot(GameRuntime.Legends1,id3).Contains("Tron Bonne")&&GameRuntime.CheckSlot(k2,id3).Length>0&&GameRuntime.CheckSlot(k3,id2).Length>0&&GameRuntime.CheckSlot(k3,id1).Length>0&&GameRuntime.CheckSlot(GameRuntime.Legends1,id2).Contains("Legends 2")&&GameRuntime.CheckSlot(k2,id1).Length>0,"a program is refused by the slot of every other game");
      Check(GameRuntime.CheckSlot(k3,new System.Collections.Generic.Dictionary<string,string>()).Length>0&&GameRuntime.CheckSlot(k3,null).Length>0&&GameRuntime.CheckSlot(k3,new System.Collections.Generic.Dictionary<string,string>{{"game","The Misadventures of Tron Bonne (USA) SLUS-00896"},{"program","rock_neo_rt"}}).Length>0&&GameRuntime.CheckSlot(GameRuntime.Legends1,new System.Collections.Generic.Dictionary<string,string>{{"program","tron_rt"}}).Length>0&&GameRuntime.CheckSlot(GameRuntime.Legends1,new System.Collections.Generic.Dictionary<string,string>{{"game","The Misadventures of Tron Bonne (USA) SLUS-00896"}}).Length>0,"the Tron Bonne slot needs both answers, and the Legends 1 slot refuses either Tron Bonne answer");
      Check(GameRuntime.IsKnownTron(k3.KnownHash,k3.KnownHash)&&GameRuntime.IsKnownTron(k3.KnownHash.ToLowerInvariant(),"x")&&!GameRuntime.IsKnownTron(known,"x")&&!GameRuntime.IsKnownLegends2(k3.KnownHash,GameRuntime.Mml2Hash)&&!GameRuntime.IsKnownBuild(k3.KnownHash,GameRuntime.KnownHash),"the known Tron Bonne build is known as that game only");
      // one settings file for three games: no save loses another game's entry
      string threePath=Path.Combine(root,"runtime-three.xml");
      GameRuntime.Save(threePath,new RuntimeSettings{Program=program,Volume=41});GameRuntime.Save(threePath,new RuntimeSettings{Program=program2,Volume=82},2);
      Check(GameRuntime.Load(threePath,3).Program==""&&GameRuntime.Load(threePath,3).Volume==100,"a file with two entries gives default settings for game 3");
      GameRuntime.Save(threePath,new RuntimeSettings{Program=program2,Volume=63,AcceptedHash=known},3);
      Check(GameRuntime.Load(threePath).Volume==41&&GameRuntime.Load(threePath,2).Volume==82&&GameRuntime.Load(threePath,3).Volume==63&&String.Equals(GameRuntime.Load(threePath,3).AcceptedHash,known,StringComparison.OrdinalIgnoreCase),"saving game 3 keeps games 1 and 2");
      GameRuntime.Save(threePath,new RuntimeSettings{Program=program,Volume=44});
      Check(GameRuntime.Load(threePath).Volume==44&&GameRuntime.Load(threePath,2).Volume==82&&GameRuntime.Load(threePath,3).Volume==63,"saving game 1 keeps games 2 and 3");
      GameRuntime.Save(threePath,new RuntimeSettings{Program=program2,Volume=85},2);
      Check(GameRuntime.Load(threePath).Volume==44&&GameRuntime.Load(threePath,2).Volume==85&&GameRuntime.Load(threePath,3).Volume==63,"saving game 2 keeps games 1 and 3");
      string onlyThree=Path.Combine(root,"runtime-only-three.xml");GameRuntime.Save(onlyThree,new RuntimeSettings{Program=program2,Volume=70},3);
      string three=File.ReadAllText(onlyThree);Check(three.Contains("id=\"3\"")&&!three.Contains("id=\"1\"")&&!three.Contains("id=\"2\"")&&GameRuntime.Load(onlyThree,3).Volume==70&&GameRuntime.Load(onlyThree).Program=="","game 3 can be saved first");
      File.WriteAllText(threePath,"<other/>");Throws<InvalidDataException>(delegate{GameRuntime.Save(threePath,s3,3);},"Tron Bonne settings are not saved over a file whose Legends 1 entry cannot be read");Check(File.ReadAllText(threePath)=="<other/>","that file was left as it was (game 3)");
      Throws<ArgumentOutOfRangeException>(delegate{GameRuntime.Save(onlyThree,s3,0);},"game 0 is not saved");Throws<ArgumentOutOfRangeException>(delegate{GameRuntime.Save(onlyThree,s3,4);},"game 4 is not saved");
      Check(GameRuntime.Load(onlyThree,3).Volume==70,"a refused save changed nothing");
      string broken=Path.Combine(root,"runtime-broken.xml");
      for(int id=1;id<=3;id++){int which=id;File.WriteAllText(broken,"garbage");bool refused=false;try{GameRuntime.Save(broken,new RuntimeSettings{Program=program},which);}catch(Exception){refused=true;}Check(refused&&File.ReadAllText(broken)=="garbage","a settings file that cannot be read is never written over, also when game "+id+" is saved");}
      Check(GameRuntime.Load(onlyThree,0).Program==""&&GameRuntime.Load(onlyThree,4).Program==""&&GameRuntime.Load(onlyThree,4).Volume==100&&GameRuntime.Load(threePath,7).Program=="","a number that is no game loads default settings, never another game's entry");
      // one game at a time across all three
      Directory.CreateDirectory(data3);
      File.WriteAllText(Path.Combine(folder2,"identity-features.txt"),"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,pad-type");GameRuntime.ForgetHashes();
      var r17=GameRuntime.Readiness(stick,cue2,dataTwo,known2,true,k2);Check(r17.Ready&&r17.HasPadType,"the features line of contract L2-2 offers the switch: "+r17.Problem);
      File.WriteAllText(Path.Combine(folder2,"stand-in-exit.txt"),"0");var g17=GameRuntime.Start(stick,cue2,dataTwo,known2,k2,new[]{data});g17.Process.WaitForExit();GameRuntime.ReleaseLock(dataTwo);
      Check(g17.Arguments==want2+" --pad-type analog","a start with the switch on passes it: "+g17.Arguments);
      // Legends 2 has no multiplayer, whatever its program says
      File.WriteAllText(Path.Combine(folder2,"identity-features.txt"),"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,pad-type,multiplayer");GameRuntime.ForgetHashes();
      Check(!GameRuntime.Readiness(stick,cue2,dataTwo,known2,true,k2).HasMultiplayer,"Legends 2 never offers multiplayer");
      Throws<InvalidOperationException>(delegate{GameRuntime.Start(stick,cue2,dataTwo,known2,k2,new[]{data},"host:47400:000102030405060708090a0b0c0d0e0f:any");},"a multiplayer start of Legends 2 is refused");Check(!GameRuntime.CardInUse(dataTwo),"that refusal left the Legends 2 card free");
      string padPath=Path.Combine(root,"runtime-pad.xml");GameRuntime.Save(padPath,new RuntimeSettings{Program=program,AnalogPad=true});GameRuntime.Save(padPath,stick,2);
      Check(GameRuntime.Load(padPath,2).AnalogPad&&!GameRuntime.Load(padPath).AnalogPad&&System.Text.RegularExpressions.Regex.Matches(File.ReadAllText(padPath),"analogPad=").Count==1,"the analog pad choice is saved for Legends 2 only; the Legends 1 entry has no such value");
      string fcPath=Path.Combine(root,"runtime-fcam.xml");GameRuntime.Save(fcPath,new RuntimeSettings{Program=program});GameRuntime.Save(fcPath,fcam,2);
      Check(GameRuntime.Load(fcPath,2).FreeCamera&&!GameRuntime.Load(fcPath).FreeCamera&&GameRuntime.Load(fcPath,2).LookSensitivity==150&&GameRuntime.Load(fcPath,2).InvertLookY,"the free camera choice is saved for Legends 2 and comes back with its speed and invert choice");
      File.WriteAllText(Path.Combine(folder2,"identity-features.txt"),"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,pad-type,free-camera");GameRuntime.ForgetHashes();
      var r23=GameRuntime.Readiness(stick,cue2,dataTwo,known2,true,k2);Check(r23.Ready&&r23.HasFreeCamera&&r23.HasPadType,"the features line of contract L2-4 offers the free camera: "+r23.Problem);
      Check(!r23.HasFreeCameraDistance,"build 023 does not offer the distance");
      File.WriteAllText(Path.Combine(folder2,"identity-features.txt"),"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,pad-type,free-camera,free-camera-distance");GameRuntime.ForgetHashes();
      var r24=GameRuntime.Readiness(stick,cue2,dataTwo,known2,true,k2);Check(r24.Ready&&r24.HasFreeCamera&&r24.HasFreeCameraDistance,"the features line of contract L2-5 offers the distance: "+r24.Problem);
      File.WriteAllText(Path.Combine(folder2,"identity-features.txt"),"identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,pad-type");GameRuntime.ForgetHashes();
      Check(!GameRuntime.Readiness(stick,cue2,dataTwo,known2,true,k2).HasFreeCamera,"a program whose features lack free-camera does not offer it");
      // the sentence for a program that does not answer names the right program for each game
      Check(GameRuntime.CheckIdentity(null,k2.ProgramHint).Contains("mml2_rt_018.exe")&&!GameRuntime.CheckIdentity(null,k2.ProgramHint).Contains("rock_neo")&&GameRuntime.CheckIdentity(null).Contains("rock_neo_rt_hc2.exe (or rock_neo_rt_fd.exe)"),"a program that does not answer is told apart by game");
  }

  // Multiplayer (game-side contract MP-1): the setting reaches the game through its environment only.
  static void Friend(string folder,string program,string cue,string data,string known,RuntimeSettings s){
    const string secret="000102030405060708090a0b0c0d0e0f";string setting="host:47400:"+secret+":any";
    string featuresPath=Path.Combine(folder,"identity-features.txt");string before=File.Exists(featuresPath)?File.ReadAllText(featuresPath):null;
    const string plainFeatures="identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume";
    File.WriteAllText(featuresPath,plainFeatures);File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");GameRuntime.ForgetHashes();
    var plain=GameRuntime.Readiness(s,cue,data,known,true);Check(plain.Ready&&!plain.HasMultiplayer,"a program that does not report multiplayer does not offer it: "+plain.Problem);
    File.Delete(Path.Combine(folder,"received.txt"));
    try{GameRuntime.Start(s,cue,data,known,null,null,setting);failures++;Console.WriteLine("FAIL (no error): a multiplayer start of a program without multiplayer is refused");}
    catch(InvalidOperationException e){Check(!e.Message.Contains(secret)&&!e.Message.Contains("host:"),"the refusal does not show the setting");}
    Check(!File.Exists(Path.Combine(folder,"received.txt"))&&!GameRuntime.CardInUse(data),"the refused multiplayer start ran nothing and left the card free");
    File.WriteAllText(featuresPath,plainFeatures+",multiplayer");GameRuntime.ForgetHashes();
    var ready=GameRuntime.Readiness(s,cue,data,known,true);Check(ready.Ready&&ready.HasMultiplayer,"the feature word multiplayer offers it: "+ready.Problem);
    File.WriteAllText(GameRuntime.MultiplayerStatusPath(data),"state=connected joins=1 shown=1");   // left by an earlier session
    var hosted=GameRuntime.Start(s,cue,data,known,null,null,setting);hosted.Process.WaitForExit();GameRuntime.ReleaseLock(data);
    string[] got=File.ReadAllLines(Path.Combine(folder,"received-env.txt"));
    Check(got.Length==2&&got[0]==setting&&got[1]==GameRuntime.MultiplayerStatusPath(data),"the game received the setting and the status file place in its environment");
    Check(!hosted.Arguments.Contains(secret)&&!hosted.Arguments.Contains("RMX_MP")&&!hosted.Arguments.Contains("host:")&&!File.ReadAllText(Path.Combine(folder,"received.txt")).Contains(secret),"the key is not on the command line");
    Check(!File.Exists(GameRuntime.MultiplayerStatusPath(data)),"a status file of an earlier session is removed before the start");
    Check(Environment.GetEnvironmentVariable("RMX_MP")==null,"the launcher's own environment is not changed");
    // an ordinary game never gets the setting, also not one that the launcher itself was started with
    Environment.SetEnvironmentVariable("RMX_MP",setting);Environment.SetEnvironmentVariable("RMX_MP_STATUS",Path.Combine(folder,"elsewhere.txt"));
    try{var ordinary=GameRuntime.Start(s,cue,data,known);ordinary.Process.WaitForExit();GameRuntime.ReleaseLock(data);}
    finally{Environment.SetEnvironmentVariable("RMX_MP",null);Environment.SetEnvironmentVariable("RMX_MP_STATUS",null);}
    got=File.ReadAllLines(Path.Combine(folder,"received-env.txt"));Check(got.Length==2&&got[0]=="(none)"&&got[1]=="(none)","an ordinary game gets no multiplayer setting, even when the launcher has one");
    foreach(string file in Directory.GetFiles(data,"*",SearchOption.AllDirectories))Check(!File.ReadAllText(file).Contains(secret),"no file in the game's data folder holds the key: "+Path.GetFileName(file));
    if(before==null)File.Delete(featuresPath);else File.WriteAllText(featuresPath,before);GameRuntime.ForgetHashes();
  }

  public static int Main(string[] args){
    if(args.Length>0&&args[0]=="--identity")return StandInIdentity();
    foreach(string arg in args)if(arg=="--identity")return StandInIdentity(); // "whatever else is on the command line"
    if(args.Length>0&&args[0]=="--live")return StandIn(args);
    string root=Path.GetFullPath(args[0]);
    if(Directory.Exists(root)){foreach(string old in Directory.GetFiles(root,"*",SearchOption.AllDirectories))File.SetAttributes(old,FileAttributes.Normal);Directory.Delete(root,true);}
    Directory.CreateDirectory(root);
    string data=Path.Combine(root,"games","mml1"),folder=Path.Combine(root,"my game folder");Directory.CreateDirectory(folder);
    string program=Path.Combine(folder,"rock_neo_rt_fd.exe");File.Copy(System.Reflection.Assembly.GetExecutingAssembly().Location,program);
    string cue=Path.Combine(folder,"Mega Man Legends (USA).cue");File.WriteAllText(cue,"FILE \"track.bin\" BINARY\r\n  TRACK 01 MODE2/2352\r\n");File.WriteAllBytes(Path.Combine(folder,"track.bin"),new byte[16]);
    string known=GameRuntime.FileSha256(program);

    // settings round trip
    string settingsPath=Path.Combine(root,"runtime.xml");
    var s=GameRuntime.Load(settingsPath);Check(s.Program==""&&s.Scale==3&&!s.Mute&&s.Volume==100,"defaults when no settings file");
    s.Program=program;s.Scale=5;s.Mute=true;s.Volume=40;s.AcceptedHash="abc";GameRuntime.Save(settingsPath,s);
    var back=GameRuntime.Load(settingsPath);Check(back.Program==program&&back.Scale==5&&back.Mute&&back.Volume==40&&back.AcceptedHash=="ABC","settings round trip");
    Check(!back.OriginalPicture&&back.WindowHeight==720,"originalPicture is off and windowHeight is 720 by default and in a settings file from before");s.WindowHeight=1440;GameRuntime.Save(settingsPath,s);Check(GameRuntime.Load(settingsPath).WindowHeight==1440,"windowHeight round trip");s.WindowHeight=12345;GameRuntime.Save(settingsPath,s);Check(GameRuntime.Load(settingsPath).WindowHeight==720,"a windowHeight that is not a preset falls back to 720");s.WindowHeight=720;s.OriginalPicture=true;GameRuntime.Save(settingsPath,s);Check(GameRuntime.Load(settingsPath).OriginalPicture,"originalPicture round trip");s.OriginalPicture=false;GameRuntime.Save(settingsPath,s);Check(!GameRuntime.Load(settingsPath).OriginalPicture,"originalPicture cleared round trip");
    s.Scale=99;s.Volume=250;GameRuntime.Save(settingsPath,s);Check(GameRuntime.Load(settingsPath).Scale==3&&GameRuntime.Load(settingsPath).Volume==100,"out-of-range scale and volume fall back");
    File.WriteAllText(settingsPath,"<flutterDeckRuntime schema=\"1\"><game id=\"1\" program=\"C:\\x\\rock_neo_rt.exe\" discProgram=\"C:\\x\\ROCK_NEO.EXE\" acceptedHash=\"\" scale=\"4\" mute=\"0\"/></flutterDeckRuntime>");
    var old1=GameRuntime.Load(settingsPath);Check(old1.Program==@"C:\x\rock_neo_rt.exe"&&old1.Scale==4&&old1.Volume==100,"a settings file from before (with a disc program) still loads");
    File.WriteAllText(settingsPath,"<other/>");Throws<InvalidDataException>(delegate{GameRuntime.Load(settingsPath);},"foreign settings file");
    s.Scale=3;s.Mute=false;s.Volume=100;s.AcceptedHash="";

    // readiness, in order: game program, disc image; no disc program setting and no card needed
    var blank=new RuntimeSettings();
    Check(!GameRuntime.Readiness(blank,cue,data,known,true).Ready&&GameRuntime.Readiness(blank,cue,data,known,true).Problem.Contains("game program"),"needs the game program first");
    Check(GameRuntime.CheckProgram(cue).Length>0&&GameRuntime.CheckProgram(Path.Combine(folder,"gone.exe")).Length>0&&GameRuntime.CheckProgram(program).Length==0,"game program must be an existing .exe");
    Check(GameRuntime.Readiness(s,"",data,known,true).Problem.Contains("disc image")&&GameRuntime.Readiness(s,Path.Combine(folder,"track.bin"),data,known,true).Problem.Contains("CUE"),"then a CUE disc image");
    var fresh=GameRuntime.Readiness(s,cue,data,known,true);Check(fresh.Ready&&fresh.Build.Contains("contract 2.0"),"ready with only the game program and disc image; build shown: "+fresh.Build+" "+fresh.Problem);
    string bare=GameRuntime.BuildArguments(s,cue,data);Check(bare==Expected(cue,data,"--card-new",3,100,false),"with no card yet the game is asked to create an empty one: "+bare);
    Directory.CreateDirectory(data);File.WriteAllBytes(GameRuntime.CardPath(data),new byte[10]);Check(!GameRuntime.Readiness(s,cue,data,known,true).Ready,"a damaged card blocks");File.Delete(GameRuntime.CardPath(data));

    // memory card: copy in only
    string devCard=Path.Combine(folder,"progress.mcd");var cardBytes=new byte[GameRuntime.CardBytes];new Random(7).NextBytes(cardBytes);File.WriteAllBytes(devCard,cardBytes);File.SetAttributes(devCard,FileAttributes.ReadOnly);
    string wrongCard=Path.Combine(folder,"short.mcd");File.WriteAllBytes(wrongCard,new byte[1000]);
    Throws<InvalidDataException>(delegate{GameRuntime.AdoptCard(wrongCard,data);},"wrong-size card refused");
    Throws<FileNotFoundException>(delegate{GameRuntime.AdoptCard(Path.Combine(folder,"none.mcd"),data);},"missing card refused");
    Check(!GameRuntime.HasCard(data),"refused cards leave no launcher card");
    GameRuntime.AdoptCard(devCard,data);
    Check(GameRuntime.HasCard(data)&&GameRuntime.FileSha256(GameRuntime.CardPath(data))==GameRuntime.FileSha256(devCard)&&File.Exists(devCard),"card copied, identical, original still there");
    Check((File.GetAttributes(GameRuntime.CardPath(data))&FileAttributes.ReadOnly)==0,"copied card is writable");
    string other=Path.Combine(folder,"other.mcd");File.WriteAllBytes(other,new byte[GameRuntime.CardBytes]);
    Throws<InvalidOperationException>(delegate{GameRuntime.AdoptCard(other,data);},"a second card never replaces the first");
    Check(GameRuntime.FileSha256(GameRuntime.CardPath(data))==GameRuntime.FileSha256(devCard),"existing launcher card untouched");
    string data2=Path.Combine(root,"games","gme");string gme=Path.Combine(folder,"save.gme");File.WriteAllBytes(gme,new byte[GameRuntime.GmeBytes]);
    GameRuntime.AdoptCard(gme,data2);Check(GameRuntime.HasImport(data2)&&!GameRuntime.HasCard(data2)&&GameRuntime.CheckCard(data2).Length==0,".gme kept for the game to convert");
    string gmeArgs=GameRuntime.BuildArguments(s,cue,data2);
    Check(gmeArgs==Expected(cue,data2,"--card-from-gme \""+GameRuntime.ImportPath(data2)+"\"",3,100,false)&&!gmeArgs.Contains("--card-new"),"a .gme import is used instead of --card-new, never both: "+gmeArgs);
    string badGme=Path.Combine(folder,"bad.gme");File.WriteAllBytes(badGme,new byte[GameRuntime.CardBytes]);Throws<InvalidDataException>(delegate{GameRuntime.AdoptCard(badGme,Path.Combine(root,"games","x"));},".gme of the wrong size refused");

    // build identity by hash, then by what the program says about itself
    var ready=GameRuntime.Readiness(s,cue,data,known,true);Check(ready.Ready&&ready.KnownBuild&&ready.Hash==known,"known build is ready");
    string otherHash=new string('0',64);
    var unknown=GameRuntime.Readiness(s,cue,data,otherHash,true);Check(!unknown.Ready&&unknown.NeedsAcknowledgement&&!unknown.KnownBuild&&unknown.Hash==known,"unknown build blocked until acknowledged");
    Throws<InvalidOperationException>(delegate{GameRuntime.Start(s,cue,data,otherHash);},"unknown build does not start");
    s.AcceptedHash=new string('1',64);Check(!GameRuntime.Readiness(s,cue,data,otherHash,true).Ready,"acknowledging a different hash does not help");
    s.AcceptedHash=known.ToLowerInvariant();Check(GameRuntime.Readiness(s,cue,data,otherHash,true).Ready,"acknowledging this exact hash allows it");
    s.AcceptedHash="";
    // an unacknowledged unknown program is never run, not even to ask what it is
    string probe=Path.Combine(root,"unknown build");Directory.CreateDirectory(probe);string probeExe=Path.Combine(probe,"rock_neo_rt_fd.exe");File.Copy(program,probeExe);File.AppendAllText(probeExe,"x"); // different hash, would not even load
    var unk=GameRuntime.Readiness(new RuntimeSettings{Program=probeExe},cue,data,known,true);Check(unk.NeedsAcknowledgement&&unk.Build=="","an unacknowledged build is blocked");
    Throws<InvalidOperationException>(delegate{GameRuntime.Start(new RuntimeSettings{Program=probeExe},cue,data,known);},"an unacknowledged build is not started");
    Thread.Sleep(300);Check(!File.Exists(Path.Combine(probe,"identity-asked.txt"))&&!File.Exists(Path.Combine(probe,"received.txt")),"an unacknowledged build is never run, not even to ask what it is");
    var okNow=GameRuntime.Readiness(new RuntimeSettings{Program=probeExe,AcceptedHash=GameRuntime.FileSha256(probeExe)},cue,data,known,true);Check(okNow.Ready&&File.Exists(Path.Combine(probe,"identity-asked.txt")),"(control: once acknowledged, the same build is asked and accepted)");
    // builds that are not contract 2.x, or lack an option the launcher passes
    // (each variant is a different file, so a different hash, acknowledged here as the developer would on the setup screen)
    Func<string,string,string,RuntimeSettings> variant=delegate(string name,string file,string text){string f=Path.Combine(root,name);Directory.CreateDirectory(f);string exe=Path.Combine(f,"rock_neo_rt_fd.exe");File.Copy(program,exe);File.AppendAllText(exe,name);File.WriteAllText(Path.Combine(f,file),text);return new RuntimeSettings{Program=exe,AcceptedHash=GameRuntime.FileSha256(exe)};};
    var o=GameRuntime.Readiness(variant("refusing build","identity-refuses.txt","1"),cue,data,known,true);Check(!o.Ready&&!o.NeedsAcknowledgement&&o.Problem.Contains("identity"),"a program that does not answer --identity is refused: "+o.Problem);
    o=GameRuntime.Readiness(variant("contract 3 build","identity-contract.txt","3.0"),cue,data,known,true);Check(!o.Ready&&o.Problem.Contains("contract 3.0"),"an unknown contract version is refused: "+o.Problem);
    o=GameRuntime.Readiness(variant("lacking build","identity-features.txt","identity,exe-from-disc,fail-file,volume"),cue,data,known,true);Check(!o.Ready&&o.Problem.Contains("card-new"),"a build lacking a needed option is refused: "+o.Problem);
    o=GameRuntime.Readiness(variant("fine rebuilt build","note.txt","x"),cue,data,known,true);Check(o.Ready&&!o.KnownBuild&&o.Build.Contains("contract 2.0"),"an acknowledged rebuilt contract 2.0 build is accepted: "+o.Problem);
    // a refused answer is not asked again on every redraw, but is asked again after the user acts, and never blocks another copy
    var refusing=variant("flaky build","identity-refuses.txt","1");string flakyFolder=Path.GetDirectoryName(refusing.Program);
    Check(!GameRuntime.Readiness(refusing,cue,data,known,true).Ready&&!GameRuntime.Readiness(refusing,cue,data,known,false).Ready&&File.ReadAllLines(Path.Combine(flakyFolder,"identity-asked.txt")).Length==1,"a refusing build is run once, not on every check");
    File.Delete(Path.Combine(flakyFolder,"identity-refuses.txt"));
    Check(!GameRuntime.Readiness(refusing,cue,data,known,true).Ready,"(still refused while the short memory lasts)");
    GameRuntime.ForgetHashes();Check(GameRuntime.Readiness(refusing,cue,data,known,true).Ready,"after the user acts, a build that now answers properly is accepted without restarting the launcher");
    var broken=variant("broken copy","identity-refuses.txt","1");string goodFolder=Path.Combine(root,"good copy same bytes");Directory.CreateDirectory(goodFolder);string goodExe=Path.Combine(goodFolder,"rock_neo_rt_fd.exe");File.Copy(broken.Program,goodExe);
    Check(!GameRuntime.Readiness(broken,cue,data,known,true).Ready&&GameRuntime.Readiness(new RuntimeSettings{Program=goodExe,AcceptedHash=broken.AcceptedHash},cue,data,known,true).Ready,"a broken copy does not block a working copy of the same build elsewhere");
    Check(GameRuntime.IsEarlierBuild(GameRuntime.EarlierHash,GameRuntime.KnownHash)&&GameRuntime.IsEarlierBuild(GameRuntime.EarlierHash.ToLowerInvariant(),GameRuntime.KnownHash)&&!GameRuntime.IsEarlierBuild(GameRuntime.KnownHash,GameRuntime.KnownHash)&&!GameRuntime.IsEarlierBuild(known,GameRuntime.KnownHash),"the earlier 1.x program is recognised by its hash");
    Check(GameRuntime.CheckIdentity(null).Length>0&&GameRuntime.CheckIdentity(new System.Collections.Generic.Dictionary<string,string>()).Length>0,"no identity at all is refused");
    // the earlier 1.x program is named and refused without being run or acknowledged
    Check(GameRuntime.Readiness(s,cue,data,known,true).Ready,"(the stand-in's own hash is treated as the known build here)");
    Check(!GameRuntime.Readiness(s,cue,data,known,true).HasPictureOption,"a 2.0 program does not report the picture option");
    Check(!GameRuntime.Readiness(s,cue,data,known,true).HasWindowHeight,"a 2.0 program does not report the window height option");
    {var hpv=variant("contract 2.5 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height");File.WriteAllText(Path.Combine(Path.GetDirectoryName(hpv.Program),"identity-contract.txt"),"2.5");var hpr=GameRuntime.Readiness(hpv,cue,data,known,true);Check(hpr.Ready&&hpr.HasWindowHeight&&hpr.HasPictureOption,"a program with contract 2.5 and window-height is ready and reports both options: "+hpr.Problem);}
    {var hcv=variant("contract 2.6 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid");File.WriteAllText(Path.Combine(Path.GetDirectoryName(hcv.Program),"identity-contract.txt"),"2.6");var hcr=GameRuntime.Readiness(hcv,cue,data,known,true);Check(hcr.Ready&&hcr.HasWindowHeight&&hcr.HasPictureOption,"a program with contract 2.6 (features end with controller-hid) is ready and still reports both options: "+hcr.Problem);hcv.WindowHeight=2160;Check(GameRuntime.BuildArguments(hcv,cue,data,hcr.HasPictureOption,hcr.HasWindowHeight).Contains(" --window-height 2160 ")&&!GameRuntime.BuildArguments(hcv,cue,data,hcr.HasPictureOption,hcr.HasWindowHeight).Contains("--scale"),"for a 2.6 program the launcher sends --window-height 2160 and no --scale");}
    {var rbv=variant("contract 2.9 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble");File.WriteAllText(Path.Combine(Path.GetDirectoryName(rbv.Program),"identity-contract.txt"),"2.9");var rbr=GameRuntime.Readiness(rbv,cue,data,known,true);Check(rbr.Ready&&rbr.HasRumble&&rbr.HasWindowHeight,"a program with contract 2.9 and the rumble feature is ready and reports it: "+rbr.Problem);rbv.NoRumble=false;Check(!GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,true).Contains("--no-rumble"),"vibration on (the default): nothing is passed");rbv.NoRumble=true;Check(GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,true).Contains(" --no-rumble")&&!GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,false).Contains("--no-rumble"),"vibration ticked off: --no-rumble is passed, and only to a program that reports the feature");
    rbv.NoRumble=false;rbv.RumbleStrength=100;Check(!GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,true).Contains("--rumble"),"strength 100 (the default): nothing is passed");
    rbv.RumbleStrength=60;Check(GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,true).Contains(" --rumble 60")&&!GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,false).Contains("--rumble"),"strength 60: --rumble 60 is passed, and only to a program that reports the feature");
    rbv.NoRumble=true;Check(GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,true).Contains(" --no-rumble")&&!GameRuntime.BuildArguments(rbv,cue,data,true,true,false,false,false,false,true).Contains("--rumble 60"),"vibration ticked off wins over a strength");
    rbv.NoRumble=false;rbv.RumbleStrength=100;}
    {var imv=variant("contract 2.12 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie");File.WriteAllText(Path.Combine(Path.GetDirectoryName(imv.Program),"identity-contract.txt"),"2.12");var imr=GameRuntime.Readiness(imv,cue,data,known,true);Check(imr.Ready&&imr.HasIntroMovie&&imr.HasRumble,"a program with contract 2.12 and the intro-movie feature is ready and reports it: "+imr.Problem);
    imv.NoIntroMovie=false;Check(!GameRuntime.BuildArguments(imv,cue,data,true,true,false,false,false,false,true,false,null,true).Contains("--no-intro-movie"),"the movie on (the default): nothing is passed");
    imv.NoIntroMovie=true;Check(GameRuntime.BuildArguments(imv,cue,data,true,true,false,false,false,false,true,false,null,true).Contains(" --no-intro-movie")&&!GameRuntime.BuildArguments(imv,cue,data,true,true,false,false,false,false,true,false,null,false).Contains("--no-intro-movie"),"the movie ticked off: --no-intro-movie is passed, and only to a program that reports the feature");
    {string tmpx=Path.Combine(Path.GetTempPath(),"fd-movie-"+Guid.NewGuid().ToString("N")+".xml");try{GameRuntime.Save(tmpx,imv);var movieBack=GameRuntime.Load(tmpx);Check(movieBack.NoIntroMovie,"the movie choice (off) is saved and read back");imv.NoIntroMovie=false;GameRuntime.Save(tmpx,imv);Check(!GameRuntime.Load(tmpx).NoIntroMovie,"the movie choice (on) is saved and read back");}finally{if(File.Exists(tmpx))File.Delete(tmpx);if(File.Exists(tmpx+".bak"))File.Delete(tmpx+".bak");}}
    {var vsv=variant("contract 2.13 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie,volume-split");File.WriteAllText(Path.Combine(Path.GetDirectoryName(vsv.Program),"identity-contract.txt"),"2.13");var vsr=GameRuntime.Readiness(vsv,cue,data,known,true);Check(vsr.Ready&&vsr.HasVolumeSplit,"a program with contract 2.13 and the volume-split feature is ready and reports it: "+vsr.Problem);
    vsv.MusicVolume=100;vsv.VoiceVolume=100;vsv.EffectsVolume=100;string vsDefault=GameRuntime.BuildArguments(vsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true);Check(!vsDefault.Contains("--music-volume")&&!vsDefault.Contains("--voice-volume")&&!vsDefault.Contains("--effects-volume"),"all three levels at 100 (the default): nothing is passed");
    vsv.MusicVolume=40;vsv.VoiceVolume=0;vsv.EffectsVolume=75;string vsSet=GameRuntime.BuildArguments(vsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true);Check(vsSet.Contains(" --music-volume 40")&&vsSet.Contains(" --voice-volume 0")&&vsSet.Contains(" --effects-volume 75"),"levels 40, 0 and 75: each is passed, 0 included");
    {string vsNone=GameRuntime.BuildArguments(vsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,false);Check(!vsNone.Contains("--music-volume")&&!vsNone.Contains("--voice-volume")&&!vsNone.Contains("--effects-volume")&&vsNone.Contains(" --volume "),"the levels are not passed to a program without the feature (the plain --volume stays)");}
    {string tmpv=Path.Combine(Path.GetTempPath(),"fd-levels-"+Guid.NewGuid().ToString("N")+".xml");try{GameRuntime.Save(tmpv,vsv);var vsBack=GameRuntime.Load(tmpv);Check(vsBack.MusicVolume==40&&vsBack.VoiceVolume==0&&vsBack.EffectsVolume==75,"the three levels are saved and read back");}finally{if(File.Exists(tmpv))File.Delete(tmpv);if(File.Exists(tmpv+".bak"))File.Delete(tmpv+".bak");}}
    Check(new RuntimeSettings().MusicVolume==100&&new RuntimeSettings().VoiceVolume==100&&new RuntimeSettings().EffectsVolume==100,"a new settings object starts with every level at 100");
    var vsOld=variant("contract 2.12 copy again","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie");File.WriteAllText(Path.Combine(Path.GetDirectoryName(vsOld.Program),"identity-contract.txt"),"2.12");Check(!GameRuntime.Readiness(vsOld,cue,data,known,true).HasVolumeSplit,"a program without the feature does not report the level options");}
    {var lcv=variant("contract 2.34 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie,volume-split,teleport,live-control");File.WriteAllText(Path.Combine(Path.GetDirectoryName(lcv.Program),"identity-contract.txt"),"2.34");var lcr=GameRuntime.Readiness(lcv,cue,data,known,true);Check(lcr.Ready&&lcr.HasLiveControl,"a program with contract 2.34 and the live-control feature is ready and reports it: "+lcr.Problem);}
    {var tpv=variant("contract 2.14 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie,volume-split,teleport");File.WriteAllText(Path.Combine(Path.GetDirectoryName(tpv.Program),"identity-contract.txt"),"2.14");var tpr=GameRuntime.Readiness(tpv,cue,data,known,true);Check(tpr.Ready&&tpr.HasTeleport,"a program with contract 2.14 and the teleport feature is ready and reports it: "+tpr.Problem);
    tpv.Teleport=false;Check(!GameRuntime.BuildArguments(tpv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true).Contains("--teleport-file"),"teleport switched off: nothing is passed");
    tpv.Teleport=true;string tpOn=GameRuntime.BuildArguments(tpv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true);Check(tpOn.Contains(" --teleport-file \""+GameRuntime.TeleportRequestPath(data)+"\""),"teleport ticked: --teleport-file with the request path is passed");
    Check(!GameRuntime.BuildArguments(tpv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,false).Contains("--teleport-file"),"the option is not passed to a program that does not report the feature");
    Check(GameRuntime.TeleportRequestPath(data).EndsWith("teleport-request.txt")&&GameRuntime.TeleportTablePath(data).EndsWith("teleport-table.txt")&&Path.GetDirectoryName(GameRuntime.TeleportRequestPath(data))==data,"the request and table files sit in the game's data folder");
    {// the list of teleport places of a kit-built game is copied into the data folder when the player has none (builder kit 17)
      string kitOut=Path.Combine(root,"tpl-out"),kitGame=Path.Combine(kitOut,"build","game"),tplData=Path.Combine(root,"tpl-data");Directory.CreateDirectory(kitGame);Directory.CreateDirectory(tplData);
      string kitExe=Path.Combine(kitGame,"rock_neo_rt_kit.exe");File.WriteAllText(kitExe,"x");string listText="# teleport-table.txt: made from the player's own disc\ndest 0 1 -256 -1 -5248 0  from 0\n";
      Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData)&&!File.Exists(GameRuntime.TeleportTablePath(tplData)),"no built list: nothing is copied");
      File.WriteAllText(Path.Combine(kitOut,"teleport-table.txt"),listText);
      Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData)&&!File.Exists(GameRuntime.TeleportTablePath(tplData)),"a list without the kit's marker beside it is not taken");
      File.WriteAllText(Path.Combine(kitOut,"rmx-kit-build.json"),"{}");
      Check(GameRuntime.TakeBuiltTeleportList(kitExe,tplData)&&File.ReadAllText(GameRuntime.TeleportTablePath(tplData))==listText&&!File.Exists(GameRuntime.TeleportTablePath(tplData)+".part"),"the built list is copied into the data folder, byte for byte");
      File.WriteAllText(GameRuntime.TeleportTablePath(tplData),"the player's own list");File.WriteAllText(Path.Combine(kitOut,"teleport-table.txt"),listText+"dest 0 2 -256 -1 6272 2048  from 0\n");
      Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData)&&File.ReadAllText(GameRuntime.TeleportTablePath(tplData))=="the player's own list","a list the player already has is never replaced");
      File.Delete(GameRuntime.TeleportTablePath(tplData));
      File.WriteAllText(Path.Combine(kitOut,"teleport-table.txt"),"not a list");Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData)&&!File.Exists(GameRuntime.TeleportTablePath(tplData)),"a file that does not start like a list is not copied");
      File.WriteAllText(Path.Combine(kitOut,"teleport-table.txt"),"");Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData),"an empty file is not copied");
      File.WriteAllBytes(Path.Combine(kitOut,"teleport-table.txt"),Encoding.ASCII.GetBytes("# teleport-table.txt: x\n"+new string('d',(int)GameRuntime.TeleportListLimit)));Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData)&&!File.Exists(GameRuntime.TeleportTablePath(tplData)),"a file larger than the limit is not copied");
      File.WriteAllText(Path.Combine(kitOut,"teleport-table.txt"),listText);
      string loose=Path.Combine(root,"tpl-loose");Directory.CreateDirectory(loose);File.WriteAllText(Path.Combine(loose,"rock_neo_rt.exe"),"x");File.WriteAllText(Path.Combine(root,"teleport-table.txt"),listText);
      Check(!GameRuntime.TakeBuiltTeleportList(Path.Combine(loose,"rock_neo_rt.exe"),tplData)&&!File.Exists(GameRuntime.TeleportTablePath(tplData)),"a program that is not in a kit's build\\game folder brings no list");
      Check(!GameRuntime.TakeBuiltTeleportList("",tplData)&&!GameRuntime.TakeBuiltTeleportList(kitExe,"")&&!GameRuntime.TakeBuiltTeleportList("C:\\no\\such\\build\\game\\x.exe",tplData),"empty and missing paths do nothing and do not throw");
      Directory.CreateDirectory(GameRuntime.TeleportTablePath(tplData));Check(!GameRuntime.TakeBuiltTeleportList(kitExe,tplData),"a folder where the list should be is left alone");Directory.Delete(GameRuntime.TeleportTablePath(tplData));
      {// a leftover or blocked ".part" of an older try is not in the way, and many starts at once end with exactly one whole list
        string blocked=Path.Combine(root,"tpl-blocked");Directory.CreateDirectory(blocked);Directory.CreateDirectory(GameRuntime.TeleportTablePath(blocked)+".part");
        Check(GameRuntime.TakeBuiltTeleportList(kitExe,blocked)&&File.ReadAllText(GameRuntime.TeleportTablePath(blocked))==listText,"a folder named like the old temporary file does not block the copy");
        int bad=0,none=0;for(int round=0;round<40;round++){string many=Path.Combine(root,"tpl-many",round.ToString());var tplThreads=new Thread[6];for(int k=0;k<tplThreads.Length;k++){tplThreads[k]=new Thread(delegate(){try{GameRuntime.TakeBuiltTeleportList(kitExe,many);}catch{Interlocked.Increment(ref bad);}});}
          foreach(var th in tplThreads)th.Start();foreach(var th in tplThreads)th.Join();
          if(!File.Exists(GameRuntime.TeleportTablePath(many)))none++;else if(File.ReadAllText(GameRuntime.TeleportTablePath(many))!=listText)bad++;
          if(Directory.GetFiles(many,"*.part").Length!=0)bad++;}
        Check(bad==0&&none==0,"six starts at once, 40 times: always one whole list, no leftover temporary file, nothing thrown (missing "+none+", wrong "+bad+")");
      }
      string freshData=Path.Combine(root,"tpl-fresh","games","mml1");Check(GameRuntime.TakeBuiltTeleportList(kitExe,freshData)&&File.Exists(GameRuntime.TeleportTablePath(freshData)),"a data folder that does not exist yet is made (a new player)");
    }
    {string tmpt=Path.Combine(Path.GetTempPath(),"fd-tele-"+Guid.NewGuid().ToString("N")+".xml");try{GameRuntime.Save(tmpt,tpv);Check(GameRuntime.Load(tmpt).Teleport,"the teleport choice (on) is saved and read back");tpv.Teleport=false;GameRuntime.Save(tmpt,tpv);Check(!GameRuntime.Load(tmpt).Teleport,"the teleport choice (off) is saved and read back");}finally{if(File.Exists(tmpt))File.Delete(tmpt);if(File.Exists(tmpt+".bak"))File.Delete(tmpt+".bak");}}
    Check(new RuntimeSettings().Teleport,"teleport is on in a new settings object (the Teleport line in the pause menu shows by default)");
    {var wsv=variant("contract 2.20 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie,volume-split,teleport,widescreen,wide-sky");File.WriteAllText(Path.Combine(Path.GetDirectoryName(wsv.Program),"identity-contract.txt"),"2.20");var wsr=GameRuntime.Readiness(wsv,cue,data,known,true);Check(wsr.Ready&&wsr.HasWidescreen,"a program with contract 2.20 and the widescreen feature is ready and reports it: "+wsr.Problem);
    Check(!new RuntimeSettings().Widescreen,"widescreen is off in a new settings object (it runs a changed game, so it is only on when the player ticks it)");
    wsv.Widescreen=false;Check(!GameRuntime.BuildArguments(wsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true,true).Contains("--widescreen"),"widescreen not ticked: nothing is passed");
    wsv.Widescreen=true;string wsOn=GameRuntime.BuildArguments(wsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true,true);Check(wsOn.Contains(" --widescreen 53 ")||wsOn.EndsWith(" --widescreen 53"),"widescreen ticked: --widescreen 53 is passed: "+wsOn);
    Check(!GameRuntime.BuildArguments(wsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true,false).Contains("--widescreen"),"the option is not passed to a program that does not report the feature");
    wsv.OriginalPicture=true;Check(!GameRuntime.BuildArguments(wsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true,true).Contains("--widescreen"),"with the original picture chosen, widescreen is not passed (the program needs the sharper picture)");wsv.OriginalPicture=false;
    Check(GameRuntime.BuildArguments(wsv,cue,data,true,true,false,false,false,false,true,false,null,true,false,false,true,true,true).Contains("--teleport-file")&&wsOn.Contains("--teleport-file")&&wsOn.Contains(" --window-height ")&&wsOn.Contains(" --card0 ")&&wsOn.Contains(" --widescreen 53"),"other options (teleport file, window height, card) are still passed in the same line as --widescreen 53");
    {string tmpw=Path.Combine(Path.GetTempPath(),"fd-wide-"+Guid.NewGuid().ToString("N")+".xml");try{GameRuntime.Save(tmpw,wsv);Check(GameRuntime.Load(tmpw).Widescreen,"the widescreen choice (on) is saved and read back");wsv.Widescreen=false;GameRuntime.Save(tmpw,wsv);Check(!GameRuntime.Load(tmpw).Widescreen,"the widescreen choice (off) is saved and read back");}finally{if(File.Exists(tmpw))File.Delete(tmpw);if(File.Exists(tmpw+".bak"))File.Delete(tmpw+".bak");}}
    var wsNo=GameRuntime.Readiness(variant("contract 2.14 copy for widescreen","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie,volume-split,teleport"),cue,data,known,true);Check(!wsNo.HasWidescreen,"a program without the feature does not report widescreen");
    // the feature word lock-fallback in the program report is what makes the launcher turn the backup target picker off
    Check(GameRuntime.Readiness(variant("lock-fallback copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,lock-fallback"),cue,data,known,true).HasLockFallback,"the feature word lock-fallback is read from the program report");
    Check(!GameRuntime.Readiness(variant("no lock-fallback copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume"),cue,data,known,true).HasLockFallback,"a program without the word does not report the backup picker option");
    {var allv=variant("contract 2.23 copy, long feature line","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,movement,thirdperson,pad-map,look-options,lockon,aim-style,camera-distance,lock-marker,intro-movie,volume-split,teleport,teleport-menu,teleport-line,bar-color,multiplayer,hires-feedback-exact,widescreen,wide-hud,wide-sky,wide-test");File.WriteAllText(Path.Combine(Path.GetDirectoryName(allv.Program),"identity-contract.txt"),"2.23");var allr=GameRuntime.Readiness(allv,cue,data,known,true);Check(allr.Ready&&allr.HasWidescreen&&allr.HasMultiplayer&&allr.HasTeleport&&allr.HasCameraDistance,"a feature line longer than 300 characters is read to its end (the contract 2.23 program: widescreen and multiplayer are its last words): "+allr.Problem);Check(GameRuntime.IdentityLineLimit>=2000&&GameRuntime.IdentityTextLimit>=4*GameRuntime.IdentityLineLimit,"the limits of the identity answer leave room for a long feature line");}
    Check(GameRuntime.SkHash.Length==64&&GameRuntime.SkHash==GameRuntime.SkHash.ToUpperInvariant()&&GameRuntime.SkHash!=GameRuntime.TgHash&&GameRuntime.IsKnownBuild(GameRuntime.SkHash,GameRuntime.KnownHash),"the widescreen build (rock_neo_rt_sk.exe) is a known build");
    Check(GameRuntime.FbHash.Length==64&&GameRuntime.FbHash==GameRuntime.FbHash.ToUpperInvariant()&&GameRuntime.FbHash!=GameRuntime.SkHash&&GameRuntime.IsKnownBuild(GameRuntime.FbHash,GameRuntime.KnownHash),"the contract 2.21 build (rock_neo_rt_fb.exe) is a known build");
    Check(GameRuntime.AllHash.Length==64&&GameRuntime.AllHash==GameRuntime.AllHash.ToUpperInvariant()&&GameRuntime.AllHash!=GameRuntime.FbHash&&GameRuntime.IsKnownBuild(GameRuntime.AllHash,GameRuntime.KnownHash),"the contract 2.23 build (rock_neo_rt_all2.exe, every feature in one program) is a known build");
    Check(GameRuntime.All5Hash.Length==64&&GameRuntime.All5Hash==GameRuntime.All5Hash.ToUpperInvariant()&&GameRuntime.All5Hash!=GameRuntime.AllHash&&GameRuntime.IsKnownBuild(GameRuntime.All5Hash,GameRuntime.KnownHash),"the contract 2.24 build (rock_neo_rt_all5.exe) is a known build");
    Check(GameRuntime.Fv2Hash.Length==64&&GameRuntime.Fv2Hash!=GameRuntime.All5Hash&&GameRuntime.IsKnownBuild(GameRuntime.Fv2Hash,GameRuntime.KnownHash),"the contract 2.25 build (rock_neo_rt_fv2.exe) is a known build");
    Check(GameRuntime.All6Hash.Length==64&&GameRuntime.All6Hash==GameRuntime.All6Hash.ToUpperInvariant()&&GameRuntime.All6Hash!=GameRuntime.Fv2Hash&&GameRuntime.IsKnownBuild(GameRuntime.All6Hash,GameRuntime.KnownHash),"the contract 2.27 build (rock_neo_rt_all6.exe) is a known build");
    Check(GameRuntime.All7Hash.Length==64&&GameRuntime.All7Hash==GameRuntime.All7Hash.ToUpperInvariant()&&GameRuntime.All7Hash!=GameRuntime.All6Hash&&GameRuntime.IsKnownBuild(GameRuntime.All7Hash,GameRuntime.KnownHash),"the contract 2.28 build (rock_neo_rt_all7.exe) is a known build");}
    Check(GameRuntime.TfHash.Length==64&&GameRuntime.TfHash!=GameRuntime.TlHash&&GameRuntime.TfHash==GameRuntime.TfHash.ToUpperInvariant()&&GameRuntime.IsKnownBuild(GameRuntime.TfHash,GameRuntime.KnownHash),"the Teleport-font build is a known build");
    {string tmpd=Path.Combine(Path.GetTempPath(),"fd-tele-def-"+Guid.NewGuid().ToString("N")+".xml");try{tpv.Teleport=false;GameRuntime.Save(tmpd,tpv);string xml=File.ReadAllText(tmpd);Check(xml.Contains("teleport=\"0\""),"the switched-off choice is written as 0");File.WriteAllText(tmpd,xml.Replace(" teleport=\"0\"",""));Check(GameRuntime.Load(tmpd).Teleport,"a saved file with no teleport choice reads as on");}finally{if(File.Exists(tmpd))File.Delete(tmpd);if(File.Exists(tmpd+".bak"))File.Delete(tmpd+".bak");}}
    var tpOld=variant("contract 2.13 copy again","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble,intro-movie,volume-split");File.WriteAllText(Path.Combine(Path.GetDirectoryName(tpOld.Program),"identity-contract.txt"),"2.13");Check(!GameRuntime.Readiness(tpOld,cue,data,known,true).HasTeleport,"a program without the feature does not report teleport");}
    var olds=variant("contract 2.9 copy again","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture,window-height,controller-hid,rumble");File.WriteAllText(Path.Combine(Path.GetDirectoryName(olds.Program),"identity-contract.txt"),"2.9");Check(!GameRuntime.Readiness(olds,cue,data,known,true).HasIntroMovie,"a program without the feature does not report the movie option");}
    {var hdv=variant("contract 2.4 copy","identity-features.txt","identity,card-new,exe-from-disc,run-dir-created,fail-file,settings,volume,present,hires,hires-filter,first-boot-picture");File.WriteAllText(Path.Combine(Path.GetDirectoryName(hdv.Program),"identity-contract.txt"),"2.4");var hdr=GameRuntime.Readiness(hdv,cue,data,known,true);Check(hdr.Ready&&hdr.HasPictureOption&&hdr.Build.StartsWith("contract 2.4"),"a program with contract 2.4 and first-boot-picture is ready and reports the picture option: "+hdr.Problem+" "+hdr.Build);}
    Check(GameRuntime.EarlierHash!=GameRuntime.KnownHash&&GameRuntime.EarlierHash.Length==64&&GameRuntime.KnownHash.Length==64,"the earlier and current build hashes are distinct");
    Check(GameRuntime.HpHash.Length==64&&GameRuntime.HpHash!=GameRuntime.HdHash&&GameRuntime.HpHash!=GameRuntime.KnownHash&&GameRuntime.HpHash==GameRuntime.HpHash.ToUpperInvariant()&&GameRuntime.IsKnownBuild(GameRuntime.HpHash,GameRuntime.KnownHash),"the 2.5 build hash is a different, full, upper-case, known fingerprint");
    Check(GameRuntime.HdHash.Length==64&&GameRuntime.HdHash!=GameRuntime.KnownHash&&GameRuntime.HdHash!=GameRuntime.EarlierHash&&GameRuntime.HdHash==GameRuntime.HdHash.ToUpperInvariant(),"the 2.4 build hash is a different, full, upper-case fingerprint");
    Check(GameRuntime.IsKnownBuild(GameRuntime.KnownHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.HdHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.HdHash.ToLowerInvariant(),GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.HpHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.HcHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.HcHash.ToLowerInvariant(),GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.Hc2Hash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.MrHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.Mr2Hash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.MiHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.VsHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.TeleHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.TmHash,GameRuntime.KnownHash)&&GameRuntime.IsKnownBuild(GameRuntime.TlHash,GameRuntime.KnownHash)&&GameRuntime.TmHash.Length==64&&GameRuntime.TlHash.Length==64&&GameRuntime.TeleHash.Length==64&&GameRuntime.VsHash.Length==64&&!GameRuntime.IsKnownBuild(GameRuntime.EarlierHash,GameRuntime.KnownHash)&&!GameRuntime.IsKnownBuild("00",GameRuntime.KnownHash),"the 2.0, 2.4, 2.5 and 2.6 builds are known, the 1.x build and any other are not");

    // arguments
    string a=GameRuntime.BuildArguments(s,cue,data);
    Check(a==Expected(cue,data,"--card-new",3,100,false),"exact argument line with an existing card (--card-new is safe on every start): "+a);
    s.Mute=true;s.Scale=5;s.Volume=35;Check(GameRuntime.BuildArguments(s,cue,data)==Expected(cue,data,"--card-new",5,35,true),"scale, volume and mute from settings");s.Mute=false;s.Scale=3;s.Volume=100;
    s.OriginalPicture=true;Check(GameRuntime.BuildArguments(s,cue,data)==a&&GameRuntime.BuildArguments(s,cue,data,false)==a,"--original-picture is never passed unless the program reports the feature");
    Check(GameRuntime.BuildArguments(s,cue,data,true)==a+" --original-picture"&&GameRuntime.BuildArguments(s,cue,data,true).EndsWith(" --original-picture"),"--original-picture is added (last) when ticked and the program reports first-boot-picture");
    {bool op=s.OriginalPicture;s.OriginalPicture=false;s.OriginalMovement=false;
      Check(GameRuntime.BuildArguments(s,cue,data,false,false,false)==a&&!GameRuntime.BuildArguments(s,cue,data,true,true,false).Contains("--movement"),"--movement is never passed unless the program reports the feature 'movement'");
      Check(GameRuntime.BuildArguments(s,cue,data,false,false,true)==a+" --movement modern","smooth movement on (the default): --movement modern is passed explicitly, last");
      s.OriginalMovement=true;Check(GameRuntime.BuildArguments(s,cue,data,false,false,true)==a+" --movement original","smooth movement ticked off: --movement original");
      s.OriginalMovement=false;s.OriginalPicture=true;Check(GameRuntime.BuildArguments(s,cue,data,true,false,true).EndsWith(" --original-picture --movement modern"),"movement comes after the picture option");
      s.OriginalPicture=op;s.OriginalMovement=false;
      Check(GameRuntime.IsKnownBuild(GameRuntime.MmHash,GameRuntime.KnownHash),"the contract 2.7 build is a known build");
      Check(GameRuntime.IsKnownBuild(GameRuntime.TpHash,GameRuntime.KnownHash),"the contract 2.8 build is a known build");
      {var t=new RuntimeSettings();string d2=Path.Combine(Path.GetTempPath(),"fd-tp-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(d2);
        try{
          string b=GameRuntime.BuildArguments(t,cue,data,false,false,true,false,false,false);
          Check(!b.Contains("--controls")&&!b.Contains("--look-")&&!b.Contains("--pad-map"),"2.8 options are never passed unless the program reports their features");
          string c3=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true);
          Check(c3.Contains(" --controls thirdperson")&&c3.Contains(" --movement modern")&&c3.EndsWith(" --look-sensitivity 100 --look-invert 0"),"third-person on (default): --controls thirdperson, --movement modern, look options last");
          t.OriginalControls=true;t.OriginalMovement=true;c3=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,false);
          Check(c3.Contains(" --controls original")&&c3.Contains(" --movement original"),"third-person off and smooth movement off: both original");
          t.OriginalControls=false;t.OriginalMovement=true;c3=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,false);
          Check(c3.Contains(" --controls thirdperson")&&c3.Contains(" --movement modern")&&!c3.Contains("--movement original"),"third-person on forces smooth movement on (the game refuses the other combination)");
          t.OriginalControls=false;t.OriginalMovement=false;t.LookSensitivity=150;t.InvertLookY=true;c3=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true);
          Check(c3.EndsWith(" --look-sensitivity 150 --look-invert 1"),"camera speed and invert are passed");
          var c4=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true);
          Check(c4.EndsWith(" --look-sensitivity 150 --look-invert 1 --lockon 1 --aim-style free"),"z-target on and free aim are passed explicitly (contract 2.11)");
          t.ZTargetOff=true;t.AimStyleCamera=true;c4=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true);
          Check(c4.EndsWith(" --lockon 0 --aim-style camera"),"z-target off and camera aim are passed");
          t.ZTargetOff=false;t.AimStyleCamera=false;t.OriginalControls=true;c4=GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true);t.OriginalControls=false;
          Check(c4.EndsWith(" --lockon 0 --aim-style free"),"with the original controls the lock-on option is passed as off (it needs third-person controls)");
          Check(!GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,false).Contains("--lockon"),"an older program (no lockon feature) gets neither option");
          t.CameraDistance=130;Check(GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true,null,false,false,true).EndsWith(" --camera-distance 130"),"camera distance is passed when the program reports it");
          Check(!GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true).Contains("--camera-distance"),"an older program gets no camera distance");t.CameraDistance=100;
          Check(!new RuntimeSettings().CameraWalls,"camera wall collision is off in a new settings object (experimental)");
          t.CameraWalls=false;Check(!GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true,null,false,false,false,false,false,false,true).Contains("--camera-walls"),"camera walls not ticked: nothing is passed");
          t.CameraWalls=true;Check(GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true,null,false,false,false,false,false,false,true).Contains(" --camera-walls 1"),"camera walls ticked: --camera-walls 1");
          Check(new RuntimeSettings().FarView==160,"draw distance defaults to 160 percent");
          t.FarView=140;Check(GameRuntime.BuildArguments(t,cue,data,farViewOption:true).EndsWith(" --far-view 140"),"draw distance is passed when the program reports far-view");
          Check(!GameRuntime.BuildArguments(t,cue,data).Contains("--far-view"),"a program without far-view gets no draw distance");
          t.FarView=0;Check(GameRuntime.BuildArguments(t,cue,data,farViewOption:true).EndsWith(" --far-view 0"),"draw distance 0 (the game's own) is passed explicitly");
          t.FarView=999;Check(GameRuntime.BuildArguments(t,cue,data,farViewOption:true).EndsWith(" --far-view 200"),"an out-of-range draw distance is clamped to 200");t.FarView=160;
          Check(!GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true,false,true).Contains("--camera-walls"),"a program that does not report camera-walls gets nothing");t.CameraWalls=false;
          t.LookSensitivity=137;Check(GameRuntime.BuildArguments(t,cue,data,false,false,true,true,false,true).Contains(" --look-sensitivity 150 "),"a camera speed that is not a preset is moved to the nearest preset");
          Check(!GameRuntime.BuildArguments(t,cue,d2,false,false,true,true,true,true).Contains("--pad-map"),"no --pad-map while the file does not exist");
          GameRuntime.EnsurePadMap(d2);string pm=File.ReadAllText(GameRuntime.PadMapPath(d2));
          Check(pm.Contains("CAM_RESET")&&pm.Contains("# A = CROSS")&&pm.Split(new[]{'\n'}).All(l=>l.Trim().Length==0||l.TrimStart().StartsWith("#")),"the pad map template is written, every line a comment or blank");
          Check(GameRuntime.BuildArguments(t,cue,d2,false,false,true,true,true,true).Contains(" --pad-map \""+GameRuntime.PadMapPath(d2)+"\""),"--pad-map is passed once the file exists");
          File.WriteAllText(GameRuntime.PadMapPath(d2),"A = SQUARE\r\n");GameRuntime.EnsurePadMap(d2);Check(File.ReadAllText(GameRuntime.PadMapPath(d2))=="A = SQUARE\r\n","an existing pad map is never rewritten");
          string xml2=Path.Combine(d2,"r.xml");t.OriginalControls=true;t.InvertLookY=true;t.LookSensitivity=75;t.Program="C:/x/p.exe";GameRuntime.Save(xml2,t);var u=GameRuntime.Load(xml2);
          Check(u.OriginalControls&&u.InvertLookY&&u.LookSensitivity==75,"controls, invert and camera speed are saved and read back");
        }finally{try{Directory.Delete(d2,true);}catch(Exception){}}
      }
      string xml=Path.Combine(Path.GetTempPath(),"fd-mm-"+Guid.NewGuid().ToString("N")+".xml");
      try{var t=new RuntimeSettings{Program="C:\\x\\rock_neo_rt_mm.exe",OriginalMovement=true};GameRuntime.Save(xml,t);Check(GameRuntime.Load(xml).OriginalMovement,"the movement choice is saved and read back (off)");t.OriginalMovement=false;GameRuntime.Save(xml,t);Check(!GameRuntime.Load(xml).OriginalMovement,"the movement choice is saved and read back (on)");}
      finally{try{File.Delete(xml);}catch(Exception){}}
    }
    s.OriginalPicture=false;Check(GameRuntime.BuildArguments(s,cue,data,true)==a,"nothing is added when the setting is off, even for a 2.4 program");
    s.WindowHeight=1080;Check(GameRuntime.BuildArguments(s,cue,data,true,false)==a&&!GameRuntime.BuildArguments(s,cue,data,true,false).Contains("--window-height"),"--window-height is never passed unless the program reports the feature");
    string ah=GameRuntime.BuildArguments(s,cue,data,true,true);Check(ah==a.Replace(" --scale 3"," --window-height 1080")&&!ah.Contains("--scale"),"--window-height H takes the place of --scale N (same position): "+ah);
    s.OriginalPicture=true;Check(GameRuntime.BuildArguments(s,cue,data,true,true).EndsWith(" --window-height 1080 --volume 100 --fail-file \""+GameRuntime.FailFilePath(data)+"\" --original-picture"),"with the picture option the order is kept (height in place of scale, original picture last)");s.OriginalPicture=false;s.WindowHeight=720;
    foreach(int hgt in GameRuntime.WindowHeights){s.WindowHeight=hgt;Check(GameRuntime.BuildArguments(s,cue,data,true,true).Contains(" --window-height "+hgt+" "),"preset "+hgt+" is passed as it is");}s.WindowHeight=720;
    File.WriteAllText(settingsPath,"<flutterDeckRuntime schema=\"1\"><game id=\"1\" program=\"C:\\x\\rock_neo_rt_fd.exe\" acceptedHash=\"\" scale=\"4\" mute=\"0\" volume=\"100\" /></flutterDeckRuntime>");Check(GameRuntime.Load(settingsPath).WindowHeight==1080&&GameRuntime.Load(settingsPath).Scale==4,"a settings file from before the presets (scale 4, no windowHeight) loads as 1080p");
    Check(GameRuntime.HeightFromScale(2)==480&&GameRuntime.HeightFromScale(3)==720&&GameRuntime.HeightFromScale(4)==1080&&GameRuntime.HeightFromScale(5)==1080&&GameRuntime.HeightFromScale(6)==1440,"an old whole-number window size maps to the nearest preset (2x to 6x -> 480p, 720p, 1080p, 1080p, 1440p)");
    Check(GameRuntime.ClampHeight(0)==720&&GameRuntime.ClampHeight(999)==720&&GameRuntime.ClampHeight(-5)==720&&GameRuntime.ClampHeight(4320)==720&&GameRuntime.ClampHeight(2160)==2160&&GameRuntime.ClampHeight(480)==480,"only the five presets are accepted, anything else is 720");
    foreach(string banned in new[]{"--max-seconds","--max-frames","--no-activate","--audio-silent","--pad-script","--pad-pipe","--live-inject","--poke","--dump-prims","--test-","--unpaced","--wav","--fullscreen","--card1","--settings","--exe \"","--identity"})Check(!a.Contains(banned),"never passes "+banned);
    Throws<ArgumentException>(delegate{GameRuntime.BuildArguments(s,folder+"\\we\"ird.cue",data);},"quotation mark in a path refused");

    // start with the stand-in: exit 0, even though standard error is not empty
    File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");File.WriteAllText(GameRuntime.FailFilePath(data),"exit=2\r\nreason=left over from an earlier refusal\r\n");
    var session=GameRuntime.Start(s,cue,data,known);Check(session.Process.WaitForExit(20000),"stand-in exited");session.Process.WaitForExit();
    string[] got=File.ReadAllLines(Path.Combine(folder,"received.txt"));
    Check(got.Length==18&&got[0]=="--live"&&got[1]=="--exe-from-disc"&&got[2]=="ROCK_NEO.EXE"&&got[4]==GameRuntime.SessionsPath(data)&&got[6]==cue&&got[8]==GameRuntime.CardPath(data)&&got[9]=="--card-new"&&got[11]==GameRuntime.KeysPath(data)&&got[13]=="3"&&got[15]=="100","paths with spaces and parentheses arrive whole: "+String.Join(" | ",got));
    Check(got[got.Length-2]=="--fail-file"&&got[got.Length-1]==GameRuntime.FailFilePath(data),"fail file passed last");
    Check(session.Process.ExitCode==0&&session.ErrorText.Length>0&&GameRuntime.ExitReason(0,session.SessionsFolder,session.StartedUtc,session.ErrorText.ToString(),session.Arguments)=="","exit 0 shows nothing, whatever was written to standard error");
    Check(!File.Exists(GameRuntime.FailFilePath(data)),"an earlier fail file is removed before each start");
    Check(Directory.Exists(GameRuntime.SessionsPath(data)),"session folder created before start");
    string keys=File.ReadAllText(GameRuntime.KeysPath(data));int lines=0;foreach(string line in GameRuntime.DefaultKeys){if(keys.Contains(line+"\r\n"))lines++;}Check(lines==14&&keys.Replace("\r\n","").IndexOf('\n')<0,"default key map written with all fourteen buttons, CRLF");
    File.WriteAllText(GameRuntime.KeysPath(data),"CROSS = K\r\n");GameRuntime.EnsureKeys(data);Check(File.ReadAllText(GameRuntime.KeysPath(data))=="CROSS = K\r\n","an existing key map is never rewritten");

    // exit 3: TRAP line found anywhere in the log
    File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"3");
    session=GameRuntime.Start(s,cue,data,known);session.Process.WaitForExit();
    string reason=GameRuntime.ExitReason(session.Process.ExitCode,session.SessionsFolder,session.StartedUtc,session.ErrorText.ToString(),session.Arguments);
    Check(session.Process.ExitCode==3&&reason.Contains("TRAP: cannot open disc image")&&reason.Contains("live_test.log"),"exit 3 shows the TRAP line and the log: "+reason);
    Check(!File.Exists(GameRuntime.FailFilePath(data)),"no fail file after exit 3");
    // exit 2: the reason from the game's fail file, and the arguments
    File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"2");File.Delete(Path.Combine(GameRuntime.SessionsPath(data),"live_test.log"));
    session=GameRuntime.Start(s,cue,data,known);session.Process.WaitForExit();Thread.Sleep(300);
    reason=GameRuntime.ExitReason(session.Process.ExitCode,session.SessionsFolder,session.StartedUtc,session.ErrorText.ToString(),session.Arguments);
    Check(session.Process.ExitCode==2&&reason.Contains("an option without its value: '--example'")&&reason.Contains("--live --exe-from-disc")&&reason.Contains("launcher problem"),"exit 2 shows the fail-file reason and the arguments: "+reason);
    File.Delete(GameRuntime.FailFilePath(data));
    Check(GameRuntime.ExitReason(2,session.SessionsFolder,session.StartedUtc,"said on standard error","args").Contains("said on standard error"),"exit 2 falls back to standard error when there is no fail file");
    File.WriteAllText(GameRuntime.FailFilePath(data),"exit=2\r\nreason=stale\r\n");File.SetLastWriteTimeUtc(GameRuntime.FailFilePath(data),DateTime.UtcNow.AddHours(-1));
    Check(!GameRuntime.ExitReason(2,session.SessionsFolder,DateTime.UtcNow,"","args").Contains("stale"),"a fail file older than the session is not used");File.Delete(GameRuntime.FailFilePath(data));
    // other codes: RUN END line
    File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"1");
    session=GameRuntime.Start(s,cue,data,known);session.Process.WaitForExit();
    reason=GameRuntime.ExitReason(session.Process.ExitCode,session.SessionsFolder,session.StartedUtc,"",session.Arguments);
    Check(reason.Contains("exit code 1")&&reason.Contains("RUN END: stopped (exit code 1)"),"other exit codes show the RUN END line: "+reason);
    Check(GameRuntime.ExitReason(3,Path.Combine(root,"nowhere"),DateTime.UtcNow,"","").Contains("gave no reason"),"exit 3 with no log says so");
    Check(GameRuntime.ExitReason(3,Path.Combine(root,"nowhere"),DateTime.UtcNow,"state report\r\nTRAP: --card-new: cannot create the card\r\nmore\r\n","").Contains("TRAP: --card-new: cannot create the card"),"exit 3 with no log falls back to the TRAP line on standard error");
    Check(GameRuntime.NewestLog(GameRuntime.SessionsPath(data),DateTime.UtcNow.AddHours(1))==null,"a log older than the session is not used");

    // one game at a time on a card, whichever copy of the program is started and whichever launcher asks
    Check(!GameRuntime.AlreadyRunning(program)&&!GameRuntime.CardInUse(data),"not running when nothing is");
    File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"sleep");
    string folder2=Path.Combine(root,"second copy");Directory.CreateDirectory(folder2);string program2=Path.Combine(folder2,"rock_neo_rt_fd.exe");File.Copy(program,program2);File.WriteAllText(Path.Combine(folder2,"stand-in-exit.txt"),"0");
    var second=new RuntimeSettings{Program=program2,Scale=3};
    session=GameRuntime.Start(s,cue,data,known);
    Check(GameRuntime.CardInUse(data)&&GameRuntime.AlreadyRunning(program),"a running game marks its card in use");
    Throws<InvalidOperationException>(delegate{GameRuntime.Start(s,cue,data,known);},"the same program is not started twice");
    Throws<InvalidOperationException>(delegate{GameRuntime.Start(second,cue,data,known);},"another copy of the program is not started on the same card");
    Check(!File.Exists(Path.Combine(folder2,"received.txt")),"the refused second copy never ran");
    GameRuntime.ReleaseLock(data);Check(GameRuntime.CardInUse(data),"the marker of a running game is not removed");
    session.Process.WaitForExit();
    Check(!GameRuntime.CardInUse(data),"the card is free again once the game has ended");
    GameRuntime.ReleaseLock(data);Check(!File.Exists(GameRuntime.LockPath(data)),"marker removed after the game ended");
    var me=System.Diagnostics.Process.GetCurrentProcess();
    File.WriteAllText(GameRuntime.LockPath(data),"999999|123|rock_neo_rt_fd");Check(!GameRuntime.CardInUse(data),"a marker naming a process that is gone does not block");
    File.WriteAllText(GameRuntime.LockPath(data),"garbage");Check(!GameRuntime.CardInUse(data),"an unreadable marker does not block");
    File.WriteAllText(GameRuntime.LockPath(data),me.Id+"|1|"+me.ProcessName);Check(!GameRuntime.CardInUse(data),"a reused process id with another start time does not block");
    File.WriteAllText(GameRuntime.LockPath(data),me.Id+"|"+me.StartTime.ToUniversalTime().Ticks+"|some_other_program");Check(!GameRuntime.CardInUse(data),"a reused process id running another program does not block");
    foreach(int systemPid in new[]{0,4}){File.WriteAllText(GameRuntime.LockPath(data),systemPid+"|0|rock_neo_rt_fd");Check(!GameRuntime.CardInUse(data),"a marker that now points at a system process does not lock the card: pid "+systemPid);}
    File.WriteAllText(GameRuntime.LockPath(data),me.Id+"|"+me.StartTime.ToUniversalTime().Ticks+"|"+me.ProcessName);Check(GameRuntime.CardInUse(data),"a marker naming a live process with the same name and start time does block");
    GameRuntime.ReleaseLock(data);Check(!File.Exists(GameRuntime.LockPath(data)),"a launcher clears a mark that still names itself");
    File.WriteAllText(GameRuntime.LockPath(data),"999999|123|rock_neo_rt_fd");GameRuntime.ReleaseLock(data);Check(!File.Exists(GameRuntime.LockPath(data)),"a stale marker is removed");
    // clearing a stale marker waits its turn: while another launcher holds the card's gate (a start in progress), nothing is removed
    File.WriteAllText(GameRuntime.LockPath(data),"999999|123|rock_neo_rt_fd");
    var holding=new ManualResetEvent(false);var letGo=new ManualResetEvent(false);
    var holder=new Thread(delegate(){using(var gate=new Mutex(false,GameRuntime.GateName(data))){gate.WaitOne();holding.Set();letGo.WaitOne();gate.ReleaseMutex();}});holder.Start();holding.WaitOne();
    GameRuntime.ReleaseLock(data);Check(File.Exists(GameRuntime.LockPath(data)),"a marker is not cleared while a start is in progress on that card");
    letGo.Set();holder.Join();GameRuntime.ReleaseLock(data);Check(!File.Exists(GameRuntime.LockPath(data)),"and is cleared once the gate is free");
    // if the card cannot be marked, nothing is started
    Directory.CreateDirectory(GameRuntime.LockPath(data));File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");File.Delete(Path.Combine(folder,"received.txt"));
    Throws<InvalidOperationException>(delegate{GameRuntime.Start(s,cue,data,known);},"no start when the card cannot be marked");
    Thread.Sleep(300);Check(!File.Exists(Path.Combine(folder,"received.txt")),"the game did not run unmarked");
    Directory.Delete(GameRuntime.LockPath(data));
    // six launchers pressing Play at the same moment, each with its own copy of the program: exactly one game starts
    string raceData=Path.Combine(root,"games","race");
    int started=0,refused=0;var threads=new System.Collections.Generic.List<Thread>();var go=new ManualResetEvent(false);var sessions=new System.Collections.Generic.List<GameSession>();
    for(int i=0;i<6;i++){
      string f=Path.Combine(root,"race copy "+i);Directory.CreateDirectory(f);string exe=Path.Combine(f,"rock_neo_rt_fd.exe");File.Copy(program,exe);File.WriteAllText(Path.Combine(f,"stand-in-exit.txt"),"sleep");
      var rs=new RuntimeSettings{Program=exe,Scale=3};
      var t=new Thread(delegate(){go.WaitOne();try{var g=GameRuntime.Start(rs,cue,raceData,known);lock(sessions){sessions.Add(g);}Interlocked.Increment(ref started);}catch(InvalidOperationException){Interlocked.Increment(ref refused);}});
      threads.Add(t);t.Start();
    }
    go.Set();foreach(var t in threads)t.Join();
    Check(started==1&&refused==5,"simultaneous starts on one card: "+started+" started, "+refused+" refused");
    int ran=0;for(int i=0;i<6;i++)if(File.Exists(Path.Combine(root,"race copy "+i,"received.txt")))ran++;
    Check(ran==1,"only one copy of the game actually ran: "+ran);
    foreach(var g in sessions)g.Process.WaitForExit();
    File.Delete(GameRuntime.LockPath(data));File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");

    // hand-edited settings: relative locations are refused, odd sizes fall back, an empty key map is repaired
    Check(GameRuntime.CheckProgram("rock_neo_rt_fd.exe").Contains("full path")&&GameRuntime.CheckProgram(@"sub\rock_neo_rt_fd.exe").Contains("full path")&&GameRuntime.CheckProgram(folder+@"\..\my game folder\rock_neo_rt_fd.exe").Contains("full path")&&GameRuntime.CheckProgram(program.Replace('\\','/')).Contains("full path"),"relative or unnormalised game program refused");
    Check(GameRuntime.CheckCue("game.cue").Contains("full path"),"relative disc image refused");
    Check(GameRuntime.ClampScale(1)==3&&GameRuntime.ClampScale(7)==3&&GameRuntime.ClampScale(2)==2&&GameRuntime.ClampScale(6)==6,"window size limited to what the screen offers");
    Check(GameRuntime.ClampVolume(-1)==100&&GameRuntime.ClampVolume(101)==100&&GameRuntime.ClampVolume(0)==0&&GameRuntime.ClampVolume(55)==55,"volume limited to 0..100");
    File.WriteAllText(GameRuntime.KeysPath(data),"");GameRuntime.EnsureKeys(data);Check(File.ReadAllText(GameRuntime.KeysPath(data)).Contains("SELECT = RSHIFT"),"an empty key map is repaired");
    File.WriteAllBytes(GameRuntime.KeysPath(data),new byte[]{0xEF,0xBB,0xBF,0x0D,0x0A,0x20});File.SetAttributes(GameRuntime.KeysPath(data),FileAttributes.ReadOnly);GameRuntime.EnsureKeys(data);Check(File.ReadAllText(GameRuntime.KeysPath(data)).Contains("SELECT = RSHIFT"),"a read-only key map holding only a byte-order mark and blanks is repaired");
    File.WriteAllText(GameRuntime.KeysPath(data),new string('\n',4096));GameRuntime.EnsureKeys(data);Check(File.ReadAllText(GameRuntime.KeysPath(data)).Contains("SELECT = RSHIFT"),"a large key map of only blank lines is repaired");
    // clearing stale markers never removes the mark of a game that is being started or is running
    File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"sleep");bool stop=false;var releaser=new Thread(delegate(){while(!stop)GameRuntime.ReleaseLock(data);});releaser.Start();
    session=GameRuntime.Start(s,cue,data,known);Thread.Sleep(400);stop=true;releaser.Join();
    Check(GameRuntime.CardInUse(data),"a marker is never removed while its game is starting or running");
    session.Process.WaitForExit();GameRuntime.ReleaseLock(data);File.WriteAllText(Path.Combine(folder,"stand-in-exit.txt"),"0");
    // an earlier session's log is never used to explain a later one
    File.WriteAllText(Path.Combine(GameRuntime.SessionsPath(data),"earlier.log"),"TRAP: from an earlier session\r\n");Thread.Sleep(50);
    Check(!GameRuntime.ExitReason(3,GameRuntime.SessionsPath(data),DateTime.UtcNow,"","").Contains("earlier session"),"an earlier session's TRAP line is not shown");

    // leftovers of a failed save are found, never removed
    File.WriteAllBytes(GameRuntime.CardPath(data)+".tmp",new byte[4]);File.WriteAllBytes(GameRuntime.CardPath(data)+".unsaved-1.mcd",new byte[4]);
    var unsaved=GameRuntime.UnsavedFiles(data);Check(unsaved.Count==2&&File.Exists(GameRuntime.CardPath(data)+".tmp"),"failed-save files are listed and kept");

    LegendsTwo(root,folder,program,cue,data,known,s);
    Friend(folder,program,cue,data,known,s);

    if(failures>0){Console.WriteLine(failures+" game runtime check(s) failed");return 1;}
    Console.WriteLine("PASS: settings, readiness, build hash and identity, copy-only memory card, exact contract 2.0, 2.4, 2.5 and 2.6 play arguments, stand-in start with exit 0/1/2/3 reasons, fail file, one game per card, key map, failed-save files; Mega Man Legends 2 (contract L2-1): its own start line, folder, card, settings entry and slot check, and one game at a time across both; multiplayer (contract MP-1): the setting goes into the game environment only, never on the command line or into a file, and an ordinary game never gets it. Stand-in program and scratch folder only: no real game program, disc, card or launcher setting touched.");return 0;
  }
}
