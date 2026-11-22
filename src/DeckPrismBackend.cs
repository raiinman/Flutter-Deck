using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Runtime.InteropServices;
using System.Threading;
using System.Web.Script.Serialization;

public sealed class DeckPrismTarget {
    public long Hwnd;
    public int Pid;
    public long StartedUtcTicks;
    public string Title, ExePath;
}

public sealed class DeckPrismResult {
    public bool Ready;
    public string Message, Profile, RequestId, ServerInstanceId, SettingsPath, RawStatus;
}

// Calls must run off the UI thread. Only the dedicated process and its command
// children belong to this object. Settings are created and written by Prism.
public sealed class DeckPrismBackend : IDisposable {
    readonly string baseDirectory,dataDirectory;
    readonly object operation = new object();
    readonly Dictionary<string,string> imports = new Dictionary<string,string>(StringComparer.Ordinal);
    Process owned;
    long ownedStarted;
    string executable, settings, sessionDirectory, serverInstance;
    int stopRequested;
    int receiptNumber;
    static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [DllImport("user32.dll")] static extern bool IsWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window, out uint pid);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr window,StringBuilder value,int length);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool CreateHardLink(string path,string existing,IntPtr security);
    [DllImport("kernel32.dll",EntryPoint="QueryFullProcessImageNameW",CharSet=CharSet.Unicode,SetLastError=true)] static extern bool QueryFullProcessImageName(IntPtr process,uint flags,StringBuilder path,ref uint characters);

    public static string ProcessImagePath(Process process) {
        if(process==null)throw new ArgumentNullException("process");
        var path=new StringBuilder(32768);uint characters=(uint)path.Capacity;
        if(!QueryFullProcessImageName(process.Handle,0,path,ref characters))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"The process image path could not be verified.");
        if(characters==0)throw new InvalidOperationException("The process image path is empty.");
        return Path.GetFullPath(path.ToString());
    }

    public DeckPrismBackend(string baseDirectory) : this(baseDirectory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FlutterDeck")) {}
    public DeckPrismBackend(string baseDirectory,string dataDirectory) {
        if(String.IsNullOrWhiteSpace(baseDirectory))throw new ArgumentException("Launcher directory is required.");
        if(String.IsNullOrWhiteSpace(dataDirectory))throw new ArgumentException("Launcher data directory is required.");
        this.baseDirectory=Path.GetFullPath(baseDirectory);
        this.dataDirectory=Path.GetFullPath(dataDirectory);
    }

    public static string DiscoverExecutable(string baseDirectory) {
        string configured=Environment.GetEnvironmentVariable("FLUTTERDECK_PRISM_EXE");
        if(!String.IsNullOrWhiteSpace(configured)) {
            if(!Path.IsPathRooted(configured)||!File.Exists(configured)||!String.Equals(Path.GetFileName(configured),"DSI.Scaling.exe",StringComparison.OrdinalIgnoreCase))throw new FileNotFoundException("FLUTTERDECK_PRISM_EXE must name an existing absolute DSI.Scaling.exe.",configured);
            return Path.GetFullPath(configured);
        }
        string bundled=Path.Combine(Path.GetFullPath(baseDirectory),"prism","DSI.Scaling.exe");
        if(File.Exists(bundled))return bundled;
        throw new FileNotFoundException("Prism was not found. Bundle prism/DSI.Scaling.exe or set FLUTTERDECK_PRISM_EXE to the tested Prism executable.",bundled);
    }

    public DeckPrismResult Apply(DeckPrismTarget target,string presetPath,Action<string> progress) {
        lock(operation) {
            DeckPrismResult answer=new DeckPrismResult();
            try {
                CheckStop();
                if(target==null)throw new ArgumentNullException("target");
                // Copy caller data once so a UI selection change cannot retarget an action.
                target=new DeckPrismTarget {Hwnd=target.Hwnd,Pid=target.Pid,StartedUtcTicks=target.StartedUtcTicks,Title=target.Title,ExePath=target.ExePath};
                uint thread;string windowClass;
                ValidateTarget(target,out thread,out windowClass);
                uint expectedThread=thread;string expectedClass=windowClass;
                if(!File.Exists(presetPath))throw new FileNotFoundException("Picture preset is missing.",presetPath);
                EnsureServer(progress);
                answer.SettingsPath=settings;answer.ServerInstanceId=serverInstance;
                string profile=EnsureImported(Path.GetFullPath(presetPath),progress);
                answer.Profile=profile;
                ValidateTarget(target,out thread,out windowClass);
                RequireWindowIdentity(thread,windowClass,expectedThread,expectedClass);
                string request=Guid.NewGuid().ToString("D");answer.RequestId=request;
                Report(progress,"Applying "+profile+" to "+target.Title+"...");
                Dictionary<string,object> response;
                try { response=Command(ApplyArguments(profile,target,request),request); }
                catch(UnknownCommandException) {
                    // Never replay an apply whose admission is unknown. Status with
                    // its original ID is the only recovery operation.
                    response=null;
                }
                Stopwatch clock=Stopwatch.StartNew();
                Stopwatch preparation=Stopwatch.StartNew();
                while(clock.ElapsedMilliseconds<45000) {
                    CheckStop();ValidateTarget(target,out thread,out windowClass);
                    RequireWindowIdentity(thread,windowClass,expectedThread,expectedClass);
                    if(response!=null) {
                        answer.RawStatus=Serialize(response);
                        VerifyServer(response);
                        Dictionary<string,object> status=Status(response);
                        string state=Text(status,"queryOperationState");
                        string operationError=Text(status,"queryOperationError")??Text(response,"error");
                        if(state=="rejected"&&Text(status,"queryOperationId")==request&&QualificationPending(operationError)&&preparation.ElapsedMilliseconds<180000) {
                            // This action is positively recorded as rejected before
                            // output retirement. Qualification is asynchronous; a
                            // fresh action after this known rejection is deliberate.
                            Report(progress,"Waiting for Prism GPU checks...");
                            Pause(1000);ValidateTarget(target,out thread,out windowClass);
                            RequireWindowIdentity(thread,windowClass,expectedThread,expectedClass);
                            request=Guid.NewGuid().ToString("D");answer.RequestId=request;
                            try{response=Command(ApplyArguments(profile,target,request),request);}catch(UnknownCommandException){response=null;}
                            clock.Restart();continue;
                        }
                        if(state=="rejected"||state=="failed")throw new InvalidOperationException(operationError??"Prism rejected the picture change.");
                        if(!Boolean(response,"ok")&&state==null)throw new InvalidOperationException(Text(response,"error")??"Prism command failed.");
                        if(state=="applied"&&Text(status,"queryOperationId")==request&&Text(status,"state")=="running"&&Boolean(status,"captureReady")&&Text(status,"sessionProfile")==profile&&Text(status,"selectedProfile")==profile) {
                            if(Number(status,"sourceHwnd")!=target.Hwnd||Number(status,"sourcePid")!=target.Pid||Number(status,"sourceProcessStartedUtcTicks")!=target.StartedUtcTicks||Number(status,"sourceThread")!=thread||Text(status,"sourceClass")!=windowClass)throw new InvalidOperationException("Prism reported another source window. This action was not accepted as ready.");
                            answer.Ready=true;answer.Message=profile+" is active on the selected game.";return answer;
                        }
                    }
                    Pause(350);
                    try {response=Command("--prism-control status --request-id "+request,request);}catch(UnknownCommandException){response=null;}
                }
                throw new TimeoutException("Prism did not confirm capture readiness for this request and source. Its outcome remains unconfirmed; no apply was replayed.");
            } catch(Exception error) {
                try {
                    string directory=sessionDirectory??Path.Combine(dataDirectory,"prism-diagnostics");Directory.CreateDirectory(directory);
                    File.WriteAllText(Path.Combine(directory,"backend-error-"+Guid.NewGuid().ToString("N")+".log"),DateTime.UtcNow.ToString("o",Invariant)+Environment.NewLine+error.ToString(),new UTF8Encoding(false));
                }catch{}
                answer.Ready=false;answer.Message=error.Message;return answer;
            }
        }
    }

    static string ApplyArguments(string profile,DeckPrismTarget target,string request) {
        return "--prism-control apply-profile "+Quote(profile)+" --target "+target.Hwnd.ToString(Invariant)+" "+target.Pid.ToString(Invariant)+" "+target.StartedUtcTicks.ToString(Invariant)+" --request-id "+request;
    }
    static bool QualificationPending(string error) {
        return error!=null&&(error.StartsWith("GPU checks are pending;",StringComparison.Ordinal)||error.StartsWith("GPU qualification is pending for the selected adapter.",StringComparison.Ordinal)||error.StartsWith("Prism Learn checks are running for the selected GPU.",StringComparison.Ordinal));
    }
    static void RequireWindowIdentity(uint thread,string windowClass,uint expectedThread,string expectedClass) {
        if(thread!=expectedThread||windowClass!=expectedClass)throw new InvalidOperationException("The selected source window identity changed during the action.");
    }

    void EnsureServer(Action<string> progress) {
        CheckStop();
        if(owned!=null) {
            if(serverInstance!=null){RequireOwned();return;}
        } else {
            string source=DiscoverExecutable(baseDirectory);
            sessionDirectory=Path.Combine(dataDirectory,"prism-sessions",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(sessionDirectory);
            string runtime=Path.Combine(sessionDirectory,"runtime");
            StageRuntime(Path.GetDirectoryName(source),runtime);
            executable=Path.Combine(runtime,Path.GetFileName(source));
            settings=Path.Combine(sessionDirectory,"profiles.json");
            if(File.Exists(settings))throw new IOException("Fresh Prism settings scope unexpectedly exists.");
            Report(progress,"Starting a private Prism session...");
            owned=Process.Start(new ProcessStartInfo(executable,"--start-minimized --settings-path "+Quote(settings)) {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=runtime,WindowStyle=ProcessWindowStyle.Hidden});
            if(owned==null)throw new InvalidOperationException("Prism did not start.");
            ownedStarted=owned.StartTime.ToUniversalTime().Ticks;
            imports.Clear();serverInstance=null;
        }
        Stopwatch clock=Stopwatch.StartNew();string last=null;
        while(clock.ElapsedMilliseconds<90000) {
            CheckStop();
            RequireOwned();
            string id=Guid.NewGuid().ToString("D");
            try {
                Dictionary<string,object> response=Command("--prism-control status --request-id "+id,id);
                if(Boolean(response,"ok")) {serverInstance=Text(response,"serverInstanceId");if(String.IsNullOrEmpty(serverInstance))throw new InvalidDataException("Prism instance identity is missing.");return;}
                last=Text(response,"error");
            }catch(UnknownCommandException error){last=error.Message;}
            Pause(350);
        }
        throw new TimeoutException("The private Prism control endpoint did not become ready. "+last);
    }

    string EnsureImported(string presetPath,Action<string> progress) {
        string hash=Hash(presetPath),profile;
        if(imports.TryGetValue(hash,out profile))return profile;
        string request=Guid.NewGuid().ToString("D");
        Report(progress,"Importing the picture preset into this private session...");
        Dictionary<string,object> response;
        try {response=Command("--prism-control import-preset "+Quote(presetPath)+" --request-id "+request,request);}
        catch(UnknownCommandException){response=null;}
        Stopwatch clock=Stopwatch.StartNew();
        while(clock.ElapsedMilliseconds<15000) {
            CheckStop();
            if(response!=null) {
                VerifyServer(response);var status=Status(response);
                string state=Text(status,"queryImportState")??Text(status,"importState");
                if(state=="imported") {
                    profile=Text(status,"queryImportedProfile")??Text(status,"importedProfile");
                    if(String.IsNullOrEmpty(profile))throw new InvalidDataException("Prism did not name the imported profile.");
                    imports.Add(hash,profile);return profile;
                }
                if(state=="rejected"||!Boolean(response,"ok"))throw new InvalidOperationException(Text(status,"queryImportError")??Text(response,"error")??"Prism rejected the preset.");
            }
            Pause(250);
            try {response=Command("--prism-control status --request-id "+request,request);}catch(UnknownCommandException){response=null;}
        }
        throw new TimeoutException("Preset import outcome is unknown. Import was not replayed.");
    }

    Dictionary<string,object> Command(string arguments,string request) {
        CheckStop();RequireOwned();
        string stdout,stderr;int exit;
        using(Process client=new Process()) {
            client.StartInfo=new ProcessStartInfo(executable,arguments+" --settings-path "+Quote(settings)){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(executable),WindowStyle=ProcessWindowStyle.Hidden};
            var output=new StringBuilder();var errors=new StringBuilder();object textGate=new object();
            client.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)lock(textGate){if(output.Length<65536)output.AppendLine(e.Data);}};
            client.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){if(e.Data!=null)lock(textGate){if(errors.Length<8192)errors.AppendLine(e.Data);}};
            if(!client.Start())throw new UnknownCommandException("Prism control client did not start.");
            client.BeginOutputReadLine();client.BeginErrorReadLine();
            Stopwatch wait=Stopwatch.StartNew();
            while(!client.WaitForExit(100)) {
                if(Volatile.Read(ref stopRequested)!=0||wait.ElapsedMilliseconds>10000) {
                    try{client.Kill();client.WaitForExit(1000);}catch(InvalidOperationException){}
                    throw new UnknownCommandException("Prism control response was interrupted; action admission is unknown.");
                }
            }
            client.WaitForExit();exit=client.ExitCode;
            lock(textGate){stdout=output.ToString();stderr=errors.ToString();}
        }
        File.WriteAllText(Path.Combine(sessionDirectory,(++receiptNumber).ToString("D5",Invariant)+"-"+request+".json"),Serialize(new Dictionary<string,object>{{"arguments",arguments},{"exitCode",exit},{"stdout",stdout},{"stderr",stderr}}),new UTF8Encoding(false));
        Dictionary<string,object> response;
        try{response=new JavaScriptSerializer{MaxJsonLength=65536}.DeserializeObject(stdout) as Dictionary<string,object>;}catch(ArgumentException){throw new UnknownCommandException("Prism returned an unreadable response; query the original request.");}
        if(response==null||Number(response,"version")!=1||Text(response,"requestId")!=request)throw new UnknownCommandException("Prism response correlation failed.");
        if(exit==4)throw new UnknownCommandException(Text(response,"error")??"Prism transport failed.");
        return response;
    }

    static void ValidateTarget(DeckPrismTarget target,out uint thread,out string windowClass) {
        if(target.Hwnd<=0||target.Pid<=0||target.StartedUtcTicks<=0||String.IsNullOrWhiteSpace(target.ExePath))throw new InvalidOperationException("The selected game has no complete window/process identity.");
        IntPtr handle=new IntPtr(target.Hwnd);uint pid;
        thread=GetWindowThreadProcessId(handle,out pid);
        if(thread==0||pid!=target.Pid||!IsWindow(handle)||!IsWindowVisible(handle)||IsIconic(handle))throw new InvalidOperationException("Restore the selected game window before changing its picture preset.");
        using(Process process=Process.GetProcessById(target.Pid)) {
            if(process.HasExited||process.StartTime.ToUniversalTime().Ticks!=target.StartedUtcTicks||!String.Equals(ProcessImagePath(process),Path.GetFullPath(target.ExePath),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The selected game process changed. Select its live overlay again.");
        }
        var value=new StringBuilder(256);if(GetClassName(handle,value,value.Capacity)==0)throw new InvalidOperationException("The selected window class is unavailable.");windowClass=value.ToString();
    }

    void VerifyServer(Dictionary<string,object> response) {if(Text(response,"serverInstanceId")!=serverInstance)throw new InvalidOperationException("Prism restarted. This request cannot be confirmed across instances.");}
    void RequireOwned() {
        if(owned==null||owned.HasExited)throw new InvalidOperationException("The private Prism process stopped.");
        if(owned.StartTime.ToUniversalTime().Ticks!=ownedStarted)throw new InvalidOperationException("The private Prism process identity changed.");
        if(!String.Equals(ProcessImagePath(owned),executable,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The private Prism executable identity changed.");
    }
    void CheckStop(){if(Volatile.Read(ref stopRequested)!=0)throw new OperationCanceledException("The game session is closing.");}
    void Pause(int milliseconds){for(int time=0;time<milliseconds;time+=50){CheckStop();Thread.Sleep(Math.Min(50,milliseconds-time));}}
    static void Report(Action<string> progress,string message){if(progress!=null)progress(message);}
    static string Hash(string path){using(var sha=SHA256.Create())using(var file=File.OpenRead(path))return Convert.ToBase64String(sha.ComputeHash(file));}
    static string Serialize(object value){return new JavaScriptSerializer{MaxJsonLength=262144}.Serialize(value);}
    static Dictionary<string,object> Status(Dictionary<string,object> response){object value;if(!response.TryGetValue("status",out value)||!(value is Dictionary<string,object>))throw new InvalidDataException("Prism status is missing.");return (Dictionary<string,object>)value;}
    static string Text(Dictionary<string,object> value,string key){object item;return value.TryGetValue(key,out item)?item as string:null;}
    static bool Boolean(Dictionary<string,object> value,string key){object item;return value.TryGetValue(key,out item)&&item is bool&&(bool)item;}
    static long Number(Dictionary<string,object> value,string key){object item;return value.TryGetValue(key,out item)&&item!=null?Convert.ToInt64(item,Invariant):0;}
    static string Quote(string value){var b=new StringBuilder("\"");int slash=0;foreach(char c in value){if(c=='\\'){slash++;continue;}if(c=='\"'){b.Append('\\',slash*2+1);b.Append(c);}else{b.Append('\\',slash);b.Append(c);}slash=0;}b.Append('\\',slash*2);return b.Append('"').ToString();}
    void StageRuntime(string source,string destination) {
        string sourceRoot=Path.GetFullPath(source).TrimEnd('\\')+"\\";
        string destinationRoot=Path.GetFullPath(destination).TrimEnd('\\')+"\\";
        if(destinationRoot.StartsWith(sourceRoot,StringComparison.OrdinalIgnoreCase)||sourceRoot.StartsWith(destinationRoot,StringComparison.OrdinalIgnoreCase))throw new IOException("Prism source and private runtime must not contain each other.");
        var files=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(string name in new[]{"DSI.Scaling.exe","DSI.Scaling.exe.config","DSI.Gpu.dll","DSI.Foreground.dll","DSI.RecoveryProbe.dll"}) {
            string path=Path.Combine(source,name);
            if(File.Exists(path))AddRuntimeFile(files,sourceRoot,path);
            else if(name!="DSI.RecoveryProbe.dll")throw new FileNotFoundException("Required Prism runtime file is missing.",path);
        }
        foreach(string name in new[]{"assets","locales","messages","vendor"}) {
            string path=Path.Combine(source,name);if(Directory.Exists(path))CollectRuntimeFiles(files,sourceRoot,path);
        }
        var closure=new StringBuilder();
        foreach(var file in files){CheckStop();closure.Append(file.Key).Append('\n').Append(Hash(file.Value)).Append('\n');}
        string identity;using(var sha=SHA256.Create())identity=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(closure.ToString()))).Replace("-","").ToLowerInvariant();
        string cache=Path.Combine(dataDirectory,"prism-runtime-cache",identity);
        using(var cacheLock=new Mutex(false,"Local\\FlutterDeck.PrismCache."+identity)) {
            bool entered=false;
            try {
                while(!entered){CheckStop();try{entered=cacheLock.WaitOne(100);}catch(AbandonedMutexException){entered=true;}}
                string marker=Path.Combine(cache,"payload.complete");
                if(!File.Exists(marker)) {
                    // Preserve partial cache evidence. A fresh unique directory
                    // prevents overwriting incomplete files from an earlier run.
                    if(Directory.Exists(cache))cache=cache+"-"+Guid.NewGuid().ToString("N");
                    Directory.CreateDirectory(cache);
                    foreach(var file in files){CheckStop();string output=Path.Combine(cache,file.Key);Directory.CreateDirectory(Path.GetDirectoryName(output));File.Copy(file.Value,output,false);if(Hash(output)!=Hash(file.Value))throw new IOException("Prism runtime changed while staging.");}
                    File.WriteAllText(Path.Combine(cache,"payload.complete"),identity,new UTF8Encoding(false));
                } else if(File.ReadAllText(marker)!=identity)throw new IOException("Private Prism runtime cache identity is invalid.");
                Directory.CreateDirectory(destination);
                foreach(var file in files) {
                    CheckStop();string cached=Path.Combine(cache,file.Key),output=Path.Combine(destination,file.Key);
                    if(Hash(cached)!=Hash(file.Value))throw new IOException("Private Prism runtime cache content changed.");
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    // A unique executable path gives this process its own Prism
                    // singleton. DLL/data hard links avoid copying the payload
                    // for every game; files remain private immutable artifacts.
                    if(file.Key=="DSI.Scaling.exe"||!CreateHardLink(output,cached,IntPtr.Zero))File.Copy(cached,output,false);
                }
            }finally{if(entered)cacheLock.ReleaseMutex();}
        }
    }
    static void CollectRuntimeFiles(SortedDictionary<string,string> files,string root,string directory) {
        if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked Prism runtime directories are unsupported.");
        foreach(string file in Directory.GetFiles(directory))AddRuntimeFile(files,root,file);
        foreach(string child in Directory.GetDirectories(directory))CollectRuntimeFiles(files,root,child);
    }
    static void AddRuntimeFile(SortedDictionary<string,string> files,string root,string file) {
        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked Prism runtime files are unsupported.");
        files.Add(Path.GetFullPath(file).Substring(root.Length),file);
    }

    public bool OwnsForegroundWindow() {
        try {
            Process process=owned;if(process==null||process.HasExited)return false;
            uint pid;IntPtr foreground=GetForegroundWindow();
            return foreground!=IntPtr.Zero&&GetWindowThreadProcessId(foreground,out pid)!=0&&pid==process.Id&&process.StartTime.ToUniversalTime().Ticks==ownedStarted;
        }catch{return false;}
    }

    public void StopOwned() {
        Interlocked.Exchange(ref stopRequested,1);
        lock(operation) {
            if(owned==null)return;
            try {
                if(!owned.HasExited) {
                    RequireOwned();owned.CloseMainWindow();
                    if(!owned.WaitForExit(5000)){RequireOwned();owned.Kill();owned.WaitForExit(3000);}
                }
            }finally{owned.Dispose();owned=null;}
        }
    }
    public void Dispose(){StopOwned();}
    sealed class UnknownCommandException:Exception {public UnknownCommandException(string message):base(message){}}
}
