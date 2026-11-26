$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\MediaFoldersPureTest.exe MediaFolders.cs LibraryCore.cs DeckPaths.cs ..\tests\MediaFoldersPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Library folder test compile failed'}
& .\.dream-loop\MediaFoldersPureTest.exe .dream-loop\folders-fixture
if($LASTEXITCODE -ne 0){throw 'Library folder tests failed'}
$m=Get-Content MediaFolders.cs -Raw
$code=($m -replace '(?m)//.*$','')
# The search only reads the user's folder: no program start, no network, and no write other than its own two settings files.
if($code -cmatch 'Process|System\.Net|WebRequest|WebClient|HttpClient|Registry|DllImport|Reflection'){throw 'MediaFolders.cs must not start a program, use the network, the registry, native calls or reflection'}
if($code -cmatch 'Directory\.Delete|Directory\.Move|File\.Copy|File\.SetAttributes|File\.AppendAll|File\.WriteAllText|File\.WriteAllLines|File\.Create|File\.Open(Write)?\(|\.MoveTo\(|\.CopyTo\(|\.Create(Text)?\(\)|StreamWriter|FileStream|FileMode\.|FileAccess\.|SetLastWriteTime|SetCreationTime|\.Encrypt\(|\.Decrypt\('){throw 'MediaFolders.cs must not change, copy or remove a file or folder'}
if(([regex]::Matches($code,'\.Delete\(')).Count -ne 1){throw 'MediaFolders.cs deletes only its own temporary file'}
if(([regex]::Matches($code,'File\.WriteAllBytes\(')).Count -ne 1 -or ([regex]::Matches($code,'File\.Delete\(')).Count -ne 1 -or ([regex]::Matches($code,'File\.Move\(')).Count -ne 1 -or ([regex]::Matches($code,'File\.Replace\(')).Count -ne 1 -or -not $code.Contains('File.WriteAllBytes(temp,bytes);if(File.Exists(path))File.Replace(temp,path,path+".bak");else File.Move(temp,path);}') -or -not $code.Contains('finally{if(File.Exists(temp))File.Delete(temp);}')){throw 'MediaFolders.cs writes only through WriteWhole (its own settings files, through a temporary file)'}
foreach($g in @('DriveType.Network','if(gamesOnly)continue;','int link=LinkState(sub);if(link<0){result.Unread=true;continue;}','if(link>0||Skipped(sub))continue;','if(++result.Entries>MaxEntries){result.Truncated=true;break;}','if(scan==null||scan.Problem.Length>0||scan.Root.Length==0)return next;','if(scan.Complete)next.RemoveAll(','!Inside(root,path))return null;','public bool Complete {get{return Problem.Length==0&&!Truncated&&!Unread;}}')){if(-not $m.Contains($g)){throw "Missing library folder guard: $g"}}
$p=Get-Content Program.cs -Raw
# The launcher: a search never changes the chosen game, never acknowledges a build, and never touches a running game's settings.
foreach($g in @('MediaFolders.LoadFolders(MediaFolders.PathName)','MediaFolders.LoadFound(MediaFolders.FoundPathName)','scan=MediaFolders.Scan(folder,DiscGame,ProgramGame,true);','if(System.Threading.Interlocked.Exchange(ref folderBusy,1)==1){folderPending=true;return;}','if(setupLoadFailed||GameRunning||screen=="Setup")return said;','if(discs[slot].Count!=1||GameRuntime.CheckCue(localSetup.Game[slot]).Length==0||','if(runtimeLoadFailed||GameRunning)return said;','if(programs[slot].Count!=1)continue;','if(complete&&used>0){said+=SetFoundDiscs(discs);said+=SetFoundPrograms(programs);}','candidate.Game[slot]=discs[slot][0];','runtimes[slot].Program=programs[slot][0];','if(!missing)continue;','GameRuntime.Save(GameRuntime.SettingsPath,runtimes[slot],slot+1);','"library-folder-add"','"library-folder-note"','all.AddRange(MediaFolders.Shown(libraryUser,libraryFound));')){if(-not $p.Contains($g)){throw "Missing library folder guard in Program.cs: $g"}}
foreach($name in 'ScanFolders','ApplyScans','SetFoundDiscs','SetFoundPrograms','AddLibraryFolder','StopLibraryFolder','RenderLibraryFolders','FoundPicture','SetPictureLater','LibraryAll','DiscGame','ProgramGame'){
  $body=[regex]::Match($p,'(?s)(void|string|int|ImageSource|List<LibraryItem>) '+$name+'\(.*?\r?\n  \}').Value
  if($body.Length -eq 0){throw "Method not found: $name"}
  if($body -cmatch '(?<![A-Za-z_])game\s*(=[^=]|\+\+|--)|AcceptedHash|gamePlay|PlayGame|PressPlay|ShowLibraryItem|OpenLibraryItem|LibraryPrimary|LibraryOpen|Navigate\(|GameRuntime\.(Start|Identity|Readiness|AdoptCard|ReleaseLock)|Process\.|\.Delete\(|\.MoveTo\(|\.CopyTo\(|File\.(Move|Copy|Write|Append|Create|Replace|SetAttributes)|Directory\.(Delete|Move|Create)|libraryUser\s*(=[^=]|\.(Add|AddRange|Insert|Remove|RemoveAll|RemoveAt|Clear)\()|runtimes\[(?!slot\])|\.Game\[(?!slot\])|\.Bios\[|StreamWriter|FileMode\.(Create|Append|OpenOrCreate|Truncate)|FileAccess\.(Write|ReadWrite)'){throw "$name must not choose a game, acknowledge a build, start anything or change a file of the user"}
}
$d=Get-Content DeckLibrary.cs -Raw
foreach($g in @('LibraryAll()','MediaFolders.Episodes(item.Path)','MediaFolders.Pictures(item.Path)','if(item.BuiltIn==0&&!item.Found)actions.Children.Add(LibButton("Remove from library",','LibraryPrimary(new LibraryItem{Kind="movie",Title=LibraryCore.TitleFromPath(file),Path=file},item)')){if(-not $d.Contains($g)){throw "Missing library folder guard in DeckLibrary.cs: $g"}}
if($d.Contains('LibraryCore.All(libraryUser)')){throw 'The Library must show the found items too (LibraryAll)'}
# Found items never go into library.txt, and both new files are settings that uninstall removes and the build includes.
if($p -cmatch 'libraryUser\.Add(Range)?\(\s*(MediaFolders|libraryFound)'){throw 'A found item must not be added to the hand-made list'}
# A build is acknowledged in the same two places as before, and a file is opened from the same places as before, in the whole of Program.cs.
if(([regex]::Matches($p,'AcceptedHash\s*=[^=]')).Count -ne 2){throw 'AcceptedHash is written only by the confirm button and when the program changes'}
if(([regex]::Matches($p,'OpenLibraryItem\(')).Count -ne 1 -or ([regex]::Matches($p,'LibraryOpen\.Open\(')).Count -ne 1){throw 'Program.cs opens a Library file from one method only'}
if(([regex]::Matches($p,'SetFoundDiscs\(')).Count -ne 2 -or ([regex]::Matches($p,'SetFoundPrograms\(')).Count -ne 2 -or ([regex]::Matches($p,'ApplyScans\(')).Count -ne 2){throw 'The found settings are applied from one place'}
if(-not (Get-Content InstallCore.cs -Raw).Contains('"library-folders.txt","library-found.txt"')){throw 'The two library folder files are missing from the uninstall list'}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('MediaFolders.cs')){throw 'MediaFolders.cs missing from build'}
'PASS: library folder tests on a scratch tree with stand-ins; the search only reads, refuses network and linked folders, and writes only its own two settings files; a search with a problem removes nothing; a search never chooses a game, never acknowledges a build and sets a disc image or game program only for the game slot it names. No real folder of the user, disc image or game program was read by this check.'
