using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

// Teleport (contract 2.14). The game program can be asked, while it runs, to send Mega Man to a stage and room: it reads a one-line request
// file ("stage room x y z facing") and does what the game itself does after a door. This unit reads the list of places (made from the
// player's own disc by the game's own tools, one "dest" line per place), and writes the request file. It never starts or touches the game.
public sealed class TeleportPlace { public int Stage,Room,X,Y,Z,Facing; public string From=""; }

public static class TeleportCore {
  // Names are those of the game program's Teleport panel (rtc_telemenu.c, stage_name); each was matched to the game's own play, to the game's island map or to a picture of the stage.
  // Stage 8 is Uptown by elimination (hospital, door to the museum, harbour link; stage 29 is the town at night with rooms from stages 25, 5 and 8).
  static readonly Dictionary<int,string> Names=new Dictionary<int,string>{
    {0,"Opening ruin"},{3,"Cardon Forest and the Flutter"},{4,"Apple Market"},{5,"Downtown"},{6,"City Hall area"},{8,"Uptown"},{9,"Underground ruins"},{10,"Clozer Woods pit"},{12,"Cardon Forest, south"},{13,"Lake Jyun"},
    {14,"Cardon Forest Sub-Gate"},{15,"City Hall offices"},{16,"Yass Plains"},{17,"Clozer Woods"},{18,"Main Gate field"},{19,"Clozer Woods Sub-Gate"},{20,"Lake Jyun Sub-Gate"},{25,"Old City"},
    {26,"Main Gate ruins"},{27,"Cardon Forest (later)"},{28,"Museum"},{29,"Town at night"}};
  public static string StageName(int stage){string n;return Names.TryGetValue(stage,out n)?n:"Stage "+stage.ToString(CultureInfo.InvariantCulture);}

  // A line is "dest <stage> <room> <x> <y> <z> <facing>" and may carry text after; anything else is skipped. A place outside the ranges the
  // game program accepts (stage 0 to 30, room 0 to 29, numbers that fit a 16-bit value, facing 0 to 4095) is skipped too.
  public static bool TryParseLine(string line,out TeleportPlace p){
    p=null;if(line==null)return false;string[] w=line.Trim().Split(new[]{' ','\t'},StringSplitOptions.RemoveEmptyEntries);
    if(w.Length<7||w[0]!="dest")return false;int[] v=new int[6];
    for(int i=0;i<6;i++){if(!Int32.TryParse(w[i+1],NumberStyles.AllowLeadingSign,CultureInfo.InvariantCulture,out v[i]))return false;}
    if(v[0]<0||v[0]>30||v[0]==1||v[1]<0||v[1]>29||v[2]<-32768||v[2]>32767||v[3]<-32768||v[3]>32767||v[4]<-32768||v[4]>32767||v[5]<0||v[5]>4095)return false;
    p=new TeleportPlace{Stage=v[0],Room=v[1],X=v[2],Y=v[3],Z=v[4],Facing=v[5]};
    for(int i=7;i<w.Length;i++)if(w[i]=="from"&&i+1<w.Length){p.From=w[i+1];break;}
    return true;
  }
  public static List<TeleportPlace> Load(string path){
    var list=new List<TeleportPlace>();if(path==null||!File.Exists(path))return list;
    try{foreach(string line in File.ReadAllLines(path)){TeleportPlace p;if(TryParseLine(line,out p))list.Add(p);}}catch(Exception){list.Clear();}
    return list;
  }
  public static List<int> Stages(List<TeleportPlace> all){var s=new SortedDictionary<int,bool>();foreach(var p in all)s[p.Stage]=true;return new List<int>(s.Keys);}
  public static List<int> Rooms(List<TeleportPlace> all,int stage){var s=new SortedDictionary<int,bool>();foreach(var p in all)if(p.Stage==stage)s[p.Room]=true;return new List<int>(s.Keys);}
  public static List<TeleportPlace> Entries(List<TeleportPlace> all,int stage,int room){var l=new List<TeleportPlace>();foreach(var p in all)if(p.Stage==stage&&p.Room==room)l.Add(p);return l;}
  public static string RequestLine(TeleportPlace p){return String.Format(CultureInfo.InvariantCulture,"{0} {1} {2} {3} {4} {5}",p.Stage,p.Room,p.X,p.Y,p.Z,p.Facing);}
  // Writes the request atomically (a temporary file, then a move), so the game program never reads half a line. Returns "" or the reason it failed.
  public static string WriteRequest(string path,TeleportPlace p){
    if(String.IsNullOrEmpty(path)||p==null)return "no place chosen";
    try{string dir=Path.GetDirectoryName(path);if(!String.IsNullOrEmpty(dir))Directory.CreateDirectory(dir);
      if(File.Exists(path))return "an earlier request has not been picked up yet";
      string tmp=path+".tmp";File.WriteAllText(tmp,RequestLine(p)+"\n",new UTF8Encoding(false));File.Move(tmp,path);return "";}
    catch(Exception e){return e.Message;}
  }
  public static string RoomText(int room){return "Room "+room.ToString(CultureInfo.InvariantCulture);}
  public static string EntryText(TeleportPlace p,int index){
    string text="Entry "+(index+1).ToString(CultureInfo.InvariantCulture);int from;
    if(p.From.Length>0&&Int32.TryParse(p.From.Split(',')[0],NumberStyles.None,CultureInfo.InvariantCulture,out from))text+=" (from "+StageName(from)+")";
    return text;}
}
