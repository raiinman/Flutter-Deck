$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\DiscFinderPureTest.exe DiscFinder.cs ..\tests\DiscFinderPureTest.cs LocalSetup.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Disc finder test compile failed'}
$t=Join-Path $Here ('.dream-loop\disc-'+[guid]::NewGuid().ToString('N').Substring(0,8))
try{
  & .\.dream-loop\DiscFinderPureTest.exe $t
  if($LASTEXITCODE -ne 0){throw 'Disc finder checks failed'}
}finally{if(Test-Path $t){Remove-Item -LiteralPath $t -Recurse -Force}}
$c=(Get-Content DiscFinder.cs -Raw) -replace '(?m)^\s*//.*$',''
# The finder only reads the game folder. It starts nothing, reaches no network, and hashes, copies, moves and deletes nothing there.
if($c -cmatch 'System\.Windows|System\.Net|System\.Diagnostics|Process\.|System\.Security\.Cryptography|SHA|MD5|File\.Copy|File\.Move\(.*image|File\.Delete\(.*(image|cue)|FileAccess\.(Write|ReadWrite)|FileMode\.(Create|Append|Truncate|OpenOrCreate)|WriteAllBytes|GameRuntime\.'){throw 'DiscFinder must only read the game folder'}
# Its only writes are the temporary file and the setting itself, inside SaveFolder.
if(([regex]::Matches($c,'File\.(WriteAllText|Replace|Move|Delete)\(')).Count -ne 4 -or ([regex]::Matches($c,'File\.WriteAllText\(temp,')).Count -ne 1){throw 'DiscFinder may write only its own setting, in SaveFolder'}
# The image is opened in one place, for reading, and at most 2 + MaxRootSectors sectors are read (the last one is the start of SYSTEM.CNF, for the disc code).
if(([regex]::Matches($c,'new FileStream\(')).Count -ne 1 -or -not $c.Contains('new FileStream(image,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)') -or -not $c.Contains('Math.Min(MaxRootSectors,(length+2047)/2048)')){throw 'DiscFinder must open the image once, read-only, with a bounded read'}
if(-not $c.Contains('LocalSetup.CheckGame(cue).Length>0)return null') -or $c.IndexOf('LocalSetup.CheckGame(cue).Length>0)return null') -gt $c.IndexOf('new FileStream(')){throw 'A CUE file must pass the setup check before its image is read'}
# After a partial search nothing is set without the user's choice, and the folder is read one entry at a time.
if(-not $c.Contains('return matches.Count==1&&!partial?Use:Choose;') -or $c -cmatch 'Directory\.Get(Files|Directories)\('){throw 'A partial search must not set an image, and the search must enumerate'}
$b=Get-Content ..\scripts\build.ps1 -Raw;if(-not $b.Contains('DiscFinder.cs')){throw 'DiscFinder.cs missing from build'}
$s=Get-Content Program.cs -Raw
foreach($g in @('DiscFinder.LoadFolder(DiscFinder.PathName)','DiscFinder.SaveFolder(DiscFinder.PathName,gameFolder)','DiscFinder.Scan(gameFolder,Kind.DiscProgram)','discScanGame=game;','if(discScan!=null&&discScanGame==game)for(','"game-folder-choose"','"game-folder-search"','"game-folder-use-"','void FindDisc()','bool UseDiscImage(string path,bool found)')){if(-not $s.Contains($g)){throw "Missing game folder guard: $g"}}
# The folder is searched in one place, never while a page is drawn. That place runs at start (only when the saved image does not work) and from the two buttons.
if(([regex]::Matches($s,'DiscFinder\.Scan\(')).Count -ne 1 -or ([regex]::Matches($s,'FindDisc\(\);')).Count -ne 3){throw 'The game folder must be searched from exactly three places'}
# A found image is set through one method. It refuses when the saved setup could not be read, checks the image like a chosen one, and changes only game 1's image.
$use=[regex]::Match($s,'bool UseDiscImage\(string path,bool found\)\{[^\r\n]*').Value
if($use.Length -eq 0 -or $use.IndexOf('if(setupLoadFailed)') -lt 0 -or $use.IndexOf('GameRuntime.CheckCue(path)') -lt 0 -or $use.IndexOf('if(setupLoadFailed)') -gt $use.IndexOf('LocalSetup.Save(') -or $use.IndexOf('GameRuntime.CheckCue(path)') -gt $use.IndexOf('LocalSetup.Save(') -or -not $use.Contains('candidate.Game[game-1]=path;') -or $use -cmatch 'Bios\[|Game\[[0-9]\]|gamePlay|PlayGame|GameRuntime\.Start'){throw 'A found disc image must be set by the guarded method only'}
$find=[regex]::Match($s,'void FindDisc\(\)\{[^\r\n]*').Value
if(-not $find.Contains('string todo=DiscFinder.Decide(GameRuntime.CheckCue(localSetup.Game[game-1]).Length==0,discScan.Matches,discScan.Partial);string seen=gameFolderNote;if(todo==DiscFinder.Use){if(UseDiscImage(discScan.Matches[0],true))')){throw 'A saved disc image that works must never be replaced by the search'}
# No local path and no private file name in the module, its test or its record.
foreach($f in @('DiscFinder.cs','..\tests\DiscFinderPureTest.cs','..\docs\specs\SPEC-disc-folder.md')){$x=Get-Content $f -Raw;if($x -cmatch '(?i)[A-Z]:\\Users\\|OneDrive'){throw "A local path appears in $f"}}
$i=Get-Content InstallCore.cs -Raw;if(-not $i.Contains('"library.txt","game-folder.txt"')){throw 'Uninstall must list game-folder.txt with the other settings'}
'PASS: disc finder compiled alone (program found in four sector layouts, other and damaged images refused, bounded read-only search, decision, sentences, setting, test seam); it only reads the game folder; one guarded place sets a found image and never replaces a saved image that works; game-folder.txt is in the uninstall list. Source guards only: the screen was not run by this check.'
