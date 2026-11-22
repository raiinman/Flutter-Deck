using System;
using System.Collections.Generic;

// The launcher's side of the overlay's "Game settings" panel (SPEC-overlay.md, 2026-11-18). It turns the saved play settings of
// the running game into the rows the overlay shows, and turns a click back into a change of those settings. It starts nothing and
// writes nothing: the launcher saves, and Restart (which already exists) starts the game again with the new values.
//
// Only settings that the game program reports are offered (RuntimeCheck), and only ones the launcher really saves. Target lock-on is
// offered (the settings file keeps it as lockOff since 2026-11-19). Aim style is not: the launcher removed that box and ignores a saved
// value. Camera distance is not offered yet. Game volume and mute are not offered because the developer decided that audio enhancement
// is designed first.
public static class OverlayGameSettings {
  static readonly string[] HeightNames={"480p","720p","1080p","1440p","4K"};
  static readonly string[] ScaleNames={"2x","3x","4x","5x","6x"};
  public static readonly int[] RumbleSteps=new[]{0,25,50,75,100};
  // The steps, plus the stored strength when it is none of them (the setup slider moves in fives), so the row never shows a value the game does not get.
  public static int[] RumbleList(int stored){
    stored=Math.Max(0,Math.Min(100,stored));var l=new List<int>(RumbleSteps);if(!l.Contains(stored))l.Add(stored);l.Sort();return l.ToArray();
  }
  static string[] RumbleNames(int[] list){var n=new string[list.Length];for(int i=0;i<n.Length;i++)n[i]=list[i]==0?"0% (off)":list[i]+"%";return n;}
  static readonly string[] DistNames={"60% (closer)","80%","100% (normal)","130%","160% (farther)"};
  static readonly string[] LookNames={"50%","75%","100%","150%","200%"};

  // A copy that later changes cannot touch: taken when the game starts, to tell what the running game was given.
  public static RuntimeSettings Copy(RuntimeSettings s){
    return new RuntimeSettings{Program=s.Program,AcceptedHash=s.AcceptedHash,Scale=s.Scale,Volume=s.Volume,Mute=s.Mute,OriginalPicture=s.OriginalPicture,OriginalMovement=s.OriginalMovement,OriginalControls=s.OriginalControls,InvertLookY=s.InvertLookY,FreeCamera=s.FreeCamera,LockMarker2=s.LockMarker2,NoLockFlick=s.NoLockFlick,CameraDistance=s.CameraDistance,NoRumble=s.NoRumble,ZTargetOff=s.ZTargetOff,AimStyleCamera=s.AimStyleCamera,NoIntroMovie=s.NoIntroMovie,RumbleStrength=s.RumbleStrength,WindowHeight=s.WindowHeight,LookSensitivity=s.LookSensitivity};
  }

  static int Nearest(int[] steps,int value){int best=0;for(int i=1;i<steps.Length;i++)if(Math.Abs(steps[i]-value)<Math.Abs(steps[best]-value))best=i;return best;}
  static GameSetting Sw(string id,string label,bool on,bool enabled,string hint){return new GameSetting{Id=id,Label=label,Value=on?1:0,Enabled=enabled,Hint=enabled?"":hint};}
  static GameSetting Ch(string id,string label,string[] choices,int index,bool enabled,string hint){return new GameSetting{Id=id,Label=label,Choices=choices,Value=index,Enabled=enabled,Hint=enabled?"":hint};}

