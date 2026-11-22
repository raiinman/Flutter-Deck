using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

// Tests for SaveCards.cs. Writes only inside the folder given as the first argument.
public static class SaveCardsPureTest {
  static int failed=0,passed=0;
  static void Check(bool ok,string what){if(ok)passed++;else{failed++;Console.WriteLine("FAIL: "+what);}}
  static bool Throws<T>(Action a) where T:Exception {try{a();}catch(T){return true;}catch{return false;}return false;}
  static byte[] Card(byte fill){var b=new byte[GameRuntime.CardBytes];for(int i=0;i<b.Length;i++)b[i]=fill;b[0]=(byte)'M';b[1]=(byte)'C';return b;}
  static string NewData(string root,string name){string data=Path.Combine(root,name,"games","mml1");Directory.CreateDirectory(data);return data;}
  static bool GameTempNameUsed(string data){return File.Exists(GameRuntime.CardPath(data)+".tmp")||Directory.GetFiles(data,"*.tmp",SearchOption.AllDirectories).Length>0;}
  static int Parts(string data){return Directory.GetFiles(data,"*.part",SearchOption.AllDirectories).Length;}
  public static int Main(string[] args){
    string root=Path.GetFullPath(args[0]);
    if(Directory.Exists(root)){foreach(string f in Directory.GetFiles(root,"*",SearchOption.AllDirectories))File.SetAttributes(f,FileAttributes.Normal);Directory.Delete(root,true);}
    Directory.CreateDirectory(root);

    // No card yet.
    string data=NewData(root,"a with spaces");
    Check(SaveCards.CardState(data)=="none"&&SaveCards.List(data).Count==0,"no card and no backups at first");
    Check(Throws<FileNotFoundException>(delegate{SaveCards.Backup(data);})&&!Directory.Exists(SaveCards.BackupsPath(data)),"backing up with no card is refused and creates nothing");

    // Backup.
    string card=GameRuntime.CardPath(data);File.WriteAllBytes(card,Card(1));
    string first=SaveCards.Backup(data);
    Check(SaveCards.CardState(data)=="ok"&&first.StartsWith("card-",StringComparison.Ordinal)&&first.EndsWith("-manual.mcd",StringComparison.Ordinal)&&GameRuntime.FileSha256(Path.Combine(SaveCards.BackupsPath(data),first))==GameRuntime.FileSha256(card),"a backup is an exact copy in the backups folder: "+first);
    string second=SaveCards.Backup(data);
    Check(second!=first&&Directory.GetFiles(SaveCards.BackupsPath(data),"*.mcd").Length==2,"a second backup in the same second gets its own name and replaces nothing: "+second);
    var list=SaveCards.List(data);
    Check(list.Count==2&&list[0].SameAsCurrent&&list[1].SameAsCurrent&&list[0].Kind=="manual"&&SaveCards.KindLabel("manual")=="Backup you made","backups are listed and marked as equal to the current card");
    Check(!GameTempNameUsed(data)&&Parts(data)==0,"no temporary file is left, and the game's own .tmp name is never used");

    // The game's card-at-start copies are listed; other files in the sessions folder are not.
    string sessions=GameRuntime.SessionsPath(data);Directory.CreateDirectory(sessions);
    File.WriteAllBytes(Path.Combine(sessions,"run1.card-at-start.mcd"),Card(2));File.SetLastWriteTimeUtc(Path.Combine(sessions,"run1.card-at-start.mcd"),DateTime.UtcNow.AddHours(-2));
    File.WriteAllBytes(Path.Combine(sessions,"other.mcd"),Card(3));File.WriteAllBytes(Path.Combine(sessions,"short.card-at-start.mcd"),new byte[100]);
    File.WriteAllBytes(Path.Combine(SaveCards.BackupsPath(data),"notes.txt"),new byte[3]);File.WriteAllBytes(Path.Combine(SaveCards.BackupsPath(data),"card-short-manual.mcd"),new byte[5]);
    list=SaveCards.List(data);
    Check(list.Count==3&&list[2].Kind=="session-start"&&list[2].Name=="run1.card-at-start.mcd"&&!list[2].SameAsCurrent,"a session's card-at-start copy is listed, oldest last; wrong-size and unrelated files are not: "+list.Count);

    // Import.
    string outside=Path.Combine(root,"outside");Directory.CreateDirectory(outside);
    string raw=Path.Combine(outside,"my card.mcd");File.WriteAllBytes(raw,Card(4));File.SetAttributes(raw,FileAttributes.ReadOnly);
    string imported=SaveCards.Import(raw,data);
    Check(imported.EndsWith("-imported.mcd",StringComparison.Ordinal)&&GameRuntime.FileSha256(Path.Combine(SaveCards.BackupsPath(data),imported))==GameRuntime.FileSha256(raw)&&(File.GetAttributes(Path.Combine(SaveCards.BackupsPath(data),imported))&FileAttributes.ReadOnly)==0&&File.ReadAllBytes(card)[5]==1,"a named card file is copied into the backups; the original and the current card are untouched");
    string gme=Path.Combine(outside,"save.gme");File.WriteAllBytes(gme,new byte[GameRuntime.GmeBytes]);string fakeGme=Path.Combine(outside,"raw-size.gme");File.WriteAllBytes(fakeGme,Card(5));string small=Path.Combine(outside,"small.mcd");File.WriteAllBytes(small,new byte[1000]);
    Check(Throws<InvalidDataException>(delegate{SaveCards.Import(gme,data);})&&Throws<InvalidDataException>(delegate{SaveCards.Import(fakeGme,data);})&&Throws<InvalidDataException>(delegate{SaveCards.Import(small,data);})&&Throws<FileNotFoundException>(delegate{SaveCards.Import(Path.Combine(outside,"missing.mcd"),data);})&&Throws<FileNotFoundException>(delegate{SaveCards.Import("",data);}),"a .gme save, a wrong-size file and a missing file are not imported");

    // Restore: only from the launcher's own two folders.
    string backups=SaveCards.BackupsPath(data);
    string sub=Path.Combine(backups,"sub");Directory.CreateDirectory(sub);File.WriteAllBytes(Path.Combine(sub,"deep.mcd"),Card(6));
    File.WriteAllBytes(Path.Combine(data,"beside.mcd"),Card(6));
    foreach(string bad in new[]{raw,Path.Combine(sub,"deep.mcd"),Path.Combine(data,"beside.mcd"),card,Path.Combine(backups,"..","beside.mcd"),Path.Combine(backups,"sub","..","..","card1.mcd"),Path.Combine(sessions,"other.mcd"),Path.Combine(sessions,"short.card-at-start.mcd"),Path.Combine(backups,"card-short-manual.mcd"),Path.Combine(backups,"notes.txt"),Path.Combine(backups,"missing.mcd"),"",null,"bad\0name",backups}){
      string candidate=bad;
      Check(!SaveCards.IsRestorable(candidate,data)&&Throws<InvalidDataException>(delegate{SaveCards.Restore(candidate,data,"");}),"refused as a restore source: "+(candidate??"(null)").Replace("\0","\\0"));
    }
    Check(File.ReadAllBytes(card)[5]==1,"after all the refusals the card is unchanged");
    Check(SaveCards.IsRestorable(Path.Combine(backups,"sub","..",imported),data)&&SaveCards.IsRestorable(Path.Combine(backups.ToUpperInvariant(),imported),data),"a path that still ends directly in the backups folder is accepted, whatever its spelling");

    // Restore: keeps the current card first, then replaces it.
    File.SetAttributes(card,FileAttributes.ReadOnly);
    int before=Directory.GetFiles(backups,"*.mcd").Length;
    string kept=SaveCards.Restore(Path.Combine(backups,imported),data,"");
    Check(File.ReadAllBytes(card)[5]==4&&kept.EndsWith("-before-restore.mcd",StringComparison.Ordinal)&&File.ReadAllBytes(Path.Combine(backups,kept))[5]==1&&Directory.GetFiles(backups,"*.mcd").Length==before+1,"restore replaces the card (also a read-only one) and keeps the old one as a before-restore backup");
    Check(File.Exists(Path.Combine(backups,imported))&&!GameTempNameUsed(data)&&Parts(data)==0,"the restored backup itself is kept; no temporary file is left; the game's .tmp name is never used");
    string kept2=SaveCards.Restore(Path.Combine(sessions,"run1.card-at-start.mcd"),data,"");
    Check(File.ReadAllBytes(card)[5]==2&&File.ReadAllBytes(Path.Combine(backups,kept2))[5]==4,"a session's card-at-start copy can be restored");
    list=SaveCards.List(data);bool marked=false;foreach(var b in list)if(b.Name=="run1.card-at-start.mcd"&&b.SameAsCurrent)marked=true;
    Check(marked,"after a restore the restored backup is marked as equal to the current card");

    // Restore is refused while a game uses the card, and nothing changes.
    using(var self=Process.GetCurrentProcess())File.WriteAllText(GameRuntime.LockPath(data),self.Id+"|"+self.StartTime.ToUniversalTime().Ticks+"|"+self.ProcessName);
    int count=Directory.GetFiles(backups,"*.mcd").Length;
    Check(GameRuntime.CardInUse(data)&&Throws<InvalidOperationException>(delegate{SaveCards.Restore(Path.Combine(backups,imported),data,"");})&&File.ReadAllBytes(card)[5]==2&&Directory.GetFiles(backups,"*.mcd").Length==count,"while a game uses the card, restore is refused and makes no copy");
    File.Delete(GameRuntime.LockPath(data));

    // Restore is refused while a start holds the per-card gate.
    var taken=new System.Threading.ManualResetEvent(false);var release=new System.Threading.ManualResetEvent(false);
    var holder=new System.Threading.Thread(delegate(){using(var gate=new System.Threading.Mutex(false,GameRuntime.GateName(data))){gate.WaitOne();taken.Set();release.WaitOne();gate.ReleaseMutex();}});
    holder.Start();taken.WaitOne();
    Check(Throws<InvalidOperationException>(delegate{SaveCards.Restore(Path.Combine(backups,imported),data,"");})&&File.ReadAllBytes(card)[5]==2&&Directory.GetFiles(backups,"*.mcd").Length==count,"while a game start holds the gate, restore is refused and makes no copy");
    release.Set();holder.Join();
    SaveCards.Restore(Path.Combine(backups,imported),data,"");Check(File.ReadAllBytes(card)[5]==4,"once the gate is free, restore works again");

    // A save file waiting for the first Play blocks restore.
    File.WriteAllBytes(GameRuntime.ImportPath(data),new byte[GameRuntime.GmeBytes]);
    SaveCards.Restore(Path.Combine(sessions,"run1.card-at-start.mcd"),data,"");
    Check(File.ReadAllBytes(card)[5]==2,"a .gme left over beside an existing card does not block restore");
    File.Delete(card);
    Check(Throws<InvalidOperationException>(delegate{SaveCards.Restore(Path.Combine(backups,imported),data,"");})&&!File.Exists(card),"a .gme still waiting for the first Play (no card yet) blocks restore");
    File.Delete(GameRuntime.ImportPath(data));
    SaveCards.Restore(Path.Combine(backups,imported),data,"");Check(File.ReadAllBytes(card)[5]==4,"with the waiting .gme gone, restore creates the card");

    // A backup is dated when it was made, even when the card is old.
    File.SetLastWriteTimeUtc(card,DateTime.UtcNow.AddDays(-400));
    string dated=SaveCards.Backup(data);
    Check(Math.Abs((File.GetLastWriteTimeUtc(Path.Combine(backups,dated))-DateTime.UtcNow).TotalMinutes)<5&&File.GetLastWriteTimeUtc(card)<DateTime.UtcNow.AddDays(-399)&&SaveCards.List(data)[0].Name==dated,"a backup of an old card is dated now, is listed first, and leaves the card's own time alone");

    // While restore runs, the card never disappears and no file with a tmp ending appears beside it.
    var seen=new List<string>();int missing=0;bool stop=false;
    using(var watch=new FileSystemWatcher(data){IncludeSubdirectories=true,EnableRaisingEvents=true}){
      watch.Created+=delegate(object o,FileSystemEventArgs e){lock(seen)seen.Add(e.Name);};watch.Renamed+=delegate(object o,RenamedEventArgs e){lock(seen){seen.Add(e.Name);seen.Add(e.OldName);}};
      var reader=new System.Threading.Thread(delegate(){while(!stop){if(!File.Exists(card))missing++;}});reader.Start();
      for(int i=0;i<40;i++)SaveCards.Restore(Path.Combine(i%2==0?sessions:backups,i%2==0?"run1.card-at-start.mcd":imported),data,"");
      stop=true;reader.Join();System.Threading.Thread.Sleep(300);
    }
    bool tmpSeen=false;lock(seen)foreach(string name in seen)if(name.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)||name.IndexOf("~RF",StringComparison.OrdinalIgnoreCase)>=0)tmpSeen=true;
    Check(missing==0&&!tmpSeen&&seen.Count>0,"during 40 restores the card was never missing and no file with a tmp ending was created (missing "+missing+", events "+seen.Count+")");

