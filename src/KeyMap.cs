using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// The game's keyboard map, as the game side's launcher contract (section 4) defines it: plain text, one line per
// button, "BUTTON = KEY [, KEY ...]" with up to four keys, "#" starts a comment. A map file replaces the game's
// defaults entirely, so all fourteen buttons are always written. This class only reads, edits and writes that text;
// it has no window code, so the setup screen and a later in-game overlay can share it. See SPEC-keys.md.
public sealed class KeyMap {
  public const int MaxKeys=4;
  public const string Header="# Flutter Deck key map for Mega Man Legends. One line per button: BUTTON = KEY [, KEY ...]";
  public static readonly string[] Buttons={"UP","DOWN","LEFT","RIGHT","CROSS","CIRCLE","SQUARE","TRIANGLE","L1","R1","L2","R2","START","SELECT"};
  static readonly string[] ButtonLabels={"Up","Down","Left","Right","Cross","Circle","Square","Triangle","L1","R1","L2","R2","Start","Select"};
  static readonly string[] DefaultLines={"UP = UP","DOWN = DOWN","LEFT = LEFT","RIGHT = RIGHT","CROSS = X","SQUARE = Z","TRIANGLE = S","CIRCLE = D","L1 = Q","R1 = W","L2 = A","R2 = E","START = ENTER","SELECT = RSHIFT"};
  // Every key name the contract lists, in the order the list on screen shows them.
  public static readonly string[] Names=BuildNames();
  static string[] BuildNames(){
    var n=new List<string>();
    for(char c='A';c<='Z';c++)n.Add(c.ToString());
    for(char c='0';c<='9';c++)n.Add(c.ToString());
    n.AddRange(new[]{"UP","DOWN","LEFT","RIGHT","ENTER","SPACE","BACKSPACE","ESC","LSHIFT","RSHIFT","LCTRL","RCTRL","LALT","RALT","INSERT","DELETE","HOME","END","PAGEUP","PAGEDOWN","COMMA","PERIOD","SLASH","SEMICOLON","QUOTE","LBRACKET","RBRACKET","BACKSLASH","MINUS","EQUALS"});
    for(int i=0;i<=9;i++)n.Add("NUM"+i);
    return n.ToArray();
  }
  // Windows virtual-key codes for the names whose meaning does not depend on the keyboard layout. Punctuation names
  // are left out on purpose: a pressed punctuation key is written as its code (0xNN), which the contract also allows.
  static readonly Dictionary<int,string> ByCode=BuildCodes();
  static Dictionary<int,string> BuildCodes(){
    var d=new Dictionary<int,string>();
    for(int i=0;i<26;i++)d[0x41+i]=((char)('A'+i)).ToString();
    for(int i=0;i<10;i++){d[0x30+i]=i.ToString();d[0x60+i]="NUM"+i;}
    d[0x26]="UP";d[0x28]="DOWN";d[0x25]="LEFT";d[0x27]="RIGHT";d[0x0D]="ENTER";d[0x20]="SPACE";d[0x08]="BACKSPACE";d[0x1B]="ESC";
    d[0xA0]="LSHIFT";d[0xA1]="RSHIFT";d[0xA2]="LCTRL";d[0xA3]="RCTRL";d[0xA4]="LALT";d[0xA5]="RALT";
    d[0x2D]="INSERT";d[0x2E]="DELETE";d[0x24]="HOME";d[0x23]="END";d[0x21]="PAGEUP";d[0x22]="PAGEDOWN";
    return d;
  }
  // The game keeps these for itself (contract section 4): Tab, F11 and F12. Mouse buttons, the Windows keys and
  // the side-less Shift, Ctrl and Alt codes are not usable as game keys either.
  static bool Refused(int code){return code<=0x07||code>0xFE||code==0x09||code==0x7A||code==0x7B||code==0x5B||code==0x5C||code==0x5D||code==0x10||code==0x11||code==0x12;}

  readonly Dictionary<string,List<string>> keys=new Dictionary<string,List<string>>();
  public int NotUnderstood; // lines of a file that were not a button line this class can keep
  KeyMap(){foreach(string b in Buttons)keys[b]=new List<string>();}
  public static KeyMap Defaults(){return Parse(String.Join("\r\n",DefaultLines));}

