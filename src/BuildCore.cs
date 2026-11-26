using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

// "Build my game from my disc" (SPEC-builder.md; builder kit contracts BUILD-1 for Mega Man Legends and BUILD-2 for
// Mega Man Legends 2 and The Misadventures of Tron Bonne). Each game has its own kit, its own output folder and its own marker. This file is pure:
// where the build tools are, the one command line, the meaning of the build's "@RMX" lines and exit codes, and the check
// of the marker file before a built program may start. No window, no network, no program start.
// Rules of this file (check-builder.ps1 reads it):
//  - It writes nothing. It reads the kit's expected.json, the pointer file, the marker file and the built program.
//  - The marker file is untrusted data: the program it names must be inside the output folder, and the program's own
//    SHA-256 must equal the one in the marker, on every start.
//  - A built program starts only as the game that its marker names. The built programs of the later kits do not
//    refuse another game themselves, so this check is the one that keeps the games apart.
public sealed class BuildTools { public string Kit="",Python="",Zig="",Problem="",ManifestPin=""; public int KitVersion; public bool Offered; }
// One game that can be built. Code is what the kit's marker and its "@RMX game" line say. The first kit (BUILD-1,
// Mega Man Legends) writes no game into its marker, so for that game a marker without one is accepted too.
public sealed class BuildGame { public int Id,OldestKit; public string Slug="",Name="",Code="",GameLine="",KitKey="",Time="",Space=""; public bool MarkerNamesGame; public long SpaceNeeded; }
public sealed class BuildLine { public string Kind="text",Word="",Text=""; public int A,B; }
public sealed class BuildProgress {
  public int KitVersion,Step,Done,Total,StopCode=-1;
  public string StepWord="",Part="",StopWord="",StopReason="",DonePath="",Game="";
  public bool TrackDiffers,ZigUnproven,ChildrenNotTied;
}
public sealed class BuildMarker { public string State="",Exe="",ExeSha256="",Selftest="",Game="",Finished="";   /* Finished: the day the build ended, yyyy-MM-dd, or "" */ public bool HasGame; public int KitVersion; }

public static class BuildCore {
  public const string MarkerName="rmx-kit-build.json",ToolsFileName="builder-tools.txt",BuilderFolder="builder";
  public const int Steps=5;
  // The games that can be built. OldestKit: the oldest kit version this launcher builds with (kit 12 of Mega Man Legends
  // let a crafted .cue file open a file anywhere; kit 13 closed that). A game that an older kit built still starts: with
  // the player's own disc image it is sound, and the window asks for a new build. For Mega Man Legends 2 every kit
  // so far says version 1, so the number cannot tell a good kit from an early one: the place that names that kit also
  // pins the SHA-256 of its kit-manifest.json (ManifestPin). SpaceNeeded leaves room above what the contract says.
  static readonly BuildGame[] Table={
    new BuildGame{Id=1,Slug="mml1",Name="Mega Man Legends",Code="SLUS-00603",GameLine="Mega Man Legends (USA) SLUS-00603",KitKey="kit",OldestKit=13,MarkerNamesGame=false,Time="2 to 25 minutes",Space="up to 2 GB",SpaceNeeded=2500L*1024*1024},
    new BuildGame{Id=2,Slug="mml2",Name="Mega Man Legends 2",Code="SLUS-01140",GameLine="Mega Man Legends 2 (USA) SLUS-01140",KitKey="kit2",OldestKit=1,MarkerNamesGame=true,Time="6 to 60 minutes",Space="up to 2 GB",SpaceNeeded=2500L*1024*1024},
    new BuildGame{Id=3,Slug="tron",Name="The Misadventures of Tron Bonne",Code="SLUS-00896",GameLine="The Misadventures of Tron Bonne (USA) SLUS-00896",KitKey="kit3",OldestKit=1,MarkerNamesGame=true,Time="4 to 40 minutes",Space="up to 2 GB",SpaceNeeded=2500L*1024*1024}};
  public static BuildGame Game(int id){foreach(var g in Table)if(g.Id==id)return g;return null;}
  public static BuildGame[] Games {get{return (BuildGame[])Table.Clone();}}
  public static string BuiltRoot(string dataRoot){return Path.Combine(dataRoot,"built");}
  public static string OutputFolder(string dataRoot,BuildGame g){return Path.Combine(dataRoot,"built",g.Slug);}
  public static string LockPath(string dataRoot,BuildGame g){return Path.Combine(dataRoot,"built",g.Slug+".lock");}
  public static string LogPath(string dataRoot,BuildGame g){return Path.Combine(dataRoot,"built",g.Slug+"-last-build.txt");}

