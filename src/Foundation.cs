using System;
using System.IO;
using System.Globalization;
using System.Collections.Generic;
public sealed class LauncherPreferences {
  public double Width=1672; public bool Topmost=false; public int Game=1;
  public static string PathName {get{return Path.Combine(DeckPaths.Root,"window.txt");}}
  public static double BoundedWidth(double width){return Double.IsNaN(width)||Double.IsInfinity(width)?1672:Math.Max(1200,Math.Min(3000,width));}
  public static LauncherPreferences Load(string path){
    var p=new LauncherPreferences();
    try{if(!File.Exists(path)||new FileInfo(path).Length>256)return p;var lines=File.ReadAllLines(path);double width;int game;bool top;
      if(lines.Length!=4||lines[0]!="1"||!Double.TryParse(lines[1],NumberStyles.Float,CultureInfo.InvariantCulture,out width)||!Boolean.TryParse(lines[2],out top)||!Int32.TryParse(lines[3],out game))return p;
      p.Width=BoundedWidth(width);p.Topmost=top;p.Game=game>=1&&game<=3?game:1;
    }catch{} return p;
  }
  public static void Save(string path,LauncherPreferences p){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllLines(temp,new[]{"1",BoundedWidth(p.Width).ToString("R",CultureInfo.InvariantCulture),p.Topmost.ToString(),(p.Game>=1&&p.Game<=3?p.Game:1).ToString(CultureInfo.InvariantCulture)});if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
}
// The choices the user makes in the launcher that are not game settings: music on or off, volume, mute, repeat,
// shuffle, the bundled track, and the scaling engine and filter. See SPEC-choices.md.
// Rules: a missing, oversized or unreadable file gives the defaults; a bad value gives that value's default;
// only a bundled track's title is ever stored, never the name or location of a file the user added.
public sealed class DeckChoices {
  public bool MusicOn=true,Muted=false,Repeat=true,Shuffle=false; public int Volume=20,ScalingEngine=0,ScalingFilter=0; public string Track="";
  // For tests only: a full path to a .txt file in this variable is used instead of the real settings file.
  public const string OverrideVariable="FLUTTERDECK_CHOICES_FILE";
  public static string PathName {get{
    if(DeckPaths.IsPortable)return Path.Combine(DeckPaths.Root,"choices.txt");
    try{string other=Environment.GetEnvironmentVariable(OverrideVariable);if(IsLocalFullPath(other)&&other.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(other);}catch{}
    return Path.Combine(DeckPaths.Root,"choices.txt");}}
  // A drive letter, a colon and a backslash: not a path relative to a drive or to the current folder, and not a network path.
  public static bool IsLocalFullPath(string path){return !String.IsNullOrWhiteSpace(path)&&path.Length>3&&((path[0]>='A'&&path[0]<='Z')||(path[0]>='a'&&path[0]<='z'))&&path[1]==':'&&path[2]=='\\'&&path.IndexOfAny(Path.GetInvalidPathChars())<0;}
  public static DeckChoices Copy(DeckChoices c){return new DeckChoices{MusicOn=c.MusicOn,Muted=c.Muted,Repeat=c.Repeat,Shuffle=c.Shuffle,Volume=c.Volume,ScalingEngine=c.ScalingEngine,ScalingFilter=c.ScalingFilter,Track=c.Track};}
  // What to write: the file as it is now, with only the values this copy of the launcher changed since it last
  // loaded or saved. A second open launcher therefore cannot undo a choice it did not touch.
  public static DeckChoices Merge(DeckChoices onDisk,DeckChoices before,DeckChoices now){
    var m=Copy(onDisk);
    if(now.MusicOn!=before.MusicOn)m.MusicOn=now.MusicOn;if(now.Muted!=before.Muted)m.Muted=now.Muted;if(now.Repeat!=before.Repeat)m.Repeat=now.Repeat;if(now.Shuffle!=before.Shuffle)m.Shuffle=now.Shuffle;
    if(now.Volume!=before.Volume)m.Volume=now.Volume;if(now.ScalingEngine!=before.ScalingEngine)m.ScalingEngine=now.ScalingEngine;if(now.ScalingFilter!=before.ScalingFilter)m.ScalingFilter=now.ScalingFilter;
    if(CleanTrack(now.Track)!=CleanTrack(before.Track))m.Track=now.Track;
    return m;
  }
  static int Bound(int value,int low,int high,int fallback){return value<low||value>high?fallback:value;}
  public static string CleanTrack(string title){
    if(title==null)return "";var clean=new System.Text.StringBuilder();
    foreach(char ch in title)if(!Char.IsControl(ch)&&clean.Length<80)clean.Append(ch);
    return clean.ToString().Trim();
  }
  public static string Format(DeckChoices c){
    return String.Join("\r\n",new[]{"1","music="+(c.MusicOn?"1":"0"),"volume="+Bound(c.Volume,0,100,20).ToString(CultureInfo.InvariantCulture),"muted="+(c.Muted?"1":"0"),"repeat="+(c.Repeat?"1":"0"),"shuffle="+(c.Shuffle?"1":"0"),"track="+CleanTrack(c.Track),"scaling-engine="+Bound(c.ScalingEngine,0,1,0).ToString(CultureInfo.InvariantCulture),"scaling-filter="+Bound(c.ScalingFilter,0,3,0).ToString(CultureInfo.InvariantCulture)})+"\r\n";
  }
  // A file that another program holds for a moment (a second launcher saving, a virus scanner) is tried three times.
  public static DeckChoices Load(string path){
    for(int attempt=0;;attempt++){
      try{return LoadOnce(path);}
      catch(Exception){if(attempt>=2)return new DeckChoices();System.Threading.Thread.Sleep(60);}
    }
  }
  static DeckChoices LoadOnce(string path){
    var c=new DeckChoices();
    {
      if(!File.Exists(path)||new FileInfo(path).Length>1024)return c;
      var lines=File.ReadAllLines(path);if(lines.Length==0||lines[0].Trim()!="1")return c;
      for(int i=1;i<lines.Length&&i<32;i++){
        int at=lines[i].IndexOf('=');if(at<1)continue;
        string key=lines[i].Substring(0,at).Trim(),value=lines[i].Substring(at+1).Trim();int n;bool number=Int32.TryParse(value,NumberStyles.None,CultureInfo.InvariantCulture,out n);bool flag=value=="0"||value=="1";
        switch(key){
          case "music":if(flag)c.MusicOn=value=="1";break;
          case "muted":if(flag)c.Muted=value=="1";break;
          case "repeat":if(flag)c.Repeat=value=="1";break;
          case "shuffle":if(flag)c.Shuffle=value=="1";break;
          case "volume":if(number)c.Volume=Bound(n,0,100,20);break;
          case "track":c.Track=CleanTrack(value);break;
          case "scaling-engine":if(number)c.ScalingEngine=Bound(n,0,1,0);break;
          case "scaling-filter":if(number)c.ScalingFilter=Bound(n,0,3,0);break;
        }
      }
    }
    return c;
  }
  public static void Save(string path,DeckChoices c){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,Format(c),new System.Text.UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
}
public sealed class SafeSessionLog {
  readonly Queue<string> entries=new Queue<string>();
  public int Count {get{return entries.Count;}}
  public void Add(string code){
    switch(code){
      case "session-started":case "library-opened":case "setup-saved":case "setup-read-failed":
      case "setup-save-failed":case "preferences-save-failed":case "preferences-reset":
      case "references-checked":case "reference-check-failed":
      case "update-available":case "update-check-failed":case "update-installed":case "update-install-failed":case "update-rolled-back":
      case "game-started":case "game-ended":case "game-ended-with-error":case "multiplayer-host":case "multiplayer-join":case "game-build-started":case "game-build-ready":case "game-build-stopped":case "game-build-cancelled":case "game-files-opened":case "keys-saved":case "keys-save-failed":case "card-backed-up":case "card-backup-failed":case "card-imported":case "card-restored":case "card-restore-refused":case "card-restore-failed":break;
      default:return;
    }
    if(entries.Count==50)entries.Dequeue();entries.Enqueue(code);
  }
  public string Text {get{return String.Join(Environment.NewLine,entries.ToArray());}}
}
