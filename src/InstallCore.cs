using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

// File operations for Setup and Uninstall. No UI, registry or shortcuts here, and every folder is a
// parameter, so the same code is tested on scratch folders (see InstallPureTest.cs).
// Rules: per-user only; never delete recursively; only ever remove files this installer or the
// updater is known to create; never remove the settings folder unless the user ticked the box.
// The build tools Setup carries: zips unpacked into a folder of "builder", and single files.
public sealed class BuilderPart { public string Folder=""; public bool StripTopFolder; public Func<Stream> Open; }
public sealed class BuilderFile { public string Path=""; public Func<Stream> Open; }
public sealed class BuilderPayload { public readonly List<BuilderPart> Parts=new List<BuilderPart>(); public readonly List<BuilderFile> Files=new List<BuilderFile>(); }
// The drive does not have room for the build tools; nothing was changed.
public sealed class NotEnoughSpaceException : IOException { public readonly long Needed,Free; public NotEnoughSpaceException(long needed,long free):base("Not enough free space"){Needed=needed;Free=free;} }

public static class Installer {
  public const string AppName="Flutter Deck",ExeName="FlutterDeck.exe",UninstallerName="Uninstall Flutter Deck.exe",Publisher="Dead Signal Interactive";
  public const string UninstallKey=@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FlutterDeck";
  // Relative paths inside the install folder, in the order Setup embeds them (resources P0, P1, ...). New files go at the end.
  public static readonly string[] Payload={"FlutterDeck.exe",@"assets\FlutterDeck-OriginalLoop.wav","LICENSE","NOTICE.md",@"assets\FlutterDeck-DawnLaunch.wav",@"assets\FlutterDeck-SkywayRun.wav",@"codes\memory-codes-list.txt"};
  // Folders beside the launcher that hold payload files (developer, 2026-11-24: their own list of cheat codes ships in "codes").
  static readonly string[] PayloadFolders={"assets","codes"};
  // Files the launcher's updater can leave beside the launcher.
  public static readonly string[] Leftovers={"FlutterDeck.exe.old","FlutterDeck.exe.new","FlutterDeck.exe.rolledback","FlutterDeck.exe.old.keep"};
  const string Staging=".setup-tmp";

