using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Tests DiscFinder with small made-up disc images. No real disc image, game folder or launcher setting is touched.
// With "--fixture" as the second argument it only writes a sample game folder and stops, for a screen test.
static class DiscFinderPureTest {
  static int failed;
  static void Check(bool ok,string what){if(!ok){failed++;Console.WriteLine("FAIL: "+what);}}
  const string Program="ROCK_NEO.EXE";
  static string Boot; // when set, a SYSTEM.CNF entry gets this text as its data, in sector 19
  static void Put32(byte[] data,int at,uint value){data[at]=(byte)value;data[at+1]=(byte)(value>>8);data[at+2]=(byte)(value>>16);data[at+3]=(byte)(value>>24);}
  // Writes name.bin (20 sectors, a volume descriptor in sector 16, a root directory in sector 18) and, when asked, name.cue.
  static string Image(string folder,string name,string layout,string[] files,bool cue,string[] folders){
    Directory.CreateDirectory(folder);
    int size=layout.EndsWith("2352")?2352:layout.EndsWith("2336")?2336:2048,offset=layout=="MODE2/2352"?24:layout=="MODE1/2352"?16:layout=="MODE2/2336"?8:0;
    var image=new byte[20*size];int volume=16*size+offset,root=18*size+offset;
    image[volume]=1;Encoding.ASCII.GetBytes("CD001").CopyTo(image,volume+1);Put32(image,volume+158,18);Put32(image,volume+166,2048);
    int at=root;
    foreach(string entry in new[]{"\0","\u0001"}){image[at]=34;image[at+25]=2;image[at+32]=1;image[at+33]=(byte)entry[0];at+=34;}
    var all=new List<KeyValuePair<string,bool>>();
    foreach(string f in files)all.Add(new KeyValuePair<string,bool>(f,false));
    if(folders!=null)foreach(string f in folders)all.Add(new KeyValuePair<string,bool>(f,true));
    foreach(var entry in all){byte[] n=Encoding.ASCII.GetBytes(entry.Key);int length=33+n.Length;if(length%2==1)length++;image[at]=(byte)length;image[at+25]=(byte)(entry.Value?2:0);image[at+32]=(byte)n.Length;n.CopyTo(image,at+33);if(Boot!=null&&entry.Key.StartsWith("SYSTEM.CNF")){Put32(image,at+2,19);Put32(image,at+10,(uint)Boot.Length);Encoding.ASCII.GetBytes(Boot).CopyTo(image,19*size+offset);}at+=length;}
    File.WriteAllBytes(Path.Combine(folder,name+".bin"),image);
    string cuePath=Path.Combine(folder,name+".cue");
    if(cue)File.WriteAllText(cuePath,"FILE \""+name+".bin\" BINARY\r\n  TRACK 01 "+layout+"\r\n    INDEX 01 00:00:00\r\n");
    return cuePath;
  }
  static string Snapshot(string folder){
    var text=new StringBuilder();var names=new List<string>(Directory.GetFileSystemEntries(folder,"*",SearchOption.AllDirectories));names.Sort(StringComparer.Ordinal);
    foreach(string n in names){text.Append(n).Append('|');if(File.Exists(n))text.Append(new FileInfo(n).Length).Append('|').Append(File.GetLastWriteTimeUtc(n).Ticks);text.Append('\n');}
    return text.ToString();
  }
  static string Fixture(string root){
    string games=Path.Combine(root,"games");
    Image(Path.Combine(games,"Legends"),"Sample Legends disc","MODE2/2352",new[]{"SYSTEM.CNF;1","ROCK_NEO.EXE;1"},true,null);
    Image(Path.Combine(games,"Other game"),"Another disc","MODE2/2352",new[]{"SYSTEM.CNF;1","OTHER.EXE;1"},true,null);
    File.WriteAllBytes(Path.Combine(games,"loose image.iso"),new byte[4096]);
    return games;
  }
  static int Main(string[] args){
    string root=args[0];Directory.CreateDirectory(root);
    if(args.Length>1&&args[1]=="--fixture"){Console.WriteLine(Fixture(root));return 0;} // images with no disc code, so a screen test downloads nothing
    if(args.Length>1&&args[1]=="--fixture-code"){Boot="BOOT = cdrom:\\SLUS_006.03;1\r\nTCB = 4\r\nEVENT = 16\r\nSTACK = 801FFF00\r\n";Console.WriteLine(Fixture(root));return 0;} // images that carry a real disc code, for one deliberate download test
    // the disc code: read from the BOOT line of SYSTEM.CNF, in every sector layout
    foreach(string layout in new[]{"MODE2/2352","MODE1/2352","MODE1/2048","MODE2/2336"}){
      Boot="BOOT = cdrom:\\SLUS_006.03;1\r\nTCB = 4\r\n";string coded=Image(Path.Combine(root,"code-"+layout.Replace('/','-')),"coded",layout,new[]{"SYSTEM.CNF;1","ROCK_NEO.EXE;1"},true,null);
      Check(DiscFinder.DiscCode(coded)=="SLUS-00603"&&DiscFinder.HasProgram(coded,Program),"disc code read, layout "+layout+": "+DiscFinder.DiscCode(coded));
    }
    Boot=null;string plain=Image(Path.Combine(root,"code-none"),"plain","MODE2/2352",new[]{"SYSTEM.CNF;1"},true,null);Check(DiscFinder.DiscCode(plain)==""&&DiscFinder.DiscCode(Path.Combine(root,"absent.cue"))==""&&DiscFinder.DiscCode("")==""&&DiscFinder.DiscCode(Image(Path.Combine(root,"code-none"),"nofile","MODE2/2352",new[]{"OTHER.EXE;1"},true,null))=="","an empty SYSTEM.CNF, no SYSTEM.CNF, no image: no code");
    Boot="BOOT = cdrom:\\SLUS_006.03;1\r\n";string beyond=Image(Path.Combine(root,"code-none"),"beyond","MODE2/2352",new[]{"SYSTEM.CNF;1"},true,null);byte[] bb=File.ReadAllBytes(Path.Combine(root,@"code-none\beyond.bin"));for(int i=0;i<2048-40;i++)if(bb[18*2352+24+i+32]==12&&bb[18*2352+24+i+33]==(byte)'S'){Put32(bb,18*2352+24+i+2,0xFFFFFF00);break;}File.WriteAllBytes(Path.Combine(root,@"code-none\beyond.bin"),bb);Check(DiscFinder.DiscCode(beyond)=="","a SYSTEM.CNF placed outside the image gives no code");Boot=null;
    foreach(var pair in new[]{new[]{"BOOT = cdrom:\\SLUS_006.03;1\r\n","SLUS-00603"},new[]{"BOOT=cdrom:SLUS_011.40;1","SLUS-01140"},new[]{"boot = cdrom:\\slps_011.41;1\n","SLPS-01141"},new[]{"TCB = 4\r\nBOOT  =  cdrom:\\\\SCES_123.45;1\r\n","SCES-12345"},new[]{"BOOT = cdrom:\\EXE\\SLUS_006.03;1","SLUS-00603"},new[]{"BOOT = cdrom:\\SLUS-006.03;1","SLUS-00603"}})Check(DiscFinder.CodeFromSystemCnf(pair[0])==pair[1],"code from boot line: "+pair[1]+" got "+DiscFinder.CodeFromSystemCnf(pair[0]));
    foreach(string noCode in new[]{"","TCB = 4\r\n","BOOT = cdrom:\\PSX.EXE;1","BOOT = cdrom:\\SLUS_006.03X;1","BOOT = cdrom:\\SLUS_0060.3;1","BOOT = cdrom:\\..\\..\\x;1","BOOT = cdrom:\\SLUS_006.03/../../x","BOOT = http://example.invalid/SLUS_006.03;1",null})Check(DiscFinder.CodeFromSystemCnf(noCode)=="","no code from: "+(noCode??"null"));
    // the program is found in every sector layout a CUE file can name
    foreach(string layout in new[]{"MODE2/2352","MODE1/2352","MODE1/2048","MODE2/2336"}){
      string dir=Path.Combine(root,"layout-"+layout.Replace('/','-'));
      Check(DiscFinder.HasProgram(Image(dir,"with",layout,new[]{"SYSTEM.CNF;1","ROCK_NEO.EXE;1"},true,null),Program),"program found, layout "+layout);
      Check(!DiscFinder.HasProgram(Image(dir,"without",layout,new[]{"SYSTEM.CNF;1","OTHER.EXE;1","ROCK_NEO.EX;1","XROCK_NEO.EXE;1"},true,null),Program),"another disc is not a match, layout "+layout);
    }
    string odd=Path.Combine(root,"odd");
    Check(DiscFinder.HasProgram(Image(odd,"lower","MODE2/2352",new[]{"rock_neo.exe"},true,null),Program),"the name is compared without case and without a version");
    Check(!DiscFinder.HasProgram(Image(odd,"folder","MODE2/2352",new[]{"SYSTEM.CNF;1"},true,new[]{"ROCK_NEO.EXE"}),Program),"a folder with the name is not the program");
    // the layout is taken from the CUE file: the right image with the wrong TRACK line is not read at a guessed offset
    string wrong=Image(odd,"wrongline","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);File.WriteAllText(wrong,"FILE \"wrongline.bin\" BINARY\r\n  TRACK 01 MODE1/2048\r\n");
    Check(!DiscFinder.HasProgram(wrong,Program),"the sector layout comes from the TRACK line");
    string audio=Image(odd,"audio","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);File.WriteAllText(audio,"FILE \"audio.bin\" BINARY\r\n  TRACK 01 AUDIO\r\n  TRACK 02 MODE2/2352\r\n");
    Check(!DiscFinder.HasProgram(audio,Program),"a first track that is audio is not searched");
    string wave=Image(odd,"wave","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);File.WriteAllText(wave,"FILE \"wave.bin\" WAVE\r\n  TRACK 01 MODE2/2352\r\n");
    Check(!DiscFinder.HasProgram(wave,Program),"a file that is not BINARY is not searched");
    string gone=Image(odd,"gone","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);File.Delete(Path.Combine(odd,"gone.bin"));
    Check(!DiscFinder.HasProgram(gone,Program),"a CUE file whose track is missing is not a match");
    string cut=Image(odd,"cut","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);using(var s=new FileStream(Path.Combine(odd,"cut.bin"),FileMode.Open)){s.SetLength(17*2352);}
    Check(!DiscFinder.HasProgram(cut,Program),"an image cut short before its directory is not a match");
    string blank=Image(odd,"blank","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);File.WriteAllBytes(Path.Combine(odd,"blank.bin"),new byte[20*2352]);
    Check(!DiscFinder.HasProgram(blank,Program),"an image with no volume descriptor is not a match");
    // junk in the directory or in the descriptor is not a match and throws nothing
    string far=Image(odd,"far","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);byte[] farBytes=File.ReadAllBytes(Path.Combine(odd,"far.bin"));Put32(farBytes,16*2352+24+158,0xFFFFFFF0);Put32(farBytes,16*2352+24+166,0xFFFFFFFF);File.WriteAllBytes(Path.Combine(odd,"far.bin"),farBytes);
    Check(!DiscFinder.HasProgram(far,Program),"a directory outside the image is not read");
    string junk=Image(odd,"junk","MODE2/2352",new string[0],true,null);byte[] junkBytes=File.ReadAllBytes(Path.Combine(odd,"junk.bin"));for(int i=0;i<2048;i++)junkBytes[18*2352+24+i]=0xFF;File.WriteAllBytes(Path.Combine(odd,"junk.bin"),junkBytes);
    Check(!DiscFinder.HasProgram(junk,Program),"a directory of junk is not a match");
    Check(!DiscFinder.HasProgram(Path.Combine(odd,"absent.cue"),Program)&&!DiscFinder.HasProgram(Path.Combine(odd,"lower.bin"),Program)&&!DiscFinder.HasProgram(Path.Combine(odd,"lower.cue"),""),"a missing file, a BIN file and an empty name are not matches");
    // the search: depth, counts, order, files without a CUE file, and nothing written
    string games=Path.Combine(root,"scan");
    Image(Path.Combine(games,"b"),"Legends copy","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);
    Image(Path.Combine(games,"a"),"Legends","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);
    Image(Path.Combine(games,"c"),"Other","MODE2/2352",new[]{"OTHER.EXE;1"},true,null);
    Image(Path.Combine(games,@"d\e\f"),"Deep","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);
    Image(Path.Combine(games,@"d\e\f\g"),"Too deep","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);
    Image(games,"No cue","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},false,null);File.WriteAllBytes(Path.Combine(games,"loose.iso"),new byte[2048]);File.WriteAllText(Path.Combine(games,"notes.txt"),"x");
    string before=Snapshot(games);
    var scan=DiscFinder.Scan(games,Program);
    Check(Snapshot(games)==before,"the search changes nothing in the folder");
    Check(scan.Problem.Length==0&&scan.Matches.Count==3&&scan.Matches[0].EndsWith(@"a\Legends.cue")&&scan.Matches[1].EndsWith(@"b\Legends copy.cue")&&scan.Matches[2].EndsWith(@"d\e\f\Deep.cue"),"three matches in name order, down to three folders deep: "+String.Join(" | ",scan.Matches.ToArray()));
    Check(scan.Cues==4&&scan.Bare==2&&!scan.Truncated&&scan.Deeper&&scan.Partial&&!DiscFinder.Scan(Path.Combine(games,"a"),Program).Partial,"four CUE files opened; two image files have no CUE file (a BIN file a CUE file names is not counted): "+scan.Cues+" "+scan.Bare);
    Check(DiscFinder.Scan(Path.Combine(root,"nowhere"),Program).Problem.Contains("not found")&&DiscFinder.Scan(@"\\server\share\games",Program).Problem.Contains("drive of this computer")&&DiscFinder.Scan("games",Program).Problem.Length>0&&DiscFinder.Scan("",Program).Problem.Length>0,"a missing, network, relative or empty folder is a problem, not a crash");
    // more CUE files than the limit: the search stops and says so
    string many=Path.Combine(root,"many");Directory.CreateDirectory(many);for(int i=0;i<DiscFinder.MaxCues+5;i++)File.WriteAllText(Path.Combine(many,"n"+i.ToString("000")+".cue"),"junk");
    var big=DiscFinder.Scan(many,Program);Check(big.Truncated&&big.Cues==DiscFinder.MaxCues&&big.Matches.Count==0,"the search stops at the CUE limit and says it was cut");
    string crowd=Path.Combine(root,"crowd");for(int i=0;i<DiscFinder.MaxMatches+2;i++)Image(crowd,"m"+i.ToString("00"),"MODE1/2048",new[]{"ROCK_NEO.EXE;1"},true,null);
    var full=DiscFinder.Scan(crowd,Program);Check(full.Truncated&&full.Matches.Count==DiscFinder.MaxMatches,"at most "+DiscFinder.MaxMatches+" matches are listed");
    // what is done with the result
    var none=new List<string>();var one=new List<string>{"x"};var two=new List<string>{"x","y"};
    Check(DiscFinder.Decide(true,two,false)==DiscFinder.Keep&&DiscFinder.Decide(true,one,false)==DiscFinder.Keep&&DiscFinder.Decide(true,none,true)==DiscFinder.Keep,"a saved image that works is kept, whatever was found");
    Check(DiscFinder.Decide(false,one,false)==DiscFinder.Use&&DiscFinder.Decide(false,two,false)==DiscFinder.Choose&&DiscFinder.Decide(false,none,false)==DiscFinder.None&&DiscFinder.Decide(false,null,false)==DiscFinder.None,"one match is used, several are offered, none does nothing");
    Check(DiscFinder.Decide(false,one,true)==DiscFinder.Choose&&DiscFinder.Decide(false,none,true)==DiscFinder.None,"after a partial search one match is offered, not used");
    // an image below the depth limit is not found, and the search says that deeper folders exist
    string deep=Path.Combine(root,"deep");Image(Path.Combine(deep,@"1\2\3\4"),"Hidden","MODE2/2352",new[]{"ROCK_NEO.EXE;1"},true,null);var low=DiscFinder.Scan(deep,Program);
    Check(low.Matches.Count==0&&low.Deeper&&!low.Truncated&&DiscFinder.Describe(low,Program)=="No disc image of this game was found in this folder. Folders more than 3 levels below the game folder were not searched.","deeper folders are reported: "+DiscFinder.Describe(low,Program));
    // a folder with more entries than the limit: the search stops, says so, and one match is then not enough to set the image
    string wide=Path.Combine(root,"wide");Image(wide,"aaa","MODE1/2048",new[]{"ROCK_NEO.EXE;1"},true,null);for(int i=0;i<DiscFinder.MaxEntries+10;i++)File.WriteAllBytes(Path.Combine(wide,"z"+i+".dat"),new byte[0]);Image(Path.Combine(wide,"sub"),"second","MODE1/2048",new[]{"ROCK_NEO.EXE;1"},true,null);
    var cutScan=DiscFinder.Scan(wide,Program);Check(cutScan.Truncated&&cutScan.Matches.Count<=1&&DiscFinder.Decide(false,cutScan.Matches,cutScan.Partial)!=DiscFinder.Use,"a search cut by the entry limit never sets an image by itself: "+cutScan.Matches.Count);
    // the sentences
    Check(DiscFinder.Describe(scan,Program)=="Found 3 disc images of this game. 2 BIN, ISO or IMG files have no CUE file beside them. The game needs a CUE disc image, so Flutter Deck cannot use them. Folders more than 3 levels below the game folder were not searched.","sentence for several matches: "+DiscFinder.Describe(scan,Program));
    var lonely=new DiscScan();lonely.Cues=1;lonely.Bare=1;
    Check(DiscFinder.Describe(lonely,Program)=="No disc image of this game was found in this folder. 1 CUE image was checked; none holds ROCK_NEO.EXE. 1 BIN, ISO or IMG file has no CUE file beside it. The game needs a CUE disc image, so Flutter Deck cannot use it.","sentence for no match: "+DiscFinder.Describe(lonely,Program));
    var single=new DiscScan();single.Matches.Add("x");single.Cues=1;single.Truncated=true;
    Check(DiscFinder.Describe(single,Program)=="Found 1 disc image of this game. The folder is large, so only part of it was searched."&&DiscFinder.Describe(DiscFinder.Scan("",Program),Program).Contains("Choose a folder"),"sentences for one match, a cut search and a problem");
    // the setting
    string setting=Path.Combine(root,@"settings\game-folder.txt");
    Check(DiscFinder.LoadFolder(setting)=="","no setting means no folder");
    DiscFinder.SaveFolder(setting,games);Check(DiscFinder.LoadFolder(setting)==games&&File.ReadAllText(setting)=="1\r\n"+games+"\r\n","the folder is saved and read back");
    DiscFinder.SaveFolder(setting,odd);Check(DiscFinder.LoadFolder(setting)==odd&&File.ReadAllText(setting+".bak").Contains(games)&&Directory.GetFiles(Path.GetDirectoryName(setting),"*.tmp").Length==0,"a second save keeps the one before and leaves no temporary file");
    DiscFinder.SaveFolder(setting,"");Check(DiscFinder.LoadFolder(setting)=="","an empty folder can be saved");
    foreach(string bad in new[]{"2\r\nC:\\x\r\n","C:\\x\r\n",new string('x',DiscFinder.MaxSettingBytes+1)}){File.WriteAllText(setting,bad);bool threw=false;try{DiscFinder.LoadFolder(setting);}catch(InvalidDataException){threw=true;}Check(threw,"a setting of another format or size is an error");}
    foreach(string notFolder in new[]{"1\r\n","1\r\ngames\r\n","1\r\n\\\\server\\share\r\n","1\r\nC:\\a<b\r\n"}){File.WriteAllText(setting,notFolder);Check(DiscFinder.LoadFolder(setting)=="","a folder line that is not a full local path means no folder");}
    File.WriteAllText(setting," \r\n");Check(DiscFinder.LoadFolder(setting)=="","a blank setting means no folder");
    bool refused=false;try{DiscFinder.SaveFolder(setting,@"\\server\share");}catch(ArgumentException){refused=true;}Check(refused,"a network folder is not saved");
    // the test seam
    string old=Environment.GetEnvironmentVariable(DiscFinder.OverrideVariable);
    try{
      Environment.SetEnvironmentVariable(DiscFinder.OverrideVariable,Path.Combine(root,"seam.txt"));Check(DiscFinder.PathName==Path.Combine(root,"seam.txt"),"the test variable moves the setting");
      foreach(string ignored in new[]{"seam.txt",@"\\server\share\seam.txt",Path.Combine(root,"seam.xml"),""}){Environment.SetEnvironmentVariable(DiscFinder.OverrideVariable,ignored);Check(DiscFinder.PathName==Path.Combine(DeckPaths.Root,"game-folder.txt"),"a test variable that is not a full local .txt path is ignored");}
    }finally{Environment.SetEnvironmentVariable(DiscFinder.OverrideVariable,old);}
    if(failed>0){Console.WriteLine(failed+" check(s) failed");return 1;}
    Console.WriteLine("PASS: program found in four sector layouts; other discs, wrong layouts, cut and junk images refused; bounded read-only search; decision; sentences; setting round trip; test seam");
    return 0;
  }
}
