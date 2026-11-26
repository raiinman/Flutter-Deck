using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// The gamepad button map of Mega Man Legends (pad-map.txt, contract 2.8). The game reads it with padmap_line in the
// runtime core's rtc_padmap.h; this class reads it the same way, line by line, so what the Gamepad screen shows is what
// the game will do (check-padmap.ps1 compares the two on the same lines). Pure: it reads and writes only the file it is
// given, with no window, network or process code.
//
// The file names a physical button (Xbox name, or the PlayStation 5 name of the same place) and the game action(s) it
// gives. A button the file does not name keeps the game's default, and that default depends on the third-person
// controls (the left trigger aims with them, gives L2 without them). So the editor writes only the buttons the player
// changed. For the same button the last line wins, as in the game. See SPEC-padmap.md.
public sealed class PadMap {
  public const string Header="# Flutter Deck gamepad map for Mega Man Legends, written by the Gamepad screen of Flutter Deck.";
  public const int ButtonCount=16, DeadzoneDefault=20, DeadzoneMin=5, DeadzoneMax=50, LineLimit=512;
  public const uint CamReset=0x10000u, Aim=0x20000u, Walk=0x40000u;
  // Physical buttons, in the order of the game's PHY_ list.
  public static readonly string[] Buttons={"A","B","X","Y","LB","RB","LT","RT","BACK","START","L3","R3","DPAD_UP","DPAD_DOWN","DPAD_LEFT","DPAD_RIGHT"};
  static readonly string[] PsNames={"CROSS","CIRCLE","SQUARE","TRIANGLE","L1","R1","L2","R2","CREATE","OPTIONS","L3","R3","DPAD_UP","DPAD_DOWN","DPAD_LEFT","DPAD_RIGHT"};
  static readonly string[] ButtonLabels={"A (Xbox) / Cross (PlayStation)","B / Circle","X / Square","Y / Triangle","LB / L1","RB / R1","LT / L2 (left trigger)","RT / R2 (right trigger)","View (Back) / Create","Menu (Start) / Options","Left stick press (L3)","Right stick press (R3)","D-pad up","D-pad down","D-pad left","D-pad right"};
  // Game actions, in the order the lists on screen show them; the bits are the game's.
  public static readonly string[] Actions={"CROSS","CIRCLE","SQUARE","TRIANGLE","L1","R1","L2","R2","START","SELECT","UP","DOWN","LEFT","RIGHT","CAM_RESET","AIM","WALK","NONE"};
  static readonly uint[] ActionBits={0x4000,0x2000,0x8000,0x1000,0x0400,0x0800,0x0100,0x0200,0x0008,0x0001,0x0010,0x0040,0x0080,0x0020,CamReset,Aim,Walk,0};
  static readonly string[] ActionLabels={"Cross","Circle","Square","Triangle","L1","R1","L2","R2","Start","Select","Up","Down","Left","Right","Camera behind Mega Man (third-person only)","Aim, hold (third-person only)","Walk switch: lets a Circle hold start the skates (quick roll only)","Nothing"};
  // The 14 buttons of the game's own pad (every action but the two third-person ones and NONE).
  public static readonly string[] GameButtons={"CROSS","CIRCLE","SQUARE","TRIANGLE","L1","R1","L2","R2","START","SELECT","UP","DOWN","LEFT","RIGHT"};

  // True for a game program that has the walk switch (game contract 2.41, feature word 'roll'): its default map gives the left stick press the WALK action.
  // An older program does not know the word and skips a line that names it, so for it the editor neither offers WALK nor shows it as the default.
  // A program that reads the word WALK but was built without the quick roll does nothing with it (the game acts on the walk switch only inside its roll code),
  // so for that program too the left stick press truly gives nothing, and the editor says so. Showing WALK there would name a button that has no effect.
  public bool WalkKnown;
  readonly uint?[] act=new uint?[ButtonCount];   // null: not named in the file, so the game's default
  public int? Deadzone, LookSensitivity, LookInvert;   // null: not in the file
  public int NotUnderstood;                       // lines the game would not understand (it skips them)