  // ---- the build tools ----
  // Shipped: a folder "builder" beside the launcher file, with one kit folder for each game (kit\, kit2\, kit3\) and, once for
  // all kits, python\python.exe and zig\zig.exe.
  // On a developer's PC: the file builder-tools.txt in the data folder names the places (kit=, kit2=, kit3=, python=,
  // zig=; a line kit-manifest-sha256=, kit2-manifest-sha256= or kit3-manifest-sha256= pins that kit).
  // The build of a game is offered only when that game's kit is named. Neither: no build is offered.
  public static BuildTools Find(string baseDirectory,string dataRoot,BuildGame g){
    var t=new BuildTools();
    string beside=Path.Combine(baseDirectory,BuilderFolder);
    if(Directory.Exists(beside)){
      t.Kit=Path.Combine(beside,g.KitKey);if(!Directory.Exists(t.Kit))return new BuildTools();
      t.Offered=true;t.Python=Path.Combine(beside,"python","python.exe");t.Zig=Path.Combine(beside,"zig","zig.exe");
    }else{
      string pointer=Path.Combine(dataRoot,ToolsFileName);
      try{
        var info=new FileInfo(pointer);if(!info.Exists)return t;
        if(info.Length>8192){t.Offered=true;t.Problem="The list of build tools could not be read.";return t;}
        bool named=false;
        foreach(string raw in File.ReadAllLines(pointer)){
          string line=raw.Trim();int eq=line.IndexOf('=');if(eq<1||line.StartsWith("#",StringComparison.Ordinal))continue;
          string key=line.Substring(0,eq).Trim().ToLowerInvariant(),value=line.Substring(eq+1).Trim();
          if(key==g.KitKey){t.Kit=value;named=true;}else if(key==g.KitKey+"-manifest-sha256")t.ManifestPin=value;else if(key=="python")t.Python=value;else if(key=="zig")t.Zig=value;
        }
        if(!named)return new BuildTools();
        t.Offered=true;
      }catch(Exception){t=new BuildTools();t.Offered=true;t.Problem="The list of build tools could not be read.";return t;}
    }
    t.Problem=ToolsProblem(t);
    if(t.Problem.Length==0)t.Problem=KitProblem(t,g);
    return t;
  }
  // Is this kit the kit of this game, new enough, and (when it is pinned) unchanged?
  static string KitProblem(BuildTools t,BuildGame g){
    const string repair=" Repair or reinstall Flutter Deck.";
    string line;t.KitVersion=KitInfo(t.Kit,out line);
    if(line!=null&&line!=g.GameLine)return "This builder kit is for another game, not for "+g.Name+"."+repair;
    if(t.KitVersion<g.OldestKit)return "The builder kit is missing its version, or it is too old to be safe."+repair;
    if(line==null&&g.MarkerNamesGame)return "This builder kit is for another game, not for "+g.Name+"."+repair;
    if(t.ManifestPin.Length>0){
      if(!Regex.IsMatch(t.ManifestPin,"\\A[0-9A-Fa-f]{64}\\z"))return "The list of build tools could not be read.";
      string got;try{got=Sha256Of(Path.Combine(t.Kit,"kit-manifest.json"));}catch(Exception){return "The builder kit could not be read."+repair;}
      if(!String.Equals(got,t.ManifestPin,StringComparison.OrdinalIgnoreCase))return "The builder kit is not the one that was checked."+repair;
    }
    return "";
  }
  static string ToolsProblem(BuildTools t){
    const string repair=" Repair or reinstall Flutter Deck.";
    try{
      foreach(string path in new[]{t.Kit,t.Python,t.Zig})if(path.Length==0||path.IndexOf('"')>=0||!Path.IsPathRooted(path)||path!=Path.GetFullPath(path))return "A build tool has no usable place."+repair;
      foreach(string name in new[]{"build_game.py","kit-manifest.json","expected.json"})if(!File.Exists(Path.Combine(t.Kit,name)))return "The builder kit is missing or not complete."+repair;
      if(!String.Equals(Path.GetFileName(t.Python),"python.exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(t.Python))return "The build tool Python is missing."+repair;
      if(!String.Equals(Path.GetFileName(t.Zig),"zig.exe",StringComparison.OrdinalIgnoreCase)||!File.Exists(t.Zig))return "The build tool Zig is missing."+repair;
    }catch(Exception){return "A build tool has no usable place."+repair;}
    return "";
  }
  // The kit's expected.json: its version (0 when it cannot be read) and the game it builds (null when it names none).
  // This file comes with the kit and may hold lists, so it is not read with Flat. Each of the two keys must be there
  // at most once, as plain text or a plain number.
  public static int KitInfo(string kit,out string gameLine){
    gameLine=null;
    try{var info=new FileInfo(Path.Combine(kit,"expected.json"));if(!info.Exists||info.Length>65536)return 0;
      string text=File.ReadAllText(info.FullName,Encoding.UTF8).Trim();
      if(text.Length>0&&text[0]=='\uFEFF')text=text.Substring(1).Trim();
      if(!text.StartsWith("{",StringComparison.Ordinal)||!text.EndsWith("}",StringComparison.Ordinal)||text.IndexOf('\\')>=0)return 0;
      // Only what is directly in the outer object is looked at: everything deeper (a list, an inner object) is blanked.
      var top=new StringBuilder(text.Length);int depth=0;bool quoted=false,closed=false;
      foreach(char c in text){
        if(c=='"')quoted=!quoted;
        if(!quoted&&(c=='}'||c==']'))depth--;
        top.Append(depth==1||(depth==0&&c=='}')?c:' ');
        if(!quoted&&(c=='{'||c=='['))depth++;
        if(depth<0)return 0;
        if(depth==0&&!closed){closed=true;continue;}
        if(closed&&!Char.IsWhiteSpace(c))return 0;   // one object only: nothing may follow it
      }
      if(depth!=0||quoted)return 0;
      text=top.ToString();
      var version=Regex.Matches(text,"\"kit_version\"\\s*:\\s*([0-9]{1,9})\\s*[,}]");
      var game=Regex.Matches(text,"\"game\"\\s*:\\s*\"([^\"\\\\\\x00-\\x1f]*)\"\\s*[,}]");
      if(version.Count!=1||Regex.Matches(text,"\"kit_version\"").Count!=1||game.Count>1||Regex.Matches(text,"\"game\"").Count!=game.Count)return 0;
      string line=game.Count==1?game[0].Groups[1].Value:null;
      int n=Int32.Parse(version[0].Groups[1].Value,CultureInfo.InvariantCulture);
      gameLine=line;return n;
    }catch(Exception){gameLine=null;return 0;}
  }
  public static int KitVersionOf(string kit){string line;return KitInfo(kit,out line);}

  // ---- the command ----
  // <python> -B <kit>\build_game.py --cue <cue> --out <output folder> --zig <zig>   (contracts BUILD-1 and BUILD-2, "The command")
  public static string Arguments(BuildTools t,string cue,string outFolder){
    return "-B "+Quote(Path.Combine(t.Kit,"build_game.py"))+" --cue "+Quote(cue)+" --out "+Quote(outFolder)+" --zig "+Quote(t.Zig);
  }
  // Windows command-line quoting: a quote sign cannot be in a path; backslashes before the closing quote are doubled.
  public static string Quote(string value){
    if(String.IsNullOrEmpty(value)||value.IndexOf('"')>=0||value.IndexOfAny(new[]{'\r','\n','\0'})>=0)throw new ArgumentException("This path cannot be given to the build.");
    int tail=0;while(tail<value.Length&&value[value.Length-1-tail]=='\\')tail++;
    return "\""+value+new string('\\',tail)+"\"";
  }

  // ---- what the build says ----
  static bool Num(string text,out int n){return Int32.TryParse(text,NumberStyles.None,CultureInfo.InvariantCulture,out n)&&n<=100000000&&text.Length<=9;}
  static bool Word(string text){return Regex.IsMatch(text,"\\A[a-z0-9-]{1,40}\\z");}
  // Lines that start with "@RMX " are for the launcher. Every other line is for a person. A word this code does not
  // know gives the kind "unknown", which changes nothing.
  public static BuildLine ParseLine(string line){
    var r=new BuildLine{Text=line??""};
    if(line==null||!line.StartsWith("@RMX ",StringComparison.Ordinal))return r;
    r.Kind="unknown";
    string[] w=line.Substring(5).Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);if(w.Length==0)return r;
    int a,b;
    if(w[0]=="kit"&&w.Length==2&&Num(w[1],out a)){r.Kind="kit";r.A=a;}
    else if(w[0]=="game"&&w.Length==2&&Regex.IsMatch(w[1],"\\A[A-Z]{4}-[0-9]{5}\\z")){r.Kind="game";r.Word=w[1];}
    else if(w[0]=="step"&&w.Length==4&&Num(w[1],out a)&&Num(w[2],out b)&&a>=1&&a<=b&&Word(w[3])){r.Kind="step";r.A=a;r.B=b;r.Word=w[3];}
    else if(w[0]=="progress"&&w.Length==4&&Word(w[1])&&Num(w[2],out a)&&Num(w[3],out b)&&a<=b){r.Kind="progress";r.Word=w[1];r.A=a;r.B=b;}
    else if(w[0]=="note"&&w.Length==2&&Word(w[1])){r.Kind="note";r.Word=w[1];}
    else if(w[0]=="stop"&&w.Length==3&&Num(w[1],out a)&&Word(w[2])){r.Kind="stop";r.A=a;r.Word=w[2];}
    else if(w[0]=="done"&&w.Length>=2){r.Kind="done";r.Word=line.Substring(5).Trim().Substring(4).Trim();}
    return r;
  }
  // Takes one line of the build's output into the state. Returns true when the line was for the launcher.
  public static bool Apply(BuildProgress p,string line){
    if(line!=null&&line.StartsWith("STOPPED: ",StringComparison.Ordinal)){string why=line.Substring(9).Trim();p.StopReason=why.Length>700?why.Substring(0,700):why;return false;}
    var l=ParseLine(line);
    if(l.Kind=="text")return false;
    if(l.Kind=="kit")p.KitVersion=l.A;
    else if(l.Kind=="game")p.Game=l.Word;
    else if(l.Kind=="step"){if(l.A>=p.Step){p.Step=l.A;p.StepWord=l.Word;p.Part="";p.Done=0;p.Total=0;}}
    else if(l.Kind=="progress"){p.Part=l.Word;p.Done=l.A;p.Total=l.B;}
    else if(l.Kind=="note"){if(l.Word=="track-differs")p.TrackDiffers=true;else if(l.Word=="zig-version-unproven")p.ZigUnproven=true;else if(l.Word=="children-not-tied")p.ChildrenNotTied=true;}
    else if(l.Kind=="stop"){p.StopCode=l.A;p.StopWord=l.Word;}
    else if(l.Kind=="done")p.DonePath=l.Word;
    return true;
  }
  // 0 to 1 for a progress bar. Step 4 takes nearly all of the time, and of step 4 the compiling does (contract).
  public static double Fraction(BuildProgress p){
    if(p.DonePath.Length>0)return 1;
    double part=p.Total>0?Math.Min(1.0,(double)p.Done/p.Total):0;
    switch(p.Step){
      case 0:return 0;
      case 1:return 0.01;
      case 2:return 0.03;
      case 3:return 0.07;
      case 4:
        if(p.Part=="translate")return 0.09+0.09*part;
        if(p.Part=="compile")return 0.18+0.74*part;
        if(p.Part=="link")return 0.92+0.03*part;
        return 0.09;
      default:return 0.96;
    }
  }
  public static string StepText(BuildProgress p){
    if(p.Step==0)return "The build is starting.";
    string what=p.StepWord=="tools"?"checking the build tools":p.StepWord=="read-disc"?"reading your disc image":p.StepWord=="check-disc"?"checking that it is the right game":p.StepWord=="build"?"building the game":p.StepWord=="self-test"?"testing the new program":"working";
    string part="";
    if(p.Step==4&&p.Total>0){string verb=p.Part=="translate"?"translating":p.Part=="compile"?"compiling":p.Part=="link"?"linking":"";if(verb.Length>0)part=" ("+verb+", "+p.Done.ToString(CultureInfo.InvariantCulture)+" of "+p.Total.ToString(CultureInfo.InvariantCulture)+")";}
    return "Step "+p.Step.ToString(CultureInfo.InvariantCulture)+" of "+Steps.ToString(CultureInfo.InvariantCulture)+": "+what+part+".";
  }
  // What the player is told for each exit code (contracts BUILD-1 and BUILD-2, "Exit codes").
  public static string ExitText(int code,BuildGame g){
    switch(code){
      case 0:return "The game is ready.";
      case 2:return "Flutter Deck gave the build a wrong command. This is a fault in Flutter Deck. Keep the details for a report.";
      case 10:return "A build tool is missing or does not run. Repair or reinstall Flutter Deck.";
      case 11:return "The disc image was not found: the .cue file, or a .bin file that it names, is missing.";
      case 12:return "The folder for the built game cannot be used.";
      case 13:return "A file of the builder kit is missing or changed. Repair or reinstall Flutter Deck.";
      case 20:return "The disc image is cut, damaged, or not a PlayStation data disc, or its .cue file is not a plain cue sheet. Make a new image of your disc.";
      case 21:return "This is another game or another version, or the game's code on the image is damaged. Only "+g.Name+", USA version ("+g.Code+"), can be built here.";
      case 30:return "The build failed. Keep the details for a report.";
      case 31:return "The new program did not pass its test, so it was not put in place. Keep the details for a report.";
      case 32:return "Windows kept a file or folder locked (often a virus scanner). Build again.";
      case 33:return "An unexpected fault stopped the build. Keep the details for a report.";
      default:return "The build was ended or crashed before it could finish (code "+code.ToString(CultureInfo.InvariantCulture)+").";
    }
  }

  // ---- the kit's small JSON files ----
  // They are flat: text without escapes, and plain numbers. A file of any other shape is not used at all.
  public static Dictionary<string,string> Flat(string json){
    const string pair="\"([^\"\\\\\\x00-\\x1f]*)\"\\s*:\\s*(?:\"([^\"\\\\\\x00-\\x1f]*)\"|(-?[0-9]{1,12}(?:\\.[0-9]{1,6})?))";
    if(json==null||json.Length>65536)return null;
    if(json.Length>0&&json[0]=='\uFEFF')json=json.Substring(1);
    if(!Regex.IsMatch(json,"\\A\\s*\\{\\s*(?:"+pair+"\\s*(?:,\\s*"+pair+"\\s*)*)?\\}\\s*\\z"))return null;
    var d=new Dictionary<string,string>(StringComparer.Ordinal);
    foreach(Match m in Regex.Matches(json,pair)){if(d.ContainsKey(m.Groups[1].Value))return null;d[m.Groups[1].Value]=m.Groups[2].Success?m.Groups[2].Value:m.Groups[3].Value;}
    return d;
  }
  public static BuildMarker ReadMarker(string outFolder){
    try{
      var info=new FileInfo(Path.Combine(outFolder,MarkerName));if(!info.Exists||info.Length>65536)return null;
      var d=Flat(File.ReadAllText(info.FullName,Encoding.UTF8));if(d==null)return null;
      var m=new BuildMarker();string v;
      if(d.TryGetValue("state",out v))m.State=v;
      if(d.TryGetValue("exe",out v))m.Exe=v;
      if(d.TryGetValue("exe_sha256",out v))m.ExeSha256=v;
      if(d.TryGetValue("selftest",out v))m.Selftest=v;
      if(d.TryGetValue("game",out v)){m.Game=v;m.HasGame=true;}
      if(d.TryGetValue("kit_version",out v))Int32.TryParse(v,NumberStyles.None,CultureInfo.InvariantCulture,out m.KitVersion);
      if(d.TryGetValue("finished",out v)){var day=Regex.Match(v,"^([0-9]{4}-[0-9]{2}-[0-9]{2})($|[ T])");DateTime real;if(day.Success&&DateTime.TryParseExact(day.Groups[1].Value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out real))m.Finished=day.Groups[1].Value;}
      return m;
    }catch(Exception){return null;}
  }

  // ---- what the player is told about the builder ----
  // "builder version 23 on 2026-11-24" from the record of a build; "" when the record does not give the version.
  public static string BuiltWith(BuildMarker m){return m==null||m.KitVersion<=0?"":"builder version "+m.KitVersion.ToString(CultureInfo.InvariantCulture)+(m.Finished.Length>0?" on "+m.Finished:"");}
  // For a game's Settings page: which builder made the game program in use, and which builder this Flutter Deck has.
  // The record describes the last build, not the program in use, so it is quoted only when the program in use is exactly the program the record names (same SHA-256).
  // programHash: the hash of the program in use, "" when there is none. installedKit: 0 when the builder is not found.
  public static string BuilderLine(BuildMarker m,BuildGame g,int installedKit,string programHash){
    bool has=!String.IsNullOrEmpty(programHash);
    bool mine=has&&MarkerIsFor(m,g)&&m.State=="ready"&&String.Equals(m.ExeSha256,programHash,StringComparison.OrdinalIgnoreCase);
    string with=mine?BuiltWith(m):"";
    string first=!has?"":with.Length>0?"Your game was built with "+with+".":mine?"Your game was built by Flutter Deck. Its record does not give the builder version.":m==null?"Flutter Deck has no record of building this game, so the builder version of the game program in use is not known.":"The game program in use is not the one Flutter Deck built, so its builder version is not known.";
    string second=installedKit>0?"This Flutter Deck has builder version "+installedKit.ToString(CultureInfo.InvariantCulture)+".":"";
    string third=with.Length>0&&installedKit>m.KitVersion?"Build the game again to get the newer builder's changes.":"";
    var parts=new List<string>();foreach(string s in new[]{first,second,third})if(s.Length>0)parts.Add(s);
    return String.Join(" ",parts.ToArray());
  }

  // True when the program in use is the one the record names (as in BuilderLine) and this Flutter Deck has a newer builder than the one that built it.
  public static bool BuilderIsNewer(BuildMarker m,BuildGame g,int installedKit,string programHash){return !String.IsNullOrEmpty(programHash)&&MarkerIsFor(m,g)&&m.State=="ready"&&String.Equals(m.ExeSha256,programHash,StringComparison.OrdinalIgnoreCase)&&m.KitVersion>0&&installedKit>m.KitVersion;}

  // ---- may this built program start? ----
  public static string Sha256Of(string path){using(var sha=SHA256.Create())using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");}
  public static bool Inside(string path,string folder){
    try{string f=Path.GetFullPath(folder).TrimEnd('\\')+"\\";return Path.GetFullPath(path).StartsWith(f,StringComparison.OrdinalIgnoreCase);}catch(Exception){return false;}
  }
  // Does this marker belong to this game? A marker that names a game must name this one. A marker that names none is
  // from the first kit, which builds Mega Man Legends only.
  public static bool MarkerIsFor(BuildMarker m,BuildGame g){return m!=null&&g!=null&&(m.HasGame?m.Game==g.Code:!g.MarkerNamesGame);}
  // "" when the folder holds a finished, tested build of this game whose program is exactly the one the marker names;
  // then program and hash are set. Otherwise a sentence for the player.
  public static string BuiltProblem(string outFolder,BuildGame g,out string program,out string hash,Func<string,string> hasher=null){
    program="";hash="";
    const string again=" Build the game again.";
    if(g==null)return "This game cannot be built.";
    var m=ReadMarker(outFolder);
    if(m==null)return File.Exists(Path.Combine(outFolder,MarkerName))?"The record of the built game cannot be read."+again:"The game is not built yet.";
    if(!MarkerIsFor(m,g))return "This built game is another game, not "+g.Name+". It can be started only as the game it was built as.";
    if(m.State!="ready")return "The build of the game was not finished."+again;
    if(m.Selftest!="passed")return "The built game did not pass its test."+again;
    if(!Regex.IsMatch(m.ExeSha256,"\\A[0-9A-Fa-f]{64}\\z"))return "The record of the built game cannot be read."+again;
    if(m.Exe.Length>200||m.Exe.Contains("..")||!Regex.IsMatch(m.Exe,"\\A[A-Za-z0-9_-][A-Za-z0-9_.-]*(/[A-Za-z0-9_-][A-Za-z0-9_.-]*)*\\.exe\\z"))return "The record of the built game names a program outside its folder."+again;
    string full;
    try{full=Path.GetFullPath(Path.Combine(outFolder,m.Exe.Replace('/','\\')));}catch(Exception){return "The record of the built game names a program outside its folder."+again;}
    if(!Inside(full,outFolder))return "The record of the built game names a program outside its folder."+again;
    if(!File.Exists(full))return "The built game program is missing."+again;
    string got;try{got=hasher!=null?hasher(full):Sha256Of(full);}catch(Exception){return "The built game program could not be read.";}
    if(!String.Equals(got,m.ExeSha256,StringComparison.OrdinalIgnoreCase))return "The built game program is not the one that passed its test."+again;
    program=full;hash=got;return "";
  }
  // The folders of a build that this program may belong to: every folder with a marker file up to three levels above
  // it, the nearest first.
  public static List<string> BuildFoldersOf(string program){
    var found=new List<string>();
    try{
      string dir=Path.GetDirectoryName(Path.GetFullPath(program));
      for(int level=0;level<3&&dir!=null;level++){if(File.Exists(Path.Combine(dir,MarkerName)))found.Add(dir);dir=Path.GetDirectoryName(dir);}
    }catch(Exception){}
    return found;
  }
  public static string BuildFolderOf(string program){var all=BuildFoldersOf(program);return all.Count>0?all[0]:null;}
  // True while a build of this game into the launcher's output folder runs, in this Flutter Deck or in another one:
  // the gate file beside the folder is held open, unshared, for the whole build.
  public static bool BuildRunning(string dataRoot,BuildGame g){
    try{
      string gate=LockPath(dataRoot,g);if(!File.Exists(gate))return false;
      using(new FileStream(gate,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){}
      return false;
    }catch(FileNotFoundException){return false;}
    catch(DirectoryNotFoundException){return false;}
    catch(IOException){return true;}
    catch(Exception){return false;}
  }
  // True while a build of any game runs. No game is started then: a build tests its new program by running it.
  public static bool AnyBuildRunning(string dataRoot){foreach(var g in Table)if(BuildRunning(dataRoot,g))return true;return false;}
  // Asked before a game program is run in any way: for a start, and for the short run that asks the program what it
  // is, as the game g. "" lets it go on. A program that a build made (it has a marker above it, or it is anywhere
  // inside the launcher's own folder for built games) runs only while a marker of this game proves it. When more than
  // one marker is above it, each of them must prove it, so a marker put nearer to the program cannot stand in for the
  // real one. A file inside the folder for built games runs only from the folder of its own game, and only while that
  // folder's marker proves it. A copy that the player made elsewhere is a file of the player's own choice, like any
  // other game program; a marker beside such a copy still decides its game. hasher: null computes the SHA-256 now
  // (always so for a start).
  // real: gives the true place of a file or folder, behind every folder link (junction, symbolic link, substituted
  // drive); null when it cannot be found. The launcher always gives one (RealPath in BuildRun.cs), so a link into a build
  // folder is checked as the file it leads to. Without one the path is taken as written (tests of the pure rules).
  // same: gives a text that is equal for two names of one folder and for no two folders (FileId in BuildRun.cs); null
  // when it cannot. With it, a program that is reached by another name of the folder for built games (a share of this
  // PC, a mapped drive) is found to be inside that folder all the same, and so is another name of a program file in a
  // game's build folder (a hard link, or a share whose top is below the folder for built games).
  public static string StartProblem(string program,BuildGame g,string dataRoot,Func<string,string> hasher=null,Func<string,string> real=null,Func<string,string> same=null){
    if(String.IsNullOrEmpty(program))return "";
    if(g==null)return "This game cannot be started.";
    string full;try{full=Path.GetFullPath(program);}catch(Exception){return "";}
    string built;try{built=Path.GetFullPath(BuiltRoot(dataRoot)).TrimEnd('\\');}catch(Exception){return "The folder for built games cannot be checked.";}
    if(real!=null){
      string place;try{place=real(full);}catch(Exception){place=null;}
      if(String.IsNullOrEmpty(place)){if(!File.Exists(full))return "";return "The place of this game program cannot be checked. Choose the game program again.";}
      full=place;
      if(Directory.Exists(built)){
        string builtPlace;try{builtPlace=real(built);}catch(Exception){builtPlace=null;}
        if(String.IsNullOrEmpty(builtPlace))return "The folder for built games cannot be checked.";
        built=builtPlace.TrimEnd('\\');
      }
    }
    var homes=BuildFoldersOf(full);
    // Is the program inside the folder for built games? By its path, or, when the path is another name of that folder,
    // by walking up from the program until a folder is the same folder. first: the folder directly below it.
    string root=null,first=null;
    if(Inside(full,built)){
      string below=full.Substring(built.Length+1);int cut=below.IndexOf('\\');
      root=built;first=cut<1?"":below.Substring(0,cut);
    }else if(same!=null&&Directory.Exists(built)){
      string builtId;try{builtId=same(built);}catch(Exception){builtId=null;}
      if(String.IsNullOrEmpty(builtId))return "The folder for built games cannot be checked.";
      string child=null,dir=Path.GetDirectoryName(full);
      for(int level=0;level<64&&dir!=null;level++){
        string id;try{id=same(dir);}catch(Exception){id=null;}
        if(id==builtId){root=dir.TrimEnd('\\');first=child==null?"":Path.GetFileName(child.TrimEnd('\\'));break;}
        child=dir;dir=Path.GetDirectoryName(dir);
      }
      // Not met on the way up (a share or a link whose top is below that folder, or a hard link): is the file itself
      // one of the programs in a game's build folder?
      if(root==null){
        string me;try{me=same(full);}catch(Exception){me=null;}
        if(!String.IsNullOrEmpty(me))foreach(var other in Table){
          string[] programs;try{string where=Path.Combine(built,other.Slug,"build","game");programs=Directory.Exists(where)?Directory.GetFiles(where,"*.exe"):new string[0];}catch(Exception){programs=new string[0];}
          for(int i=0;i<programs.Length&&i<256&&root==null;i++){string it;try{it=same(programs[i]);}catch(Exception){it=null;}if(it==me){root=built;first=other.Slug;}}
          if(root!=null)break;
        }
      }
    }
    if(root!=null){
      if(first.Length==0)return "This file is not the program that a build finished. Build the game again.";
      string own=Path.Combine(root,first);
      if(!String.Equals(first,g.Slug,StringComparison.OrdinalIgnoreCase))return "This built game is in the folder of another game, not of "+g.Name+". It can be started only as the game it was built as.";
      if(!homes.Exists(delegate(string h){return String.Equals(h.TrimEnd('\\'),own,StringComparison.OrdinalIgnoreCase);}))homes.Add(own);
    }
    if(homes.Count==0)return "";
    foreach(string home in homes){
      string exe,hash;string problem=BuiltProblem(home,g,out exe,out hash,hasher);
      if(problem.Length>0)return problem;
      if(!String.Equals(exe,full,StringComparison.OrdinalIgnoreCase)){
        // another name of the same file is the same program
        string a=null,b=null;if(same!=null){try{a=same(exe);b=same(full);}catch(Exception){a=null;}}
        if(String.IsNullOrEmpty(a)||a!=b)return "This file is not the program that the build finished. Build the game again.";
      }
    }
    return "";
  }

  // The "Files" button: the folder where this game is built, when a build of this game was made or begun there. ""
  // when there is none; the button then says so and opens nothing. Only this one folder is ever given: no path from
  // the marker file is used.
  public static string FilesFolder(string dataRoot,BuildGame g){
    if(g==null)return "";
    try{
      string folder=Path.GetFullPath(OutputFolder(dataRoot,g));
      if(!Directory.Exists(folder))return "";
      return MarkerIsFor(ReadMarker(folder),g)?folder:"";
    }catch(Exception){return "";}
  }

  // ---- a built game never leaves this PC ----
  // The output folder must not be inside a folder that a cloud service copies away.
  public static List<string> SyncRoots(){
    var roots=new List<string>();
    foreach(string name in new[]{"OneDrive","OneDriveConsumer","OneDriveCommercial"}){string v=Environment.GetEnvironmentVariable(name);if(!String.IsNullOrEmpty(v)&&!roots.Contains(v))roots.Add(v);}
    return roots;
  }
  // profile: the user's own folder. Its name is the name of the Windows account and is not looked at.
  public static string SyncProblem(string folder,IEnumerable<string> syncRoots,string profile=null){
    const string say="The folder for the built game is inside a folder that a cloud service copies to the internet. A built game must stay on this PC. Move the data folder of Flutter Deck out of it.";
    try{
      string full=Path.GetFullPath(folder);
      foreach(string root in syncRoots){if(String.IsNullOrEmpty(root))continue;if(Inside(full,root)||String.Equals(full.TrimEnd('\\'),Path.GetFullPath(root).TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return say;}
      string below=!String.IsNullOrEmpty(profile)&&Inside(full,profile)?full.Substring(Path.GetFullPath(profile).TrimEnd('\\').Length):full.Substring(Math.Min(full.Length,Path.GetPathRoot(full).Length));
      foreach(string part in below.Split('\\'))if(Regex.IsMatch(part,"\\A(OneDrive( - .+)?|Dropbox( \\(.+\\))?|Google Drive|GoogleDrive|My Drive|iCloudDrive|iCloud Drive|pCloud Drive|MEGA|MEGAsync|Box|Box Sync|Nextcloud)\\z",RegexOptions.IgnoreCase))return say;
    }catch(Exception){return "The folder for the built game cannot be used.";}
    return "";
  }
}
