using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

// One run of a builder kit's command for one game (SPEC-builder.md; contracts BUILD-1 and BUILD-2). It starts the packed Python with the one
// command line of BuildCore.Arguments, without a window, and hands every line of its output to the caller.
// Rules of this file (check-builder.ps1 reads it):
//  - It starts two programs only: the Python of the build tools, and Windows' own taskkill to end a build.
//  - It writes two files only, both beside the output folder: the gate file that keeps a second build out, and the
//    text of the last build for a report. It writes nothing into the kit folder and nothing into the output folder.
//  - It sends nothing anywhere.
//  - Its only calls into Windows itself make one job object, which ends the build when this launcher ends, and ask
//    for the true place of a file and for what tells two names of one folder apart from two folders (RealPath and
//    FileId; both open the file without reading or writing it).
public sealed class GameBuild {
  public const int LogLimit=2*1024*1024;   // characters of text
  Process process;FileStream gate;StreamWriter log;int logged;IntPtr job=IntPtr.Zero;readonly object sync=new object();
  volatile bool cancelled;
  public bool Cancelled {get{return cancelled;}}   // true only when Cancel really ended a running build
  public bool Tied {get{return job!=IntPtr.Zero;}}

  // A Windows job with "end every process in it when the last handle closes". This launcher holds the only handle,
  // so the build and everything under it end when the launcher ends in any way, also when it is killed.
  [StructLayout(LayoutKind.Sequential)] struct BasicLimits { public long PerProcessUserTimeLimit,PerJobUserTimeLimit; public uint LimitFlags; public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize; public uint ActiveProcessLimit; public UIntPtr Affinity; public uint PriorityClass,SchedulingClass; }
  [StructLayout(LayoutKind.Sequential)] struct IoCounters { public ulong ReadOperations,WriteOperations,OtherOperations,ReadTransfers,WriteTransfers,OtherTransfers; }
  [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed; }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateJobObject(IntPtr attributes,string name);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool SetInformationJobObject(IntPtr job,int infoClass,ref ExtendedLimits info,uint length);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool AssignProcessToJobObject(IntPtr job,IntPtr process);
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool CloseHandle(IntPtr handle);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern uint GetFinalPathNameByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,StringBuilder path,uint size,uint flags);
  // The true place of a file or folder, behind every junction, symbolic link and substituted drive. null when Windows
  // cannot say. The file is opened with no access (not read, not written) and every sharing allowed.
  public static string RealPath(string path){
    try{
      using(var file=CreateFile(path,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero)){   // OPEN_EXISTING; FILE_FLAG_BACKUP_SEMANTICS lets a folder be opened
        if(file.IsInvalid)return null;
        var text=new StringBuilder(2048);uint n=GetFinalPathNameByHandle(file,text,2048,0);
        if(n==0||n>=2048)return null;
        string place=text.ToString();
        if(place.StartsWith(@"\\?\UNC\",StringComparison.OrdinalIgnoreCase))return @"\\"+place.Substring(8);
        if(place.StartsWith(@"\\?\",StringComparison.Ordinal))return place.Substring(4);
        return place;
      }
    }catch(Exception){return null;}
  }
  [StructLayout(LayoutKind.Sequential)] struct FileInfoByHandle { public uint Attributes; public long Created,Accessed,Written; public uint VolumeSerial,SizeHigh,SizeLow,Links,IndexHigh,IndexLow; }
  [DllImport("kernel32.dll",SetLastError=true)] static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle file,out FileInfoByHandle info);
  // A text that is equal for every name of one file or folder (a path, a share of this PC, a mapped drive, a link) and
  // differs between two files: the number of its disk and its number on that disk. null when Windows cannot say.
  public static string FileId(string path){
    try{
      using(var file=CreateFile(path,0,7,IntPtr.Zero,3,0x02000000,IntPtr.Zero)){
        if(file.IsInvalid)return null;
        FileInfoByHandle info;if(!GetFileInformationByHandle(file,out info))return null;
        if(info.IndexHigh==0&&info.IndexLow==0)return null;   // a file system that gives no number
        return info.VolumeSerial.ToString("X8")+":"+info.IndexHigh.ToString("X8")+info.IndexLow.ToString("X8");
      }
    }catch(Exception){return null;}
  }
  void Tie(Process p){
    try{
      IntPtr made=CreateJobObject(IntPtr.Zero,null);if(made==IntPtr.Zero)return;
      var limits=new ExtendedLimits();limits.Basic.LimitFlags=0x2000;   // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
      if(!SetInformationJobObject(made,9,ref limits,(uint)Marshal.SizeOf(typeof(ExtendedLimits)))||!AssignProcessToJobObject(made,p.Handle)){CloseHandle(made);return;}
      job=made;
    }catch(Exception){}   // without the tie the build still runs; Cancel and the launcher's own close end it
  }

  // Starts the build. line() and exited() are called on other threads. Throws InvalidOperationException with a
  // sentence for the player when the build cannot start.
  public static GameBuild Start(BuildTools tools,string cue,string dataRoot,BuildGame game,Action<string> line,Action<int> exited){
    string outFolder=BuildCore.OutputFolder(dataRoot,game);
    var run=new GameBuild();
    try{Directory.CreateDirectory(Path.GetDirectoryName(outFolder));}
    catch(Exception){throw new InvalidOperationException("The folder for the built game cannot be made.");}
    // One build into this folder at a time, also across two Flutter Decks: the gate file is held open, unshared.
    try{run.gate=new FileStream(BuildCore.LockPath(dataRoot,game),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose);}
    catch(Exception){throw new InvalidOperationException("Another build of this game is running. Wait until it has finished.");}
    try{
      try{run.log=new StreamWriter(new FileStream(BuildCore.LogPath(dataRoot,game),FileMode.Create,FileAccess.Write,FileShare.Read),new UTF8Encoding(false));}catch(Exception){run.log=null;}
      var info=new ProcessStartInfo(tools.Python,BuildCore.Arguments(tools,cue,outFolder)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8,WorkingDirectory=Path.GetDirectoryName(outFolder)};
      // The build tests the new program by running it. No setting of this launcher's own games and no Python setting
      // of this PC may reach it.
      var drop=new List<string>();
      foreach(string key in info.EnvironmentVariables.Keys)if(key.StartsWith("RMX_",StringComparison.OrdinalIgnoreCase)||key.StartsWith("PYTHON",StringComparison.OrdinalIgnoreCase))drop.Add(key);
      foreach(string key in drop)info.EnvironmentVariables.Remove(key);
      var p=new Process{StartInfo=info};
      DataReceivedEventHandler take=delegate(object sender,DataReceivedEventArgs e){if(e.Data==null)return;run.Keep(e.Data);try{line(e.Data);}catch(Exception){}};
      p.OutputDataReceived+=take;p.ErrorDataReceived+=take;
      p.Start();run.process=p;
      run.Tie(p);
      try{p.StandardInput.BaseStream.Close();}catch(Exception){}   // the stream itself: closing the writer could first send a byte order mark
      p.BeginOutputReadLine();p.BeginErrorReadLine();
      var wait=new Thread(delegate(){
        int code=-1;
        try{
          while(!p.WaitForExit(500)){}   // a wait with a time limit ends when the process ends; the wait without one would also wait for the pipes
          code=p.ExitCode;
          // The last lines are still on their way. A child that outlived Python could hold the pipe open, so this waits a short time only.
          var flush=new Thread(delegate(){try{p.WaitForExit();}catch(Exception){}}){IsBackground=true};flush.Start();flush.Join(5000);
        }catch(Exception){}
        run.Close();
        try{exited(code);}catch(Exception){}
      }){IsBackground=true};
      wait.Start();
      return run;
    }catch(Exception){
      run.Close();
      throw new InvalidOperationException("The build could not be started. A build tool is missing or does not run. Repair or reinstall Flutter Deck.");
    }
  }
  void Keep(string text){
    lock(sync){
      if(log==null||logged>LogLimit)return;
      try{log.WriteLine(text);log.Flush();logged+=text.Length+2;if(logged>LogLimit)log.WriteLine("(the rest was not kept)");}catch(Exception){}
    }
  }
  // After the Python process has ended: the files are closed, and closing the job ends anything that was left under it.
  void Close(){
    lock(sync){
      try{if(log!=null)log.Dispose();}catch(Exception){}log=null;
      try{if(job!=IntPtr.Zero)CloseHandle(job);}catch(Exception){}job=IntPtr.Zero;
      try{if(gate!=null)gate.Dispose();}catch(Exception){}gate=null;
    }
  }
  public bool Running {get{try{return process!=null&&!process.HasExited;}catch(Exception){return false;}}}
  // Ends the build: the Python process and every process under it (translator, compiler, self-test). The contract's
  // own tie between them is not relied on. May take a few seconds; call it from a thread that may wait.
  // The Process object is kept, so Windows cannot give its number to another process in the meantime.
  public void Cancel(){
    var p=process;if(p==null)return;
    try{if(p.HasExited)return;}catch(Exception){return;}
    cancelled=true;
    try{
      var info=new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"taskkill.exe"),"/PID "+p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)+" /T /F"){UseShellExecute=false,CreateNoWindow=true};
      using(var kill=Process.Start(info)){if(kill!=null)kill.WaitForExit(8000);}
    }catch(Exception){}
    try{if(!p.HasExited)p.Kill();}catch(Exception){}
  }
}