  public static PadMap Defaults(){return new PadMap();}
  public static PadMap Defaults(bool walkKnown){return new PadMap{WalkKnown=walkKnown};}
  // The actions the editor offers: every action, but WALK only for a program that has it.
  public static string[] ActionsFor(bool walkKnown){var l=new List<string>();foreach(string a in Actions)if(walkKnown||a!="WALK")l.Add(a);return l.ToArray();}
  public static string ButtonLabel(int i){return ButtonLabels[i];}
  public static string ActionLabel(string action){int i=Array.IndexOf(Actions,action);return i<0?action:ActionLabels[i];}
  public static uint Bit(string action){int i=Array.IndexOf(Actions,action);if(i<0)throw new ArgumentException("Unknown action "+action);return ActionBits[i];}
  // The game's defaults: padmap_default, and padmap_default_tps with the third-person controls on.
  public static uint Default(int button,bool thirdPerson){return Default(button,thirdPerson,false);}
  public static uint Default(int button,bool thirdPerson,bool walkKnown){
    switch(button){
      case 0:return 0x4000;case 1:return 0x2000;case 2:return 0x8000;case 3:return 0x1000;case 4:return 0x0400;case 5:return 0x0800;
      case 6:return thirdPerson?Aim:0x0100;case 7:return 0x0200;case 8:return 0x0001;case 9:return 0x0008;case 10:return walkKnown?Walk:0;case 11:return CamReset;
      case 12:return 0x0010;case 13:return 0x0040;case 14:return 0x0080;case 15:return 0x0020;
    }
    throw new ArgumentOutOfRangeException("button");
  }
  // What a button gives now: its own line if the file names it, else the default.
  public uint Effective(int button,bool thirdPerson){return act[button].HasValue?act[button].Value:Default(button,thirdPerson,WalkKnown);}
  public bool IsSet(int button){return act[button].HasValue;}
  public uint? Get(int button){return act[button];}
  public void Set(int button,uint? bits){act[button]=bits;}
  public int EffectiveDeadzone {get{return Deadzone??DeadzoneDefault;}}
  // An action mask in the file's own words: "CROSS", "CROSS + CAM_RESET", "NONE".
  public static string Words(uint bits){
    if(bits==0)return "NONE";var parts=new List<string>();
    for(int i=0;i<Actions.Length;i++)if(ActionBits[i]!=0&&(bits&ActionBits[i])==ActionBits[i])parts.Add(Actions[i]);
    return String.Join(" + ",parts.ToArray());
  }
  public static string Describe(uint bits){
    if(bits==0)return "Nothing";var parts=new List<string>();
    for(int i=0;i<Actions.Length;i++)if(ActionBits[i]!=0&&(bits&ActionBits[i])==ActionBits[i])parts.Add(ActionLabels[i]);
    return String.Join(" and ",parts.ToArray());
  }
  // The game buttons that no gamepad button gives (the keyboard still can).
  public List<string> Missing(bool thirdPerson){
    uint all=0;for(int i=0;i<ButtonCount;i++)all|=Effective(i,thirdPerson);
    var missing=new List<string>();foreach(string a in GameButtons)if((all&Bit(a))==0)missing.Add(a);return missing;
  }
  // The same, leaving out what the game's own defaults already leave off the pad (L2 with the third-person controls, whose
  // left trigger aims): only what the player's changes took away.
  public List<string> Lost(bool thirdPerson){var before=Defaults(WalkKnown).Missing(thirdPerson);var lost=Missing(thirdPerson);lost.RemoveAll(before.Contains);return lost;}
  // Game actions given by more than one gamepad button.
  public List<string> Shared(bool thirdPerson){
    var shared=new List<string>();
    foreach(string a in Actions){uint b=Bit(a);if(b==0)continue;int n=0;for(int i=0;i<ButtonCount;i++)if((Effective(i,thirdPerson)&b)!=0)n++;if(n>1)shared.Add(a);}
    return shared;
  }
  public bool AnyChange {get{for(int i=0;i<ButtonCount;i++)if(act[i].HasValue)return true;return Deadzone.HasValue;}}

