using System;
using System.Collections.Generic;
using System.IO;

// Tests of TeleportCore.cs: the place list, the request line and the request file. No window, no game program.
public static class TeleportPureTest {
  static int pass,fail;
  static void Check(bool ok,string what){if(ok)pass++;else{fail++;Console.WriteLine("FAIL: "+what);}}
  public static int Main(string[] args){
    string dir=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"fd-teleport-"+Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(dir);
    TeleportPlace p;
    Check(TeleportCore.TryParseLine("dest 19 5 -4864 -769 -1344 2048  from 14,19",out p)&&p.Stage==19&&p.Room==5&&p.X==-4864&&p.Y==-769&&p.Z==-1344&&p.Facing==2048&&p.From=="14,19","a good line is read in full");
    Check(TeleportCore.TryParseLine("dest 0 0 0 0 0 0",out p)&&p.From=="","a line without a source is read");
    Check(!TeleportCore.TryParseLine("# comment",out p)&&!TeleportCore.TryParseLine("",out p)&&!TeleportCore.TryParseLine(null,out p),"comments, empty and null lines are skipped");
    Check(!TeleportCore.TryParseLine("dest 19 5 1 2 3",out p),"a line with five numbers is skipped");
    Check(!TeleportCore.TryParseLine("dest 1 0 0 0 0 0",out p),"stage 1 (the title and the opening text) is skipped");
    Check(!TeleportCore.TryParseLine("dest 31 0 0 0 0 0",out p)&&!TeleportCore.TryParseLine("dest 3 30 0 0 0 0",out p),"stage 31 and room 30 are skipped");
    Check(!TeleportCore.TryParseLine("dest 3 1 40000 0 0 0",out p)&&!TeleportCore.TryParseLine("dest 3 1 0 -40000 0 0",out p),"numbers that do not fit 16 bits are skipped");
    Check(!TeleportCore.TryParseLine("dest 3 1 0 0 0 4096",out p)&&!TeleportCore.TryParseLine("dest 3 1 0 0 0 -1",out p),"a facing outside 0 to 4095 is skipped");
    Check(!TeleportCore.TryParseLine("dest a b c d e f",out p),"words instead of numbers are skipped");
    Check(TeleportCore.TryParseLine("dest 30 29 32767 -32768 32767 4095",out p),"the largest allowed numbers are accepted");

    string table=Path.Combine(dir,"teleport-table.txt");
    File.WriteAllText(table,"# header\ndest 19 0 -256 -1 -1856 2048  from 19\ndest 19 5 -4864 -769 -1344 2048  from 19\ndest 19 5 256 -1 704 2048  from 19\ndest 3 2 1792 -1 15104 0  from 3\nbroken line\ndest 99 0 0 0 0 0\n");
    var all=TeleportCore.Load(table);
    Check(all.Count==4,"four good places are read, two bad lines skipped (got "+all.Count+")");
    var st=TeleportCore.Stages(all);Check(st.Count==2&&st[0]==3&&st[1]==19,"stages are listed in order, without repeats");
    var rm=TeleportCore.Rooms(all,19);Check(rm.Count==2&&rm[0]==0&&rm[1]==5,"rooms of stage 19: 0 and 5");
    Check(TeleportCore.Entries(all,19,5).Count==2&&TeleportCore.Entries(all,19,0).Count==1&&TeleportCore.Entries(all,4,0).Count==0,"entries per room");
    Check(TeleportCore.Load(Path.Combine(dir,"missing.txt")).Count==0&&TeleportCore.Load(null).Count==0,"a missing table or no path gives an empty list");
    Check(TeleportCore.StageName(19)=="Clozer Woods Sub-Gate"&&TeleportCore.StageName(7)=="Stage 7","named stages have names, the rest are numbered");
    Check(TeleportCore.EntryText(all[0],0)=="Entry 1 (from Clozer Woods Sub-Gate)"&&TeleportCore.EntryText(new TeleportPlace(),1)=="Entry 2","entry text, with and without a source");

    var place=TeleportCore.Entries(all,19,5)[0];
    Check(TeleportCore.RequestLine(place)=="19 5 -4864 -769 -1344 2048","the request line has the six numbers the game program reads");
    string req=Path.Combine(dir,"sub","teleport-request.txt");
    Check(TeleportCore.WriteRequest(req,place)==""&&File.ReadAllText(req)=="19 5 -4864 -769 -1344 2048\n","the request is written (the folder is made)");
    Check(!File.Exists(req+".tmp"),"no temporary file is left");
    Check(TeleportCore.WriteRequest(req,place).Length>0&&File.ReadAllText(req)=="19 5 -4864 -769 -1344 2048\n","a second request is refused while the first is not picked up, and the first stays");
    File.Delete(req);
    Check(TeleportCore.WriteRequest(null,place).Length>0&&TeleportCore.WriteRequest(req,null).Length>0,"no path or no place is refused");
    Console.WriteLine((fail==0?"PASS":"FAIL")+": "+pass+" teleport checks passed, "+fail+" failed");
    try{Directory.Delete(dir,true);}catch(Exception){}
    return fail==0?0:1;
  }
}