  // The canonical spelling of a key (upper-case name, or 0xNN with two upper-case hex digits), or null if it is not one.
  public static string Normal(string key){
    if(key==null)return null;string k=key.Trim().ToUpperInvariant();
    if(Array.IndexOf(Names,k)>=0)return k;
    if(k.Length>=3&&k.Length<=4&&k.StartsWith("0X",StringComparison.Ordinal)){
      int code;if(!Int32.TryParse(k.Substring(2),System.Globalization.NumberStyles.AllowHexSpecifier,System.Globalization.CultureInfo.InvariantCulture,out code))return null;
      if(Refused(code))return null;string name;
      return ByCode.TryGetValue(code,out name)?name:"0x"+code.ToString("X2");
    }
    return null;
  }
  // The virtual-key code a key stands for, used only to tell whether two spellings are the same key. For the
  // punctuation names this assumes the usual Windows codes of a US keyboard (COMMA 0xBC and so on); the contract
  // does not state them, so they are used for comparing and warning only, never for rewriting what the user chose.
  static readonly Dictionary<string,int> ByName=BuildByName();
  static Dictionary<string,int> BuildByName(){
    var d=new Dictionary<string,int>();foreach(var pair in ByCode)d[pair.Value]=pair.Key;
    d["SEMICOLON"]=0xBA;d["EQUALS"]=0xBB;d["COMMA"]=0xBC;d["MINUS"]=0xBD;d["PERIOD"]=0xBE;d["SLASH"]=0xBF;d["LBRACKET"]=0xDB;d["BACKSLASH"]=0xDC;d["RBRACKET"]=0xDD;d["QUOTE"]=0xDE;
    return d;
  }
  public static int Code(string key){int code;if(ByName.TryGetValue(key,out code))return code;return Int32.Parse(key.Substring(2),System.Globalization.NumberStyles.AllowHexSpecifier,System.Globalization.CultureInfo.InvariantCulture);}
  public static bool Same(string a,string b){return Code(a)==Code(b);}
  static int IndexOfKey(List<string> list,string key){for(int i=0;i<list.Count;i++)if(Same(list[i],key))return i;return -1;}
  // What to write for a key the user pressed, from its Windows virtual-key code; null if the game cannot use it.
  public static string FromVirtualKey(int code){
    if(Refused(code))return null;string name;
    return ByCode.TryGetValue(code,out name)?name:"0x"+code.ToString("X2");
  }
  public static string ButtonLabel(string button){int i=Array.IndexOf(Buttons,button);return i<0?button:ButtonLabels[i];}
  public static string KeyLabel(string key){
    if(key.StartsWith("0x",StringComparison.Ordinal))return "Key code "+key;
    switch(key){
      case "UP":return "Up arrow";case "DOWN":return "Down arrow";case "LEFT":return "Left arrow";case "RIGHT":return "Right arrow";
      case "ENTER":return "Enter";case "SPACE":return "Space";case "BACKSPACE":return "Backspace";case "ESC":return "Esc";
      case "LSHIFT":return "Left Shift";case "RSHIFT":return "Right Shift";case "LCTRL":return "Left Ctrl";case "RCTRL":return "Right Ctrl";case "LALT":return "Left Alt";case "RALT":return "Right Alt";
      case "INSERT":return "Insert";case "DELETE":return "Delete";case "HOME":return "Home";case "END":return "End";case "PAGEUP":return "Page Up";case "PAGEDOWN":return "Page Down";
      case "COMMA":return "Comma ,";case "PERIOD":return "Period .";case "SLASH":return "Slash /";case "SEMICOLON":return "Semicolon ;";case "QUOTE":return "Quote '";
      case "LBRACKET":return "Left bracket [";case "RBRACKET":return "Right bracket ]";case "BACKSLASH":return "Backslash \\";case "MINUS":return "Minus -";case "EQUALS":return "Equals =";
    }
    if(key.StartsWith("NUM",StringComparison.Ordinal))return "Numpad "+key.Substring(3);
    return key;
  }