  // ---- reading: exactly padmap_line of rtc_padmap.h ----
  static bool AsciiEq(string a,string b){
    if(a.Length!=b.Length)return false;
    for(int i=0;i<a.Length;i++){char x=a[i],y=b[i];if(x>='a'&&x<='z')x=(char)(x-32);if(y>='a'&&y<='z')y=(char)(y-32);if(x!=y)return false;}
    return true;
  }
  static bool SpaceTab(char c){return c==' '||c=='\t';}
  static bool CSpace(char c){return c==' '||c=='\t'||c=='\n'||c=='\v'||c=='\f'||c=='\r';}
  // strtol(v,&end,10) with *end==0 required; false when the text is not a whole decimal number.
  static bool WholeNumber(string v,out long x){
    x=0;int i=0;while(i<v.Length&&CSpace(v[i]))i++;
    bool neg=false;if(i<v.Length&&(v[i]=='+'||v[i]=='-')){neg=v[i]=='-';i++;}
    int start=i;while(i<v.Length&&v[i]>='0'&&v[i]<='9'){if(x<1000000000L)x=x*10+(v[i]-'0');i++;}
    if(i==start||i!=v.Length)return false;if(neg)x=-x;return true;
  }
  // One line as the game's padmap_line gets it: a C string (no zero byte), with its newline when it had one, one char per
  // byte. 1 = applied, 0 = blank or comment, -1 = not understood.
  public int Line(string line){
    if(line.Length>=LineLimit)return -1;                              // padmap_line's 512-byte buffer
    int hash=line.IndexOf('#');if(hash>=0)line=line.Substring(0,hash);
    int p=0;while(p<line.Length&&SpaceTab(line[p]))p++;
    if(p==line.Length||line[p]=='\r'||line[p]=='\n')return 0;
    int eq=line.IndexOf('=',p);if(eq<0)return -1;
    string k=line.Substring(p,eq-p).TrimEnd(' ','\t');
    string v=line.Substring(eq+1).TrimStart(' ','\t').TrimEnd(' ','\t','\r','\n');
    if(v.Length==0)return -1;
    long x;
    if(AsciiEq(k,"look_sensitivity")){if(!WholeNumber(v,out x)||x<10||x>400)return -1;LookSensitivity=(int)x;return 1;}
    if(AsciiEq(k,"look_invert_y")){if(v!="0"&&v!="1")return -1;LookInvert=v=="1"?1:0;return 1;}
    if(AsciiEq(k,"stick_deadzone")){if(!WholeNumber(v,out x)||x<DeadzoneMin||x>DeadzoneMax)return -1;Deadzone=(int)x;return 1;}
    int b=-1;for(int i=0;i<ButtonCount;i++)if(AsciiEq(k,Buttons[i])||AsciiEq(k,PsNames[i])){b=i;break;}
    if(b<0)return -1;
    if(v[v.Length-1]=='+')return -1;
    uint bits=0;
    foreach(string part in v.Split('+')){
      string t=part.Trim(' ','\t');int a=-1;
      for(int i=0;i<Actions.Length;i++)if(AsciiEq(t,Actions[i])){a=i;break;}
      if(a<0)return -1;bits|=ActionBits[a];
    }
    act[b]=bits;return 1;
  }
  // Reads the bytes as the game's loop in rtc_move_init does, byte for byte:
  // - fopen "r" on Windows is text mode: a Ctrl-Z byte (0x1A) ends the file, and CR LF becomes LF;
  // - fgets fills at most 1023 bytes and stops after a newline; the game then calls a line "too long" when strchr finds no
  //   newline (strchr stops at a zero byte) and the end of the file was not reached yet, refuses it and skips to the next
  //   newline; padmap_line sees the line only up to its first zero byte.
  // A byte-order mark is not removed (the game keeps it too), so a first line that starts with one is not understood.
  public static PadMap Parse(byte[] bytes){
    var m=new PadMap();
    int end=Array.IndexOf(bytes,(byte)0x1A);if(end<0)end=bytes.Length;
    var s=new List<byte>(end);
    for(int i=0;i<end;i++){if(bytes[i]==(byte)'\r'&&i+1<end&&bytes[i+1]==(byte)'\n')continue;s.Add(bytes[i]);}
    var latin=Encoding.GetEncoding(28591);
    int pos=0,len=s.Count;bool eof=false;
    while(true){
      if(pos>=len){break;}                                            // fgets reads nothing: the loop ends
      int start=pos;bool newline=false;
      while(pos<len&&pos-start<1023){byte c=s[pos++];if(c==(byte)'\n'){newline=true;break;}}
      if(!newline&&pos-start<1023&&pos>=len)eof=true;                 // fgets ran into the end of the file
      int cut=start;while(cut<pos&&s[cut]!=0)cut++;                   // the C string ends at the first zero byte
      string line=latin.GetString(s.GetRange(start,cut-start).ToArray());
      bool tooLong=line.IndexOf('\n')<0&&!eof;
      int r;
      if(tooLong){while(true){if(pos>=len){eof=true;break;}if(s[pos++]==(byte)'\n')break;}r=-1;}
      else r=m.Line(line);
      if(r<0)m.NotUnderstood++;
    }
    return m;
  }
  public static PadMap Load(string path){
    if(!File.Exists(path))return Defaults();
    var info=new FileInfo(path);if(info.Length>1048576)throw new IOException("The gamepad map is too large to be a map.");
    return Parse(File.ReadAllBytes(path));
  }

