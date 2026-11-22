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
    try{Everything(scratch);}catch(Exception e){failed++;Console.WriteLine("FAIL: unexpected exception "+e);}
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
      var mC=All();mC.HasLockMarker2=true;var mS=new RuntimeSettings();
      var mR=OverlayGameSettings.Build(mS,mC);
      Check(Row(mR,"lock-marker-style")!=null&&Row(mR,"lock-marker-style").IsSwitch&&Row(mR,"lock-marker-style").Value==0,"a program that reports lock-marker-2 gets a marker look switch, off by default");
      Check(Row(OverlayGameSettings.Build(mS,All()),"lock-marker-style")==null,"a program that does not report it gets no row");
      Check(OverlayGameSettings.Set(mS,mC,"lock-marker-style",1)==""&&mS.LockMarker2,"the switch is taken");
      var fC=All();fC.HasLockFlick=true;var fS=new RuntimeSettings();
      Check(Row(OverlayGameSettings.Build(fS,fC),"lock-flick")!=null&&Row(OverlayGameSettings.Build(fS,fC),"lock-flick").Value==1&&Row(OverlayGameSettings.Build(fS,All()),"lock-flick")==null,"the flick row only for a program that reports lock-flick, on by default");
      Check(OverlayGameSettings.Set(fS,fC,"lock-flick",0)==""&&fS.NoLockFlick,"the flick can be switched off");
      var mRun=new RuntimeSettings();var mNow=OverlayGameSettings.Copy(mRun);OverlayGameSettings.Set(mNow,mC,"lock-marker-style",1);
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
}