    // A backups folder that is a link to somewhere else is not a restore source.
    string data4=NewData(root,"d");File.WriteAllBytes(GameRuntime.CardPath(data4),Card(1));
    string elsewhere=Path.Combine(root,"elsewhere");Directory.CreateDirectory(elsewhere);File.WriteAllBytes(Path.Combine(elsewhere,"card-x-manual.mcd"),Card(9));
    var link=Process.Start(new ProcessStartInfo("cmd.exe","/c mklink /J \""+SaveCards.BackupsPath(data4)+"\" \""+elsewhere+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true});link.WaitForExit();
    string viaLink=Path.Combine(SaveCards.BackupsPath(data4),"card-x-manual.mcd");
    Check(File.Exists(viaLink)&&!SaveCards.IsRestorable(viaLink,data4)&&Throws<InvalidDataException>(delegate{SaveCards.Restore(viaLink,data4,"");})&&File.ReadAllBytes(GameRuntime.CardPath(data4))[5]==1,"a backup reached through a linked backups folder is refused");
    Directory.Delete(SaveCards.BackupsPath(data4)); // removes the link only, not the folder it points to

    // Part files left by a cut-off restore are cleared by the next restore; younger ones and other files are kept.
    string stale=card+"."+new string('a',32)+".part",young=card+"."+new string('b',32)+".part",foreign=Path.Combine(data,"notes.part");
    File.WriteAllBytes(stale,new byte[4]);File.SetLastWriteTimeUtc(stale,DateTime.UtcNow.AddHours(-3));File.WriteAllBytes(young,new byte[4]);File.WriteAllBytes(foreign,new byte[4]);File.SetLastWriteTimeUtc(foreign,DateTime.UtcNow.AddHours(-3));
    SaveCards.Restore(Path.Combine(sessions,"run1.card-at-start.mcd"),data,"");
    Check(!File.Exists(stale)&&File.Exists(young)&&File.Exists(foreign),"an old part file of this unit is removed; a recent one and a file of another name are kept");
    File.Delete(young);File.Delete(foreign);
    SaveCards.Restore(Path.Combine(backups,imported),data,"");