  public static KeyMap Parse(string text){
    var map=new KeyMap();if(text==null)return map;
    foreach(string raw in text.Replace("\r\n","\n").Replace('\r','\n').Split('\n')){
      string line=raw.Trim((char)0xFEFF,' ','\t');int hash=line.IndexOf('#');if(hash>=0)line=line.Substring(0,hash).Trim();
      if(line.Length==0)continue;
      int eq=line.IndexOf('=');if(eq<=0){map.NotUnderstood++;continue;}
      string button=line.Substring(0,eq).Trim().ToUpperInvariant();
      if(Array.IndexOf(Buttons,button)<0){map.NotUnderstood++;continue;}
      var found=new List<string>();bool clean=true;
      foreach(string part in line.Substring(eq+1).Split(',')){
        string k=Normal(part);
        if(k==null){clean=false;continue;}
        if(IndexOfKey(found,k)>=0)continue;
        if(found.Count>=MaxKeys){clean=false;continue;}
        found.Add(k);
      }
      if(!clean||map.keys[button].Count>0)map.NotUnderstood++; // a bad key, a fifth key, or a second line for one button
      map.keys[button]=found;
    }
    return map;
  }
  public KeyMap Copy(){var c=new KeyMap();foreach(string b in Buttons)c.keys[b]=new List<string>(keys[b]);c.NotUnderstood=NotUnderstood;return c;}
  public IList<string> Keys(string button){return keys[button].AsReadOnly();}
  public string Describe(string button){var parts=new List<string>();foreach(string k in keys[button])parts.Add(KeyLabel(k));return parts.Count==0?"No key":String.Join(", ",parts.ToArray());}
  public List<string> Missing(){var m=new List<string>();foreach(string b in Buttons)if(keys[b].Count==0)m.Add(b);return m;}
  // Keys that sit on more than one button. Allowed by the game, but usually a mistake, so the screen says so.
  public List<string> Shared(){
    var count=new Dictionary<int,int>();var first=new Dictionary<int,string>();
    foreach(string b in Buttons)foreach(string k in keys[b]){int code=Code(k),n;count.TryGetValue(code,out n);count[code]=n+1;if(!first.ContainsKey(code))first[code]=k;}
    var s=new List<string>();foreach(var pair in count)if(pair.Value>1)s.Add(first[pair.Key]);return s;
  }
  // Gives a button exactly one key. Every other button that had that key (under any spelling) swaps: it gets this
  // button's former first key in its place, or loses the key if there is nothing to give or it already has that
  // one. Returns the other buttons that were changed (empty when nobody else had the key). Throws
  // ArgumentException for a key the game cannot use.
  public List<string> SetSingle(string button,string key){
    string k=Normal(key);if(k==null||Array.IndexOf(Buttons,button)<0)throw new ArgumentException();
    string former=keys[button].Count>0?keys[button][0]:null;var others=new List<string>();
    foreach(string b in Buttons){
      if(b==button)continue;int at=IndexOfKey(keys[b],k);if(at<0)continue;
      others.Add(b);
      if(former!=null&&!Same(former,k)&&IndexOfKey(keys[b],former)<0)keys[b][at]=former;else keys[b].RemoveAt(at);
    }
    keys[button]=new List<string>{k};
    return others;
  }
  public string Format(){
    var text=new StringBuilder(Header).Append("\r\n");
    foreach(string b in Buttons)text.Append(b).Append(" = ").Append(String.Join(", ",keys[b].ToArray())).Append("\r\n");
    return text.ToString();
  }
  // A missing file is the default map (the launcher writes that file at the first Play). An oversized file is refused.
  public static KeyMap Load(string path){
    if(!File.Exists(path))return Defaults();
    if(new FileInfo(path).Length>65536)throw new InvalidDataException();
    return Parse(File.ReadAllText(path));
  }
  // Writes all fourteen buttons through a temporary file. An existing map is kept beside it as "<name>.bak".
  // Refuses to write a map in which a button has no key, because the game would then have no key for it.
  public void Save(string path){
    if(Missing().Count>0)throw new InvalidOperationException("Every button needs a key.");
    string full=Path.GetFullPath(path),temp=full+".tmp";
    Directory.CreateDirectory(Path.GetDirectoryName(full));
    File.WriteAllText(temp,Format(),new UTF8Encoding(false));
    bool wasReadOnly=false;
    try{
      if(File.Exists(full)){
        var attributes=File.GetAttributes(full);wasReadOnly=(attributes&FileAttributes.ReadOnly)!=0;
        if(wasReadOnly)File.SetAttributes(full,attributes&~FileAttributes.ReadOnly);
        string backup=full+".bak";if(File.Exists(backup)){var old=File.GetAttributes(backup);if((old&FileAttributes.ReadOnly)!=0)File.SetAttributes(backup,old&~FileAttributes.ReadOnly);}
        File.Replace(temp,full,backup);
      }
      else File.Move(temp,full);
    }catch{
      try{File.Delete(temp);}catch{}
      try{if(wasReadOnly&&File.Exists(full))File.SetAttributes(full,File.GetAttributes(full)|FileAttributes.ReadOnly);}catch{}
      throw;
    }
  }
}