  // ---- writing ----
  public string Format(){
    var s=new StringBuilder();
    s.Append(Header).Append("\r\n");
    s.Append("# One setting per line; a line starting with # does nothing. Only the buttons you changed are listed:\r\n");
    s.Append("# every other button does what the game does by default. Buttons: A B X Y LB RB LT RT BACK START L3 R3\r\n");
    s.Append("# DPAD_UP DPAD_DOWN DPAD_LEFT DPAD_RIGHT (PlayStation names also work: CROSS CIRCLE SQUARE TRIANGLE L1 R1 L2 R2 CREATE OPTIONS).\r\n");
    s.Append("# Actions: CROSS CIRCLE SQUARE TRIANGLE L1 R1 L2 R2 START SELECT UP DOWN LEFT RIGHT CAM_RESET AIM WALK NONE; several with +.\r\n");
    for(int i=0;i<ButtonCount;i++)if(act[i].HasValue)s.Append(Buttons[i]).Append(" = ").Append(Words(act[i].Value)).Append("\r\n");
    if(Deadzone.HasValue)s.Append("stick_deadzone = ").Append(Deadzone.Value).Append("\r\n");
    if(LookSensitivity.HasValue)s.Append("look_sensitivity = ").Append(LookSensitivity.Value).Append("\r\n");
    if(LookInvert.HasValue)s.Append("look_invert_y = ").Append(LookInvert.Value).Append("\r\n");
    return s.ToString();
  }
  public const string FirstCopySuffix=".before-editor";
  // Written through a temporary file; the file it replaces is kept beside it with .bak added (as the key map does). The
  // first time the editor replaces a file it did not write (the template, or the player's own), that file is also kept
  // as pad-map.txt.before-editor, and never overwritten, so hand-written lines survive later saves.
  public void Save(string path){
    string full=Path.GetFullPath(path),temp=full+".tmp";
    Directory.CreateDirectory(Path.GetDirectoryName(full));
    if(File.Exists(full)&&!File.Exists(full+FirstCopySuffix)){
      string first="";using(var r=new StreamReader(full,Encoding.GetEncoding(28591)))first=r.ReadLine()??"";
      if(first!=Header)File.Copy(full,full+FirstCopySuffix);
    }
    File.WriteAllText(temp,Format(),new UTF8Encoding(false));
    try{
      if(File.Exists(full)){
        var attributes=File.GetAttributes(full);if((attributes&FileAttributes.ReadOnly)!=0)File.SetAttributes(full,attributes&~FileAttributes.ReadOnly);
        string backup=full+".bak";if(File.Exists(backup)){var old=File.GetAttributes(backup);if((old&FileAttributes.ReadOnly)!=0)File.SetAttributes(backup,old&~FileAttributes.ReadOnly);}
        File.Replace(temp,full,backup);
      }
      else File.Move(temp,full);
    }
    finally{try{if(File.Exists(temp))File.Delete(temp);}catch(Exception){}}
  }
}
