$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\BuildPureTest.exe BuildCore.cs BuildRun.cs GameFilesOpen.cs ..\tests\BuildPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Builder test compile failed'}
& .\.dream-loop\BuildPureTest.exe .dream-loop\builder-fixture
if($LASTEXITCODE -ne 0){throw 'Builder tests failed'}
# Code without its comments: block comments first (also over several lines), then whole-line comments.
function Code($name){ return ([regex]::Replace((Get-Content $name -Raw),'(?s)/\*.*?\*/','') -replace '(?m)^\s*//.*$','') }
$core=Code 'BuildCore.cs'; $run=Code 'BuildRun.cs'; $deck=Code 'DeckBuilder.cs'; $p=Code 'Program.cs'
# BuildCore.cs is pure and writes nothing.
if($core -cmatch 'System\.Windows|System\.Net|Process|Registry|DllImport|Clipboard'){throw 'BuildCore.cs must stay free of window, network, process and registry code'}
if($core -cmatch 'File\.(Write|Append|Delete|Move|Copy|Create|Replace)|Directory\.(Create|Delete|Move)|SearchOption|StreamWriter|FileMode\.(Create|CreateNew|Append|Truncate|OpenOrCreate)|FileAccess\.(Write|ReadWrite)'){throw 'BuildCore.cs must write nothing'}
# The command is the contract's, and the developer switch that skips the self-test is nowhere in the launcher.
if(-not $core.Contains('return "-B "+Quote(Path.Combine(t.Kit,"build_game.py"))+" --cue "+Quote(cue)+" --out "+Quote(outFolder)+" --zig "+Quote(t.Zig);')){throw 'The build command must be the one of contracts BUILD-1 and BUILD-2'}
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -ne '..\tests\BuildPureTest.cs' })){ if((Get-Content $f.FullName -Raw).Contains('skip-selftest')){throw "$($f.Name) must not know the switch that skips the self-test"} }
# A built program starts only while its marker proves it: its game, state, self-test, a program inside the folder, and
# its hash. Anything inside the launcher's folder for built games needs a marker, whatever game it is started as.
foreach($need in @('if(m.State!="ready")return','if(m.Selftest!="passed")return','if(!Inside(full,outFolder))return','if(!String.Equals(got,m.ExeSha256,StringComparison.OrdinalIgnoreCase))return','if(!String.Equals(exe,full,StringComparison.OrdinalIgnoreCase)){','if(homes.Count==0)return "";','foreach(string home in homes){','string exe,hash;string problem=BuiltProblem(home,g,out exe,out hash,hasher);','if(t.KitVersion<g.OldestKit)return','m.Exe.Contains("..")','KitKey="kit",OldestKit=13,MarkerNamesGame=false,','KitKey="kit2",OldestKit=1,MarkerNamesGame=true,','KitKey="kit3",OldestKit=1,MarkerNamesGame=true,','public static BuildGame Game(int id){foreach(var g in Table)if(g.Id==id)return g;return null;}','if(!MarkerIsFor(m,g))return','public static bool MarkerIsFor(BuildMarker m,BuildGame g){return m!=null&&g!=null&&(m.HasGame?m.Game==g.Code:!g.MarkerNamesGame);}','if(g==null)return "This game cannot be started.";','if(Inside(full,built)){','if(first.Length==0)return','if(line!=null&&line!=g.GameLine)return','if(line==null&&g.MarkerNamesGame)return','if(!String.Equals(got,t.ManifestPin,StringComparison.OrdinalIgnoreCase))return','if(d.TryGetValue("game",out v)){m.Game=v;m.HasGame=true;}','if(!named)return new BuildTools();','if(!String.Equals(first,g.Slug,StringComparison.OrdinalIgnoreCase))return','if(String.IsNullOrEmpty(builtId))return "The folder for built games cannot be checked.";','if(id==builtId){root=dir.TrimEnd(','string built;try{built=Path.GetFullPath(BuiltRoot(dataRoot)).TrimEnd(''\\'');}catch(Exception){return "The folder for built games cannot be checked.";}','string place;try{place=real(full);}catch(Exception){place=null;}','if(String.IsNullOrEmpty(place)){if(!File.Exists(full))return "";return "The place of this game program cannot be checked.','full=place;','string builtPlace;try{builtPlace=real(built);}catch(Exception){builtPlace=null;}','if(String.IsNullOrEmpty(builtPlace))return "The folder for built games cannot be checked.";','built=builtPlace.TrimEnd(','if(it==me){root=built;first=other.Slug;}','if(String.IsNullOrEmpty(a)||a!=b)return','if(!homes.Exists(delegate(string h){return String.Equals(h.TrimEnd(''\\''),own,StringComparison.OrdinalIgnoreCase);}))homes.Add(own);','top.Append(depth==1||(depth==0&&c==''}'')?c:'' '');','text=top.ToString();')){ if(-not $core.Contains($need)){throw "BuildCore.cs lost a rule: $need"} }
# BuildRun.cs starts two programs only, writes two files only, and sends nothing.
if($run -cmatch 'System\.Net|System\.Windows|Registry|Clipboard|WebClient|Http'){throw 'BuildRun.cs must stay free of network, window and registry code'}
if(([regex]::Matches($run,'DllImport\(')).Count -ne 7 -or ([regex]::Matches($run,'DllImport\("kernel32\.dll"[^\]]*\)\] static extern [\w.]+ (CreateJobObject|SetInformationJobObject|AssignProcessToJobObject|CloseHandle|CreateFile|GetFinalPathNameByHandle|GetFileInformationByHandle)\(')).Count -ne 7){throw 'BuildRun.cs calls Windows itself only to make its one job object and to ask for the true place of a file'}
if(([regex]::Matches($run,'CreateFile\(')).Count -ne 3 -or ([regex]::Matches($run,'using\(var file=CreateFile\(path,0,7,IntPtr\.Zero,3,0x02000000,IntPtr\.Zero\)\)')).Count -ne 2){throw 'RealPath and FileId open the file with no access, and nothing else in BuildRun.cs opens a file that way'}
if(([regex]::Matches($run,'new ProcessStartInfo\(')).Count -ne 2 -or -not $run.Contains('new ProcessStartInfo(tools.Python,BuildCore.Arguments(tools,cue,outFolder)){UseShellExecute=false,CreateNoWindow=true,') -or -not $run.Contains('new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"taskkill.exe"),"/PID "+p.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)+" /T /F"){UseShellExecute=false,CreateNoWindow=true}')){throw 'BuildRun.cs starts the build Python and taskkill only, both without a window and without the shell'}
if(([regex]::Matches($run,'new FileStream\(')).Count -ne 2 -or -not $run.Contains('new FileStream(BuildCore.LockPath(dataRoot,game),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,1,FileOptions.DeleteOnClose)') -or -not $run.Contains('new FileStream(BuildCore.LogPath(dataRoot,game),FileMode.Create,FileAccess.Write,FileShare.Read)') -or -not $run.Contains('string outFolder=BuildCore.OutputFolder(dataRoot,game);') -or $run -cmatch 'File\.(Write|Append|Delete|Move|Copy|Replace)|Directory\.(Delete|Move)'){throw 'BuildRun.cs writes the gate file and the text of the last build only'}
if(-not $run.Contains('key.StartsWith("RMX_",StringComparison.OrdinalIgnoreCase)||key.StartsWith("PYTHON",StringComparison.OrdinalIgnoreCase)') -or -not $run.Contains('foreach(string key in drop)info.EnvironmentVariables.Remove(key);')){throw 'The build must not get the settings of this launcher or of a Python on this PC'}
# DeckBuilder.cs: the built program is never copied or sent; a build starts after a yes; the log gets fixed words.
if($deck -cmatch 'System\.Net|WebClient|Http|Registry|DllImport|Process\.|ProcessStartInfo|explorer|File\.(Write|Append|Delete|Move|Copy|Replace|Open)|Directory\.(Create|Delete|Move)|FileStream|StreamWriter'){throw 'DeckBuilder.cs must not copy, move, send or write files, and must not start programs itself'}
if(([regex]::Matches($deck,'GameBuild\.Start\(')).Count -ne 1){throw 'A build is started in one place'}
$start=[regex]::Match($deck,'(?s)void BuildStart\(\)\{.*?\r?\n  \}').Value
if($start.Length -eq 0 -or $start.IndexOf('    if(MessageBox.Show(buildWindow??(Window)this,"Build "+g.Name+" from your disc image?') -lt 0 -or $start.IndexOf('MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;') -lt 0 -or $start.IndexOf('MessageBoxButton.OKCancel') -gt $start.IndexOf('GameBuild.Start(') -or $start.IndexOf('BuildCore.SyncProblem(BuildFolderOf(g),BuildCore.SyncRoots(),') -lt 0 -or $start.IndexOf('BuildCore.SyncProblem') -gt $start.IndexOf('GameBuild.Start(') -or $start.IndexOf('if(GameRunning)') -lt 0 -or $start.IndexOf('var g=BuildCore.Game(buildPick);if(g==null)return;') -lt 0 -or $start.IndexOf('var g=BuildCore.Game(buildPick)') -gt $start.IndexOf('BuildToolsNow(g,true)') -or $start.IndexOf('string cue=localSetup.Game[g.Id-1]??"";') -lt 0 -or $start.IndexOf('buildRun=GameBuild.Start(tools,cue,DeckPaths.Root,g,') -lt 0 -or $start.IndexOf('BuildEnded(ticket,code,g);') -lt 0 -or $start.IndexOf('if(BuildCore.AnyBuildRunning(DeckPaths.Root)){BuildSay(') -lt 0 -or $start.IndexOf('if(buildRun!=null||GameRunning||BuildCore.AnyBuildRunning(DeckPaths.Root))return;') -lt $start.IndexOf('MessageBoxButton.OKCancel') -or $start.IndexOf('if(buildRun!=null||GameRunning||BuildCore.AnyBuildRunning(DeckPaths.Root))return;') -gt $start.IndexOf('GameBuild.Start(') -or ([regex]::Matches($start,'\bg\s*=[^=]')).Count -ne 1){throw 'A build starts only after the checks and the yes of the player, for one game that is fixed at the start'}
if($deck -cmatch 'sessionLog\.Add\((?!"game-build-(started|ready|stopped|cancelled)"\)|"game-program-set"\)|"game-files-opened"\))'){throw 'The session log gets fixed words only from the builder'}
if(([regex]::Matches($deck,'Clipboard\.')).Count -ne 1){throw 'The clipboard is used for the details of the last build only'}
if(([regex]::Matches($deck,'\.Program=exe')).Count -ne 1 -or ([regex]::Matches($deck,'slot\.Program=exe')).Count -ne 1 -or -not $deck.Contains('var slot=runtimes[g.Id-1];') -or ([regex]::Matches($deck,'BuildUse\(g,exe,hash\)')).Count -ne 2 -or ([regex]::Matches($deck,'BuildUse\(')).Count -ne 3 -or ([regex]::Matches($deck,'string exe,hash;string problem=BuildCore\.BuiltProblem\(BuildFolderOf\(g\),g,out exe,out hash\);')).Count -ne 2 -or ([regex]::Matches($deck,'BuiltProblem\(')).Count -ne 2){throw 'The built program becomes the game program in one place, after the marker of its game proved it'}
$use=[regex]::Match($deck,'(?s)  string BuildUse\(BuildGame g,string exe,string hash\)\{.*?\r?\n  \}').Value
if($use.Length -eq 0 -or $use.IndexOf('string proven=BuildCore.StartProblem(exe,g,DeckPaths.Root,null,GameBuild.RealPath,GameBuild.FileId);') -lt 0 -or $use -cmatch '\bg\s*=[^=]' -or $use.IndexOf('if(proven.Length>0)return proven;') -lt 0 -or $use.IndexOf('if(proven.Length>0)return proven;') -gt $use.IndexOf('slot.Program=exe') -or $use.IndexOf('GameRuntime.Save(GameRuntime.SettingsPath,slot,g.Id)') -lt 0 -or $use.IndexOf('if(RunningGameIs(g.Id))') -lt 0){throw 'BuildUse names a program only for the game its marker names, in that game''s own settings'}
# The builder never takes the game from what is selected now, except in the two gates (which are about the game to start).
if(([regex]::Matches($deck,'game==')).Count -ne 0 -or ([regex]::Matches($deck,'BuildCore\.Game\(game\)')).Count -ne 2 -or ([regex]::Matches($deck,'\bruntime\.')).Count -ne 2 -or $deck -cmatch 'runtimes\[[0-9]\]|Game\[[0-9]\]|GameRuntime\.Game\([0-9]\)|RunningGameIs\([0-9]\)|BuildCore\.Game\([0-9]\)(?!\?\?)' ){throw 'DeckBuilder.cs must take the game of a build from the build, not from a fixed number or the selected card'}
$ended=[regex]::Match($deck,'(?s)void BuildEnded\(int ticket,int code,BuildGame g\)\{.*?\r?\n  \}').Value
if($ended.Length -eq 0 -or $ended.IndexOf('if(problem.Length>0)') -lt 0 -or $ended.IndexOf('if(problem.Length>0)') -gt $ended.IndexOf('BuildUse(g,exe,hash)') -or $ended.IndexOf('else if(code==0)') -gt $ended.IndexOf('BuildUse(g,exe,hash)') -or $ended.IndexOf('BuildUse(g,exe,hash)') -lt 0 -or $ended.IndexOf('if(problem.Length==0&&(g.MarkerNamesGame?p.Game!=g.Code:p.Game.Length>0&&p.Game!=g.Code))problem=') -lt 0 -or $ended.IndexOf('var run=buildRun;buildRun=null;buildPick=g.Id;') -lt 0 -or $ended -cmatch '\bg\s*=[^=]'){throw 'After a build, only exit code 0 and a proven marker name the program'}
if(-not $deck.Contains('if(runtimeLoadFailed)return "The saved settings need attention first.')){throw 'BuildUse must not write over settings that could not be read'}
# The game program is asked what it is in one place, and the builder's check comes first.
if(([regex]::Matches($p,'GameRuntime\.Readiness\(')).Count -ne 1 -or -not $p.Contains('playCheck=BuildReadiness()??GameRuntime.Readiness(runtime,')){throw 'Program.cs must ask BuildReadiness() before its one GameRuntime.Readiness'}
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notmatch 'Test\.cs$' -and $_.Name -notin 'Program.cs','GameRuntime.cs' })){ if((Get-Content $f.FullName -Raw) -cmatch 'GameRuntime\.(Readiness|Identity)\('){throw "$($f.Name) must not ask a game program what it is past the gate"} }
if(-not $deck.Contains('Closed+=delegate{EndBuild();};') -or -not $deck.Contains('void EndBuild(){var run=buildRun;if(run==null)return;buildTicket++;buildRun=null;run.Cancel();}')){throw 'Closing the launcher must end a running build'}
# Program.cs: the gate is passed before the one place that starts a game.
if(([regex]::Matches($p,'GameRuntime\.Start\(')).Count -ne 1 -or $p -cnotmatch '(?m)^\s*BuildGate\(\);[^\r\n]*\r?\n\s*var session=GameRuntime\.Start\(' -or $p -cmatch 'GameRuntime\.Identity\('){throw 'Program.cs must call BuildGate() right before its one GameRuntime.Start'}
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notmatch 'Test\.cs$' -and $_.Name -ne 'Program.cs' })){ if((Get-Content $f.FullName -Raw) -cmatch 'GameRuntime\.Start\('){throw "$($f.Name) must not start a game past the gate"} }
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notin 'DeckBuilder.cs','BuildRun.cs','..\tests\BuildPureTest.cs' })){ if((Get-Content $f.FullName -Raw) -cmatch '\bGameBuild\b'){throw "$($f.Name) must not run a build"} }
# The hooks in the shared files.
if(-not (Get-Content DeckLibrary.cs -Raw).Contains('if(BuildOffered(item))actions.Children.Add(LibButton("Build my game...",delegate{ShowBuildWindow(item.BuiltIn);},"library-build","Build my game from my disc"));')){throw 'The Library panel lost the build button'}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains(' BuildCore.cs BuildRun.cs DeckBuilder.cs GameFilesOpen.cs ')){throw '..\scripts\build.ps1 must compile the builder files'}
# The Files button: one folder only (the game's own build folder, never a path from the marker), shown by Windows; a
# folder, never a file; and both buttons go through the one method.
$files=Code 'GameFilesOpen.cs'
if(([regex]::Matches($files,'Process\.Start\(')).Count -ne 1 -or -not $files.Contains('Process.Start(new ProcessStartInfo(folder.TrimEnd(''\\'')+"\\"){UseShellExecute=true,Verb="open"});') -or -not $files.Contains('||Path.GetExtension(folder.TrimEnd(''\\'')).Length>0||!Directory.Exists(folder))return') -or $files.IndexOf('!Directory.Exists(folder))return') -lt 0 -or $files.IndexOf('!Directory.Exists(folder))return') -gt $files.IndexOf('Process.Start(') -or $files -cmatch 'System\.Net|File\.|Directory\.(Create|Delete|Move)|Copy|WebClient|Http'){throw 'GameFilesOpen.cs shows one existing folder and does nothing else'}
if(-not $core.Contains('return MarkerIsFor(ReadMarker(folder),g)?folder:"";') -or -not $core.Contains('string folder=Path.GetFullPath(OutputFolder(dataRoot,g));')){throw 'The Files folder is the build folder of the game, and only when its marker is this game''s'}
if(([regex]::Matches($deck,'GameFilesOpen\.Open\(')).Count -ne 1 -or -not $deck.Contains('string folder=BuildCore.FilesFolder(DeckPaths.Root,g);') -or -not $deck.Contains('string said=GameFilesOpen.Open(folder);')){throw 'DeckBuilder.cs opens the Files folder in one place, from BuildCore.FilesFolder'}
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notin 'DeckBuilder.cs','GameFilesOpen.cs','..\tests\BuildPureTest.cs' })){ if((Code $f.Name) -cmatch '\bGameFilesOpen\b'){throw "$($f.Name) must not open a folder of game files itself"} }
if(-not (Get-Content DeckLibrary.cs -Raw).Contains('delegate{OpenGameFiles(item.BuiltIn);},"library-files"') -or -not $p.Contains('IconAction("Files",delegate{OpenGameFiles(game);},1416);')){throw 'Both Files buttons must open the files of the game they belong to'}
if(-not (Get-Content Foundation.cs -Raw).Contains('case "game-files-opened":')){throw 'The session log must accept the Files word'}
if(-not (Get-Content Foundation.cs -Raw).Contains('case "game-build-started":case "game-build-ready":case "game-build-stopped":case "game-build-cancelled":')){throw 'The session log must accept the builder words'}
if(-not (Get-Content InstallCore.cs -Raw).Contains('"builder-tools.txt"')){throw 'Uninstall must remove the pointer file of the build tools'}
# The build tools and the kit are never inside this folder (a snapshot commit would publish them), and no source or
# record names where they are on a developer's PC.
if(Test-Path builder){throw 'A folder named builder must not be inside the source folder'}
foreach($f in (Get-ChildItem *.cs,..\tests\*.cs,..\scripts\*.ps1,..\docs\*.md,..\docs\specs\*.md,..\README.md | Where-Object { $_.Name -ne 'check-builder.ps1' })){ if((Get-Content $f.FullName -Raw) -cmatch 'builder-kit-20|rmx-pyembed|rmx-zig'){throw "$($f.Name) must not name the place of the build tools on a developer's PC"} }
# The two gate methods are pinned as whole text, and each is named once where it is made and once where it is called,
# so no local of the same name, no early return and no condition in front can switch a gate off unnoticed.
function Body($text,$pattern){ return (([regex]::Match($text,$pattern).Value -replace "`r`n","`n") -replace '[ \t]+(?=\n)','') }
$gateWant=@'
  void BuildGate(){
    if(buildRun!=null||BuildCore.AnyBuildRunning(DeckPaths.Root))throw new InvalidOperationException("A game is being built. Wait until the build has finished, or cancel it. Nothing was started.");
    string stop=BuildCore.StartProblem(runtime.Program,BuildCore.Game(game),DeckPaths.Root,null,GameBuild.RealPath,GameBuild.FileId);
    if(stop.Length>0)throw new InvalidOperationException(stop+" Nothing was started.");
  }