  public static string DefaultFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Flutter Deck");}}
  public static string SettingsFolder {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlutterDeck");}}

  static string Full(string folder){
    // A local drive path only ("C:\..."): no relative, drive-relative ("C:foo"), rooted-only ("\foo"), UNC or device paths.
    if(String.IsNullOrWhiteSpace(folder)||folder.Length<4||!Char.IsLetter(folder[0])||folder[1]!=':'||folder[2]!='\\'||folder.IndexOf('/')>=0)throw new ArgumentException("Install folder must be a full local path");
    string full=Path.GetFullPath(folder).TrimEnd('\\');
    if(full.Length<4||Path.GetPathRoot(full).TrimEnd('\\')==full)throw new ArgumentException("Install folder cannot be a drive root");
    return full;
  }
  // A folder that is really a link to somewhere else is never installed into or cleaned out.
  static bool IsLink(string path){try{return (Directory.Exists(path)||File.Exists(path))&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0;}catch(Exception){return true;}}
  static void RefuseLinks(string full){foreach(string inner in new[]{"","assets","codes",BuilderDir,BuilderStage,BuilderOld})if(IsLink(inner.Length==0?full:Path.Combine(full,inner)))throw new InvalidOperationException("That folder is a link to another location");}
  // True when the folder is empty/missing, or already holds a Flutter Deck install. Setup refuses anything else.
  public static bool CanInstallInto(string folder){
    string full=Full(folder);if(File.Exists(full))return false;if(!Directory.Exists(full))return true;
    if(File.Exists(Path.Combine(full,ExeName))||File.Exists(Path.Combine(full,UninstallerName)))return true;
    return OnlyOurNames(full);
  }
  // Every name Setup or the updater can create in the install folder, including Setup's own temporary names.
  static List<string> KnownNames(){
    var names=new List<string>(Payload);names.AddRange(Leftovers);names.Add(ExeName+Aside);
    foreach(string name in Payload){names.Add(name+Staging);names.Add(name+Aside);}
    names.Add(UninstallerName+Staging);names.Add(UninstallerName+Aside);names.Add(UninstallerName);
    return names;
  }
  // True when the folder holds nothing but (possibly leftover) files of ours, so a failed first install can simply be retried.
  static bool OnlyOurNames(string full){
    var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string name in KnownNames())known.Add(Path.Combine(full,name));
    var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase);foreach(string f in PayloadFolders)folders.Add(Path.Combine(full,f));
    foreach(string entry in Directory.GetFileSystemEntries(full)){
      string leaf=Path.GetFileName(entry);
      if((String.Equals(leaf,BuilderDir,StringComparison.OrdinalIgnoreCase)||String.Equals(leaf,BuilderStage,StringComparison.OrdinalIgnoreCase)||String.Equals(leaf,BuilderOld,StringComparison.OrdinalIgnoreCase)||leaf.StartsWith(BuilderKept,StringComparison.OrdinalIgnoreCase))&&Directory.Exists(entry)&&!IsLink(entry))continue;
      if(folders.Contains(entry)&&Directory.Exists(entry)&&!IsLink(entry)){foreach(string inner in Directory.GetFileSystemEntries(entry))if(!known.Contains(inner)||Directory.Exists(inner))return false;continue;}
      if(!known.Contains(entry)||Directory.Exists(entry))return false;
    }
    return true;
  }
  // True only for a folder Setup installed into: it must hold the uninstaller Setup put there.
  // A folder that merely contains a FlutterDeck.exe (a portable copy, a source checkout) is not one.
  public static bool IsInstallFolder(string folder){
    try{string full=Full(folder);return Directory.Exists(full)&&!IsLink(full)&&File.Exists(Path.Combine(full,UninstallerName));}catch(ArgumentException){return false;}
  }
  // Windows refuses write access to the file of a running program, so asking for it shows whether the
  // launcher is running. A read-only flag would give the same refusal, so it is cleared first.
  // Setup and Uninstall ask the user to close the launcher rather than ending it.
  // After a self-update or rollback the running program's file carries one of the updater's leftover
  // names until restart, so those are checked as well.
  public static bool LauncherInUse(string folder){
    string full=Full(folder);var names=new List<string>(Leftovers);names.Add(ExeName);names.Add(ExeName+Aside);
    foreach(string name in names){
      string path=Path.Combine(full,name);if(!File.Exists(path))continue;
      Unprotect(path);
      try{using(new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}}catch(IOException){return true;}catch(UnauthorizedAccessException){return true;}
    }
    return false;
  }
  static void Unprotect(string path){try{if(File.Exists(path))File.SetAttributes(path,File.GetAttributes(path)&~FileAttributes.ReadOnly);}catch(Exception){}}

  // Writes every payload file beside its destination first, then swaps them in, so a failed copy
  // (disk full, unreadable source) leaves an existing install untouched. uninstallerSource is copied
  // in as the uninstaller. Stale updater leftovers are removed so Roll back cannot offer an unrelated build.
  public static void Install(string folder,Func<int,Stream> open,string uninstallerSource){Install(folder,open,uninstallerSource,null,null);}
  // With builder: the build tools are written to a staging folder first (the long part; progress reports it) and swapped in
  // with the other files. If anything fails, the previous launcher and the previous tools are put back.
  public static void Install(string folder,Func<int,Stream> open,string uninstallerSource,BuilderPayload builder,Action<long,long> progress){
    string full=Full(folder);
    if(!CanInstallInto(full))throw new InvalidOperationException("That folder already holds other files");
    RefuseLinks(full);
    if(LauncherInUse(full))throw new IOException("Flutter Deck is running");
    var targets=new List<string>();foreach(string name in Payload)targets.Add(Path.Combine(full,name));targets.Add(Path.Combine(full,UninstallerName));
    var swapped=new List<string>();var hadOld=new List<bool>();
    string liveTools=Path.Combine(full,BuilderDir),stageTools=Path.Combine(full,BuilderStage),oldTools=Path.Combine(full,BuilderOld);bool toolsAside=false,toolsIn=false;
    try{
      // 0. The build tools, into their staging folder. Nothing installed has been touched yet.
      if(builder!=null){
        RemoveListed(oldTools);KeepAside(full,oldTools);
        StageBuilder(full,builder,progress);
      }
      // 1. Write every new file beside its destination. Nothing installed has been touched yet.
      for(int i=0;i<Payload.Length;i++){
        Directory.CreateDirectory(Path.GetDirectoryName(targets[i]));Unprotect(targets[i]+Staging);
        using(var source=open(i)){if(source==null)throw new FileNotFoundException("Setup is missing "+Payload[i]);using(var target=new FileStream(targets[i]+Staging,FileMode.Create,FileAccess.Write,FileShare.None))source.CopyTo(target);}
      }
      Unprotect(targets[Payload.Length]+Staging);File.Copy(uninstallerSource,targets[Payload.Length]+Staging,true);Unprotect(targets[Payload.Length]+Staging);
      // 2. Swap by renaming: the old file (or tools folder) is set aside, not deleted, until every swap has worked.
      // A build running from the old tools holds their files, so the folder cannot be renamed and Setup stops here.
      if(builder!=null){
        if(Directory.Exists(liveTools)){Directory.Move(liveTools,oldTools);toolsAside=true;}
        Directory.Move(stageTools,liveTools);toolsIn=true;
      }
      foreach(string dest in targets){
        Unprotect(dest+Aside);if(File.Exists(dest+Aside))File.Delete(dest+Aside);
        Unprotect(dest);bool had=File.Exists(dest);if(had)File.Move(dest,dest+Aside);
        swapped.Add(dest);hadOld.Add(had);
        File.Move(dest+Staging,dest);
      }
    }
    catch{
      // Put back every file already swapped, newest first, so a failure leaves the previous install as it was.
      // A file whose new copy never arrived still has its staged copy beside it; otherwise the new copy is removed.
      for(int i=swapped.Count-1;i>=0;i--){string dest=swapped[i];try{if(!File.Exists(dest+Staging)&&File.Exists(dest))File.Delete(dest);if(hadOld[i]&&File.Exists(dest+Aside))File.Move(dest+Aside,dest);}catch(Exception){}}
      foreach(string dest in targets){try{if(File.Exists(dest+Staging))File.Delete(dest+Staging);}catch(Exception){}}
      if(builder!=null){
        try{if(toolsIn&&!Directory.Exists(stageTools))Directory.Move(liveTools,stageTools);}catch(Exception){}
        try{if(toolsAside&&!Directory.Exists(liveTools))Directory.Move(oldTools,liveTools);}catch(Exception){}
        try{RemoveListed(stageTools);}catch(Exception){}
      }
      // A first-time install that failed leaves no empty folders behind.
      foreach(string f in PayloadFolders)RemoveIfEmpty(Path.Combine(full,f));RemoveIfEmpty(full);
      throw;
    }
    foreach(string dest in targets){try{Unprotect(dest+Aside);if(File.Exists(dest+Aside))File.Delete(dest+Aside);}catch(Exception){}}
    foreach(string name in Leftovers){string path=Path.Combine(full,name);try{Unprotect(path);if(File.Exists(path))File.Delete(path);}catch(Exception){}}
    // The old tools go last, by their own list; a file the user added there stays (and so does that folder).
    if(toolsAside){try{RemoveListed(oldTools);KeepAside(full,oldTools);}catch(Exception){}}
  }

  // ---- the build tools (developer, 2026-11-20) ----
  // They go into the folder "builder" beside the launcher, where "Build my game..." looks for them (BuildCore.Find).
  // Each part arrives as a zip (the official Python and Zig zips, unchanged, and one zip per kit) or as one file.
  // The folder is written whole into "builder.setup-tmp" first and then swapped in by renaming, like the other files.
  // Every file written is first recorded in builder\installed-files.txt; removal takes exactly the files that list
  // names (checked to be inside the folder, never through a link), then folders that end up empty. Anything else
  // a user put there stays. Nothing here deletes a folder with its contents.
  public const string BuilderDir="builder",BuilderList="installed-files.txt";
  const string BuilderStage="builder.setup-tmp",BuilderOld="builder.setup-old",BuilderKept="builder.kept-";
  // Free space above the size of the tools that must remain on the drive, so Windows and the player's builds have room.
  public const long SpareBytes=200L*1024*1024;
  // A test may replace this to act as if the drive were nearly full.
  public static Func<string,long> FreeBytes=delegate(string folder){return new DriveInfo(Path.GetPathRoot(folder)).AvailableFreeSpace;};

  // A relative path inside the builder folder, or null when it is not one: no rooted, drive or device form, no "." or
  // ".." step, no empty step, no step ending in a dot or space (Windows would drop it), no character Windows refuses.
  public static string CleanRelative(string rel){
    if(String.IsNullOrEmpty(rel)||rel.Length>300)return null;
    rel=rel.Replace('/','\\');
    if(rel[0]=='\\'||rel.IndexOf(':')>=0)return null;
    foreach(string step in rel.Split('\\')){
      if(step.Length==0||step=="."||step==".."||step.EndsWith(".",StringComparison.Ordinal)||step.EndsWith(" ",StringComparison.Ordinal)||step.StartsWith(" ",StringComparison.Ordinal))return null;
      if(step.IndexOfAny(Path.GetInvalidFileNameChars())>=0)return null;
    }
    return rel;
  }
  // The full path of rel inside dir, or null when it would land anywhere else.
  static string Inside(string dir,string rel){
    rel=CleanRelative(rel);if(rel==null)return null;
    string path=Path.GetFullPath(Path.Combine(dir,rel));
    return path.StartsWith(dir.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)?path:null;
  }
  // True when a folder between dir and the file is a link to somewhere else (then the file is never removed).
  static bool LinkOnPath(string dir,string path){
    string top=dir.TrimEnd('\\');
    for(string at=Path.GetDirectoryName(path);at!=null&&at.Length>top.Length;at=Path.GetDirectoryName(at))if(IsLink(at))return true;
    return IsLink(top);
  }

  // Every file the payload holds, as (relative path, size), in the order it is written; checks every name first.
  public static List<KeyValuePair<string,long>> BuilderFiles(BuilderPayload payload){
    var files=new List<KeyValuePair<string,long>>();var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    Action<string,long> add=delegate(string rel,long size){
      string clean=CleanRelative(rel);
      if(clean==null||String.Equals(clean,BuilderList,StringComparison.OrdinalIgnoreCase)||!seen.Add(clean))throw new InvalidDataException("The build tools in Setup hold an unusable file name: "+rel);
      files.Add(new KeyValuePair<string,long>(clean,size));
    };
    foreach(BuilderPart part in payload.Parts){
      using(Stream s=part.Open()){
        if(s==null)throw new FileNotFoundException("Setup is missing the build tool "+part.Folder);
        using(var zip=new ZipArchive(s,ZipArchiveMode.Read)){
          string top=part.StripTopFolder?TopFolder(zip):"";
          foreach(ZipArchiveEntry e in zip.Entries){
            if(e.FullName.EndsWith("/",StringComparison.Ordinal)||e.FullName.EndsWith("\\",StringComparison.Ordinal))continue;
            add(part.Folder+"\\"+e.FullName.Replace('\\','/').Substring(top.Length),e.Length);
          }
        }
      }
    }
    foreach(BuilderFile file in payload.Files){using(Stream s=file.Open()){if(s==null)throw new FileNotFoundException("Setup is missing "+file.Path);add(file.Path,s.Length);}}
    return files;
  }
  // The one folder every entry of the zip sits in ("zig-x86_64-windows-0.17.1/"), which is left out when unpacking.
  static string TopFolder(ZipArchive zip){
    string top=null;
    foreach(ZipArchiveEntry e in zip.Entries){
      string name=e.FullName.Replace('\\','/');int slash=name.IndexOf('/');
      if(slash<1)throw new InvalidDataException("A build tool zip is not in one folder");
      string first=name.Substring(0,slash+1);
      if(top==null)top=first;else if(!String.Equals(top,first,StringComparison.Ordinal))throw new InvalidDataException("A build tool zip is not in one folder");
    }
    if(top==null)throw new InvalidDataException("A build tool zip is empty");
    return top;
  }
  public static long BuilderBytes(BuilderPayload payload){long total=0;foreach(var f in BuilderFiles(payload))total+=f.Value;return total;}

  // Writes the whole builder folder into the staging folder. Its list is written before any file, so a staging folder
  // left by a crash can always be removed by its list. progress(done, total) is called now and then, in bytes.
  static void StageBuilder(string full,BuilderPayload payload,Action<long,long> progress){
    string stage=Path.Combine(full,BuilderStage);
    RemoveListed(stage);KeepAside(full,stage);
    var files=BuilderFiles(payload);long total=0;foreach(var f in files)total+=f.Value;
    // A first install needs all of it; a replace needs it too, because the old tools stay until the new ones are in.
    long free=FreeBytes(full);
    if(free<total+SpareBytes)throw new NotEnoughSpaceException(total+SpareBytes,free);
    Directory.CreateDirectory(stage);
    var names=new List<string>();foreach(var f in files)names.Add(f.Key);
    File.WriteAllLines(Path.Combine(stage,BuilderList),names.ToArray(),new System.Text.UTF8Encoding(false));
    long done=0,reported=0;var buffer=new byte[1<<16];
    Action<Stream,string> write=delegate(Stream source,string rel){
      string target=Inside(stage,rel);if(target==null)throw new InvalidDataException("Unusable file name "+rel);
      Directory.CreateDirectory(Path.GetDirectoryName(target));
      using(var output=new FileStream(target,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
        int n;while((n=source.Read(buffer,0,buffer.Length))>0){output.Write(buffer,0,n);done+=n;if(progress!=null&&done-reported>=(1<<21)){reported=done;progress(done,total);}}
      }
    };
    if(progress!=null)progress(0,total);
    foreach(BuilderPart part in payload.Parts){
      using(Stream s=part.Open())using(var zip=new ZipArchive(s,ZipArchiveMode.Read)){
        string top=part.StripTopFolder?TopFolder(zip):"";
        foreach(ZipArchiveEntry e in zip.Entries){
          if(e.FullName.EndsWith("/",StringComparison.Ordinal)||e.FullName.EndsWith("\\",StringComparison.Ordinal))continue;
          using(Stream source=e.Open())write(source,CleanRelative(part.Folder+"\\"+e.FullName.Replace('\\','/').Substring(top.Length)));
        }
      }
    }
    foreach(BuilderFile file in payload.Files){using(Stream s=file.Open())write(s,CleanRelative(file.Path));}
    if(progress!=null)progress(total,total);
  }

  // Removes the files named in dir\installed-files.txt, then that list, then folders left empty, then dir if empty.
  // Returns the names it could not remove. A file outside dir, or reached through a link, is never touched.
  static List<string> RemoveListed(string dir){
    var failed=new List<string>();
    if(!Directory.Exists(dir))return failed;
    string name=Path.GetFileName(dir);
    if(IsLink(dir)){failed.Add(name);return failed;}
    string list=Path.Combine(dir,BuilderList);
    if(File.Exists(list)){
      string[] lines;
      try{if(new FileInfo(list).Length>8L*1024*1024)throw new InvalidDataException("list too long");lines=File.ReadAllLines(list);}catch(Exception){failed.Add(name+"\\"+BuilderList);return failed;}
      var folders=new List<string>();var known=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
      foreach(string line in lines){
        // A line that cannot be a path inside dir (too long, odd form) is skipped, never followed and never fatal.
        string path;try{path=Inside(dir,line.Trim());if(path==null||LinkOnPath(dir,path))continue;}catch(Exception){continue;}
        try{Unprotect(path);if(File.Exists(path))File.Delete(path);}catch(Exception){failed.Add(name+"\\"+CleanRelative(line.Trim()));}
        for(string at=Path.GetDirectoryName(path);at!=null&&at.Length>dir.TrimEnd('\\').Length;at=Path.GetDirectoryName(at))if(known.Add(at))folders.Add(at);
      }
      // deepest first, so a folder is tried only after the folders inside it
      folders.Sort(delegate(string a,string b){return b.Length.CompareTo(a.Length);});
      foreach(string folder in folders)RemoveIfEmpty(folder);
      if(failed.Count==0){try{Unprotect(list);File.Delete(list);}catch(Exception){failed.Add(name+"\\"+BuilderList);}}
    }
    RemoveEmptyFolders(dir);RemoveIfEmpty(dir);
    return failed;
  }
  // Folders left empty inside dir (one by one, deepest first; never through a link).
  static void RemoveEmptyFolders(string dir){
    try{foreach(string sub in Directory.GetDirectories(dir)){if(IsLink(sub))continue;RemoveEmptyFolders(sub);RemoveIfEmpty(sub);}}catch(Exception){}
  }
  // A staging or set-aside tools folder that still holds files no list names (something a user put there) is never
  // deleted: it is renamed to "builder.kept-<date and time>" beside the launcher, so it cannot block the next install.
  static void KeepAside(string full,string dir){
    if(!Directory.Exists(dir))return;
    string kept=Path.Combine(full,BuilderKept+DateTime.Now.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture));
    for(int n=2;Directory.Exists(kept)||File.Exists(kept);n++)kept=Path.Combine(full,BuilderKept+DateTime.Now.ToString("yyyyMMdd-HHmmss",System.Globalization.CultureInfo.InvariantCulture)+"-"+n);
    Directory.Move(dir,kept);
  }
  const string Aside=".setup-old";

  // Removes only the files Setup and the updater create, then the folders if they are empty.
  // Returns the names that could not be removed. Anything else the user put there is left alone.
  public static List<string> Uninstall(string folder){
    string full=Full(folder);var failed=new List<string>();
    if(!IsInstallFolder(full))throw new InvalidOperationException("That folder does not hold an installed Flutter Deck");
    RefuseLinks(full);
    if(LauncherInUse(full))throw new IOException("Flutter Deck is running");
    // The build tools first, by their own lists (also any staging or set-aside copy a failed Setup left).
    foreach(string tools in new[]{BuilderDir,BuilderStage,BuilderOld})failed.AddRange(RemoveListed(Path.Combine(full,tools)));
    // The uninstaller goes last: while it is still there the folder is recognised and a retry can finish the job.
    var names=KnownNames();
    foreach(string name in names){
      if(name==UninstallerName&&failed.Count>0)break;
      string path=Path.Combine(full,name);try{Unprotect(path);if(File.Exists(path))File.Delete(path);}catch(Exception){failed.Add(name);}
    }
    foreach(string f in PayloadFolders)RemoveIfEmpty(Path.Combine(full,f));RemoveIfEmpty(full);
    return failed;
  }
  // The launcher's own settings files, by name: each file, its backup, and the temporary copies written while saving.
  static List<string> SettingsFiles(string full){
    var found=new List<string>();if(!Directory.Exists(full)||IsLink(full))return found;
    foreach(string name in new[]{"window.txt","text-size.txt","setup.xml","updates.txt","runtime.xml","choices.txt","overlay.txt","library.txt","game-folder.txt","covers.txt","library-view.txt","library-folders.txt","library-found.txt","multiplayer-rules.txt","builder-tools.txt"}){
      foreach(string suffix in new[]{"",".bak",".tmp"}){string path=Path.Combine(full,name+suffix);if(File.Exists(path))found.Add(path);}
      found.AddRange(Directory.GetFiles(full,name+".*.tmp"));
    }
    // downloaded cover pictures and the notes of codes with no picture (SPEC-covers.md); only files named by a disc code
    string covers=Path.Combine(full,"covers");
    if(Directory.Exists(covers)&&!IsLink(covers)){foreach(string file in Directory.GetFiles(covers)){if(System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(file),"\\A[A-Z]{4}-[0-9]{5}\\.(jpg|none|jpg\\.[0-9a-f]{32}\\.tmp)\\z"))found.Add(file);}}
    string news=Path.Combine(full,"news");
    if(Directory.Exists(news)&&!IsLink(news)){foreach(string feed in Directory.GetDirectories(news)){if(IsLink(feed))continue;foreach(string name in new[]{"cache.xml","cache.xml.bak","cache.xml.tmp","read.txt","read.txt.bak","read.txt.tmp"}){string path=Path.Combine(feed,name);if(File.Exists(path))found.Add(path);}}}
    return found;
  }
  // True while any of those files is still there.
  public static bool SettingsRemain(string settingsFolder){try{return SettingsFiles(Full(settingsFolder)).Count>0;}catch(Exception){return true;}}
  static void RemoveIfEmpty(string folder){try{if(Directory.Exists(folder)&&Directory.GetFileSystemEntries(folder).Length==0)Directory.Delete(folder,false);}catch(Exception){}}

  // Opt-in only. Removes the launcher's own settings files by name, never the user's game, BIOS or music files
  // (the settings only point at those). Unknown files are left, and so is the folder if anything remains.
  public static void RemoveSettings(string settingsFolder){
    string full=Full(settingsFolder);if(!Directory.Exists(full)||IsLink(full))return;
    foreach(string path in SettingsFiles(full)){try{Unprotect(path);File.Delete(path);}catch(Exception){}}
    string news=Path.Combine(full,"news");
    if(Directory.Exists(news)&&!IsLink(news)){foreach(string feed in Directory.GetDirectories(news)){if(!IsLink(feed))RemoveIfEmpty(feed);}RemoveIfEmpty(news);}
    string covers=Path.Combine(full,"covers");if(Directory.Exists(covers)&&!IsLink(covers))RemoveIfEmpty(covers);
    RemoveIfEmpty(full);
  }

  // "1.2.3" style comparison for the downgrade warning; anything unreadable counts as older.
  public static int CompareVersions(string a,string b){
    Version x,y;bool okX=Version.TryParse(Pad(a),out x),okY=Version.TryParse(Pad(b),out y);
    if(!okX&&!okY)return 0;if(!okX)return -1;if(!okY)return 1;return x.CompareTo(y);
  }
  static string Pad(string v){if(String.IsNullOrWhiteSpace(v))return "";string[] parts=v.Trim().Split('.');return parts.Length>=3?parts[0]+"."+parts[1]+"."+parts[2]:v.Trim();}
}