    // A game program that is running blocks restore even when no lock file names it (here: this test program itself).
    string me;using(var self2=Process.GetCurrentProcess())me=self2.MainModule.FileName;
    Check(Throws<InvalidOperationException>(delegate{SaveCards.Restore(Path.Combine(sessions,"run1.card-at-start.mcd"),data,me);})&&File.ReadAllBytes(card)[5]==4,"a running copy of the game program blocks restore without a lock file");

    // A damaged current card is kept, then repaired by restore. No current card: restore creates it.
    File.WriteAllBytes(card,new byte[77]);
    Check(SaveCards.CardState(data)=="damaged"&&Throws<InvalidDataException>(delegate{SaveCards.Backup(data);}),"a damaged card is reported and is not offered as a normal backup");
    string keptDamaged=SaveCards.Restore(Path.Combine(backups,imported),data,"");
    Check(keptDamaged.EndsWith("-damaged.mcd",StringComparison.Ordinal)&&new FileInfo(Path.Combine(backups,keptDamaged)).Length==77&&SaveCards.CardState(data)=="ok"&&File.ReadAllBytes(card)[5]==4,"restore keeps the damaged card and repairs the card");
    bool damagedListed=false;foreach(var b in SaveCards.List(data))if(b.Name==keptDamaged&&b.Kind=="damaged"&&!b.SameAsCurrent)damagedListed=true;
    Check(damagedListed&&!SaveCards.IsRestorable(Path.Combine(backups,keptDamaged),data),"the kept damaged card is listed but cannot be restored");
    File.Delete(card);
    Check(SaveCards.Restore(Path.Combine(backups,imported),data,"")==""&&File.ReadAllBytes(card)[5]==4,"with no card, restore creates it and reports no safety copy");

