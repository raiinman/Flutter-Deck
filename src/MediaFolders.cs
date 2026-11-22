using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
// Library folders (SPEC-library-folders.md): the user names a folder once, and the Library is filled from it.
//  - The search only reads. It never writes, moves, renames or deletes anything in the folder, and it starts no program.
//  - What it finds is kept in library-found.txt, apart from library.txt (the items the user added by hand), whose format does not change.
//  - A search that failed, or that could not finish, removes nothing: an item is dropped only by a complete search of its folder.
//  - Which disc image holds which game, and which program file is a known game program, are asked through two functions the
//    caller gives. So this file reads no disc image and no program file itself, and the tests use stand-ins.
//  - A network drive and a linked folder are refused, like the game folder (DiscFinder.cs): a search there can be very slow.
public sealed class MediaScan {
  public string Root="";
  public readonly List<LibraryItem> Items=new List<LibraryItem>();
  public const int Games=3;
  public readonly List<string>[] Discs={new List<string>(),new List<string>(),new List<string>()};     // disc images that hold Mega Man Legends / Mega Man Legends 2 / Tron Bonne
  public readonly List<string>[] Programs={new List<string>(),new List<string>(),new List<string>()};  // known game programs for the three games
  public bool Truncated,Deeper,Unread; public string Problem=""; public int Entries; // Unread: a folder inside could not be read (no access, or it went away during the search)
  // Complete: every folder down to the depth limit was read. The depth limit is the same at every search, so a folder below it
  // was never listed and cannot be lost; the entry limit can cut a search at a different place each time, so it is not complete.
  public bool Complete {get{return Problem.Length==0&&!Truncated&&!Unread;}}
}
public static class MediaFolders {
  public const int MaxFolders=8,MaxDepth=8,MaxEntries=60000,MaxCues=400,MaxPrograms=12,MaxFound=5000,MaxListed=300,MaxPath=520,MaxSettingBytes=16384,MaxFoundBytes=4*1024*1024,MaxTitle=120;
  public static readonly string[] PictureExtensions={".jpg",".jpeg",".png",".bmp",".gif"};
  static readonly string[] CoverNames={"cover","poster","folder","front","boxart"};
  static readonly Regex EpisodeName=new Regex(@"(s\d{1,2}[ ._-]?e\d{1,3}|(?<![0-9a-z])\d{1,2}x\d{2,3}(?![0-9a-z])|(?<![a-z])(episode|ep)[ ._-]?\d{1,3}(?![0-9]))",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  static readonly Regex SeasonFolder=new Regex(@"^((season|series|staffel)[ ._-]?\d{1,2}(?![0-9]).*|s\d{1,2}|specials)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  static readonly Regex ProgramName=new Regex(@"^(rock_neo_rt|mml2_rt)[A-Za-z0-9_.-]*\.exe$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
  public static string PathName {get{return Path.Combine(DeckPaths.Root,"library-folders.txt");}}
  public static string FoundPathName {get{return Path.Combine(DeckPaths.Root,"library-found.txt");}}

  public static bool IsLocalFullPath(string path){
    return !String.IsNullOrWhiteSpace(path)&&path.Length>=3&&path.Length<=MaxPath&&Char.IsLetter(path[0])&&path[1]==':'&&path[2]=='\\'&&path.IndexOf('/')<0&&path.IndexOfAny(Path.GetInvalidPathChars())<0&&path.IndexOf('\t')<0;
  }
  static string Clean(string folder){string full=Path.GetFullPath(folder);return full.Length>3?full.TrimEnd('\\'):full;}
  public static bool SameFolder(string a,string b){try{return String.Equals(Clean(a),Clean(b),StringComparison.OrdinalIgnoreCase);}catch{return false;}}
  // True when path is the folder or lies inside it.
  public static bool Inside(string folder,string path){
    try{string root=Clean(folder),full=Path.GetFullPath(path);if(String.Equals(root,full.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase))return true;if(!root.EndsWith("\\"))root+="\\";return full.StartsWith(root,StringComparison.OrdinalIgnoreCase);}catch{return false;}
  }
  public static bool IsPicture(string path){string ext;try{ext=Path.GetExtension(path);}catch{return false;}foreach(string known in PictureExtensions)if(String.Equals(ext,known,StringComparison.OrdinalIgnoreCase))return true;return false;}
  public static bool IsVideo(string path){string ext;try{ext=Path.GetExtension(path);}catch{return false;}foreach(string known in LibraryCore.MovieExtensions)if(String.Equals(ext,known,StringComparison.OrdinalIgnoreCase))return true;return false;}
  public static bool IsEpisodeName(string file){try{return EpisodeName.IsMatch(Path.GetFileNameWithoutExtension(file));}catch{return false;}}
  static bool IsSample(string file){string name=Path.GetFileNameWithoutExtension(file);return String.Equals(name,"sample",StringComparison.OrdinalIgnoreCase)||name.EndsWith("-sample",StringComparison.OrdinalIgnoreCase)||name.EndsWith(".sample",StringComparison.OrdinalIgnoreCase);}
  static string Title(string name){if(name==null)return "";var clean=new StringBuilder();foreach(char ch in name)if(!Char.IsControl(ch)&&clean.Length<MaxTitle)clean.Append(ch=='_'?' ':ch);return clean.ToString().Trim();}

  // ---- the folders the user named: the format number 1, then one folder on each line ----
  public static List<string> LoadFolders(string path){
    var folders=new List<string>();if(!File.Exists(path))return folders;
    if(new FileInfo(path).Length>MaxSettingBytes)throw new InvalidDataException("The library folders setting is too large.");
    string text=File.ReadAllText(path,Encoding.UTF8);if(text.Trim().Length==0)return folders;
    string[] lines=text.Replace("\r\n","\n").Split('\n');
    if(lines[0].Trim()!="1")throw new InvalidDataException("The library folders setting has an unknown format.");
    for(int i=1;i<lines.Length&&folders.Count<MaxFolders;i++){string folder=lines[i].Trim();if(IsLocalFullPath(folder)&&!folders.Any(f=>SameFolder(f,folder)))folders.Add(Clean(folder));}
    return folders;
  }
  public static void SaveFolders(string path,IList<string> folders){
    var text=new StringBuilder("1\r\n");int kept=0;
    foreach(string folder in folders){if(!IsLocalFullPath(folder))throw new ArgumentException("A library folder must be a full path on a drive of this computer.");if(++kept>MaxFolders)throw new ArgumentException("Too many library folders.");text.Append(Clean(folder)).Append("\r\n");}
    WriteWhole(path,text.ToString(),MaxSettingBytes);
  }
  // "" when the folder may be added to the list; otherwise the reason.
  public static string CheckNewFolder(IList<string> folders,string folder){
    if(!IsLocalFullPath(folder))return "Choose a folder on a drive of this computer.";
    if(folders.Count>=MaxFolders)return "Flutter Deck keeps at most "+MaxFolders+" library folders. Stop using one first.";
    foreach(string known in folders){if(SameFolder(known,folder))return "That folder is already a library folder.";if(Inside(known,folder))return "That folder is inside a library folder you already have: "+known;if(Inside(folder,known))return "That folder holds a library folder you already have ("+known+"). Stop using that one first.";}
    return "";
  }

  // ---- what was found: the format number 1, then kind, title, file or folder, cover picture and library folder on each line ----
  public static List<LibraryItem> LoadFound(string path){
    var found=new List<LibraryItem>();if(!File.Exists(path))return found;
    if(new FileInfo(path).Length>MaxFoundBytes)throw new InvalidDataException("The found items file is too large.");
    string text=File.ReadAllText(path,Encoding.UTF8);if(text.Trim().Length==0)return found;
    string[] lines=text.Replace("\r\n","\n").Split('\n');
    if(lines[0].Trim()!="1")throw new InvalidDataException("The found items file has an unknown format.");
    for(int i=1;i<lines.Length&&found.Count<MaxFound;i++){
      string[] cell=lines[i].Split('\t');if(cell.Length!=5)continue;
      var item=Make(cell[0],cell[1],cell[2],cell[3],cell[4]);if(item!=null&&!found.Any(f=>LibraryCore.Same(f,item)))found.Add(item);
    }
    return found;
  }
  public static void SaveFound(string path,IList<LibraryItem> found){
    var text=new StringBuilder("1\r\n");
    foreach(var item in found){if(item==null||!item.Found)continue;text.Append(item.Kind).Append('\t').Append(Title(item.Title)).Append('\t').Append(item.Path??"").Append('\t').Append(item.Cover??"").Append('\t').Append(item.Root??"").Append("\r\n");}
    WriteWhole(path,text.ToString(),MaxFoundBytes);
  }
  static void WriteWhole(string path,string text,int limit){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{byte[] bytes=new UTF8Encoding(false).GetBytes(text);if(bytes.Length>limit)throw new InvalidDataException("Too much to save.");File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
  // A found item, or null when a value is not one this build accepts. A found item always lies inside its library folder.
  public static LibraryItem Make(string kind,string title,string path,string cover,string root){
    if(kind!="game"&&kind!="movie"&&kind!="show"&&kind!="art")return null;
    if(!IsLocalFullPath(root)||!IsLocalFullPath(path)||!Inside(root,path))return null;
    if(kind=="movie"&&!LibraryCore.IsMovieFile(path))return null;
    if(kind=="game"&&!String.Equals(Path.GetExtension(path),".cue",StringComparison.OrdinalIgnoreCase))return null;
    string clean=Title(title);if(clean.Length==0)return null;
    if(!String.IsNullOrEmpty(cover)&&(!IsLocalFullPath(cover)||!IsPicture(cover)||!Inside(root,cover)))cover="";
    return new LibraryItem{Kind=kind,Title=clean,Path=path,Cover=cover??"",Root=Clean(root),Found=true};
  }

  // ---- the search ----
  sealed class Dir { public readonly List<string> Videos=new List<string>(),Pictures=new List<string>(),Cues=new List<string>(); public int Other; }
  // Folders of tools and of work in progress are not media: they are not searched.
  static readonly string[] SkippedFolders={"node_modules","__pycache__","$RECYCLE.BIN","System Volume Information"};
  static bool Skipped(string folder){
    string name=Path.GetFileName(folder);if(name.StartsWith(".",StringComparison.Ordinal))return true;
    foreach(string known in SkippedFolders)if(String.Equals(name,known,StringComparison.OrdinalIgnoreCase))return true;
    try{return (File.GetAttributes(folder)&(FileAttributes.Hidden|FileAttributes.System))!=0;}catch{return true;}
  }
  static bool Linked(string path){try{return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;}catch{return true;}}
  // 1: a linked folder. 0: an ordinary folder. -1: it cannot be told (no access, or the folder went away).
  static int LinkState(string path){try{return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0?1:0;}catch{return -1;}}
  // Files that say nothing about what a folder holds: the notes Windows and media tools leave, and picture formats that are not shown.
  static readonly string[] IgnoredNames={"thumbs.db","desktop.ini",".ds_store"};
  static readonly string[] IgnoredExtensions={".ini",".db",".txt",".nfo",".url",".srt",".sub",".idx",".xmp",".heic",".heif",".cr2",".cr3",".nef",".arw",".dng",".tif",".tiff",".webp",".psd"};
  static bool Ignored(string file){string name=Path.GetFileName(file).ToLowerInvariant();if(Array.IndexOf(IgnoredNames,name)>=0)return true;return Array.IndexOf(IgnoredExtensions,Path.GetExtension(name))>=0;}
  // discGame: 1 or 2 when the disc image holds that game, 0 for another disc, -1 when the image cannot be read.
  // programGame: 1 or 2 when the file is a known program of that game, else 0.
  // gamesOnly: only disc images and game programs are looked for; movies, shows and albums are not listed.
  // The developer chose this on 2026-11-18: the launcher passes true.
  public static MediaScan Scan(string folder,Func<string,int> discGame,Func<string,int> programGame,bool gamesOnly=false){
    var result=new MediaScan();
    if(!IsLocalFullPath(folder)){result.Problem="Choose a folder on a drive of this computer.";return result;}
    string root;try{root=Clean(folder);}catch{result.Problem="Choose a folder on a drive of this computer.";return result;}
    result.Root=root;
    try{if(!Directory.Exists(root)){result.Problem="The folder was not found. It may be on a drive that is not connected.";return result;}}
    catch{result.Problem="The folder could not be read.";return result;}
    try{if(new DriveInfo(Path.GetPathRoot(root)).DriveType==DriveType.Network){result.Problem="Choose a folder on a drive of this computer. A network drive is not searched.";return result;}}catch{}
    if(Path.GetPathRoot(root).TrimEnd('\\')!=root.TrimEnd('\\')&&Linked(root)){result.Problem="This is a linked folder. Choose the folder it points to.";return result;}
    var dirs=new SortedDictionary<string,Dir>(StringComparer.OrdinalIgnoreCase);var programs=new List<string>();
    var waiting=new Queue<KeyValuePair<string,int>>();waiting.Enqueue(new KeyValuePair<string,int>(root,0));
    bool first=true;
    while(waiting.Count>0&&!result.Truncated){
      var here=waiting.Dequeue();var dir=new Dir();
      try{
        // Read one entry at a time, so the entry limit also bounds the reading.
        foreach(string file in Directory.EnumerateFiles(here.Key)){
          if(++result.Entries>MaxEntries){result.Truncated=true;break;}
          if(file.Length>MaxPath)continue;
          string ext=Path.GetExtension(file).ToLowerInvariant();
          if(ext==".cue")dir.Cues.Add(file);
          else if(IsVideo(file)){if(!IsSample(file))dir.Videos.Add(file);}
          else if(IsPicture(file))dir.Pictures.Add(file);
          else if(ext==".exe"&&ProgramName.IsMatch(Path.GetFileName(file)))programs.Add(file);
          else if(!Ignored(file))dir.Other++;
        }
        if(!result.Truncated)foreach(string sub in Directory.EnumerateDirectories(here.Key)){
          if(++result.Entries>MaxEntries){result.Truncated=true;break;}
          int link=LinkState(sub);if(link<0){result.Unread=true;continue;}
          if(link>0||Skipped(sub))continue;
          if(here.Value>=MaxDepth){result.Deeper=true;break;}
          waiting.Enqueue(new KeyValuePair<string,int>(sub,here.Value+1));
        }
      }
      catch(UnauthorizedAccessException){if(first){result.Problem="The folder could not be read.";return result;}result.Unread=true;}
      catch(IOException){if(first){result.Problem="The folder could not be read.";return result;}result.Unread=true;}
      first=false;
      if(dir.Videos.Count+dir.Pictures.Count+dir.Cues.Count>0){dir.Videos.Sort(StringComparer.OrdinalIgnoreCase);dir.Pictures.Sort(StringComparer.OrdinalIgnoreCase);dir.Cues.Sort(StringComparer.OrdinalIgnoreCase);dirs[here.Key]=dir;}
    }
    // game programs: only a file the caller knows
    programs.Sort(StringComparer.OrdinalIgnoreCase);int asked=0;
    foreach(string program in programs){if(asked>=MaxPrograms){result.Truncated=true;break;}asked++;int game=0;try{game=programGame==null?0:programGame(program);}catch{game=0;}if(game>=1&&game<=MediaScan.Games)result.Programs[game-1].Add(program);}
    var shows=new SortedDictionary<string,string>(StringComparer.OrdinalIgnoreCase); // show folder -> cover
    int cues=0;
    foreach(var pair in dirs){
      string where=pair.Key;Dir dir=pair.Value;
      string dirCover=DirCover(dir);
      // disc images
      var plain=new List<string>();
      foreach(string cue in dir.Cues){
        if(cues>=MaxCues){result.Truncated=true;break;}cues++;
        int game=-1;try{game=discGame==null?0:discGame(cue);}catch{game=-1;}
        if(game>=1&&game<=MediaScan.Games)result.Discs[game-1].Add(cue);else if(game==0)plain.Add(cue);
      }
      foreach(string cue in plain)Add(result,Make("game",Path.GetFileNameWithoutExtension(cue),cue,NamedCover(dir,cue)??(plain.Count==1&&dir.Videos.Count==0?dirCover:null),root));
      // videos: an episode goes to its show, any other video is a movie
      if(gamesOnly)continue;
      // A folder is a show folder when at least two of its videos, and at least half of them, have episode names, or when it is a season folder.
      var movies=new List<string>();int episodes=0;foreach(string video in dir.Videos)if(IsEpisodeName(video))episodes++;
      string show=(episodes>=2&&episodes*2>=dir.Videos.Count)||(episodes>=1&&SeasonFolder.IsMatch(Path.GetFileName(where)))?ShowFolder(where,root):null;
      foreach(string video in dir.Videos){
        if(show==null||!IsEpisodeName(video))movies.Add(video);else if(!shows.ContainsKey(show))shows[show]=null;
      }
      foreach(string video in movies)Add(result,Make("movie",Path.GetFileNameWithoutExtension(video),video,NamedCover(dir,video)??(movies.Count==1&&dir.Cues.Count==0?dirCover:null),root));
      // pictures alone in a folder are an album: at least two, and not a folder that mostly holds other files
      // A few videos among many pictures (a camera folder) do not stop it.
      if(dir.Cues.Count==0&&dir.Pictures.Count>=2&&(dir.Videos.Count==0||dir.Pictures.Count>=8*dir.Videos.Count+8)&&dir.Other<=dir.Pictures.Count/4)Add(result,Make("art",Path.GetFileName(where),where,dir.Pictures[0],root));
    }
    foreach(string show in shows.Keys.ToList()){
      Dir own;string cover=dirs.TryGetValue(show,out own)?DirCover(own):null;
      Add(result,Make("show",Path.GetFileName(show),show,cover,root));
    }
    // An album inside a show folder is that show's extra pictures, not an album of its own.
    result.Items.RemoveAll(item=>item.Kind=="art"&&shows.Keys.Any(show=>Inside(show,item.Path)));
    return result;
  }
  static void Add(MediaScan result,LibraryItem item){
    if(item==null)return;
    if(result.Items.Count>=MaxFound){result.Truncated=true;return;}
    if(!result.Items.Any(known=>LibraryCore.Same(known,item)))result.Items.Add(item);
  }
  static string DirCover(Dir dir){foreach(string name in CoverNames)foreach(string picture in dir.Pictures)if(String.Equals(Path.GetFileNameWithoutExtension(picture),name,StringComparison.OrdinalIgnoreCase))return picture;return null;}
  static string NamedCover(Dir dir,string file){
    string name=Path.GetFileNameWithoutExtension(file);
    foreach(string picture in dir.Pictures){string p=Path.GetFileNameWithoutExtension(picture);if(String.Equals(p,name,StringComparison.OrdinalIgnoreCase)||String.Equals(p,name+"-poster",StringComparison.OrdinalIgnoreCase)||String.Equals(p,name+"-cover",StringComparison.OrdinalIgnoreCase))return picture;}
    return null;
  }
  // The folder that stands for the show of an episode in "where": that folder, or its parent when it is a season folder.
  // The library folder itself is never a show: an episode directly in it is listed as a movie.
  static string ShowFolder(string where,string root){
    string show=where;
    if(SeasonFolder.IsMatch(Path.GetFileName(where))){string parent=Path.GetDirectoryName(where);if(parent!=null&&!SameFolder(parent,root)&&Inside(root,parent))show=parent;}
    return SameFolder(show,root)?null:show;
  }

  // ---- after a search: what the list of found items becomes ----
  // A search with a problem changes nothing. A complete search replaces the items of its folder. A search that could not
  // finish only adds: it has not seen everything, so it cannot say that an item is gone.
  public static List<LibraryItem> Merge(IList<LibraryItem> found,MediaScan scan){
    var next=new List<LibraryItem>(found);
    if(scan==null||scan.Problem.Length>0||scan.Root.Length==0)return next;
    if(scan.Complete)next.RemoveAll(item=>SameFolder(item.Root,scan.Root));
    foreach(var item in scan.Items){if(next.Count>=MaxFound)break;if(!next.Any(known=>LibraryCore.Same(known,item)))next.Add(item);}
    return next;
  }
  public static List<LibraryItem> WithoutFolder(IList<LibraryItem> found,string folder){var next=new List<LibraryItem>(found);next.RemoveAll(item=>SameFolder(item.Root,folder));return next;}
  // The found items that are shown: not one the user already added by hand (the same file).
  public static List<LibraryItem> Shown(IList<LibraryItem> user,IList<LibraryItem> found){
    var shown=new List<LibraryItem>();
    foreach(var item in found){bool known=false;foreach(var mine in user)if(mine.Path.Length>0&&String.Equals(mine.Path,item.Path,StringComparison.OrdinalIgnoreCase)){known=true;break;}if(!known)shown.Add(item);}
    return shown;
  }
  public static bool Present(LibraryItem item){
    try{if(item==null||!item.Found)return false;return item.Kind=="show"||item.Kind=="art"?Directory.Exists(item.Path):File.Exists(item.Path);}catch{return false;}
  }

  // ---- what an item holds, read when it is shown ----
  // The episodes of a show: the videos in its folder and in the folders directly inside it, in name order.
  public static List<string> Episodes(string showFolder){
    var list=new List<string>();
    try{
      if(!IsLocalFullPath(showFolder)||!Directory.Exists(showFolder))return list;
      var folders=new List<string>{showFolder};
      foreach(string sub in Directory.EnumerateDirectories(showFolder)){if(folders.Count>60)break;if(!Linked(sub))folders.Add(sub);}
      folders.Sort(1,folders.Count-1,StringComparer.OrdinalIgnoreCase);
      foreach(string folder in folders){
        var here=new List<string>();
        try{foreach(string file in Directory.EnumerateFiles(folder)){if(here.Count>2000)break;if(IsVideo(file)&&!IsSample(file)&&LibraryCore.IsMovieFile(file))here.Add(file);}}catch(UnauthorizedAccessException){}catch(IOException){}
        here.Sort(StringComparer.OrdinalIgnoreCase);
        foreach(string file in here){if(list.Count>=MaxListed)return list;list.Add(file);}
      }
    }catch{}
    return list;
  }
  public static List<string> Pictures(string albumFolder){
    var list=new List<string>();
    try{
      if(!IsLocalFullPath(albumFolder)||!Directory.Exists(albumFolder))return list;
      foreach(string file in Directory.EnumerateFiles(albumFolder)){if(list.Count>4000)break;if(IsPicture(file)&&file.Length<=MaxPath)list.Add(file);}
      list.Sort(StringComparer.OrdinalIgnoreCase);if(list.Count>MaxListed)list.RemoveRange(MaxListed,list.Count-MaxListed);
    }catch{}
    return list;
  }
  // One sentence about a search, for the screen.
  public static string Describe(MediaScan scan,bool gamesOnly=false){
    if(scan==null)return "";
    if(gamesOnly&&scan.Problem.Length==0){
      int n=scan.Items.Count(i=>i.Kind=="game")+scan.Discs.Count(d=>d.Count>0);
      string only=n==0?"No game was found in this folder.":"Found "+Count(n,"game")+".";
      if(scan.Unread)only+=" Some folders inside could not be read, so nothing was taken out of the Library.";
      if(scan.Truncated)only+=" The folder is very large, so the search stopped early; choose a smaller folder to find the rest.";else if(scan.Deeper)only+=" Folders more than "+MaxDepth+" levels down were not searched.";
      return only;
    }
    if(scan.Problem.Length>0)return scan.Problem;
    int games=scan.Items.Count(i=>i.Kind=="game")+scan.Discs.Count(d=>d.Count>0),movies=scan.Items.Count(i=>i.Kind=="movie"),shows=scan.Items.Count(i=>i.Kind=="show"),art=scan.Items.Count(i=>i.Kind=="art");
    string text=games+movies+shows+art==0?"Nothing was found in this folder.":"Found "+Count(games,"game")+", "+Count(movies,"movie")+", "+Count(shows,"show")+" and "+Count(art,"art album")+".";
    if(scan.Unread)text+=" Some folders inside could not be read, so nothing was taken out of the Library.";
    if(scan.Truncated)text+=" The folder is very large, so the search stopped early; choose a smaller folder to find the rest.";
    else if(scan.Deeper)text+=" Folders more than "+MaxDepth+" levels down were not searched.";
    return text;
  }
  static string Count(int n,string word){return n+" "+word+(n==1?"":"s");}
}
