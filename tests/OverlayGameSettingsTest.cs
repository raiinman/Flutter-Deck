using System;
using System.Collections.Generic;
using System.IO;

// Pure test of OverlayGameSettings.cs, the launcher's side of the overlay's Game settings panel. No window, no game, no real
// settings folder: the only file written is a scratch settings file named on the command line.
public static class OverlayGameSettingsTest {
  static int run,failed;
  static void Check(bool ok,string what){run++;if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}
  static RuntimeCheck All(){return new RuntimeCheck{Ready=true,HasPictureOption=true,HasWindowHeight=true,HasMovementOption=true,HasThirdPerson=true,HasPadMap=true,HasLookOptions=true,HasRumble=true,HasZTarget=true,HasIntroMovie=true};}
  static GameSetting Row(List<GameSetting> l,string id){foreach(GameSetting g in l)if(g.Id==id)return g;return null;}
  static string Ids(List<GameSetting> l){var a=new List<string>();foreach(GameSetting g in l)a.Add(g.Id);return String.Join(",",a.ToArray());}
  const string Cue="C:\\fixture\\disc.cue",Data="C:\\fixture\\data";

  public static int Main(string[] args){
    string scratch=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"overlay-game-settings-test");
    if(Directory.Exists(scratch))Directory.Delete(scratch,true);Directory.CreateDirectory(scratch);
    try{Everything(scratch);CodeBookChecks(scratch);}catch(Exception e){failed++;Console.WriteLine("FAIL: unexpected exception "+e);}
    Console.WriteLine((failed==0?"PASS":"FAILED")+": "+run+" game settings checks, "+failed+" failed");
    return failed==0?0:1;
  }

  static void Everything(string scratch){
    var c=All();var s=new RuntimeSettings();
    var rows=OverlayGameSettings.Build(s,c);
    Check(Ids(rows)=="window-size,sharp-picture,smooth-movement,vibration,vibration-strength,intro-movie,third-person,camera-speed,invert-look,lock-on","a program that reports everything offers these ten settings, in the setup screen's order ("+Ids(rows)+")");
    bool noAudio=true,noLock=true;foreach(GameSetting g in rows){if(g.Id.Contains("volume")||g.Id.Contains("mute")||g.Id.Contains("sound"))noAudio=false;if(g.Id.Contains("aim"))noLock=false;}
    Check(noAudio,"no game volume or mute row (audio is designed first)");
    Check(noLock,"no aim style row (the launcher removed that setting)");
    Check(Row(rows,"lock-on")!=null&&Row(rows,"lock-on").Enabled&&Row(rows,"lock-on").Value==1,"lock-on is offered, on by default, and open while third-person controls are on");
    var noZ=All();noZ.HasZTarget=false;Check(Row(OverlayGameSettings.Build(s,noZ),"lock-on")==null,"a program that does not report lock-on gets no lock-on row");
    Check(OverlayGameSettings.Build(null,c).Count==0&&OverlayGameSettings.Build(s,null).Count==0,"no settings, no rows");
    {   // Legends 1: the Legends 2 look of the lock-on marker (contract 2.29, L16)
      var mC=All();mC.HasLockMarker2=true;var mS=new RuntimeSettings{LockMarker2=false};
      var mR=OverlayGameSettings.Build(mS,mC);
      Check(Row(mR,"lock-marker-style")!=null&&Row(mR,"lock-marker-style").IsSwitch&&Row(mR,"lock-marker-style").Value==0,"a program that reports lock-marker-2 gets a marker look switch (here switched off)");
      Check(Row(OverlayGameSettings.Build(new RuntimeSettings(),mC),"lock-marker-style").Value==1,"the Legends 2 marker is on by default (owner, 2026-11-22)");
      Check(Row(OverlayGameSettings.Build(mS,All()),"lock-marker-style")==null,"a program that does not report it gets no row");
      Check(OverlayGameSettings.Set(mS,mC,"lock-marker-style",1)==""&&mS.LockMarker2,"the switch is taken");
      var fC=All();fC.HasLockFlick=true;var fS=new RuntimeSettings();
      Check(Row(OverlayGameSettings.Build(fS,fC),"lock-flick")!=null&&Row(OverlayGameSettings.Build(fS,fC),"lock-flick").Value==1&&Row(OverlayGameSettings.Build(fS,All()),"lock-flick")==null,"the flick row only for a program that reports lock-flick, on by default");
      Check(OverlayGameSettings.Set(fS,fC,"lock-flick",0)==""&&fS.NoLockFlick,"the flick can be switched off");
      var sC=All();sC.HasMovementOption=true;sC.HasSkateShoot=true;sC.HasSkateLock=true;sC.HasSkateToggle=true;var sS=new RuntimeSettings();var sR=OverlayGameSettings.Build(sS,sC);
      Check(Row(sR,"skate-shoot")!=null&&Row(sR,"skate-shoot").Value==1&&Row(sR,"skate-lock").Value==1&&Row(sR,"skate-toggle").Value==1,"the three skates rows, on by default");
      Check(Row(OverlayGameSettings.Build(sS,All()),"skate-shoot")==null&&Row(OverlayGameSettings.Build(sS,All()),"skate-toggle")==null,"no skates rows for a program without the words");
      var sOrig=new RuntimeSettings{OriginalControls=true,OriginalMovement=true};var sOR=OverlayGameSettings.Build(sOrig,sC);
      Check(!Row(sOR,"skate-lock").Enabled&&!Row(sOR,"skate-toggle").Enabled&&Row(sOR,"skate-shoot").Enabled,"lock on the skates waits for third-person controls, the toggle for smooth movement");
      Check(OverlayGameSettings.Set(sS,sC,"skate-shoot",0)==""&&sS.NoSkateShoot&&OverlayGameSettings.Set(sS,sC,"skate-toggle",0)==""&&sS.NoSkateToggle,"the switches are taken");
      var hC=All();hC.HasLockHealth=true;var hS=new RuntimeSettings();
      Check(Row(OverlayGameSettings.Build(hS,hC),"lock-health")!=null&&Row(OverlayGameSettings.Build(hS,hC),"lock-health").Value==1&&Row(OverlayGameSettings.Build(hS,All()),"lock-health")==null,"the lock-on by health row: on by default, and none for a program without the word");
      Check(OverlayGameSettings.Set(hS,hC,"lock-health",0)==""&&hS.NoLockHealth&&OverlayGameSettings.Copy(hS).NoLockHealth,"the lock-on by health switch is taken and kept by the copy");
      Check(OverlayGameSettings.Differs(new RuntimeSettings(),new RuntimeSettings{NoLockHealth=true},hC,GameRuntime.Legends1,"c.cue","d")&&!OverlayGameSettings.Differs(new RuntimeSettings(),new RuntimeSettings{NoLockHealth=true},All(),GameRuntime.Legends1,"c.cue","d"),"a changed lock-on by health switch differs from the running game, and only for a program with the word");
      var dgCopy=OverlayGameSettings.Copy(new RuntimeSettings{NoDiagnostics=true,DiagnosticsNoticeSeen=true});Check(dgCopy.NoDiagnostics&&dgCopy.DiagnosticsNoticeSeen,"the copy of the running settings keeps the diagnostics choice and its seen mark");{var gc=OverlayGameSettings.Copy(new RuntimeSettings{GuideSeen=22,GuideBuildSeen=27});Check(gc.GuideSeen==22&&gc.GuideBuildSeen==27,"the copy of the running settings keeps the guide topics already shown");}
      var sRun=new RuntimeSettings();var sNow=OverlayGameSettings.Copy(sRun);OverlayGameSettings.Set(sNow,sC,"skate-lock",0);
      Check(OverlayGameSettings.Differs(sRun,sNow,sC,GameRuntime.Legends1,Cue,Data),"a skates choice counts for a restart");
      {   // quick roll (contract 2.40) and shot aim (contract 2.38)
        var rC=All();rC.HasRoll=true;rC.HasShotAim=true;var rS=new RuntimeSettings();var rR=OverlayGameSettings.Build(rS,rC);
        Check(Row(rR,"roll")!=null&&Row(rR,"roll").IsSwitch&&Row(rR,"roll").Value==0&&Row(rR,"roll").Enabled,"a program that reports roll gets a quick roll switch, off by default");
        Check(Row(rR,"shot-aim")!=null&&Row(rR,"shot-aim").IsSwitch&&Row(rR,"shot-aim").Value==1&&Row(rR,"shot-aim").Enabled,"a program that reports shot-aim gets a shot aim switch, on by default");
        Check(Row(OverlayGameSettings.Build(rS,All()),"roll")==null&&Row(OverlayGameSettings.Build(rS,All()),"shot-aim")==null,"no roll or shot aim row for a program without the words");
        var rOrig=new RuntimeSettings{OriginalControls=true,OriginalMovement=true};var rOR=OverlayGameSettings.Build(rOrig,rC);
        Check(!Row(rOR,"roll").Enabled&&Row(rOR,"shot-aim").Enabled,"the roll waits for smooth movement, the shot aim does not");
        Check(OverlayGameSettings.Set(rS,rC,"roll",1)==""&&rS.Roll&&OverlayGameSettings.Set(rS,rC,"shot-aim",0)==""&&rS.NoShotAim&&OverlayGameSettings.Set(rS,rC,"roll",0)==""&&!rS.Roll,"the two switches are taken");
        Check(OverlayGameSettings.Set(rOrig,rC,"roll",1)!=""&&!rOrig.Roll,"the roll is refused while smooth movement is off");
        Check(OverlayGameSettings.RollUsable(new RuntimeSettings(),rC)&&!OverlayGameSettings.RollUsable(rOrig,rC)&&OverlayGameSettings.RollUsable(new RuntimeSettings{OriginalMovement=true},rC)&&!OverlayGameSettings.RollUsable(new RuntimeSettings{OriginalMovement=true,OriginalControls=true},rC)&&OverlayGameSettings.RollUsable(new RuntimeSettings{OriginalControls=true},rC),"the Settings screen uses the same rule: the roll row is live with smooth movement or the third-person controls, grey with neither");
        var rNoMove=All();rNoMove.HasRoll=true;rNoMove.HasMovementOption=false;Check(!OverlayGameSettings.RollUsable(new RuntimeSettings(),rNoMove),"a program without the movement option never has a live roll row");
        var rNoTp=All();rNoTp.HasRoll=true;rNoTp.HasThirdPerson=false;Check(!OverlayGameSettings.RollUsable(new RuntimeSettings{OriginalMovement=true},rNoTp)&&OverlayGameSettings.RollUsable(new RuntimeSettings(),rNoTp),"a program without the third-person controls: the roll row follows the smooth movement choice alone");
        var rCopy=OverlayGameSettings.Copy(new RuntimeSettings{Roll=true,NoShotAim=true});Check(rCopy.Roll&&rCopy.NoShotAim,"the copy of the running settings keeps both choices");
        var rRun=new RuntimeSettings();var rNow=OverlayGameSettings.Copy(rRun);OverlayGameSettings.Set(rNow,rC,"roll",1);
        Check(OverlayGameSettings.Differs(rRun,rNow,rC,GameRuntime.Legends1,Cue,Data),"the quick roll counts for a restart");
        var aRun=new RuntimeSettings();var aNow=OverlayGameSettings.Copy(aRun);OverlayGameSettings.Set(aNow,rC,"shot-aim",0);
        Check(OverlayGameSettings.Differs(aRun,aNow,rC,GameRuntime.Legends1,Cue,Data),"the shot aim counts for a restart");
      }
      var mRun=new RuntimeSettings{LockMarker2=false};var mNow=OverlayGameSettings.Copy(mRun);OverlayGameSettings.Set(mNow,mC,"lock-marker-style",1);
      Check(OverlayGameSettings.Differs(mRun,mNow,mC,GameRuntime.Legends1,Cue,Data),"the marker look counts for a restart");
    }
    {   // Legends 2: the free camera (contract L2-4) and its two adjusters
      var fcC=new RuntimeCheck{Ready=true,HasFreeCamera=true};var fcS=new RuntimeSettings();var fcRows=OverlayGameSettings.Build(fcS,fcC);
      Check(Row(fcRows,"free-camera")!=null&&Row(fcRows,"free-camera").IsSwitch&&Row(fcRows,"free-camera").Value==0,"a program that reports free-camera gets a free camera switch, off by default");
      Check(!Row(fcRows,"camera-speed").Enabled&&!Row(fcRows,"invert-look").Enabled,"the two adjusters wait for the free camera");
      Check(OverlayGameSettings.Set(fcS,fcC,"free-camera",1)==""&&fcS.FreeCamera&&Row(OverlayGameSettings.Build(fcS,fcC),"camera-speed").Enabled,"the switch turns the free camera on and wakes the adjusters");
      Check(OverlayGameSettings.Set(fcS,fcC,"camera-speed",3)==""&&fcS.LookSensitivity==150&&OverlayGameSettings.Set(fcS,fcC,"invert-look",1)==""&&fcS.InvertLookY,"speed and invert are taken");
      Check(Row(OverlayGameSettings.Build(new RuntimeSettings(),new RuntimeCheck{Ready=true}),"free-camera")==null,"a program that does not report it gets no row");
      var fdC=new RuntimeCheck{Ready=true,HasFreeCamera=true,HasFreeCameraDistance=true};var fdS=new RuntimeSettings();
      Check(Row(OverlayGameSettings.Build(fdS,fcC),"camera-distance")==null&&Row(OverlayGameSettings.Build(fdS,fdC),"camera-distance")!=null,"the distance row only for a program that reports free-camera-distance");
      Check(!Row(OverlayGameSettings.Build(fdS,fdC),"camera-distance").Enabled&&OverlayGameSettings.Set(fdS,fdC,"free-camera",1)==""&&OverlayGameSettings.Set(fdS,fdC,"camera-distance",3)==""&&fdS.CameraDistance==130,"the distance waits for the free camera and then takes a step");
      var fdRun=new RuntimeSettings{FreeCamera=true};var fdNow=OverlayGameSettings.Copy(fdRun);OverlayGameSettings.Set(fdNow,fdC,"camera-distance",4);
      Check(OverlayGameSettings.Differs(fdRun,fdNow,fdC,GameRuntime.Legends2,Cue,Data),"a new distance counts for a restart");
      var fcRun=new RuntimeSettings();var fcNow=OverlayGameSettings.Copy(fcRun);OverlayGameSettings.Set(fcNow,fcC,"free-camera",1);
      Check(OverlayGameSettings.Differs(fcRun,fcNow,fcC,GameRuntime.Legends2,Cue,Data)&&!OverlayGameSettings.Differs(fcRun,fcNow,new RuntimeCheck{Ready=true},GameRuntime.Legends2,Cue,Data),"the free camera counts for a restart only for a program that reports it");
    }

    // a program that reports nothing: only the window size, in whole-number steps
    var old=OverlayGameSettings.Build(s,new RuntimeCheck{Ready=true});
    Check(old.Count==1&&old[0].Id=="window-size"&&old[0].Choices[0]=="2x"&&old[0].Choices.Length==5&&old[0].Value==1,"an older program offers only the window size, 2x to 6x (3x is the default)");
    Check(OverlayGameSettings.Set(s,new RuntimeCheck{Ready=true},"window-size",3)==""&&s.Scale==5,"whole-number window size 5x is stored as the scale");
    s.Scale=3;

    // defaults: third-person controls are on, so smooth movement is fixed on and the camera rows are open
    Check(Row(rows,"third-person").Value==1&&!Row(rows,"smooth-movement").Enabled&&Row(rows,"smooth-movement").Value==1&&Row(rows,"smooth-movement").Hint.Length>0,"third-person on: smooth movement is shown on and greyed out, with the reason");
    Check(Row(rows,"camera-speed").Enabled&&Row(rows,"invert-look").Enabled&&Row(rows,"camera-speed").Value==2&&Row(rows,"camera-speed").ValueText()=="100%","third-person on: camera rows are open and speed shows 100%");
    Check(Row(rows,"window-size").ValueText()=="720p"&&Row(rows,"sharp-picture").Value==1&&Row(rows,"vibration").Value==1&&Row(rows,"vibration-strength").ValueText()=="100%"&&Row(rows,"intro-movie").Value==1,"defaults: 720p, sharper picture, vibration at 100%, opening movie on");

    // changes land in the settings object
    Check(OverlayGameSettings.Set(s,c,"window-size",4)==""&&s.WindowHeight==2160,"window size 4K stores 2160");
    Check(OverlayGameSettings.Set(s,c,"sharp-picture",0)==""&&s.OriginalPicture,"sharper picture off means the original picture");
    Check(OverlayGameSettings.Set(s,c,"intro-movie",0)==""&&s.NoIntroMovie,"opening movie off stores no-intro-movie");
    Check(OverlayGameSettings.Set(s,c,"vibration-strength",0)==""&&s.RumbleStrength==0&&!s.NoRumble,"strength 0 is stored as strength 0");
    Check(OverlayGameSettings.Set(s,c,"vibration-strength",2)==""&&s.RumbleStrength==50,"strength 50% is stored as 50");
    Check(OverlayGameSettings.Set(s,c,"vibration",0)==""&&s.NoRumble,"vibration off is stored");
    var off=OverlayGameSettings.Build(s,c);
    Check(!Row(off,"vibration-strength").Enabled&&OverlayGameSettings.Set(s,c,"vibration-strength",4)!=""&&s.RumbleStrength==50,"vibration off: the strength row is greyed out and a change is refused");
    OverlayGameSettings.Set(s,c,"vibration",1);
    Check(OverlayGameSettings.Set(s,c,"camera-speed",3)==""&&s.LookSensitivity==150&&OverlayGameSettings.Set(s,c,"invert-look",1)==""&&s.InvertLookY,"camera speed 150% and invert are stored");
    Check(OverlayGameSettings.Set(s,c,"smooth-movement",0)!=""&&!s.OriginalMovement,"smooth movement cannot be changed while third-person is on");
    Check(OverlayGameSettings.Set(s,c,"lock-on",0)==""&&s.ZTargetOff&&OverlayGameSettings.Set(s,c,"lock-on",1)==""&&!s.ZTargetOff,"lock-on off is stored as ZTargetOff, and on clears it");
    OverlayGameSettings.Set(s,c,"lock-on",0);
    Check(OverlayGameSettings.Set(s,c,"third-person",0)==""&&s.OriginalControls,"third-person off is stored");
    var tp=OverlayGameSettings.Build(s,c);
    Check(Row(tp,"smooth-movement").Enabled&&Row(tp,"smooth-movement").Value==1&&!Row(tp,"camera-speed").Enabled&&!Row(tp,"invert-look").Enabled&&!Row(tp,"lock-on").Enabled&&Row(tp,"lock-on").Value==0,"third-person off: smooth movement opens up, the camera and lock-on rows grey out");
    Check(OverlayGameSettings.Set(s,c,"lock-on",1)!=""&&s.ZTargetOff,"third-person off: a lock-on change is refused");
    Check(OverlayGameSettings.Set(s,c,"camera-speed",0)!=""&&s.LookSensitivity==150&&OverlayGameSettings.Set(s,c,"smooth-movement",0)==""&&s.OriginalMovement,"third-person off: camera rows refuse, smooth movement can be turned off");
    Check(OverlayGameSettings.Set(s,c,"nope",1)!=""&&OverlayGameSettings.Set(s,c,"window-size",9)!=""&&OverlayGameSettings.Set(s,c,"window-size",-1)!="","an unknown setting or a value out of range is refused");
    Check(OverlayGameSettings.Set(new RuntimeSettings(),new RuntimeCheck{Ready=true},"intro-movie",0)!="","a setting the program does not report is refused");

    // a stored strength that is not one of the steps (the setup slider moves in fives) is shown as it is and kept until changed
    var odd=new RuntimeSettings{RumbleStrength=30};var oddRow=Row(OverlayGameSettings.Build(odd,c),"vibration-strength");
    Check(oddRow.ValueText()=="30%"&&oddRow.Choices.Length==6&&oddRow.Choices[1]=="25%"&&oddRow.Choices[2]=="30%","a stored strength of 30 is shown as 30%, not rounded to 25%");
    Check(OverlayGameSettings.Set(odd,c,"vibration-strength",oddRow.Next())==""&&odd.RumbleStrength==50,"one click from 30% goes to the next entry, 50%");
    var high=new RuntimeSettings{RumbleStrength=90};var highRow=Row(OverlayGameSettings.Build(high,c),"vibration-strength");
    Check(highRow.ValueText()=="90%"&&highRow.Next()==highRow.Choices.Length-1&&OverlayGameSettings.Set(high,c,"vibration-strength",highRow.Next())==""&&high.RumbleStrength==100,"a stored 90 shows 90% and one click goes to 100%, not to off");
    Check(Row(OverlayGameSettings.Build(new RuntimeSettings{RumbleStrength=5},c),"vibration-strength").ValueText()=="5%","a stored 5 is not shown as off");
    // everything the panel changes survives the settings file
    var keep=new RuntimeSettings{WindowHeight=1440,OriginalPicture=true,OriginalMovement=true,OriginalControls=true,NoRumble=true,RumbleStrength=75,NoIntroMovie=true,LookSensitivity=200,InvertLookY=true,ZTargetOff=true};
    string file=Path.Combine(scratch,"runtime.xml");GameRuntime.Save(file,keep,1);var back=GameRuntime.Load(file,1);
    Check(back.WindowHeight==1440&&back.OriginalPicture&&back.OriginalMovement&&back.OriginalControls&&back.NoRumble&&back.RumbleStrength==75&&back.NoIntroMovie&&back.LookSensitivity==200&&back.InvertLookY&&back.ZTargetOff,"every setting the panel offers is kept by the settings file, lock-on included");

    // the session wiring Program.cs uses
    {
      string wfile=Path.Combine(scratch,"wiring.xml");int saved=0,changed=0,failed2=0;bool saveThrows=false;
      var running=new RuntimeSettings();var started=OverlayGameSettings.Copy(running);
      var src=OverlayGameSettings.ForSession(running,started,c,GameRuntime.Legends1,Cue,Data,delegate(RuntimeSettings x){if(saveThrows)throw new IOException("disk");saved++;GameRuntime.Save(wfile,x,2);},delegate{changed++;},delegate{failed2++;});
      Check(src.List().Count==10&&!src.Pending(),"wiring: the list is the running game's rows and nothing is pending at the start");
      Check(src.Change("window-size",4)==""&&running.WindowHeight==2160&&saved==1&&changed==1&&src.Pending(),"wiring: a change edits the running game's settings, saves them, tells the launcher and shows as pending");
      Check(GameRuntime.Load(wfile,2).WindowHeight==2160,"wiring: the saved file holds the change for the game number the launcher named");
      Check(src.Change("window-size",9)!=""&&saved==1&&changed==1,"wiring: a refused change saves nothing and tells nobody");
      saveThrows=true;Check(src.Change("window-size",1)=="Kept until the launcher closes: the settings file could not be saved."&&failed2==1&&running.WindowHeight==720,"wiring: a failed save is reported and the change is kept in memory");
      Check(!src.Pending(),"wiring: back at the starting value nothing is pending");
      var lazy=OverlayGameSettings.ForSession(running,null,c,GameRuntime.Legends1,Cue,Data,null,null,null);
      running.Scale=3;Check(!lazy.Pending()&&lazy.Change("intro-movie",0)==""&&lazy.Pending(),"wiring: with no start copy it takes one at once, and a missing save or callbacks are allowed");
      bool threw=false;try{OverlayGameSettings.ForSession(null,null,c,GameRuntime.Legends1,Cue,Data,null,null,null);}catch(ArgumentNullException){threw=true;}Check(threw,"wiring: no settings object is refused");
    }

    // Copy is a snapshot
    var snap=OverlayGameSettings.Copy(keep);keep.WindowHeight=480;keep.NoRumble=false;
    Check(snap.WindowHeight==1440&&snap.NoRumble&&snap.LookSensitivity==200,"a copy does not follow later changes");

    // "restart to apply": it asks the function that starts the game, so only what the game would really get counts
    var run0=new RuntimeSettings();var now=OverlayGameSettings.Copy(run0);var kind=GameRuntime.Legends1;
    Check(!OverlayGameSettings.Differs(run0,now,c,kind,Cue,Data),"nothing changed: the next start is the same");
    OverlayGameSettings.Set(now,c,"window-size",3);
    Check(OverlayGameSettings.Differs(run0,now,c,kind,Cue,Data),"a new window size: the next start differs");
    OverlayGameSettings.Set(now,c,"window-size",1);
    Check(!OverlayGameSettings.Differs(run0,now,c,kind,Cue,Data),"changed back: it no longer differs");
    now.OriginalMovement=true;
    Check(!OverlayGameSettings.Differs(run0,now,c,kind,Cue,Data),"smooth movement cannot be off with third-person on, so that stored value does not count");
    now.OriginalMovement=false;now.NoRumble=true;
    Check(OverlayGameSettings.Differs(run0,now,c,kind,Cue,Data)&&!OverlayGameSettings.Differs(run0,now,new RuntimeCheck{Ready=true},kind,Cue,Data),"vibration counts only for a program that reports it");
    now.NoRumble=false;now.NoIntroMovie=true;
    Check(OverlayGameSettings.Differs(run0,now,c,kind,Cue,Data),"the opening movie counts");
    Check(!OverlayGameSettings.Differs(null,now,c,kind,Cue,Data)&&!OverlayGameSettings.Differs(run0,now,null,kind,Cue,Data),"missing input never claims a difference");
  }

  // Cheat codes (CodeBook, CodeSession). Every code below is made up for this test.
  static void CodeBookChecks(string scratch){
    string listText="# a made-up list\n[Alpha]\n800A0000 0001\n[Beta one]\nD00A0010 0002\n300A0012 0003\n[Gate A]\nC00A0020 0001\n800A0022 0001\n[Gate B]\nC00A0030 0001\n[Wait A]\nC1000000 0010\n[Wait B]\nC1000000 0020\n[Needs digit]\n800A002? 0001\n[Broken]\nthis is not a code\n[Outside]\n80000000 0001\n[Kind D1]\nD10A0040 0001\n";
    var codes=CodeBook.Parse(listText);
    Check(codes.Count==10,"a list of ten named codes is read ("+codes.Count+")");
    Check(codes[0].Name=="Alpha"&&codes[0].Lines.Count==1&&codes[0].Lines[0]=="800A0000 0001","a code keeps its normalised line");
    Check(codes[1].Lines.Count==2&&codes[1].Name=="Beta one","a code may hold several lines and a name with a space");
    Check(codes[6].NeedsValue&&!codes[0].NeedsValue,"a question mark in a line marks the code as needing a value");
    Check(codes[7].Problem.Length>0,"a line the game cannot read marks its code");
    Check(codes[8].MayNotAct&&codes[9].MayNotAct&&!codes[0].MayNotAct&&!codes[1].MayNotAct,"a write outside the game's data, or a kind the game leaves out, is flagged as may-not-act");
    Check(CodeBook.Parse("﻿[Bom]\r\n800A0000 0001 ; note\r\n").Count==1,"a byte order mark, CR LF line ends and a trailing comment are accepted");
    Check(CodeBook.Parse("800A0000 0001\n").Count==0&&CodeBook.Parse(null).Count==0,"lines before any name, and no text, give no codes");
    string problem;
    string good=CodeBook.Compose(new[]{codes[0],codes[1]},out problem);
    Check(good!=null&&problem==null&&good=="# memory codes written by Flutter Deck\n# Alpha\n800A0000 0001\n# Beta one\nD00A0010 0002\n300A0012 0003\n","two codes on give exactly their lines, with the names as comments");
    Check(CodeBook.Check(good)==null,"the game's refusal rules accept that text");
    Check(CodeBook.Compose(new MemoryCode[0],out problem)=="# memory codes written by Flutter Deck\n"&&CodeBook.Check(CodeBook.OffText)==null,"no code on is a good file (all off)");
    Check(CodeBook.Compose(new[]{codes[6]},out problem)==null&&problem.Contains("needs a value"),"a code that needs a value cannot be switched on");
    Check(CodeBook.Compose(new[]{codes[7]},out problem)==null&&problem.Length>0,"a code with an unreadable line cannot be switched on");
    Check(CodeBook.Compose(new[]{codes[2],codes[3]},out problem)==null&&problem.Contains("gate"),"two codes that both bring a gate line are refused, not left to overrule each other");
    Check(CodeBook.Compose(new[]{codes[4],codes[2]},out problem)!=null&&problem==null,"one gate and one wait are fine together ("+problem+")");
    Check(CodeBook.Compose(new[]{codes[4],codes[5]},out problem)==null&&problem.Contains("wait"),"two codes that both bring a wait line are refused");
    // the game's refusal rules
    Check(CodeBook.Check("800A0000 0001\n")==null&&CodeBook.Check("# c\n; c\n\n800A0000 0001 # c\n")==null,"code lines, comments and empty lines are good");
    Check(CodeBook.Check("800A000 0001\n")!=null&&CodeBook.Check("800A000G 0001\n")!=null&&CodeBook.Check("800A0000\n")!=null&&CodeBook.Check("800A002? 0001\n")!=null,"a short line, a bad digit, a missing value and a question mark are refused");
    Check(CodeBook.Check("800A0000 0001\r\n")!=null&&CodeBook.Check("800A0000 0001\0\n")!=null&&CodeBook.Check("800A0000 0001é\n")!=null,"a carriage return, a NUL byte and a non-ASCII character are refused");
    Check(CodeBook.Check("# "+new string('x',200)+"\n")!=null&&CodeBook.Check("# "+new string('x',150)+"\n")==null,"a line of 200 characters or more is refused");
    {var sb=new System.Text.StringBuilder();for(int i=0;i<512;i++)sb.Append("800A0000 0001\n");Check(CodeBook.Check(sb.ToString())==null&&CodeBook.Check(sb.ToString()+"800A0000 0001\n")!=null,"512 code lines are fine, 513 are refused");
     Check(CodeBook.Check(new string('#',CodeBook.MaxBytes))!=null,"a file near 64 KB is refused");}
    Check(CodeBook.CleanName("Max [zenny] #1; éè")=="Max zenny 1" ,"a name keeps plain ASCII only, with no brackets or comment marks ("+CodeBook.CleanName("Max [zenny] #1; éè")+")");
    // writing the file
    string data=Path.Combine(scratch,"codes-data");Directory.CreateDirectory(data);
    string file=CodeBook.FilePath(data);
    Check(CodeBook.Prepare(data)==null&&File.ReadAllText(file)==CodeBook.OffText,"at game start the file is made and every code is off");
    byte[] bytes=File.ReadAllBytes(file);Check(bytes.Length>=3&&!(bytes[0]==0xEF&&bytes[1]==0xBB),"the file has no byte order mark");
    File.WriteAllText(file+".tmp","stale");Check(CodeBook.Prepare(data)==null&&!File.Exists(file+".tmp"),"an old temporary file is removed at start");
    long fake=0;var session=new CodeSession(file,CodeBook.Parse(listText),delegate{return fake;});
    Check(session.Count==10&&!session.IsOn(0),"a session starts with every code off");
    Check(session.Set(0,true)==null&&session.IsOn(0)&&File.ReadAllText(file).Contains("800A0000 0001"),"switching a code on writes its lines");
    Check(session.Set(1,true)==null&&File.ReadAllText(file).Contains("D00A0010 0002")&&File.ReadAllText(file).Contains("800A0000 0001"),"a second code on keeps the first");
    Check(session.Set(0,false)!=null&&session.IsOn(0)&&File.ReadAllText(file).Contains("800A0000 0001"),"a code cannot go off within a second of going on (the game would never take that state)");
    fake+=1500;
    Check(session.Set(0,false)==null&&!File.ReadAllText(file).Contains("800A0000 0001")&&File.ReadAllText(file).Contains("D00A0010 0002"),"switching a code off takes its lines out and keeps the others");
    string before=File.ReadAllText(file);
    Check(session.Set(6,true)!=null&&!session.IsOn(6)&&File.ReadAllText(file)==before,"a refused code stays off and the file is not touched");
    Check(session.Set(6,true)!=null&&session.Set(6,true).Contains("Pick a value"),"a code with a question mark cannot be switched on before a value is picked");
    Check(session.SetValue(6,10)==null&&session.IsOn(6)&&session.Value(6)==10&&File.ReadAllText(file).Contains("800A002A 0001"),"picking a value switches the code on with that digit in place of the question mark");
    Check(session.SetValue(6,3)==null&&File.ReadAllText(file).Contains("800A0023 0001")&&!File.ReadAllText(file).Contains("800A002A")&&CodeBook.Check(File.ReadAllText(file))==null,"picking another value while it is on changes the line, and the file stays good");
    Check(session.SetValue(6,16)!=null&&session.SetValue(6,-1)!=null&&session.SetValue(0,3)!=null&&session.SetValue(99,1)!=null&&session.Value(6)==3,"a value outside 0 to F, a code with no value and no such code are refused, and nothing changes");
    fake+=2000;Check(session.Set(6,false)==null&&!session.IsOn(6)&&session.Value(6)==3&&!File.ReadAllText(file).Contains("800A0023")&&session.Set(6,true)==null&&session.IsOn(6)&&File.ReadAllText(file).Contains("800A0023 0001"),"off keeps the picked value and on uses it again");
    fake+=2000;Check(session.Set(6,false)==null,"the value code goes off again");
    Check(session.MayNotAct(6)==false&&CodeBook.WillNotAct(codes[6].Resolved(15))==false,"a value code is judged with its picked digit");
    Check(session.Set(2,true)==null&&session.Set(3,true)!=null&&!session.IsOn(3)&&session.IsOn(2),"a second gate code is refused and the first stays on");
    Check(session.Set(99,true)!=null&&session.Set(-1,true)!=null,"a code that does not exist is refused");
    int lastWrite=File.ReadAllBytes(file).Length;DateTime stamp=File.GetLastWriteTimeUtc(file);System.Threading.Thread.Sleep(30);
    Check(session.Set(2,true)==null&&File.GetLastWriteTimeUtc(file)==stamp,"switching a code to the state it already has writes nothing (the game would count a same-text write as a change)");
    // what the game does with a line (the code engine session's rules, Legends 1)
    Func<string,bool> may=delegate(string body){var l=CodeBook.Parse("[X]\n"+body);return l.Count==1&&l[0].MayNotAct;};
    Check(!may("800A0000 0001")&&!may("300A0000 0001")&&may("810A0000 0001")&&may("800D87FF 0001")&&!may("300D87FF 0001")&&!may("800D87FE 0001"),"a write must fit inside the game's data, whole: a 16-bit store at the last byte does not act, a byte store does");
    Check(may("800806C2 0001")&&!may("800806C4 0001")&&may("80200000 0001")&&may("A00A0000 0001")&&may("70000000 0001"),"the first and last address of the range, a write above 0x1FFFFF and other kinds are judged");
    Check(!may("50000202 0001\n800D87FC 0001")&&may("50000302 0001\n800D87FC 0001")&&may("50010202 0001\n800A0000 0001")&&may("50000202 0001")&&may("50000202 0001\nD00A0000 0001"),"a repeat pair is checked store by store, and a bad repeat line is left out");
    Check(may("D10A0000 0001\n800A0000 0001")&&may("E00A0000 0001\n800A0000 0001")&&!may("D00A0000 0001\n800A0000 0001")&&may("D01FFFFF 0001\n800A0000 0001")&&may("C01FFFFF 0001"),"tests of other kinds never hold, and a gate or test with a high address is left out");
    // a reader that holds the file open without sharing makes the write fail; the state is put back
    using(var hold=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.None)){
      string bad=session.Set(4,true);Check(bad!=null&&!session.IsOn(4),"when the file cannot be replaced the code stays off and the player is told ("+bad+")");
    }
    Check(session.Set(4,true)==null&&session.IsOn(4),"after the file is free again the same switch works");
    fake+=2000;Check(session.Set(4,false)==null&&!session.IsOn(4),"a code that has been on for a while can go off");
    var files=Directory.GetFiles(data,"*.tmp");Check(files.Length==0,"no temporary file is left behind");
    // the two lists are merged, the player's own entry wins
    string bundled=Path.Combine(scratch,"bundled.txt"),own=Path.Combine(scratch,"own.txt");
    File.WriteAllText(bundled,"[Same]\n800A0000 0001\n[Only bundled]\n800A0002 0001\n");File.WriteAllText(own,"[same]\n800A0004 0001\n[Only mine]\n800A0006 0001\n");
    var merged=CodeBook.Load(bundled,own);
    Check(merged.Count==3&&merged[0].Name=="Only bundled"&&merged[1].Name=="same"&&merged[1].Lines[0]=="800A0004 0001"&&merged[2].Name=="Only mine","a list that ships and the player's own list are merged, and the player's entry replaces one of the same name");
    Check(CodeBook.Load(Path.Combine(scratch,"nothing.txt"),null).Count==0,"a missing list gives no codes");
    // values of one to four digits
    {
      var wl=CodeBook.Parse("[One]\n300A0000 000?\n[Two]\n800A0010 00??\n800A0012 00??\n[Four]\nD00A0020 ????\n[Mixed]\n800A0030 00??\n800A0032 000?\n[Five]\n800A0040 ?????\n");
      Check(wl.Count==5&&wl[4].Problem.Length>0&&wl[0].Width==1&&wl[1].Width==2&&wl[2].Width==4&&wl[0].MaxValue==15&&wl[1].MaxValue==255&&wl[2].MaxValue==65535,"a run of question marks sets how many digits the player gives (1, 2 and 4)");
      Check(wl[3].Problem.Length>0&&wl[3].Width==2,"question marks of different lengths in one code are refused ("+wl[3].Problem+")");
      var two=wl[1].Resolved(0xB4);Check(two.Lines[0]=="800A0010 00B4"&&two.Lines[1]=="800A0012 00B4","a value fills every run of question marks with the same digits, with the zeros kept");
      Check(wl[2].Resolved(5).Lines[0]=="D00A0020 0005"&&wl[0].Resolved(15).Lines[0]=="300A0000 000F","a four-digit value is padded to four digits");
      string wf=Path.Combine(scratch,"width-codes.txt");long wclock=0;var ws=new CodeSession(wf,wl,delegate{return wclock;});
      Check(ws.SetValue(1,0xB4)==null&&File.ReadAllText(wf).Contains("800A0010 00B4")&&CodeBook.Check(File.ReadAllText(wf))==null,"a two-digit value is written into the file");
      Check(ws.SetValue(1,256)!=null&&ws.SetValue(0,16)!=null&&ws.SetValue(2,65536)!=null&&ws.Value(1)==0xB4,"a value too big for its code is refused and nothing changes");
      Check(ws.SetValue(2,0x1234)==null&&File.ReadAllText(wf).Contains("D00A0020 1234"),"a four-digit value is written too");
    }
    // a code of tests only does nothing by itself
    Check(may("D00A0000 0001")&&may("D00A0000 0001\nD00A0002 0001")&&!may("D00A0000 0001\n800A0000 0001")&&may("D00B52CD ????"),"a code with tests and no write is marked as one that may not act");
    // problems found by the independent check
    {
      var a=new MemoryCode{Name="Ends open"};a.Lines.Add("D00A0000 0001");var b2=new MemoryCode{Name="Next"};b2.Lines.Add("800A0100 0099");
      string txt=CodeBook.Compose(new[]{a,b2},out problem);
      Check(txt!=null&&txt.IndexOf("D00A0000 0001\n"+CodeBook.Filler+"\n# Next")>=0&&CodeBook.Check(txt)==null,"a code that ends in a test gets a line the game leaves out after it, so it cannot guard the next code");
      var r=new MemoryCode{Name="Repeat"};r.Lines.Add("50000402 0001");string t2=CodeBook.Compose(new[]{r,b2},out problem);
      Check(t2!=null&&t2.Contains("50000402 0001\n"+CodeBook.Filler+"\n"),"the same for a code that ends in a repeat line");
      var e=new MemoryCode{Name="Left out test"};e.Lines.Add("800A0200 0005");e.Lines.Add("E00A0000 0001");string t3=CodeBook.Compose(new[]{e,b2},out problem);
      Check(t3!=null&&t3.Contains("E00A0000 0001\n"+CodeBook.Filler+"\n"),"the same for a test kind the game leaves out");
      string t4=CodeBook.Compose(new[]{b2,a},out problem);Check(t4!=null&&!t4.Contains(CodeBook.Filler),"nothing is added after a code that ends open when it is the last one, or when it does not end open");
      string t5=CodeBook.Compose(new[]{a},out problem);Check(t5!=null&&!t5.Contains(CodeBook.Filler),"nothing is added when the open code is the only one");
      string lf1=Path.Combine(scratch,"dup1.txt"),lf2=Path.Combine(scratch,"dup2.txt");
      File.WriteAllText(lf1,"[Same]\n800A0000 0001\n[same]\n800A0002 0001\n[]\n800A0004 0001\n");File.WriteAllText(lf2,"[]\n800A0006 0001\n");
      var dm=CodeBook.Load(lf1,lf2);
      Check(dm.Count==4,"two codes with the same name in one file are both kept, and an unnamed code never replaces another ("+dm.Count+")");
      var mac=CodeBook.Parse("[A]\r800A0000 0001\r[B]\r800A0002 0002\r");
      Check(mac.Count==2&&mac[0].Lines.Count==1&&mac[1].Lines.Count==1,"old Mac line ends are read as line ends ("+mac.Count+")");
    }
    // the list that ships (the developer's list, converted; developer decision 2026-11-24)
    {
      string shipped=Path.Combine(Environment.CurrentDirectory,Path.Combine("codes","memory-codes-list.txt"));
      Check(File.Exists(shipped),"the list that ships is in the source folder (codes, memory-codes-list.txt)");
      var sl=File.Exists(shipped)?CodeBook.Parse(File.ReadAllText(shipped)):new List<MemoryCode>();
      Check(sl.Count==60,"the shipped list holds sixty codes ("+sl.Count+")");
      var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);bool allGood=true,allCompose=true,uniqueNames=true;string firstBad="";
      foreach(MemoryCode c in sl){
        if(!names.Add(c.Name))uniqueNames=false;
        if(c.Problem.Length>0||c.Lines.Count==0||(c.NeedsValue&&(c.Width<1||c.Width>4))){allGood=false;firstBad=c.Name;}
        string pr;MemoryCode one=c.NeedsValue?c.Resolved(0):c;string tx=CodeBook.Compose(new[]{one},out pr);
        if(tx==null||CodeBook.Check(tx)!=null){allCompose=false;firstBad=c.Name+" "+pr;}
      }
      Check(uniqueNames,"every shipped code has its own name");
      Check(allGood&&allCompose,"every shipped code is readable, and each one alone makes a file the game takes ("+firstBad+")");
      int valueCodes=0;foreach(MemoryCode c in sl)if(c.NeedsValue)valueCodes++;
      Check(valueCodes>0,"some shipped codes need a value ("+valueCodes+")");
    }
    // the start option
    var set=new RuntimeSettings();
    string cue=Path.Combine(scratch,"d.cue");
    string a1=GameRuntime.BuildArguments(set,cue,data,kind:GameRuntime.Legends1,codesLiveOption:true);
    Check(a1.Contains(" --codes-file \""+file+"\" --codes-watch")&&!a1.Contains("--control-file"),"a Legends 1 program that reports codes-live gets both options, with no live control needed");
    string a0=GameRuntime.BuildArguments(set,cue,data,kind:GameRuntime.Legends1);
    Check(!a0.Contains("--codes"),"a program that does not report codes-live gets no code option");
    string a2=GameRuntime.BuildArguments(set,cue,data,kind:GameRuntime.Legends2,codesLiveOption:true);
    Check(!a2.Contains("--codes"),"Legends 2 never gets the code option");
    {   // launcher gap 3 (kit 22 notes): a faulty codes file on disk must never reach the game, because the game refuses to start with it (exit 2)
      string bad=Path.Combine(scratch,"faulty-codes-data");Directory.CreateDirectory(bad);string badFile=CodeBook.FilePath(bad);
      File.WriteAllText(badFile,"800A0000 0001\r\nthis is not a code line\r\n"+new string('x',300)+"\n");
      Check(CodeBook.Check(File.ReadAllText(badFile))!=null,"the test file really is one the game would refuse");
      Check(CodeBook.Prepare(bad)==null&&File.ReadAllText(badFile)==CodeBook.OffText&&CodeBook.Check(File.ReadAllText(badFile))==null,"a faulty codes file left on disk is replaced by the all-off file at the start");
      File.WriteAllText(badFile,"zzzz\n");File.SetAttributes(badFile,FileAttributes.ReadOnly);
      string why=CodeBook.Prepare(bad);
      Check(why!=null||CodeBook.Check(File.ReadAllText(badFile))==null,"a read-only faulty file is either replaced or reported, so the start gate drops the code option and never passes a faulty file ("+(why??"replaced")+")");
      File.SetAttributes(badFile,FileAttributes.Normal);
    }
    {   // contracts 2.40 and 2.38: the start options of the quick roll and the shot aim
      string rollOn=GameRuntime.BuildArguments(new RuntimeSettings{Roll=true},cue,data,movementOption:true,kind:GameRuntime.Legends1,rollOption:true);
      Check(rollOn.Contains(" --roll 1")&&rollOn.Contains("--movement modern"),"quick roll ticked, program has it, smooth movement: --roll 1 is passed");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings(),cue,data,movementOption:true,kind:GameRuntime.Legends1,rollOption:true).Contains("--roll"),"quick roll is off by default: nothing is passed");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings{Roll=true},cue,data,movementOption:true,kind:GameRuntime.Legends1).Contains("--roll"),"a program that does not report roll gets no --roll");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings{Roll=true},cue,data,kind:GameRuntime.Legends1,rollOption:true).Contains("--roll"),"no --roll when the program has no movement option (it would do nothing)");
      string rollOrig=GameRuntime.BuildArguments(new RuntimeSettings{Roll=true,OriginalMovement=true},cue,data,movementOption:true,kind:GameRuntime.Legends1,rollOption:true);
      Check(rollOrig.Contains("--movement original")&&!rollOrig.Contains("--roll"),"original movement chosen: no --roll (it would do nothing there)");
      string rollTps=GameRuntime.BuildArguments(new RuntimeSettings{Roll=true,OriginalMovement=true},cue,data,movementOption:true,thirdPersonOption:true,kind:GameRuntime.Legends1,rollOption:true);
      Check(rollTps.Contains("--movement modern")&&rollTps.Contains(" --roll 1"),"third-person controls force smooth movement, so --roll 1 is passed");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings{Roll=true},cue,data,movementOption:true,kind:GameRuntime.Legends2,rollOption:true).Contains("--roll"),"never passed to Legends 2");
      Check(GameRuntime.BuildArguments(new RuntimeSettings{NoShotAim=true},cue,data,kind:GameRuntime.Legends1,shotAimOption:true).Contains(" --shot-aim 0"),"shot aim switched off: --shot-aim 0 is passed");
      Check(!GameRuntime.BuildArguments(new RuntimeSettings(),cue,data,kind:GameRuntime.Legends1,shotAimOption:true).Contains("--shot-aim")&&!GameRuntime.BuildArguments(new RuntimeSettings{NoShotAim=true},cue,data,kind:GameRuntime.Legends1).Contains("--shot-aim")&&!GameRuntime.BuildArguments(new RuntimeSettings{NoShotAim=true},cue,data,kind:GameRuntime.Legends2,shotAimOption:true).Contains("--shot-aim"),"shot aim on (the default) passes nothing, nor for a program without the word, nor for Legends 2");
      string rsPath=Path.Combine(scratch,"runtime-roll.xml");var rsIn=new RuntimeSettings{Roll=true,NoShotAim=true};GameRuntime.Save(rsPath,rsIn);var rsBack=GameRuntime.Load(rsPath);
      Check(rsBack.Roll&&rsBack.NoShotAim&&!new RuntimeSettings().Roll&&!new RuntimeSettings().NoShotAim,"both choices are saved and read back, and the defaults are roll off and shot aim on");
    }
  }
}
