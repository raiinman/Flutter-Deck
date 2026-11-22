using System;
using System.Collections.Generic;
using System.IO;

// Tests for KeyMap.cs. Writes only inside the folder given as the first argument.
public static class KeyMapPureTest {
  static int failed=0,passed=0;
  static void Check(bool ok,string what){if(ok)passed++;else{failed++;Console.WriteLine("FAIL: "+what);}}
  static bool Throws<T>(Action a) where T:Exception {try{a();}catch(T){return true;}catch{return false;}return false;}
  public static int Main(string[] args){
    string root=Path.GetFullPath(args[0]);if(Directory.Exists(root)){foreach(string old in Directory.GetFiles(root,"*",SearchOption.AllDirectories))File.SetAttributes(old,FileAttributes.Normal);Directory.Delete(root,true);}Directory.CreateDirectory(root);

    // Defaults: the same fourteen lines the launcher writes at first Play, in the shape the game side tested.
    var d=KeyMap.Defaults();
    Check(KeyMap.Buttons.Length==14&&d.Missing().Count==0&&d.NotUnderstood==0,"defaults give all fourteen buttons a key");
    int same=0;foreach(string line in GameRuntime.DefaultKeys)if(d.Format().Contains(line+"\r\n"))same++;
    Check(same==14,"defaults match GameRuntime.DefaultKeys line for line");
    string text=d.Format();
    Check(text.StartsWith(KeyMap.Header+"\r\n",StringComparison.Ordinal)&&text.EndsWith("\r\n",StringComparison.Ordinal)&&text.Replace("\r\n","").IndexOf('\n')<0&&text.Replace("\r\n","").IndexOf('\r')<0,"one leading comment line, CRLF line ends only");
    Check(text.Split(new[]{"\r\n"},StringSplitOptions.RemoveEmptyEntries).Length==15,"header plus fourteen lines");
    Check(KeyMap.Parse(text).Format()==text,"format then parse then format is stable");

    // Parsing what the contract allows.
    var p=KeyMap.Parse(((char)0xFEFF)+"# comment\r\ncross = k , 0x4a, NUM3 # trailing\r\n\r\nUP=up\nSTART = ENTER, SPACE, A, B\n");
    Check(String.Join("|",new List<string>(p.Keys("CROSS")).ToArray())=="K|J|NUM3","lower case, spaces, a code with a name (written as the name) and a trailing comment are read");
    Check(p.Keys("UP").Count==1&&p.Keys("UP")[0]=="UP"&&p.Keys("START").Count==4&&p.NotUnderstood==0,"LF line ends and four keys are read");
    Check(p.Missing().Count==11&&p.Missing().Contains("SELECT"),"a button with no line has no key");
    var bad=KeyMap.Parse("JUMP = X\nCROSS X\nCROSS = F5\nSQUARE = A, B, C, D, E\nCIRCLE = TAB\nL1 = 0x09\nR1 = 0x7B, Q\nL2 = Z\nL2 = Y\n");
    Check(bad.NotUnderstood==8,"unknown button, no equals sign, unknown key, fifth key, Tab, Tab by code, F12 by code and a repeated button are each counted: "+bad.NotUnderstood);
    Check(bad.Keys("SQUARE").Count==4&&bad.Keys("CROSS").Count==0&&bad.Keys("R1").Count==1&&bad.Keys("R1")[0]=="Q"&&bad.Keys("L2")[0]=="Y","good keys on a partly bad line are kept; a later line replaces an earlier one");
    Check(KeyMap.Parse(null).Missing().Count==14&&KeyMap.Parse("").Missing().Count==14,"empty text is an empty map");

    // Key names and codes.
    Check(KeyMap.Normal(" rshift ")=="RSHIFT"&&KeyMap.Normal("0x4a")=="J"&&KeyMap.Normal("0xbc")=="0xBC"&&KeyMap.Normal("0XA")=="0x0A"&&KeyMap.Normal("F11")==null&&KeyMap.Normal("TAB")==null&&KeyMap.Normal("0x")==null&&KeyMap.Normal("0x100")==null&&KeyMap.Normal("0xZZ")==null&&KeyMap.Normal("")==null&&KeyMap.Normal(null)==null,"key spelling is normalised and non-keys are refused");
    Check(KeyMap.Normal("0x09")==null&&KeyMap.Normal("0x7A")==null&&KeyMap.Normal("0x7B")==null&&KeyMap.Normal("0x00")==null&&KeyMap.Normal("0x01")==null&&KeyMap.Normal("0xFF")==null,"Tab, F11, F12, mouse buttons and out-of-range codes are refused as codes");
    Check(KeyMap.FromVirtualKey(0x41)=="A"&&KeyMap.FromVirtualKey(0x5A)=="Z"&&KeyMap.FromVirtualKey(0x30)=="0"&&KeyMap.FromVirtualKey(0x69)=="NUM9"&&KeyMap.FromVirtualKey(0x26)=="UP"&&KeyMap.FromVirtualKey(0xA1)=="RSHIFT"&&KeyMap.FromVirtualKey(0x0D)=="ENTER","pressed letters, digits, numpad, arrows and named keys become names");
    Check(KeyMap.FromVirtualKey(0xBC)=="0xBC"&&KeyMap.FromVirtualKey(0x70)=="0x70","a pressed punctuation or function key becomes its code");
    Check(KeyMap.FromVirtualKey(0x09)==null&&KeyMap.FromVirtualKey(0x7A)==null&&KeyMap.FromVirtualKey(0x7B)==null&&KeyMap.FromVirtualKey(0x5B)==null&&KeyMap.FromVirtualKey(0x10)==null&&KeyMap.FromVirtualKey(0)==null&&KeyMap.FromVirtualKey(0x1FF)==null,"Tab, F11, F12, the Windows key, side-less Shift and nonsense are refused when pressed");
    bool allNormal=true;foreach(string n in KeyMap.Names)if(KeyMap.Normal(n)!=n||KeyMap.KeyLabel(n).Length==0)allNormal=false;
    Check(allNormal&&KeyMap.Names.Length==76,"every listed name is a valid key with a label: "+KeyMap.Names.Length);
    string everyCode=null;for(int code=0;code<=0xFF;code++){string k=KeyMap.FromVirtualKey(code);if(k!=null&&KeyMap.Normal(k)!=k)everyCode=k;}
    Check(everyCode==null,"whatever a pressed key becomes is something the parser accepts");
    Check(KeyMap.ButtonLabel("CROSS")=="Cross"&&KeyMap.KeyLabel("0x70")=="Key code 0x70"&&KeyMap.KeyLabel("NUM4")=="Numpad 4"&&KeyMap.KeyLabel("UP")=="Up arrow","labels");

    // Editing.
    var e=KeyMap.Defaults();
    Check(e.SetSingle("CROSS","K").Count==0&&e.Keys("CROSS").Count==1&&e.Keys("CROSS")[0]=="K"&&e.Keys("SQUARE")[0]=="Z","a free key replaces the button's key and touches nothing else");
    Check(String.Join("|",e.SetSingle("CROSS","Z").ToArray())=="SQUARE"&&e.Keys("CROSS")[0]=="Z"&&e.Keys("SQUARE")[0]=="K"&&e.Missing().Count==0&&e.Shared().Count==0,"a key another button has is swapped, so no button is left without a key");
    Check(e.SetSingle("CROSS","Z").Count==0&&e.Keys("CROSS")[0]=="Z","setting the same key again changes nothing");
    var multi=KeyMap.Parse("CROSS = X, J\nSQUARE = Z, X\nUP = UP\n");
    Check(multi.Shared().Count==1&&multi.Shared()[0]=="X","a key on two buttons is reported as shared");
    Check(String.Join("|",multi.SetSingle("UP","X").ToArray())=="CROSS|SQUARE"&&multi.Keys("CROSS").Count==2&&multi.Keys("CROSS")[0]=="UP"&&multi.Keys("CROSS")[1]=="J"&&multi.Keys("SQUARE")[1]=="UP"&&multi.Shared().Count==1&&multi.Shared()[0]=="UP","swapping keeps the other buttons' extra keys");
    var none=KeyMap.Parse("CROSS = X\n");
    Check(String.Join("|",none.SetSingle("SQUARE","X").ToArray())=="CROSS"&&none.Keys("CROSS").Count==0&&none.Missing().Contains("CROSS"),"with nothing to give back, the other button is left without a key and reported missing");
    Check(Throws<ArgumentException>(delegate{e.SetSingle("CROSS","TAB");})&&Throws<ArgumentException>(delegate{e.SetSingle("JUMP","X");})&&e.Keys("CROSS")[0]=="Z","a refused key or unknown button changes nothing");
    // One physical key has one identity, however it is spelled.
    var id=KeyMap.Defaults();id.SetSingle("CROSS","COMMA");
    Check(String.Join("|",id.SetSingle("SQUARE",KeyMap.FromVirtualKey(0xBC)).ToArray())=="CROSS"&&id.Keys("SQUARE")[0]=="0xBC"&&id.Keys("CROSS")[0]=="Z"&&id.Shared().Count==0,"a punctuation key chosen by name and then pressed is the same key, so the buttons swap");
    var id2=KeyMap.Defaults();Check(String.Join("|",id2.SetSingle("CROSS","0x5A").ToArray())=="SQUARE"&&id2.Keys("CROSS")[0]=="Z"&&id2.Keys("SQUARE")[0]=="X","a code that has a name is that named key");
    var id3=KeyMap.Parse("CROSS = x, X, 0x58\nSQUARE = COMMA\nCIRCLE = 0xbc\n");
    Check(id3.Keys("CROSS").Count==1&&id3.Shared().Count==1&&id3.Shared()[0]=="0xBC"&&KeyMap.Same("PERIOD","0xBE")&&!KeyMap.Same("PERIOD","COMMA"),"one key spelled three ways is kept once; a name and its code on two buttons are reported as shared");
    var copy=e.Copy();copy.SetSingle("START","P");
    Check(e.Keys("START")[0]=="ENTER"&&copy.Keys("START")[0]=="P","a copy is independent");
    Check(e.Describe("SELECT")=="Right Shift"&&none.Describe("CROSS")=="No key"&&KeyMap.Parse("L1 = Q, 0x70").Describe("L1")=="Q, Key code 0x70","descriptions");

    // Files.
    string path=Path.Combine(root,"folder with spaces","keys.ini");
    Check(KeyMap.Load(path).Format()==KeyMap.Defaults().Format()&&!File.Exists(path),"a missing file loads as the defaults and is not created");
    e.Save(path);
    Check(File.ReadAllText(path)==e.Format()&&!File.Exists(path+".bak")&&!File.Exists(path+".tmp"),"first save writes the file, with no backup and no temporary file left");
    byte[] bytes=File.ReadAllBytes(path);Check(bytes[0]==(byte)'#',"no byte-order mark");
    string first=File.ReadAllText(path);var second=KeyMap.Load(path);second.SetSingle("L1","1");second.Save(path);
    Check(File.ReadAllText(path+".bak")==first&&KeyMap.Load(path).Keys("L1")[0]=="1"&&!File.Exists(path+".tmp"),"a second save keeps the previous map as .bak");
    File.SetAttributes(path,FileAttributes.ReadOnly);var third=KeyMap.Load(path);third.SetSingle("R1","2");third.Save(path);
    Check(KeyMap.Load(path).Keys("R1")[0]=="2","a read-only map is replaced");
    File.SetAttributes(path+".bak",FileAttributes.ReadOnly);var fourth=KeyMap.Load(path);fourth.SetSingle("L2","3");fourth.Save(path);
    Check(KeyMap.Load(path).Keys("L2")[0]=="3","a read-only backup does not block saving");
    string blocked=Path.Combine(root,"blocked","keys.ini");Directory.CreateDirectory(blocked); // the target is a folder, so the write must fail
    Check(Throws<Exception>(delegate{e.Save(blocked);})&&!File.Exists(blocked+".tmp")&&Directory.Exists(blocked),"a save that fails after the temporary file was written leaves no temporary file");
    string locked=Path.Combine(root,"locked.ini");e.Save(locked);File.SetAttributes(locked,FileAttributes.ReadOnly);string lockedText=File.ReadAllText(locked);
    using(var hold=new FileStream(locked,FileMode.Open,FileAccess.Read,FileShare.Read)){Check(Throws<Exception>(delegate{second.Save(locked);}),"a map held open by another program is not replaced");}
    Check(File.ReadAllText(locked)==lockedText&&(File.GetAttributes(locked)&FileAttributes.ReadOnly)!=0&&!File.Exists(locked+".tmp"),"after that failure the old map is unchanged and still read-only");
    string before=File.ReadAllText(path);
    Check(Throws<InvalidOperationException>(delegate{none.Save(path);})&&File.ReadAllText(path)==before&&!File.Exists(path+".tmp"),"a map with a button without a key is not written");
    File.WriteAllText(Path.Combine(root,"big.ini"),new string('#',70000));
    Check(Throws<InvalidDataException>(delegate{KeyMap.Load(Path.Combine(root,"big.ini"));}),"an oversized file is refused");
    File.WriteAllText(Path.Combine(root,"hand.ini"),"# mine\r\nCROSS = K\r\nTURBO = T\r\n");var hand=KeyMap.Load(Path.Combine(root,"hand.ini"));
    Check(hand.NotUnderstood==1&&hand.Missing().Count==13,"a hand-written file reports the line it does not understand and the buttons with no key");

    Console.WriteLine(failed==0?"PASS: "+passed+" key map checks":"FAILED: "+failed+" of "+(passed+failed));
    return failed==0?0:1;
  }
}