    // If the safety copy cannot be made, nothing is restored.
    string data2=NewData(root,"b");File.WriteAllBytes(GameRuntime.CardPath(data2),Card(7));
    string good=Path.Combine(GameRuntime.SessionsPath(data2),"s.card-at-start.mcd");Directory.CreateDirectory(GameRuntime.SessionsPath(data2));File.WriteAllBytes(good,Card(8));
    File.WriteAllText(SaveCards.BackupsPath(data2),"a file where the backups folder should be");
    Check(Throws<Exception>(delegate{SaveCards.Restore(good,data2,"");})&&File.ReadAllBytes(GameRuntime.CardPath(data2))[5]==7&&Parts(data2)==0,"when the current card cannot be kept first, the card is not replaced");

    // The list is capped.
    string data3=NewData(root,"c");Directory.CreateDirectory(SaveCards.BackupsPath(data3));
    for(int i=0;i<SaveCards.MaxListed+5;i++){string p=Path.Combine(SaveCards.BackupsPath(data3),"card-20260101-0000"+i.ToString("00")+"-manual.mcd");File.WriteAllBytes(p,Card(9));File.SetLastWriteTimeUtc(p,new DateTime(2026,1,1,0,0,0,DateTimeKind.Utc).AddMinutes(i));}
    var many=SaveCards.List(data3);
    Check(many.Count==SaveCards.MaxListed&&many[0].TimeUtc>many[many.Count-1].TimeUtc,"the list shows the newest backups only, newest first");

    Console.WriteLine(failed==0?"PASS: "+passed+" memory card backup checks":"FAILED: "+failed+" of "+(passed+failed));
    return failed==0?0:1;
  }
}
