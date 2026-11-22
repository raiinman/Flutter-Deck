using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// The Library list (SPEC-library.md). The Library holds any number of items of several kinds, not a fixed pair of games.
//  - Two items are built in: Mega Man Legends and Mega Man Legends 2. They select the launcher's game 1 or 2,
//    and the two built-in games have a Play path (SPEC-play.md). Nothing in this file starts a game or a program.
//  - The user adds the other items. They are kept in library.txt in the launcher's settings folder, never in the program.
//  - A movie is a video file the user picked. Open (LibraryOpen.cs) hands that file to the Windows program for its type.
//    Only a file with a video extension can be opened, whatever library.txt says.
//  - A game or an "other" item that the user adds is listed only: it has a title and no file.
public sealed class LibraryItem {
  public string Kind="other",Title="",Path="";
  // An item found in a library folder (MediaFolders.cs): it is kept in library-found.txt, never in library.txt. Cover is a picture beside it; Root is its library folder.
  public bool Found=false; public string Cover="",Root="";
  public int BuiltIn=0; // 1, 2 or 3 for the built-in games, 0 for an item the user added
}
public static class LibraryCore {
  public static readonly string[] Kinds={"game","movie","other"};
  public static readonly string[] KindNames={"Game","Movie","Other"};
  public static readonly string[] KindPlurals={"Games","Movies","Other"};
  public static readonly string[] MovieExtensions={".mp4",".mkv",".avi",".mov",".wmv",".webm",".m4v",".mpg",".mpeg"};
  public const int MaxItems=500,MaxTitle=120,MaxPath=520,MaxFileBytes=512*1024;
  // For tests only: a full path to a .txt file in this variable is used instead of the real library file.
  public const string OverrideVariable="FLUTTERDECK_LIBRARY_FILE";
  public static string PathName {get{
    if(DeckPaths.IsPortable)return Path.Combine(DeckPaths.Root,"library.txt");
    try{string other=Environment.GetEnvironmentVariable(OverrideVariable);if(IsLocalFullPath(other)&&other.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(other);}catch{}
    return Path.Combine(DeckPaths.Root,"library.txt");}}
  // A drive letter, a colon and a backslash: not a path relative to a drive or to the current folder, and not a network path.
  public static bool IsLocalFullPath(string path){return !String.IsNullOrWhiteSpace(path)&&path.Length>3&&((path[0]>='A'&&path[0]<='Z')||(path[0]>='a'&&path[0]<='z'))&&path[1]==':'&&path[2]=='\\'&&path.IndexOfAny(Path.GetInvalidPathChars())<0;}
  public static int KindIndex(string kind){return Array.IndexOf(Kinds,kind);}
  public static string KindName(string kind){if(kind=="show")return "Show";if(kind=="art")return "Art";int i=KindIndex(kind);return i<0?"Other":KindNames[i];}
  public static string CleanTitle(string title){
    if(title==null)return "";var clean=new StringBuilder();
    foreach(char ch in title)if(!Char.IsControl(ch)&&clean.Length<MaxTitle)clean.Append(ch);
    return clean.ToString().Trim();
  }
  public static bool IsMovieFile(string path){
    if(!IsLocalFullPath(path)||path.Length>MaxPath)return false;
    string ext;try{ext=Path.GetExtension(path);}catch{return false;}
    foreach(string known in MovieExtensions)if(String.Equals(ext,known,StringComparison.OrdinalIgnoreCase))return true;
    return false;
  }
  public static string TitleFromPath(string path){try{return CleanTitle(Path.GetFileNameWithoutExtension(path));}catch{return "";}}
  // Only a movie the user added can be opened, and only when its file is a video file.
  public static bool CanOpen(LibraryItem item){return item!=null&&item.BuiltIn==0&&item.Kind=="movie"&&IsMovieFile(item.Path);}
  public static List<LibraryItem> BuiltIns(){
    return new List<LibraryItem>{new LibraryItem{Kind="game",Title="Mega Man Legends",BuiltIn=1},new LibraryItem{Kind="game",Title="Mega Man Legends 2",BuiltIn=2},new LibraryItem{Kind="game",Title="The Misadventures of Tron Bonne",BuiltIn=3}};
  }
  public static List<LibraryItem> All(IList<LibraryItem> user){var all=BuiltIns();if(user!=null)all.AddRange(user);return all;}
  // kind null or empty: every item.
  public static List<LibraryItem> Filter(IList<LibraryItem> items,string kind){
    var shown=new List<LibraryItem>();foreach(var item in items)if(String.IsNullOrEmpty(kind)||item.Kind==kind)shown.Add(item);return shown;
  }
  // Search by title, without regard to capital letters. An empty search shows every item.
  public static List<LibraryItem> Search(IList<LibraryItem> items,string text){
    text=(text??"").Trim();var shown=new List<LibraryItem>();
    foreach(var item in items)if(text.Length==0||item.Title.IndexOf(text,StringComparison.OrdinalIgnoreCase)>=0)shown.Add(item);
    return shown;
  }
  // What the grid shows: one kind, the search, then the order (as added, or by title; items with the same title keep their order).
  public static List<LibraryItem> Arrange(IList<LibraryItem> items,string kind,string text,bool byTitle){
    var shown=Search(Filter(items,kind),text);
    return byTitle?shown.OrderBy(i=>i.Title,StringComparer.OrdinalIgnoreCase).ToList():shown;
  }
  // The short line under an icon's title.
  public static string Status(LibraryItem item,bool fileThere){
    if(item.BuiltIn==1)return "Game";
    if(item.BuiltIn!=0)return "Game \u00B7 features coming soon";   // owner, 2026-11-20 (version 1): not labelled "early build"
    if(item.Kind=="movie")return fileThere?"Movie":"Movie \u00B7 file missing";
    if(item.Kind=="show")return fileThere?"Show":"Show \u00B7 folder missing";
    if(item.Kind=="art")return fileThere?"Art":"Art \u00B7 folder missing";
    if(item.Found&&item.Kind=="game")return fileThere?"Game \u00B7 disc image":"Game \u00B7 file missing";
    return KindName(item.Kind)+" \u00B7 listed only";
  }
  public static int Count(IList<LibraryItem> items,string kind){return Filter(items,kind).Count;}
  // The item as it may be stored, or null with the reason. A movie needs a video file; other kinds keep no file.
  public static LibraryItem Normalize(string kind,string title,string path,out string problem){
    problem="";
    if(KindIndex(kind)<0){problem="Choose a kind.";return null;}
    string clean=CleanTitle(title);
    if(kind=="movie"){
      if(!IsMovieFile(path)){problem="Choose a video file on this computer ("+String.Join(", ",MovieExtensions)+").";return null;}
      if(clean.Length==0)clean=TitleFromPath(path);
    }else path="";
    if(clean.Length==0){problem="Type a title.";return null;}
    return new LibraryItem{Kind=kind,Title=clean,Path=path};
  }
  public static bool Same(LibraryItem a,LibraryItem b){return a.Kind==b.Kind&&String.Equals(a.Title,b.Title,StringComparison.OrdinalIgnoreCase)&&String.Equals(a.Path,b.Path,StringComparison.OrdinalIgnoreCase);}
  public static bool Add(IList<LibraryItem> user,LibraryItem item,out string problem){
    problem="";
    if(item==null){problem="Nothing to add.";return false;}
    if(user.Count>=MaxItems){problem="The library is full ("+MaxItems+" added items).";return false;}
    foreach(var known in All(user))if(Same(known,item)){problem="That item is already in the library.";return false;}
    user.Add(item);return true;
  }
  // One line for each added item: kind, title and file, with a tab between them. The first line is the format number.
  public static string Format(IList<LibraryItem> user){
    var text=new StringBuilder("1\r\n");
    foreach(var item in user){if(item.BuiltIn!=0)continue;text.Append(item.Kind).Append('\t').Append(CleanTitle(item.Title)).Append('\t').Append(item.Path??"").Append("\r\n");}
    return text.ToString();
  }
  // A line that is not a valid item is left out; the other lines are kept.
  public static List<LibraryItem> Parse(string text){
    var user=new List<LibraryItem>();if(text==null)return user;
    string[] lines=text.Replace("\r\n","\n").Split('\n');if(lines.Length==0||lines[0].Trim()!="1")return user;
    for(int i=1;i<lines.Length&&user.Count<MaxItems;i++){
      string[] cell=lines[i].Split('\t');if(cell.Length!=3)continue;
      string problem;var item=Normalize(cell[0],cell[1],cell[2],out problem);if(item==null)continue;
      Add(user,item,out problem);
    }
    return user;
  }
  // A missing or empty file is an empty library. A file that is too large or is not format 1 is an error: the caller must then
  // refuse to save, so that a file this build cannot read is never replaced.
  public static List<LibraryItem> Load(string path){
    if(!File.Exists(path))return new List<LibraryItem>();
    if(new FileInfo(path).Length>MaxFileBytes)throw new InvalidDataException("The library file is too large.");
    string text=File.ReadAllText(path,Encoding.UTF8);if(text.Trim().Length==0)return new List<LibraryItem>();
    if(text.Replace("\r\n","\n").Split('\n')[0].Trim()!="1")throw new InvalidDataException("The library file has an unknown format.");
    return Parse(text);
  }
  public static void Save(string path,IList<LibraryItem> user){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{byte[] bytes=new UTF8Encoding(false).GetBytes(Format(user));if(bytes.Length>MaxFileBytes)throw new InvalidDataException("The library is too large to save.");File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
}