  // The three callbacks the overlay needs, built from explicit inputs. Program.cs calls this once and passes plain values, so a rename
  // or a change of meaning in the launcher shows up as a compile error or in the tests below, not as a quiet wrong-game bug.
  //   now      the settings object of the game that is RUNNING (not the game shown on screen); the overlay edits it in place
  //   then     a copy taken when that game started (null: a copy of now is taken here, which is only right if nothing changed yet)
  //   check    what that game's program reported when it started
  //   save     writes now to the settings file for that game; it throws when it cannot
  //   onChanged   called after a change was taken (the launcher redraws its setup screen); onSaveFailed called when save threw
  public sealed class Source {
    public Func<List<GameSetting>> List;public Func<string,int,string> Change;public Func<bool> Pending;
  }
  public static Source ForSession(RuntimeSettings now,RuntimeSettings then,RuntimeCheck check,GameKind kind,string cue,string data,Action<RuntimeSettings> save,Action onChanged,Action onSaveFailed){
    if(now==null)throw new ArgumentNullException("now");
    RuntimeSettings started=then??Copy(now);
    var src=new Source();
    src.List=delegate{return Build(now,check);};
    src.Change=delegate(string id,int value){
      string said=Set(now,check,id,value);if(said.Length>0)return said;
      try{if(save!=null)save(now);}catch(Exception){said="Kept until the launcher closes: the settings file could not be saved.";if(onSaveFailed!=null)onSaveFailed();}
      if(onChanged!=null)onChanged();
      return said;
    };
    src.Pending=delegate{return Differs(started,now,check,kind,cue,data);};
    return src;
  }

  // The rows for this game program, in the order the launcher's setup screen shows them.
  public static List<GameSetting> Build(RuntimeSettings s,RuntimeCheck c){
    var l=new List<GameSetting>();if(s==null||c==null)return l;
    bool thirdPerson=c.HasThirdPerson&&!s.OriginalControls;
    if(c.HasWindowHeight)l.Add(Ch("window-size","Window size",HeightNames,Math.Max(0,Array.IndexOf(GameRuntime.WindowHeights,GameRuntime.ClampHeight(s.WindowHeight))),true,""));
    else l.Add(Ch("window-size","Window size",ScaleNames,Math.Max(0,Math.Min(4,GameRuntime.ClampScale(s.Scale)-2)),true,""));
    if(c.HasPictureOption)l.Add(Sw("sharp-picture","Sharper picture",!s.OriginalPicture,true,""));
    if(c.HasMovementOption)l.Add(thirdPerson?Sw("smooth-movement","Smooth movement",true,false,"Always on with third-person controls"):Sw("smooth-movement","Smooth movement",!s.OriginalMovement,true,""));
    if(c.HasRumble){
      l.Add(Sw("vibration","Controller vibration",!s.NoRumble,true,""));
      int[] rl=RumbleList(s.RumbleStrength);
      l.Add(Ch("vibration-strength","Vibration strength",RumbleNames(rl),Array.IndexOf(rl,Math.Max(0,Math.Min(100,s.RumbleStrength))),!s.NoRumble,"Turn vibration on first"));
    }
    if(c.HasIntroMovie)l.Add(Sw("intro-movie","Opening movie",!s.NoIntroMovie,true,""));
    if(c.HasFreeCamera){   // Legends 2 (contract L2-4): the free camera and its two adjusters
      l.Add(Sw("free-camera","Free camera",s.FreeCamera,true,""));
      l.Add(Ch("camera-speed","Camera speed",LookNames,Nearest(GameRuntime.LookSensitivities,s.LookSensitivity),s.FreeCamera,"Needs the free camera"));
      l.Add(Sw("invert-look","Invert camera up and down",s.InvertLookY,s.FreeCamera,"Needs the free camera"));
      if(c.HasFreeCameraDistance)l.Add(Ch("camera-distance","Camera distance",DistNames,Nearest(GameRuntime.CameraDistances,s.CameraDistance),s.FreeCamera,"Needs the free camera"));
    }
    if(c.HasThirdPerson){
      l.Add(Sw("third-person","Third-person controls",!s.OriginalControls,true,""));
      if(c.HasLookOptions){
        l.Add(Ch("camera-speed","Camera speed",LookNames,Nearest(GameRuntime.LookSensitivities,s.LookSensitivity),thirdPerson,"Needs third-person controls"));
        l.Add(Sw("invert-look","Invert camera up and down",s.InvertLookY,thirdPerson,"Needs third-person controls"));
      }
      if(c.HasZTarget)l.Add(Sw("lock-on","Target lock-on",!s.ZTargetOff,thirdPerson,"Needs third-person controls")); // BuildArguments passes --lockon 1 only with third-person on
      if(c.HasLockFlick)l.Add(Sw("lock-flick","Flick to switch target",!s.NoLockFlick,thirdPerson&&c.HasZTarget&&!s.ZTargetOff,"Needs third-person controls and the target lock-on"));   // contract 2.30 (L17)
      if(c.HasLockMarker2)l.Add(Sw("lock-marker-style","Legends 2 lock-on marker",s.LockMarker2,thirdPerson&&c.HasZTarget&&!s.ZTargetOff,"Needs third-person controls and the target lock-on"));   // contract 2.29 (L16)
    }
    return l;
  }

