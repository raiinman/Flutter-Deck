using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Tests of PadMap.cs (check-padmap.ps1). Modes:
//   PadMapPureTest <fixture folder>          the unit tests
//   PadMapPureTest corpus <folder>           writes the files the game's own reader is compared on
//   PadMapPureTest dump <file> ...           prints what PadMap reads from each file, in the format of the C harness
static class PadMapPureTest {
  static int failed=0,passed=0;
  static void Check(bool ok,string what){if(ok)passed++;else{failed++;Console.WriteLine("FAIL: "+what);}}
  static PadMap P(string text){return PadMap.Parse(Encoding.GetEncoding(28591).GetBytes(text));}
  static int Main(string[] args){
    if(args.Length>=2&&args[0]=="corpus"){Corpus(args[1]);return 0;}
    if(args.Length>=2&&args[0]=="dump"){for(int i=1;i<args.Length;i++)Console.WriteLine(Dump(args[i]));return 0;}
    string dir=args.Length>0?args[0]:".dream-loop\\padmap-fixture";
    if(Directory.Exists(dir))Directory.Delete(dir,true);Directory.CreateDirectory(dir);
    uint cross=PadMap.Bit("CROSS"),circle=PadMap.Bit("CIRCLE");
    // defaults
    var d=PadMap.Defaults();
    Check(d.Effective(0,false)==cross&&d.Effective(1,false)==circle&&d.Effective(6,false)==PadMap.Bit("L2")&&d.Effective(6,true)==PadMap.Aim&&d.Effective(11,true)==PadMap.CamReset&&d.Effective(10,false)==0,"the defaults are the game's (LT aims only with the third-person controls)");
    Check(d.Missing(false).Count==0&&d.Missing(true).Count==1&&d.Missing(true)[0]=="L2","by default every game button is on the pad; with third-person controls L2 is not (the left trigger aims)");
    Check(!d.AnyChange&&d.EffectiveDeadzone==20,"a new map changes nothing");
    // the template the launcher writes (every line a comment) changes nothing
    var t=PadMap.Parse(Encoding.UTF8.GetBytes(GameRuntime.PadMapTemplate));
    Check(!t.AnyChange&&t.NotUnderstood==0&&t.LookSensitivity==null,"the commented template changes nothing and has no mistakes");
    // reading
    var m=P("A = SQUARE\nb=cross + cam_reset # comment\n  L2 = AIM\nRT = NONE\nstick_deadzone = 35\nlook_sensitivity = 150\nlook_invert_y = 1\n");
    Check(m.Get(0)==PadMap.Bit("SQUARE")&&m.Get(1)==(cross|PadMap.CamReset)&&m.Get(6)==PadMap.Aim&&m.Get(7)==0u&&m.Deadzone==35&&m.LookSensitivity==150&&m.LookInvert==1&&m.NotUnderstood==0,"Xbox and PlayStation names, combinations, NONE, comments, case and the three settings are read");
    Check(PadMap.Words(m.Get(1).Value)=="CROSS + CAM_RESET"&&PadMap.Words(0)=="NONE","actions are written back in the file's words");
    var last=P("A = SQUARE\nA = CIRCLE\n");Check(last.Get(0)==circle,"for the same button the last line wins, as in the game");
    foreach(string bad in new[]{"A =","A = CROSS +","A = + CROSS","A = CROSS ++ CIRCLE","Q = CROSS","A = JUMP","A CROSS","stick_deadzone = 4","stick_deadzone = 51","stick_deadzone = 2x","look_invert_y = 2","look_sensitivity = 9","= CROSS"}){
      var x=P("A = TRIANGLE\n"+bad+"\n");Check(x.NotUnderstood==1&&x.Get(0)==PadMap.Bit("TRIANGLE"),"not understood and changes nothing: '"+bad+"'");
    }
    var bom=PadMap.Parse(Encoding.UTF8.GetPreamble().Length==3?new byte[]{0xEF,0xBB,0xBF,(byte)'A',(byte)' ',(byte)'=',(byte)' ',(byte)'X',(byte)'\n'}:null);
    Check(bom.NotUnderstood==1&&!bom.IsSet(0),"a first line after a byte-order mark is not understood (the game does not remove the mark)");
    var crlf=P("A = SQUARE\r\nB = CROSS\r\n");Check(crlf.Get(0)==PadMap.Bit("SQUARE")&&crlf.Get(1)==cross&&crlf.NotUnderstood==0,"Windows line ends are read");
    var nul=P("A = SQUARE\0\nB = SQUARE\n");Check(nul.IsSet(0)==false&&!nul.IsSet(1),"a zero byte makes the game refuse the line and skip the next one; so does PadMap");
    var ctrlz=P("A = SQUARE\nB = SQU\u001AARE\nX = CROSS\n");Check(ctrlz.IsSet(0)&&!ctrlz.IsSet(2),"a Ctrl-Z byte ends the file, as in the game's text mode");
    var longLine=P("A = SQUARE"+new string(' ',600)+"\nB = SQUARE\n");Check(longLine.NotUnderstood==1&&!longLine.IsSet(0)&&longLine.IsSet(1),"a line of 512 bytes or more is refused whole");
    // editing and checks
    var e=PadMap.Defaults();e.Set(9,0);Check(e.Missing(false).Count==1&&e.Missing(false)[0]=="START","a game button no pad button gives is reported");
    Check(e.Lost(true).Count==1&&e.Lost(true)[0]=="START"&&PadMap.Defaults().Lost(true).Count==0,"the editor warns only about what a change took away, not about L2 that the third-person defaults leave off");
    e.Set(0,PadMap.Bit("SQUARE"));Check(e.Shared(false).Contains("SQUARE"),"an action on two buttons is reported");
    // writing: only the changed buttons, no byte-order mark, read back the same
    var w=PadMap.Defaults();w.Set(0,PadMap.Bit("SQUARE"));w.Set(2,cross|PadMap.CamReset);w.Deadzone=30;
    string f=Path.Combine(dir,"pad-map.txt");w.Save(f);
    byte[] raw=File.ReadAllBytes(f);Check(raw.Length>3&&raw[0]==(byte)'#',"written without a byte-order mark");
    var back=PadMap.Load(f);
    Check(back.Get(0)==w.Get(0)&&back.Get(2)==w.Get(2)&&back.Deadzone==30&&!back.IsSet(1)&&!back.IsSet(6)&&back.NotUnderstood==0,"a saved map reads back the same, and the buttons not changed stay unnamed (so they keep the default of either control style)");
    string text=File.ReadAllText(f);Check(text.StartsWith(PadMap.Header)&&!text.Contains("LT =")&&text.Contains("A = SQUARE\r\n")&&text.Contains("X = CROSS + CAM_RESET\r\n")&&text.Contains("stick_deadzone = 30\r\n"),"the file lists exactly the changes");
    File.WriteAllText(f,"# my own notes\nB = SQUARE\n");var keep=PadMap.Load(f);keep.Save(f);
    Check(File.Exists(f+".bak")&&File.ReadAllText(f+".bak")=="# my own notes\nB = SQUARE\n"&&PadMap.Load(f).Get(1)==PadMap.Bit("SQUARE"),"the file it replaces is kept with .bak added, and its settings are kept");
    Check(File.Exists(f+PadMap.FirstCopySuffix)&&File.ReadAllText(f+PadMap.FirstCopySuffix)=="# my own notes\nB = SQUARE\n","the player's own file is kept once as .before-editor");
    var again=PadMap.Load(f);again.Set(3,0);again.Save(f);again.Save(f);
    Check(File.ReadAllText(f+PadMap.FirstCopySuffix)=="# my own notes\nB = SQUARE\n","later saves never replace the .before-editor copy");
    var keepLook=P("look_sensitivity = 200\nlook_invert_y = 0\n");Check(keepLook.Format().Contains("look_sensitivity = 200")&&keepLook.Format().Contains("look_invert_y = 0"),"camera lines the editor does not show are written back unchanged");
    var none=PadMap.Load(Path.Combine(dir,"missing.txt"));Check(!none.AnyChange,"no file means the defaults");
    Console.WriteLine("PadMap tests: "+passed+" passed, "+failed+" failed");
    return failed==0?0:1;
  }
  // One line per file: the 16 button masks after reading it over each default map, then look sensitivity, invert, dead zone, lines not understood.
  static string Dump(string file){
    var m=PadMap.Load(file);var s=new StringBuilder();
    foreach(bool tps in new[]{false,true}){for(int i=0;i<PadMap.ButtonCount;i++)s.Append(m.Effective(i,tps).ToString("x")).Append(' ');}
    s.Append(m.LookSensitivity??100).Append(' ').Append(m.LookInvert??0).Append(' ').Append(m.EffectiveDeadzone).Append(' ').Append(m.NotUnderstood);
    return s.ToString();
  }
  // Hand cases plus generated lines from the names the game knows, nearly right names and broken punctuation.
  static void Corpus(string dir){
    if(Directory.Exists(dir))Directory.Delete(dir,true);Directory.CreateDirectory(dir);
    var hand=new List<string>{
      "A = SQUARE\n","a=square\n","A=CROSS+CAM_RESET\n","A = CROSS +\n","A = + CROSS\n","A =\n","A = \t \n","= CROSS\n","#A = SQUARE\n","  # x\n\n\n","A = SQUARE # c\n",
      "CROSS = CIRCLE\nL1 = R1\nOPTIONS = SELECT\nCREATE = START\n","L2 = AIM\nLT = L2\n","DPAD_UP = DOWN\ndpad_down = up\n","stick_deadzone = 5\nstick_deadzone = 50\n","stick_deadzone = +7\n",
      "stick_deadzone = -7\n","stick_deadzone = 007\n","stick_deadzone =  12  \n","look_sensitivity = 400\nlook_sensitivity = 401\n","look_invert_y = 1\nlook_invert_y = 01\n","A = SQUARE\r\nB = CROSS\r\n",
      "\xEF\xBB\xBF" + "A = SQUARE\n","\xEF\xBB\xBF" + "# comment\nB = CROSS\n","A = SQUARE"+new string(' ',500)+"\nB = CROSS\n","A = SQUARE"+new string(' ',501)+"\n","A = SQUARE"+new string(' ',1100)+"\nB = CROSS\n",
      "A = SQUARE","A = NONE\nB = none + CROSS\n","R3 = CAM_RESET + AIM + CROSS\n","A\t=\tSQUARE\t\n","A = SQ UARE\n","A = SQUARE\rB = CROSS\n","A == SQUARE\n","A = SQUARE = CROSS\n","Ä = SQUARE\n"};
    // Found by an independent check (2026-11-22): zero bytes, Ctrl-Z, CR LF at the length limit, other spaces before a number.
    hand.AddRange(new[]{"A = SQUARE\0\nB = SQUARE\n","A = SQUARE\0junk","A = SQUARE\n\n\0\nB = CROSS\n","A = SQUARE\n\x001A","A = SQU\u001AARE\nB = SQUARE\n","A = SQUARE\nB = CROSS\n\u001A","\u001A",
      "A = SQUARE"+new string(' ',500)+"\r\nB = CROSS\r\n","A = SQUARE"+new string(' ',501)+"\r\n","stick_deadzone = \v30\n","stick_deadzone = \f30\n","look_sensitivity = \r200\n","A = SQUARE\rB = CROSS\r\n",
      "A = SQUARE"+new string(' ',1013)+"\nB = CROSS\n","A = SQUARE"+new string(' ',1012)+"\nB = CROSS\n","A = SQUARE"+new string(' ',1013)});
    int n=0;foreach(string h in hand)Write(dir,n++,h);
    File.WriteAllBytes(Path.Combine(dir,(n++).ToString("D4")+".txt"),Concat(Encoding.Unicode.GetPreamble(),Encoding.Unicode.GetBytes("A = SQUARE\r\nB = CROSS\r\n")));
    File.WriteAllBytes(Path.Combine(dir,(n++).ToString("D4")+".txt"),Encoding.Unicode.GetBytes("A = SQUARE\r\nB = CROSS\r\n"));
    string[] keys={"A","B","X","Y","LB","RB","LT","RT","BACK","START","L3","R3","DPAD_UP","DPAD_DOWN","DPAD_LEFT","DPAD_RIGHT","CROSS","CIRCLE","SQUARE","TRIANGLE","L1","R1","L2","R2","CREATE","OPTIONS","a","dpad_left","Q","START2","stick_deadzone","look_sensitivity","look_invert_y",""};
    string[] vals={"CROSS","CIRCLE","SQUARE","TRIANGLE","L1","R1","L2","R2","START","SELECT","UP","DOWN","LEFT","RIGHT","CAM_RESET","AIM","NONE","cross","JUMP","","0","1","2","5","20","50","51","100","10","400","9","x","-1"};
    string[] seps={" = ","=","  =\t"," =",": "};string[] joins={" + ","+"," ++ "," +"};
    var rnd=new Random(20261122);
    for(int f=0;f<400;f++){
      var s=new StringBuilder();int lines=rnd.Next(1,9);
      for(int l=0;l<lines;l++){
        int kind=rnd.Next(10);
        if(kind==0){s.Append("# note\n");continue;}if(kind==1){s.Append("\n");continue;}
        s.Append(keys[rnd.Next(keys.Length)]).Append(seps[rnd.Next(seps.Length)]).Append(vals[rnd.Next(vals.Length)]);
        int more=rnd.Next(4)==0?rnd.Next(1,3):0;for(int j=0;j<more;j++)s.Append(joins[rnd.Next(joins.Length)]).Append(vals[rnd.Next(vals.Length)]);
        if(rnd.Next(6)==0)s.Append(" # c");if(rnd.Next(5)==0)s.Append('\r');if(rnd.Next(40)==0)s.Append('\0');if(rnd.Next(60)==0)s.Append('\u001A');s.Append('\n');
      }
      Write(dir,n++,s.ToString());
    }
  }
  static byte[] Concat(byte[] a,byte[] b){var c=new byte[a.Length+b.Length];a.CopyTo(c,0);b.CopyTo(c,a.Length);return c;}
  static void Write(string dir,int n,string text){File.WriteAllBytes(Path.Combine(dir,n.ToString("D4")+".txt"),Encoding.GetEncoding(28591).GetBytes(text));}
}
