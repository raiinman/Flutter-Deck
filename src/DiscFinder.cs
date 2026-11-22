using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

// Finds the user's own Mega Man Legends disc image in a folder the user names (SPEC-disc-folder.md).
// Rules of this module:
//  - It only reads. It writes nothing into the game folder, and it copies, hashes, uploads and starts nothing.
//  - It reads a CUE file (through LocalSetup.CheckGame, 64 KiB limit) and then at most 2 + MaxRootSectors sectors of the
//    first data track: the volume descriptor, the root directory and, for the disc code only, the first sector of SYSTEM.CNF.
//  - The search is bounded in depth, in the number of files it looks at and in the number of CUE files it opens.
//    Linked folders and linked files are skipped.
//  - The only file it writes is the launcher's own setting that holds the folder.
public sealed class DiscScan {
  public readonly List<string> Matches=new List<string>(); // CUE images whose disc holds the program, sorted
  public int Cues,Bare;        // CUE files opened; BIN, ISO or IMG files that no CUE file in the folder names
  public bool Truncated;       // a count limit was reached, so only part of the folder was searched
  public bool Deeper;          // folders below the depth limit exist and were not searched
  public bool Partial {get{return Truncated||Deeper;}}
  public string Problem="";    // not empty: the folder could not be searched at all
}
public static class DiscFinder {
  public const string OverrideVariable="FLUTTERDECK_GAMEFOLDER_FILE";
  public const int MaxDepth=3,MaxEntries=4000,MaxCues=200,MaxMatches=8,MaxPath=520,MaxRootSectors=8,MaxSettingBytes=4096;
  public const string Keep="keep",Use="use",Choose="choose",None="none";
  public static bool IsLocalFullPath(string path){
    return !String.IsNullOrWhiteSpace(path)&&path.Length>=3&&path.Length<=MaxPath&&Char.IsLetter(path[0])&&path[1]==':'&&(path[2]=='\\'||path[2]=='/')&&path.IndexOfAny(Path.GetInvalidPathChars())<0;
  }
  public static string PathName {get{
    if(DeckPaths.IsPortable)return Path.Combine(DeckPaths.Root,"game-folder.txt"); // a portable run ignores the override, like the other settings
    try{string other=Environment.GetEnvironmentVariable(OverrideVariable);if(IsLocalFullPath(other)&&other.EndsWith(".txt",StringComparison.OrdinalIgnoreCase))return Path.GetFullPath(other);}catch{}
    return Path.Combine(DeckPaths.Root,"game-folder.txt");
  }}
  // The setting is two lines: the format number 1, then the folder. A missing or empty file means no folder.
  // A file that is too large or is not format 1 is an error. A folder line that is not a full local path means no folder.
  public static string LoadFolder(string path){
    if(!File.Exists(path))return "";
    if(new FileInfo(path).Length>MaxSettingBytes)throw new InvalidDataException("The game folder setting is too large.");
    string text=File.ReadAllText(path,Encoding.UTF8);if(text.Trim().Length==0)return "";
    string[] lines=text.Replace("\r\n","\n").Split('\n');
    if(lines[0].Trim()!="1")throw new InvalidDataException("The game folder setting has an unknown format.");
    string folder=lines.Length>1?lines[1].Trim():"";
    return IsLocalFullPath(folder)?folder:"";
  }
  public static void SaveFolder(string path,string folder){
    if(folder==null)folder="";
    if(folder.Length>0&&!IsLocalFullPath(folder))throw new ArgumentException("The game folder must be a full path on a drive of this computer.");
    Directory.CreateDirectory(Path.GetDirectoryName(path));
    string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
    try{File.WriteAllText(temp,"1\r\n"+folder+"\r\n",new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}
    finally{if(File.Exists(temp))File.Delete(temp);}
  }
  static bool Linked(string path){try{return (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;}catch{return true;}}
  public static DiscScan Scan(string folder,string programName){
    var result=new DiscScan();
    if(!IsLocalFullPath(folder)){result.Problem="Choose a folder on a drive of this computer.";return result;}
    try{if(!Directory.Exists(folder)){result.Problem="The game folder was not found. It may be on a drive that is not connected.";return result;}}
    catch{result.Problem="The game folder could not be read.";return result;}
    // A mapped network drive has a drive letter too. It is refused: a search there can be very slow.
    try{if(new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))).DriveType==DriveType.Network){result.Problem="Choose a folder on a drive of this computer.";return result;}}catch{}
    if(Path.GetPathRoot(Path.GetFullPath(folder)).TrimEnd('\\')!=Path.GetFullPath(folder).TrimEnd('\\')&&Linked(folder)){result.Problem="The game folder is a linked folder. Choose the folder it points to.";return result;}
    var cues=new List<string>();var bare=new List<string>();int entries=0;
    var waiting=new Queue<KeyValuePair<string,int>>();waiting.Enqueue(new KeyValuePair<string,int>(Path.GetFullPath(folder),0));
    while(waiting.Count>0&&!result.Truncated){
      var here=waiting.Dequeue();
      try{
        // The folder is read one entry at a time, so the entry limit also bounds the reading.
        foreach(string file in Directory.EnumerateFiles(here.Key)){
          if(++entries>MaxEntries){result.Truncated=true;break;}
          string ext=Path.GetExtension(file).ToLowerInvariant();
          if(ext==".cue")cues.Add(file);else if(ext==".bin"||ext==".iso"||ext==".img")bare.Add(file);
        }
        if(result.Truncated)continue;
        foreach(string sub in Directory.EnumerateDirectories(here.Key)){
          if(++entries>MaxEntries){result.Truncated=true;break;}
          if(Linked(sub))continue;
          if(here.Value>=MaxDepth){result.Deeper=true;break;}
          waiting.Enqueue(new KeyValuePair<string,int>(sub,here.Value+1));
        }
      }catch(UnauthorizedAccessException){}catch(IOException){}
    }
    cues.Sort(StringComparer.OrdinalIgnoreCase);
    var named=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach(string cue in cues){
      if(result.Cues>=MaxCues){result.Truncated=true;break;}
      result.Cues++;
      foreach(string track in TrackFiles(cue))named.Add(track);
      if(!HasProgram(cue,programName))continue;
      if(result.Matches.Count>=MaxMatches){result.Truncated=true;break;}
      result.Matches.Add(cue);
    }
    foreach(string file in bare)if(!named.Contains(file))result.Bare++;
    return result;
  }
  // The files a CUE file names, as full paths beside it. A CUE file that cannot be read names nothing.
  static List<string> TrackFiles(string cue){
    var list=new List<string>();
    try{
      if(Linked(cue)||new FileInfo(cue).Length>65536)return list;
      string folder=Path.GetDirectoryName(Path.GetFullPath(cue));
      foreach(Match m in Regex.Matches(File.ReadAllText(cue),"^\\s*FILE\\s+(?:\"([^\"]+)\"|([^\\s]+))\\s+",RegexOptions.Multiline|RegexOptions.IgnoreCase)){
        string name=m.Groups[1].Success?m.Groups[1].Value:m.Groups[2].Value;
        if(Path.GetFileName(name)==name)list.Add(Path.Combine(folder,name));
      }
    }catch{}
    return list;
  }
  static byte[] ReadSector(FileStream stream,long sector,int size,int offset){
    long position=sector*size+offset;if(sector<0||position+2048>stream.Length)return null;
    var data=new byte[2048];stream.Position=position;int got=0;
    while(got<2048){int n=stream.Read(data,got,2048-got);if(n<=0)return null;got+=n;}
    return data;
  }
  // Looks in the root directory of the first data track of this CUE image for a file with this name.
  // Returns null when the file is not there or the image cannot be read; nothing is thrown.
  // With wantData it returns the first bytes of the file (at most one sector); without, an empty array.
  static byte[] RootFile(string cue,string wanted,bool wantData){
    try{
      if(String.IsNullOrEmpty(wanted)||Linked(cue)||LocalSetup.CheckGame(cue).Length>0)return null;
      if(!String.Equals(Path.GetExtension(cue),".cue",StringComparison.OrdinalIgnoreCase))return null;
      string text=File.ReadAllText(cue);
      var file=Regex.Match(text,"^\\s*FILE\\s+(?:\"([^\"]+)\"|([^\\s]+))\\s+(\\w+)",RegexOptions.Multiline|RegexOptions.IgnoreCase);
      var track=Regex.Match(text,"^\\s*TRACK\\s+\\d+\\s+(\\S+)",RegexOptions.Multiline|RegexOptions.IgnoreCase);
      if(!file.Success||!track.Success||track.Index<file.Index||!String.Equals(file.Groups[3].Value,"BINARY",StringComparison.OrdinalIgnoreCase))return null;
      // The sector layout comes from the first TRACK line. The user data of a sector is 2048 bytes.
      string layout=track.Groups[1].Value.ToUpperInvariant();int size,offset;
      if(layout=="MODE2/2352"){size=2352;offset=24;}else if(layout=="MODE1/2352"){size=2352;offset=16;}
      else if(layout=="MODE1/2048"){size=2048;offset=0;}else if(layout=="MODE2/2336"){size=2336;offset=8;}else return null;
      string name=file.Groups[1].Success?file.Groups[1].Value:file.Groups[2].Value;
      string image=Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cue)),name);
      using(var stream=new FileStream(image,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)){
        byte[] volume=ReadSector(stream,16,size,offset);
        if(volume==null||volume[0]!=1||Encoding.ASCII.GetString(volume,1,5)!="CD001")return null;
        long first=BitConverter.ToUInt32(volume,158),length=BitConverter.ToUInt32(volume,166);
        long sectors=Math.Min(MaxRootSectors,(length+2047)/2048);
        for(long i=0;i<sectors;i++){
          byte[] data=ReadSector(stream,first+i,size,offset);if(data==null)return null;
          int at=0;
          while(at<2048){
            int record=data[at];if(record==0)break;
            if(record<34||at+record>2048)break;
            int nameLength=data[at+32];
            if(33+nameLength<=record&&(data[at+25]&2)==0){
              string found=Encoding.ASCII.GetString(data,at+33,nameLength);int version=found.IndexOf(';');if(version>=0)found=found.Substring(0,version);
              if(String.Equals(found,wanted,StringComparison.OrdinalIgnoreCase)){
                if(!wantData)return new byte[0];
                long where=BitConverter.ToUInt32(data,at+2),bytes=Math.Min(2048,BitConverter.ToUInt32(data,at+10));
                byte[] sector=ReadSector(stream,where,size,offset);if(sector==null)return null;
                var part=new byte[bytes];Array.Copy(sector,part,(int)bytes);return part;
              }
            }
            at+=record;
          }
        }
      }
    }catch{}
    return null;
  }
  // True when the first track of this CUE image is a data track whose root directory holds a file with this name.
  // Any image this cannot read is simply not a match.
  public static bool HasProgram(string cue,string programName){return RootFile(cue,programName,false)!=null;}
  // The disc code of a PlayStation disc image, for example "SLUS-00603", or "" when it cannot be read.
  // It comes from the BOOT line of the disc's SYSTEM.CNF file (one more sector is read for it).
  public static string DiscCode(string cue){byte[] data=RootFile(cue,"SYSTEM.CNF",true);return data==null?"":CodeFromSystemCnf(Encoding.ASCII.GetString(data));}
  public static string CodeFromSystemCnf(string text){
    if(text==null)return "";
    var boot=Regex.Match(text,"^\\s*BOOT\\d?\\s*=\\s*cdrom\\d*:\\\\*([^;\\r\\n]*)",RegexOptions.Multiline|RegexOptions.IgnoreCase);if(!boot.Success)return "";
    string name=boot.Groups[1].Value;int slash=Math.Max(name.LastIndexOf('\\'),name.LastIndexOf('/'));if(slash>=0)name=name.Substring(slash+1);
    var code=Regex.Match(name.Trim(),"\\A([A-Za-z]{4})[_-]([0-9]{3})\\.([0-9]{2})\\z");
    return code.Success?code.Groups[1].Value.ToUpperInvariant()+"-"+code.Groups[2].Value+code.Groups[3].Value:"";
  }
  // What to do with what was found. A saved disc image that still works is never replaced without the user's own choice.
  // After a partial search there may be images that were not seen, so nothing is set without the user's own choice.
  public static string Decide(bool savedWorks,IList<string> matches,bool partial){
    if(savedWorks)return Keep;
    if(matches==null||matches.Count==0)return None;
    return matches.Count==1&&!partial?Use:Choose;
  }
  static string Count(int n,string one,string many){return n+" "+(n==1?one:many);}
  public static string Describe(DiscScan scan,string programName){
    if(scan.Problem.Length>0)return scan.Problem;
    var text=new StringBuilder();
    if(scan.Matches.Count==0){
      text.Append("No disc image of this game was found in this folder.");
      if(scan.Cues>0)text.Append(" ").Append(Count(scan.Cues,"CUE image was","CUE images were")).Append(" checked; none holds ").Append(programName).Append(".");
    }else text.Append("Found ").Append(Count(scan.Matches.Count,"disc image of this game","disc images of this game")).Append(".");
    if(scan.Bare>0)text.Append(" ").Append(Count(scan.Bare,"BIN, ISO or IMG file has","BIN, ISO or IMG files have")).Append(" no CUE file beside ").Append(scan.Bare==1?"it":"them").Append(". The game needs a CUE disc image, so Flutter Deck cannot use ").Append(scan.Bare==1?"it":"them").Append(".");
    if(scan.Truncated)text.Append(" The folder is large, so only part of it was searched.");
    if(scan.Deeper)text.Append(" Folders more than "+MaxDepth+" levels below the game folder were not searched.");
    return text.ToString();
  }
}