  // Applies one click to the settings object. Returns "" when the value was taken, or a line saying why not. It never saves.
  public static string Set(RuntimeSettings s,RuntimeCheck c,string id,int value){
    GameSetting row=null;foreach(GameSetting g in Build(s,c))if(g.Id==id)row=g;
    if(row==null)return "This game program does not offer that setting.";
    if(!row.Enabled)return row.Hint+".";
    if(row.IsSwitch)value=value!=0?1:0;else if(value<0||value>=row.Choices.Length)return "That value is not on offer.";
    bool on=value!=0;
    switch(id){
      case "window-size":if(c.HasWindowHeight)s.WindowHeight=GameRuntime.WindowHeights[value];else s.Scale=value+2;break;
      case "sharp-picture":s.OriginalPicture=!on;break;
      case "smooth-movement":s.OriginalMovement=!on;break;
      case "vibration":s.NoRumble=!on;break;
      case "vibration-strength":s.RumbleStrength=RumbleList(s.RumbleStrength)[value];break;
      case "intro-movie":s.NoIntroMovie=!on;break;
      case "third-person":s.OriginalControls=!on;break;
      case "free-camera":s.FreeCamera=on;break;
      case "camera-speed":s.LookSensitivity=GameRuntime.LookSensitivities[value];break;
      case "invert-look":s.InvertLookY=on;break;
      case "camera-distance":s.CameraDistance=GameRuntime.CameraDistances[value];break;
      case "lock-on":s.ZTargetOff=!on;break;
      case "lock-marker-style":s.LockMarker2=on;break;
      case "lock-flick":s.NoLockFlick=!on;break;
      default:return "This game program does not offer that setting.";
    }
    return "";
  }

  // True when starting the game now would use other arguments than the running game was started with. It asks the same
  // function that starts the game, so a setting the program ignores, or one that another setting overrides, does not count.
  public static bool Differs(RuntimeSettings running,RuntimeSettings now,RuntimeCheck c,GameKind kind,string cue,string data){
    try{
      if(running==null||now==null||c==null)return false;
      return GameRuntime.BuildArguments(running,cue,data,c.HasPictureOption,c.HasWindowHeight,c.HasMovementOption,c.HasThirdPerson,c.HasPadMap,c.HasLookOptions,c.HasRumble,c.HasZTarget,kind,c.HasIntroMovie,freeCameraOption:c.HasFreeCamera,freeCameraDistanceOption:c.HasFreeCameraDistance,lockMarker2Option:c.HasLockMarker2,lockFlickOption:c.HasLockFlick)
        !=GameRuntime.BuildArguments(now,cue,data,c.HasPictureOption,c.HasWindowHeight,c.HasMovementOption,c.HasThirdPerson,c.HasPadMap,c.HasLookOptions,c.HasRumble,c.HasZTarget,kind,c.HasIntroMovie,freeCameraOption:c.HasFreeCamera,freeCameraDistanceOption:c.HasFreeCameraDistance,lockMarker2Option:c.HasLockMarker2,lockFlickOption:c.HasLockFlick);
    }catch(Exception){return false;}
  }
}
