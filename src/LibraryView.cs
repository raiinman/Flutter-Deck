using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
// How the user looks at the Library (SPEC-library.md): favourites, hidden items, the order, the view, and when an item was last opened.
//  - This is kept in library-view.txt, apart from library.txt. The list of items and its file format do not change.
//  - Nothing in this file starts a game or a program, and nothing here reaches the network.
//  - An entry is tied to an item by a key made from the item itself, so it survives a change of the order of the list.
public sealed class LibraryViewState {
  public string Sort="added",View="grid";
  public bool ShowHidden; // for this run of the launcher only; not saved
  public readonly HashSet<string> Favorites=new HashSet<string>(StringComparer.OrdinalIgnoreCase),Hidden=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
  public readonly Dictionary<string,long> Opened=new Dictionary<string,long>(StringComparer.OrdinalIgnoreCase);   // UTC ticks of the last time
  public readonly Dictionary<string,int> Times=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
}
public static class LibraryView {
  public static readonly string[] Sorts={"added","titleAZ","titleZA","recent"};
  public static readonly string[] SortNames={"As added","Alphabetical A-Z","Alphabetical Z-A","Recently opened"};
  public static readonly string[] Views={"grid","details"};
  public static readonly string[] Tabs={"game","movie","other","fav"};
  public static readonly string[] TabNames={"Games","Movies","Other","Favorites"};
  public const int MaxEntries=600,MaxFileBytes=2*1024*1024,MaxKey=700; // 600 entries of each sort at the longest key stay under the file limit
  // For tests only: a full path to a .txt file in this variable is used instead of the real file. A portable run ignores it.
  public const string OverrideVariable="FLUTTERDECK_LIBRARY_VIEW_FILE";
  public static string PathName {get{
    if(DeckPaths.IsPortable)return Path.Combine(DeckPaths.Root,"library-view.txt");
    try{string other=Environment.GetEnvironmentVariable(OverrideVariable);if(LibraryCore.IsLocalFullPath(other)&&other.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(other);}catch{}
    return Path.Combine(DeckPaths.Root,"library-view.txt");}}
  public static string Key(LibraryItem item){return item.BuiltIn!=0?"builtin:"+item.BuiltIn:item.Kind+"|"+item.Title+"|"+(item.Path??"");}
  static bool GoodKey(string key){if(String.IsNullOrEmpty(key)||key.Length>MaxKey)return false;foreach(char ch in key)if(Char.IsControl(ch))return false;return true;}
  public static bool IsFavorite(LibraryViewState s,LibraryItem item){return s.Favorites.Contains(Key(item));}
  public static bool IsHidden(LibraryViewState s,LibraryItem item){return s.Hidden.Contains(Key(item));}
  public static void SetFavorite(LibraryViewState s,LibraryItem item,bool on){if(on){if(s.Favorites.Count<MaxEntries)s.Favorites.Add(Key(item));}else s.Favorites.Remove(Key(item));}
  public static void SetHidden(LibraryViewState s,LibraryItem item,bool on){if(on){if(s.Hidden.Count<MaxEntries)s.Hidden.Add(Key(item));}else s.Hidden.Remove(Key(item));}
  public static void MarkOpened(LibraryViewState s,LibraryItem item,DateTime utc){
    string key=Key(item);if(!s.Opened.ContainsKey(key)&&s.Opened.Count>=MaxEntries)return;
    s.Opened[key]=utc.Ticks;int n;s.Times.TryGetValue(key,out n);s.Times[key]=n<int.MaxValue?n+1:n;
  }
  // The item's entries go away with the item.
  public static void Forget(LibraryViewState s,LibraryItem item){string key=Key(item);s.Favorites.Remove(key);s.Hidden.Remove(key);s.Opened.Remove(key);s.Times.Remove(key);}
  public static long LastOpened(LibraryViewState s,LibraryItem item){long t;return s.Opened.TryGetValue(Key(item),out t)?t:0;}
  public static int TimesOpened(LibraryViewState s,LibraryItem item){int n;return s.Times.TryGetValue(Key(item),out n)?n:0;}
  public static bool InTab(LibraryViewState s,LibraryItem item,string tab){return tab=="fav"?IsFavorite(s,item):tab=="movie"?(item.Kind=="movie"||item.Kind=="show"):item.Kind==tab;}
  // What a tab shows: its items, without the hidden ones unless they are asked for, then the search, then the order.
  public static List<LibraryItem> Arrange(IList<LibraryItem> items,LibraryViewState s,string tab,string search){
    var shown=new List<LibraryItem>();
    foreach(var item in items)if(InTab(s,item,tab)&&(s.ShowHidden||!IsHidden(s,item)))shown.Add(item);
    shown=LibraryCore.Search(shown,search);
    if(s.Sort=="titleAZ")return shown.OrderBy(i=>i.Title,StringComparer.OrdinalIgnoreCase).ToList();
    if(s.Sort=="titleZA")return shown.OrderByDescending(i=>i.Title,StringComparer.OrdinalIgnoreCase).ToList();
    if(s.Sort=="recent")return shown.OrderByDescending(i=>LastOpened(s,i)).ToList(); // items never opened keep their order, after the others
    return shown;
  }
  // The number a tab shows beside its name: its items that are not hidden.
  public static int Count(IList<LibraryItem> items,LibraryViewState s,string tab){int n=0;foreach(var item in items)if(InTab(s,item,tab)&&!IsHidden(s,item))n++;return n;}
  public static int HiddenCount(IList<LibraryItem> items,LibraryViewState s,string tab){int n=0;foreach(var item in items)if(InTab(s,item,tab)&&IsHidden(s,item))n++;return n;}
  public static string WhenText(long utcTicks,DateTime nowUtc){
    if(utcTicks<630822816000000000L)return "Never"; // before the year 2000: not a time this launcher wrote
    DateTime then;try{then=new DateTime(utcTicks,DateTimeKind.Utc);}catch{return "Never";}
    int days=(int)Math.Floor((nowUtc.ToLocalTime().Date-then.ToLocalTime().Date).TotalDays);
    if(days<0)return then.ToLocalTime().ToString("d MMM yyyy");if(days==0)return "Today";if(days==1)return "Yesterday";if(days<30)return days+" days ago";
    return then.ToLocalTime().ToString("d MMM yyyy");
  }
  // The format number, then one entry on each line. A line that is not understood is left out.
  public static string Format(LibraryViewState s){
    var text=new StringBuilder("1\r\n");
    text.Append("sort=").Append(Array.IndexOf(Sorts,s.Sort)<0?Sorts[0]:s.Sort).Append("\r\n").Append("view=").Append(Array.IndexOf(Views,s.View)<0?Views[0]:s.View).Append("\r\n");
    foreach(string key in s.Favorites.OrderBy(k=>k,StringComparer.OrdinalIgnoreCase))text.Append("fav\t").Append(key).Append("\r\n");
    foreach(string key in s.Hidden.OrderBy(k=>k,StringComparer.OrdinalIgnoreCase))text.Append("hide\t").Append(key).Append("\r\n");
    foreach(string key in s.Opened.Keys.OrderBy(k=>k,StringComparer.OrdinalIgnoreCase)){int n;s.Times.TryGetValue(key,out n);text.Append("open\t").Append(s.Opened[key]).Append('\t').Append(Math.Max(1,n)).Append('\t').Append(key).Append("\r\n");}
    return text.ToString();
  }
  public static LibraryViewState Parse(string text){
    var s=new LibraryViewState();if(text==null)return s;
    string[] lines=text.Replace("\r\n","\n").Split('\n');if(lines.Length==0||lines[0].Trim()!="1")return s;
    for(int i=1;i<lines.Length;i++){
      string line=lines[i];
      if(line.StartsWith("sort=",StringComparison.Ordinal)){string v=line.Substring(5).Trim();if(Array.IndexOf(Sorts,v)>=0)s.Sort=v;continue;}
      if(line.StartsWith("view=",StringComparison.Ordinal)){string v=line.Substring(5).Trim();if(Array.IndexOf(Views,v)>=0)s.View=v;continue;}
      if(line.StartsWith("fav\t",StringComparison.Ordinal)){string key=line.Substring(4);if(GoodKey(key)&&s.Favorites.Count<MaxEntries)s.Favorites.Add(key);continue;}
      if(line.StartsWith("hide\t",StringComparison.Ordinal)){string key=line.Substring(5);if(GoodKey(key)&&s.Hidden.Count<MaxEntries)s.Hidden.Add(key);continue;}
      if(line.StartsWith("open\t",StringComparison.Ordinal)){
        string[] cell=line.Split(new[]{'\t'},4);long ticks;int n;
        if(cell.Length==4&&long.TryParse(cell[1],out ticks)&&int.TryParse(cell[2],out n)&&ticks>0&&ticks<=DateTime.MaxValue.Ticks&&n>0&&GoodKey(cell[3])&&s.Opened.Count<MaxEntries){s.Opened[cell[3]]=ticks;s.Times[cell[3]]=n;}
      }
    }
    return s;
  }
  // A missing or empty file is the default view. A file that is too large or is not format 1 is an error: the caller then does not save,
  // so that a file this build cannot read is never replaced.
  public static LibraryViewState Load(string path){
    if(!File.Exists(path))return new LibraryViewState();
    if(new FileInfo(path).Length>MaxFileBytes)throw new InvalidDataException("The library view file is too large.");
    string text=File.ReadAllText(path,Encoding.UTF8);if(text.Trim().Length==0)return new LibraryViewState();
    if(text.Replace("\r\n","\n").Split('\n')[0].Trim()!="1")throw new InvalidDataException("The library view file has an unknown format.");
    return Parse(text);
  }
  public static void Save(string path,LibraryViewState s){
    Directory.CreateDirectory(Path.GetDirectoryName(path));string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{byte[] bytes=new UTF8Encoding(false).GetBytes(Format(s));if(bytes.Length>MaxFileBytes)throw new InvalidDataException("The library view is too large to save.");File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
}
