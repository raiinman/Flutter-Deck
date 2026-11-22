using System;
using System.Collections.Generic;
using System.IO;

// Backups of the launcher's own memory card for the game, and restoring one of them. See SPEC-saves.md.
// Rules: a backup is a verified copy and is never overwritten; Restore is the only thing in the launcher that
// replaces the card, it keeps a copy of the current card first, it accepts only a backup from the launcher's
// own two folders, and it runs inside the same per-card gate as starting the game, never while a game runs.
// The card's name with a tmp ending belongs to the game (it is unsaved progress), so this file never creates
// it: its own temporary files end in "part".
public sealed class CardBackup {
  public string Path,Name,Kind; // Kind: manual, imported, before-restore, damaged, session-start
  public DateTime TimeUtc;
  public bool SameAsCurrent;
}
public static class SaveCards {
  [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true,CharSet=System.Runtime.InteropServices.CharSet.Unicode)]
  static extern bool MoveFileEx(string existing,string replacement,int flags);
  // Renames the part file over the card in one step (replace existing, write through): there is no moment with
  // no card, and no extra file is created beside it.
  static void ReplaceInOneStep(string part,string card){
    // Another program (a virus scanner reading the card just written, for example) can hold the card for a moment.
    // "Access denied" and "in use" are retried for about two seconds; the card stays whole throughout.
    for(int attempt=0;;attempt++){
      if(MoveFileEx(part,card,0x1|0x8))return;
      int error=System.Runtime.InteropServices.Marshal.GetLastWin32Error();
      if((error!=5&&error!=32)||attempt>=40)throw new IOException("The card could not be replaced.",new System.ComponentModel.Win32Exception(error));
      System.Threading.Thread.Sleep(50);
    }
  }
  // Part files left by a restore or backup that was cut off (for example by a power loss). Only this unit's own
  // names are removed: "<name>.<32 hex digits>.part", older than one hour.
  static void RemoveStaleParts(string folder){
    try{
      if(!Directory.Exists(folder))return;
      foreach(string path in Directory.GetFiles(folder,"*.part")){
        try{
          string name=Path.GetFileName(path);string[] pieces=name.Split('.');
          if(pieces.Length<3||pieces[pieces.Length-2].Length!=32)continue;
          bool hex=true;foreach(char ch in pieces[pieces.Length-2])if(!Uri.IsHexDigit(ch))hex=false;
          if(hex&&File.GetLastWriteTimeUtc(path)<DateTime.UtcNow.AddHours(-1))File.Delete(path);
        }catch(Exception){}
      }
    }catch(Exception){}
  }
  public const int MaxListed=60;
  const string SessionSuffix=".card-at-start.mcd";
  public static string BackupsPath(string data){return Path.Combine(data,"backups");}
  // "none", "ok" or "damaged" (a card file of the wrong size).
  public static string CardState(string data){
    try{string card=GameRuntime.CardPath(data);if(!File.Exists(card))return "none";return new FileInfo(card).Length==GameRuntime.CardBytes?"ok":"damaged";}
    catch(Exception){return "damaged";}
  }
  static string NewName(string folder,DateTime utc,string kind){
    string stem="card-"+utc.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture)+"-"+kind;
    string path=Path.Combine(folder,stem+".mcd");
    for(int n=2;File.Exists(path)&&n<1000;n++)path=Path.Combine(folder,stem+"-"+n+".mcd");
    return path;
  }
  // Copies through a uniquely named part file, checks the copy against the original, and never replaces a file.
  static void CopyVerified(string source,string target){
    Directory.CreateDirectory(Path.GetDirectoryName(target));
    string part=target+"."+Guid.NewGuid().ToString("N")+".part";
    try{
      File.Copy(source,part,false);File.SetAttributes(part,FileAttributes.Normal);
      if(GameRuntime.FileSha256(part)!=GameRuntime.FileSha256(source))throw new IOException("The copy did not match the original.");
      File.SetLastWriteTimeUtc(part,DateTime.UtcNow); // a backup is dated when it was made
      File.Move(part,target); // fails rather than replaces
    }finally{try{if(File.Exists(part))File.Delete(part);}catch(Exception){}}
  }
  // Keeps a copy of the current card. Returns the new backup's file name.
  public static string Backup(string data){return Path.GetFileName(BackupAs(data,"manual",false));}
  static string BackupAs(string data,string kind,bool evenIfDamaged){
    string card=GameRuntime.CardPath(data);
    if(!File.Exists(card))throw new FileNotFoundException("There is no memory card to back up yet.");
    if(!evenIfDamaged&&new FileInfo(card).Length!=GameRuntime.CardBytes)throw new InvalidDataException("The memory card file is damaged (wrong size).");
    string target=NewName(BackupsPath(data),DateTime.UtcNow,kind);
    CopyVerified(card,target);return target;
  }
  // Copies a card file the user names into the backups, where Restore can use it. The original is only read.
  // Raw cards only: a .gme save is a different format and is not converted here.
  public static string Import(string source,string data){
    if(String.IsNullOrWhiteSpace(source)||!File.Exists(source))throw new FileNotFoundException("Memory card file not found.");
    if(String.Equals(Path.GetExtension(source),".gme",StringComparison.OrdinalIgnoreCase)||new FileInfo(source).Length!=GameRuntime.CardBytes)throw new InvalidDataException("Not a raw memory card: it must be exactly 131,072 bytes. A .gme save can only be copied in before the first Play, on the setup and readiness screen.");
    string target=NewName(BackupsPath(data),DateTime.UtcNow,"imported");
    CopyVerified(source,target);return Path.GetFileName(target);
  }
  static string KindOf(string name){
    if(name.EndsWith(SessionSuffix,StringComparison.OrdinalIgnoreCase))return "session-start";
    foreach(string kind in new[]{"before-restore","damaged","imported","manual"})if(name.IndexOf("-"+kind,StringComparison.OrdinalIgnoreCase)>=0)return kind;
    return "manual";
  }
  public static string KindLabel(string kind){
    switch(kind){case "session-start":return "Card as it was when a game session began";case "before-restore":return "Card kept before a restore";case "damaged":return "Damaged card kept before a restore";case "imported":return "Card file you added";default:return "Backup you made";}
  }
  // Newest first: the launcher's backups, and the card-at-start copies the game leaves in the sessions folder.
  // Only whole cards are listed, except a kept damaged card, which is listed so it is not forgotten.
  public static List<CardBackup> List(string data){
    var found=new List<CardBackup>();string current=null;
    try{if(CardState(data)=="ok")current=GameRuntime.FileSha256(GameRuntime.CardPath(data));}catch(Exception){}
    foreach(string folder in new[]{BackupsPath(data),GameRuntime.SessionsPath(data)}){
      string[] files;try{if(!Directory.Exists(folder))continue;files=Directory.GetFiles(folder,"*.mcd");}catch(Exception){continue;}
      bool sessions=folder==GameRuntime.SessionsPath(data);
      foreach(string path in files){
        try{
          string name=Path.GetFileName(path);var info=new FileInfo(path);
          if(sessions&&!name.EndsWith(SessionSuffix,StringComparison.OrdinalIgnoreCase))continue;
          string kind=sessions?"session-start":KindOf(name);
          if(info.Length!=GameRuntime.CardBytes&&kind!="damaged")continue;
          found.Add(new CardBackup{Path=path,Name=name,Kind=kind,TimeUtc=info.LastWriteTimeUtc});
        }catch(Exception){}
      }
    }
    found.Sort(delegate(CardBackup a,CardBackup b){int byTime=b.TimeUtc.CompareTo(a.TimeUtc);return byTime!=0?byTime:String.CompareOrdinal(a.Name,b.Name);});
    if(found.Count>MaxListed)found.RemoveRange(MaxListed,found.Count-MaxListed);
    if(current!=null)foreach(var b in found){try{b.SameAsCurrent=new FileInfo(b.Path).Length==GameRuntime.CardBytes&&GameRuntime.FileSha256(b.Path)==current;}catch(Exception){}}
    return found;
  }
  // True only for a whole card file that sits directly in the launcher's backups or sessions folder.
  public static bool IsRestorable(string backupPath,string data){
    try{
      if(String.IsNullOrWhiteSpace(backupPath)||backupPath.IndexOfAny(Path.GetInvalidPathChars())>=0)return false;
      string full=Path.GetFullPath(backupPath),folder=Path.GetDirectoryName(full),name=Path.GetFileName(full);
      string backups=Path.GetFullPath(BackupsPath(data)).TrimEnd('\\'),sessions=Path.GetFullPath(GameRuntime.SessionsPath(data)).TrimEnd('\\');
      bool inBackups=String.Equals(folder,backups,StringComparison.OrdinalIgnoreCase),inSessions=String.Equals(folder,sessions,StringComparison.OrdinalIgnoreCase);
      if(!inBackups&&!inSessions)return false;
      if(inSessions&&!name.EndsWith(SessionSuffix,StringComparison.OrdinalIgnoreCase))return false;
      if(!name.EndsWith(".mcd",StringComparison.OrdinalIgnoreCase)||!File.Exists(full))return false;
      var info=new FileInfo(full);
      if((info.Attributes&FileAttributes.ReparsePoint)!=0||(new DirectoryInfo(folder).Attributes&FileAttributes.ReparsePoint)!=0)return false;
      return info.Length==GameRuntime.CardBytes;
    }catch(Exception){return false;}
  }
  // Replaces the launcher's card with a backup. The current card is copied to the backups first; if that copy
  // fails, nothing is restored. Returns the file name of that safety copy, or "" when there was no card.
  // Throws InvalidOperationException with a message for the user when it must not run now.
  public static string Restore(string backupPath,string data,string program){
    if(!IsRestorable(backupPath,data))throw new InvalidDataException("That is not a memory card backup Flutter Deck can restore.");
    string source=Path.GetFullPath(backupPath),card=GameRuntime.CardPath(data),kept="";
    // A save file copied in before the first Play becomes the card at that Play. Until then a restored card would
    // silently cancel it. Once a card exists the save file is no longer used, so it blocks nothing.
    if(File.Exists(GameRuntime.ImportPath(data))&&!File.Exists(card))throw new InvalidOperationException("A save file is waiting to be turned into the card at the first Play. Press Play once before restoring a backup.");
    using(var gate=new System.Threading.Mutex(false,GameRuntime.GateName(data))){
      bool held=false;
      try{
        try{held=gate.WaitOne(0);}catch(System.Threading.AbandonedMutexException){held=true;}
        if(!held)throw new InvalidOperationException("The game is starting or stopping. Try again in a moment.");
        if(GameRuntime.CardInUse(data)||(!String.IsNullOrEmpty(program)&&GameRuntime.AlreadyRunning(program)))throw new InvalidOperationException("A game is using the memory card. Close the game first.");
        if(File.Exists(card))kept=Path.GetFileName(BackupAs(data,new FileInfo(card).Length==GameRuntime.CardBytes?"before-restore":"damaged",true));
        RemoveStaleParts(data);RemoveStaleParts(BackupsPath(data));
        string part=card+"."+Guid.NewGuid().ToString("N")+".part";bool wasReadOnly=false;
        try{
          File.Copy(source,part,false);File.SetAttributes(part,FileAttributes.Normal);
          if(GameRuntime.FileSha256(part)!=GameRuntime.FileSha256(source)||new FileInfo(part).Length!=GameRuntime.CardBytes)throw new IOException("The copy did not match the backup.");
          if(File.Exists(card)){var attributes=File.GetAttributes(card);wasReadOnly=(attributes&FileAttributes.ReadOnly)!=0;if(wasReadOnly)File.SetAttributes(card,attributes&~FileAttributes.ReadOnly);}
          try{ReplaceInOneStep(part,card);}
          catch{try{if(wasReadOnly&&File.Exists(card))File.SetAttributes(card,File.GetAttributes(card)|FileAttributes.ReadOnly);}catch(Exception){}throw;}
        }finally{try{if(File.Exists(part))File.Delete(part);}catch(Exception){}}
      }finally{if(held)gate.ReleaseMutex();}
    }
    return kept;
  }
}