'@ -replace "`r`n","`n"
$readyWant=@'
  RuntimeCheck BuildReadiness(){
    if(buildRun!=null||BuildCore.AnyBuildRunning(DeckPaths.Root))return new RuntimeCheck{Problem="A game is being built. Wait until the build has finished, or cancel it."};
    string stop=BuildCore.StartProblem(runtime.Program,BuildCore.Game(game),DeckPaths.Root,GameRuntime.CachedHash,GameBuild.RealPath,GameBuild.FileId);
    return stop.Length>0?new RuntimeCheck{Problem=stop}:null;
  }
'@ -replace "`r`n","`n"
if((Body $deck '(?s)  void BuildGate\(\)\{.*?\r?\n  \}') -cne $gateWant){throw 'BuildGate is not the text this check knows. Change the check together with the method, after a review.'}
if((Body $deck '(?s)  RuntimeCheck BuildReadiness\(\)\{.*?\r?\n  \}') -cne $readyWant){throw 'BuildReadiness is not the text this check knows. Change the check together with the method, after a review.'}
if(([regex]::Matches($deck,'\bBuildGate\b')).Count -ne 1 -or ([regex]::Matches($deck,'\bBuildReadiness\b')).Count -ne 1 -or ([regex]::Matches($p,'\bBuildGate\b')).Count -ne 1 -or ([regex]::Matches($p,'\bBuildReadiness\b')).Count -ne 1){throw 'Each gate is made once in DeckBuilder.cs and called once in Program.cs'}
if($p -cnotmatch 'try\{[ \t]*\r?\n[ \t]*BuildGate\(\);[ \t]*\r?\n[ \t]*var session=GameRuntime\.Start\('){throw 'In PlayGame the gate is the first statement of the try block, right before the start'}
if(([regex]::Matches($p,'GameRuntime\s*\.\s*Start\s*\(')).Count -ne 1 -or ([regex]::Matches($p,'GameRuntime\s*\.\s*Readiness\s*\(')).Count -ne 1 -or $p -cmatch 'GameRuntime\s*\.\s*Identity\s*\('){throw 'Program.cs runs a game program in two places only, both behind a gate'}
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notmatch 'Test\.cs$' -and $_.Name -notin 'Program.cs','DeckBuilder.cs' })){ if((Code $f.Name) -cmatch '\b(BuildGate|BuildReadiness)\b'){throw "$($f.Name) must not name a gate method"} }
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notmatch 'Test\.cs$' -and $_.Name -notin 'Program.cs','GameRuntime.cs' })){ if((Code $f.Name) -cmatch 'GameRuntime\s*\.\s*(Start|Readiness|Identity)\s*\('){throw "$($f.Name) must not run a game program past the gates"} }
# Every check of a start in the launcher goes by the true place of the program (a folder link cannot stand in for it).
if(([regex]::Matches($deck,'BuildCore\.StartProblem\(')).Count -ne 3 -or ([regex]::Matches($deck,'BuildCore\.StartProblem\([^;]*,GameBuild\.RealPath,GameBuild\.FileId\);')).Count -ne 3){throw 'Each StartProblem call in DeckBuilder.cs must give GameBuild.RealPath and GameBuild.FileId'}
foreach($f in (Get-ChildItem *.cs | Where-Object { $_.Name -notmatch 'Test\.cs$' -and $_.Name -notin 'BuildCore.cs','DeckBuilder.cs' })){ if((Code $f.Name) -cmatch 'StartProblem|BuiltProblem'){throw "$($f.Name) must not decide about a built program itself"} }
# The gates take the game the way the launcher itself does, so they agree with the program and the disc image it starts.
if(-not $p.Contains('RuntimeSettings runtime {get{return runtimes[game-1];}}')){throw 'Program.cs changed how it picks the game: review the two gates in DeckBuilder.cs'}
# The methods that decide are pinned as whole text, by the SHA-256 of each (comments left out, line ends made equal).
# A change to one of them fails here with the new number: put it in only after the change was reviewed.
function Pin($text,$pattern,$want,$name){
  $m=[regex]::Matches($text,$pattern); if($m.Count -ne 1){throw "$name was not found exactly once"}
  $body=(($m[0].Value -replace "`r`n","`n") -replace '[ \t]+(?=\n)','')
  $sha=[BitConverter]::ToString([Security.Cryptography.SHA256]::Create().ComputeHash([Text.Encoding]::UTF8.GetBytes($body))).Replace('-','')
  if($env:FD_PRINT_PINS -eq '1'){Write-Output "PIN $name $sha";return}
  if($sha -cne $want){throw "$name is not the text this check knows (now $sha). Change the check together with the method, after a review."}
}
Pin $core '(?s)  public static string StartProblem\(.*?\r?\n  \}' '699FF122923BDA122BE9DBD91C5E4288A6C84423B7E079E956C4B56D7BDA4D68' 'BuildCore.StartProblem'
Pin $core '(?s)  public static string BuiltProblem\(.*?\r?\n  \}' '6EE30E9A43B1093F765A0D7C44CF5EE4A5B4D8CCB668A904DE5812048667F84D' 'BuildCore.BuiltProblem'
Pin $core '  public static bool MarkerIsFor[^\r\n]*' '6CFA5E444647DF92396340CC501F917B9E33AF8448013604639E1FC2259D2449' 'BuildCore.MarkerIsFor'
Pin $core '(?s)  public static string FilesFolder\(.*?\r?\n  \}' '587F147F0CE0546C73B6470264C47F4374D241B1738C9520EE68477B6F4CFE29' 'BuildCore.FilesFolder'
Pin $core '(?s)  static string KitProblem\(.*?\r?\n  \}' '3277BEF7A3583A739C3B78AB8F21DB2A61B32BD9F66F1210BB1BA1C579E1D170' 'BuildCore.KitProblem'
Pin $core '(?s)  public static List<string> BuildFoldersOf\(.*?\r?\n  \}' 'F93C51663140BE3091559FE8DD8C5E1D9CD8FA69E5AE75AE06859E5E3AB33BF4' 'BuildCore.BuildFoldersOf'
Pin $core '(?s)  public static bool Inside\(.*?\r?\n  \}' 'FED5D6965FAAB22666CC9081D4C66FC8C70B5C175C1E7503E2C0E07B5E3BD536' 'BuildCore.Inside'
Pin $run '(?s)  public static string RealPath\(.*?\r?\n  \}' '53EA9A8724AAFDF6199739A0A0645A9FA757D760BA9526546FBF399DFE2B00DD' 'GameBuild.RealPath'
Pin $run '(?s)  public static string FileId\(.*?\r?\n  \}' '93B921562875C3C68EDC71856F585B6194E105F9D8B902CD43CE74092A89E861' 'GameBuild.FileId'
Pin $deck '(?s)  void BuildStart\(\)\{.*?\r?\n  \}' 'B9BAFD553A517F61ADAE56C1A411CF1EE89F38792471B3BAC5B8077BA2E8DB27' 'Deck.BuildStart'
Pin $deck '(?s)  void BuildEnded\(int ticket,int code,BuildGame g\)\{.*?\r?\n  \}' 'E609FADE86EBD1BCE43735C13951169F5E508862854D2874A4854A5A791E0DE3' 'Deck.BuildEnded'
Pin $deck '(?s)  string BuildUse\(BuildGame g,string exe,string hash\)\{.*?\r?\n  \}' '464C68E798EAE303146CA389E3C859639FA09A1485FF24808E5B302BD1F1AEEF' 'Deck.BuildUse'
Pin $deck '(?s)  void OpenGameFiles\(int gameId\)\{.*?\r?\n  \}' 'AAFA00C24477345C5BC52ACDA5ABF50DC6A30307AF644FFEFF823B47A7041EE4' 'Deck.OpenGameFiles'
Pin $files '(?s)  public static string Open\(.*?\r?\n  \}' '012F3A1AD388B3E8A73D41FDA42C6A18DAAF54BFC389810D03AE2F417A1305A1' 'GameFilesOpen.Open'
if($env:FD_PRINT_PINS -eq '1'){throw 'The pins were printed. This run is not a pass.'}
'PASS: builder'
