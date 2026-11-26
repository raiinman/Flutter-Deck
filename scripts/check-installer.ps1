$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe "/reference:$fx\System.IO.Compression.dll" /out:.dream-loop\InstallPureTest.exe InstallCore.cs ..\tests\InstallPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Installer test compile failed'}
& .\.dream-loop\InstallPureTest.exe .dream-loop\install-fixture
if($LASTEXITCODE -ne 0){throw 'Installer tests failed'}
$c=Get-Content InstallCore.cs -Raw
if($c -cmatch 'Directory\.Delete\([^)]*true\)|SearchOption\.AllDirectories|Registry|Process\.|Kill\('){throw 'InstallCore must not delete recursively, touch the registry or start/stop processes'}
if(-not $c.Contains('Directory.Delete(folder,false)')){throw 'Folder removal must be non-recursive'}
$s=Get-Content Setup.cs -Raw
# Per-user only: no machine-wide registry, no elevation, no ending other programs, no hidden install switches.
if($s -cmatch 'LocalMachine|HKEY_LOCAL_MACHINE|runas|Verb\s*=|\.Kill\(|/silent|/quiet|/S\b|/dir'){throw 'Setup must stay per-user, unelevated, visible and non-redirectable'}
foreach($g in @('Registry.CurrentUser.CreateSubKey(Installer.UninstallKey)','if(!String.Equals(location,folder,StringComparison.OrdinalIgnoreCase))return;','if(String.Equals(points,target,StringComparison.OrdinalIgnoreCase))File.Delete(linkPath);','Option("Also remove my Flutter Deck settings','IsChecked=on','if(!Installer.IsInstallFolder(args[1]))','Installer.LauncherInUse(folder)','new SetupWindow(false,Installer.DefaultFolder)')){if(-not $s.Contains($g)){throw "Missing setup guard: $g"}}
if(-not ($s -cmatch 'settings=Option\("Also remove my Flutter Deck settings[^"]*",false\)')){throw 'Settings removal must be off by default'}
# The launcher's own registry touch: correct one value in an existing entry for its own folder, nothing more.
$ai=Get-Content AppInfo.cs -Raw
if($ai -cmatch 'CreateSubKey|DeleteSubKey|DeleteValue|LocalMachine|Process\.'){throw 'AppInfo.cs may only correct the version in an existing current-user entry'}
foreach($g in @('Registry.CurrentUser.OpenSubKey(','if(key==null)return;','StringComparison.OrdinalIgnoreCase))return;','key.SetValue("DisplayVersion",Updater.Version)')){if(-not $ai.Contains($g)){throw "Missing install-record guard: $g"}}
if(([regex]::Matches($ai,'SetValue\(')).Count -ne 1){throw 'AppInfo.cs must write exactly one registry value'}
foreach($g in @('File.Exists(Path.Combine(full,UninstallerName))','RefuseLinks(full);','File.Move(dest,dest+Aside)','names.Add(UninstallerName);')){if(-not $c.Contains($g)){throw "Missing install-core guard: $g"}}
# One version everywhere: launcher file details, Setup file details and the updater all read Updater.Version.
foreach($f in 'AppInfo.cs','Setup.cs'){if(-not (Get-Content $f -Raw).Contains('[assembly: AssemblyFileVersion(Updater.Version+".0")]')){throw "$f must take its version from Updater.Version"}}
$b=Get-Content ..\scripts\build.ps1 -Raw
foreach($g in @('/win32icon:assets\FlutterDeck.ico','AppInfo.cs')){if(-not $b.Contains($g)){throw "..\scripts\build.ps1 missing $g"}}
# The installer itself must still compile from its source list (it once stopped compiling unnoticed when Updater.cs
# gained a need for DeckPaths.cs). It is built here around a stand-in file, into the ignored .dream-loop folder.
# Stand-in build tools (tiny, made here) so the installer can be built quickly: three kits that match their manifests,
# a python.zip and a zig.zip shaped like the official ones. The real ones are passed by ..\scripts\release.ps1.
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$tools=Join-Path $FdSrc '.dream-loop\setup-check-tools'
if(Test-Path -LiteralPath $tools){Remove-Item -LiteralPath $tools -Recurse -Force}
function New-StandInKit([string]$folder,[string]$game){
  $k=Join-Path $tools $folder; New-Item -ItemType Directory -Force (Join-Path $k 'sub') | Out-Null
  $exp='{"kit_version": 1, "game": "'+$game+'"}'
  $files=@{'build_game.py'='print(1)';'expected.json'=$exp;'THIRD-PARTY-NOTICES.txt'='notices';'sub\part.c'='int x;'}
  $m=[ordered]@{}
  foreach($n in ($files.Keys|Sort-Object)){[IO.File]::WriteAllText((Join-Path $k $n),$files[$n]);$m[$n.Replace('\','/')]=(Get-FileHash -LiteralPath (Join-Path $k $n) -Algorithm SHA256).Hash.ToLowerInvariant()}
  [IO.File]::WriteAllText((Join-Path $k 'kit-manifest.json'),($m|ConvertTo-Json))
}
New-StandInKit 'kit' 'Mega Man Legends (USA) SLUS-00603';New-StandInKit 'kit2' 'Mega Man Legends 2 (USA) SLUS-01140';New-StandInKit 'kit3' 'The Misadventures of Tron Bonne (USA) SLUS-00896'
function New-StandInZip([string]$zipPath,[hashtable]$entries){
  $fs=[IO.File]::Open($zipPath,'Create');$z=New-Object IO.Compression.ZipArchive($fs,[IO.Compression.ZipArchiveMode]::Create)
  try{foreach($n in $entries.Keys){$w=New-Object IO.StreamWriter($z.CreateEntry($n).Open());$w.Write($entries[$n]);$w.Dispose()}}finally{$z.Dispose();$fs.Dispose()}
}
New-StandInZip (Join-Path $tools 'python.zip') @{'python.exe'='stand-in';'LICENSE.txt'='psf'}
New-StandInZip (Join-Path $tools 'zig.zip') @{'zig-x/zig.exe'='stand-in';'zig-x/LICENSE'='mit';'zig-x/lib/std/std.zig'='x'}
$standIn=Join-Path $FdSrc '.dream-loop\setup-check-launcher.bin'; [IO.File]::WriteAllText($standIn,'stand-in, not a launcher')
# A launcher without the public mark is refused (developer, 2026-11-20: a public release must never ship with friend play working).
$eap=$ErrorActionPreference;$ErrorActionPreference='Continue';$refusal=(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $FdSrc '..\scripts\build-setup.ps1') -LauncherPath $standIn -Tools $tools -AnyToolZips 2>&1 | Out-String);$refusedCode=$LASTEXITCODE;$ErrorActionPreference=$eap
if($refusedCode -eq 0 -or -not $refusal.Contains('This launcher is not a public build')){throw '..\scripts\build-setup.ps1 packed a launcher that is not a public build'}
[IO.File]::WriteAllBytes($standIn,[Text.Encoding]::ASCII.GetBytes('stand-in, not a launcher ')+[byte[]](0x12)+[Text.Encoding]::ASCII.GetBytes('FlutterDeckEdition')+[byte[]](0x09)+[Text.Encoding]::ASCII.GetBytes('public-v1'))
# A kit holding a file its manifest does not list (a cache, a log, a hidden .git or desktop.ini) is refused.
foreach($stray in 'kit2\stray.log','kit\.git\HEAD'){
  $sp=Join-Path $tools $stray;New-Item -ItemType Directory -Force (Split-Path $sp) | Out-Null;[IO.File]::WriteAllText($sp,'not listed');(Get-Item -LiteralPath $sp -Force).Attributes='Hidden'
  $ErrorActionPreference='Continue';$msg=(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $FdSrc '..\scripts\build-setup.ps1') -LauncherPath $standIn -Tools $tools -AnyToolZips 2>&1 | Out-String);$strayCode=$LASTEXITCODE;$ErrorActionPreference=$eap
  if($strayCode -eq 0 -or -not $msg.Contains('is not in its kit-manifest.json')){throw "..\scripts\build-setup.ps1 packed a kit holding the unlisted hidden file $stray"}
  Remove-Item -LiteralPath $sp -Force
}
# The real Python and Zig zips are pinned; the stand-ins are refused without -AnyToolZips.
$ErrorActionPreference='Continue';$msg=(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $FdSrc '..\scripts\build-setup.ps1') -LauncherPath $standIn -Tools $tools 2>&1 | Out-String);$pinCode=$LASTEXITCODE;$ErrorActionPreference=$eap
if($pinCode -eq 0 -or -not $msg.Contains('python.zip is not the pinned')){throw '..\scripts\build-setup.ps1 must refuse a python.zip that is not the pinned one'}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $FdSrc '..\scripts\build-setup.ps1') -LauncherPath $standIn -Tools $tools -AnyToolZips | Out-Null
if($LASTEXITCODE -ne 0){throw 'The installer does not compile: ..\scripts\build-setup.ps1 failed'}
# The installer carries every tool under the names Setup.cs reads, and the small uninstaller.
$built=[Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $FdSrc '.dream-loop\setup\FlutterDeckSetup.exe')))
$res=@($built.GetManifestResourceNames())
foreach($n in 'P0','P1','P2','P3','P4','P5','P6','Icon','Uninstaller','ToolKit1','ToolKit2','ToolKit3','ToolPython','ToolZig','ToolNotices','ToolZigNotices','ToolPythonNotices'){if($res -notcontains $n){throw "The installer is missing its resource $n"}}
# The kit zip holds exactly the listed files and the manifest.
$kz=New-Object IO.Compression.ZipArchive($built.GetManifestResourceStream('ToolKit1'));try{$kn=@($kz.Entries|ForEach-Object{$_.FullName}|Sort-Object)}finally{$kz.Dispose()}
if(($kn -join ',') -ne 'build_game.py,expected.json,kit-manifest.json,sub/part.c,THIRD-PARTY-NOTICES.txt'){throw "The kit zip must hold exactly the listed files: $($kn -join ',')"}
$sc=Get-Content Setup.cs -Raw
foreach($n in 'ToolKit1","kit"','ToolKit2","kit2"','ToolKit3","kit3"','ToolPython","python"','ToolZig","zig","y"','ToolNotices","NOTICES.txt"','ToolPythonNotices",@"python','ToolZigNotices",@"zig\THIRD-PARTY-NOTICES.txt"'){if(-not $sc.Contains($n)){throw "Setup.cs must unpack $n"}}
if(([Reflection.Assembly]::Load([IO.File]::ReadAllBytes((Join-Path $FdSrc '.dream-loop\setup\FlutterDeckSetup.exe')))).GetManifestResourceStream('Uninstaller').Length -gt 2MB){throw 'The uninstaller must be the small build without the tools'}
foreach($g in @('if(busy)e.Cancel=true;','assembly.GetManifestResourceStream("Uninstaller")','worker.IsBackground=false;','e is NotEnoughSpaceException')){if(-not $sc.Contains($g)){throw "Missing setup guard: $g"}}
# The release zip (..\scripts\build-zip.ps1) is laid out by the installer's own code: the launcher and the builder folder, Zig's top folder
# left out, the notices added; no uninstaller and no install list (developer, 2026-11-21).
$zipOut=Join-Path $FdSrc '.dream-loop\setup-check.zip';if(Test-Path -LiteralPath $zipOut){Remove-Item -LiteralPath $zipOut -Force}
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $FdSrc '..\scripts\build-zip.ps1') -SetupPath (Join-Path $FdSrc '.dream-loop\setup\FlutterDeckSetup.exe') -LauncherPath $standIn -OutZip $zipOut | Out-Null
if($LASTEXITCODE -ne 0){throw '..\scripts\build-zip.ps1 failed on the stand-in installer'}
$zz=[IO.Compression.ZipFile]::OpenRead($zipOut);try{$zn=@($zz.Entries|ForEach-Object{$_.FullName})}finally{$zz.Dispose()}
foreach($n in 'FlutterDeck/FlutterDeck.exe','FlutterDeck/assets/FlutterDeck-OriginalLoop.wav','FlutterDeck/assets/FlutterDeck-DawnLaunch.wav','FlutterDeck/assets/FlutterDeck-SkywayRun.wav','FlutterDeck/codes/memory-codes-list.txt','FlutterDeck/LICENSE','FlutterDeck/NOTICE.md','FlutterDeck/builder/kit/build_game.py','FlutterDeck/builder/kit2/sub/part.c','FlutterDeck/builder/kit3/kit-manifest.json','FlutterDeck/builder/python/python.exe','FlutterDeck/builder/python/THIRD-PARTY-NOTICES.txt','FlutterDeck/builder/zig/zig.exe','FlutterDeck/builder/zig/lib/std/std.zig','FlutterDeck/builder/zig/THIRD-PARTY-NOTICES.txt','FlutterDeck/builder/NOTICES.txt'){if($zn -notcontains $n){throw "The release zip is missing $n"}}
foreach($n in 'FlutterDeck/Uninstall Flutter Deck.exe','FlutterDeck/builder/installed-files.txt'){if($zn -contains $n){throw "The release zip must not hold $n"}}
if(@($zn|Where-Object{$_.Contains('\') -or $_ -like 'FlutterDeck/builder/zig/zig-x/*'}).Count){throw 'The release zip must use / in its names and leave out the Zig top folder'}
Remove-Item -LiteralPath $zipOut -Force
if(-not (Get-Content ..\scripts\build-zip.ps1 -Raw).Contains('is not exactly what Setup installs')){throw '..\scripts\build-zip.ps1 must refuse a zip folder that is not exactly what Setup installs'}
# A launcher file that is not the one inside the installer is refused, and no zip is left behind.
$other=Join-Path $FdSrc '.dream-loop\setup-check-other.bin';[IO.File]::WriteAllText($other,'another file')
$ErrorActionPreference='Continue';$msg=(& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $FdSrc '..\scripts\build-zip.ps1') -SetupPath (Join-Path $FdSrc '.dream-loop\setup\FlutterDeckSetup.exe') -LauncherPath $other -OutZip $zipOut 2>&1 | Out-String);$zipCode=$LASTEXITCODE;$ErrorActionPreference=$eap
Remove-Item -LiteralPath $other -Force
if($zipCode -eq 0 -or -not $msg.Contains('is not the given launcher file') -or (Test-Path -LiteralPath $zipOut)){throw '..\scripts\build-zip.ps1 must refuse a launcher that is not the one in the installer'}
if(-not (Get-Content ..\scripts\release.ps1 -Raw).Contains('-File ..\scripts\build-zip.ps1 -SetupPath $setup -LauncherPath $exe -OutZip $zip')){throw '..\scripts\release.ps1 must build its zip with ..\scripts\build-zip.ps1 from the installer it built'}
# Python and Zig go in whole (developer, 2026-11-20: no trimming).
if((Get-Content ..\scripts\build-setup.ps1 -Raw) -cmatch 'Remove-Item[^\n]*zig|Where-Object[^\n]*lib\\libc'){throw '..\scripts\build-setup.ps1 must carry the official Zig zip unchanged'}
Remove-Item -LiteralPath $tools -Recurse -Force
# That file is not an installer anyone may use: it is removed, with its stand-in, so it cannot be taken for a real one.
Remove-Item -LiteralPath (Join-Path $FdSrc '.dream-loop\setup\FlutterDeckSetup.exe'),$standIn -Force -ErrorAction SilentlyContinue
$bs=Get-Content ..\scripts\build-setup.ps1 -Raw
$expect=@('P0','assets\FlutterDeck-OriginalLoop.wav,P1','..\LICENSE,P2','..\NOTICE.md,P3','assets\FlutterDeck-DawnLaunch.wav,P4','assets\FlutterDeck-SkywayRun.wav,P5','codes\memory-codes-list.txt,P6')
foreach($g in $expect){if(-not $bs.Contains($g)){throw "..\scripts\build-setup.ps1 resource order missing $g"}}
if(-not $c.Contains('Payload={"FlutterDeck.exe",@"assets\FlutterDeck-OriginalLoop.wav","LICENSE","NOTICE.md",@"assets\FlutterDeck-DawnLaunch.wav",@"assets\FlutterDeck-SkywayRun.wav",@"codes\memory-codes-list.txt"}')){throw 'Installer.Payload order changed; update ..\scripts\build-setup.ps1 to match'}
# The icon file is a valid ICO holding every expected size.
$ico=[IO.File]::ReadAllBytes("$FdSrc\assets\FlutterDeck.ico")
if($ico.Length -lt 22 -or $ico[0] -ne 0 -or $ico[2] -ne 1){throw 'assets\FlutterDeck.ico is not an icon file'}
$count=[BitConverter]::ToUInt16($ico,4);$sizes=@(for($i=0;$i -lt $count;$i++){$w=$ico[6+16*$i];if($w -eq 0){256}else{[int]$w}})
foreach($need in 256,48,32,16){if($sizes -notcontains $need){throw "Icon is missing the $need px size"}}
'PASS: installer file-operation tests on a scratch folder (with build tools: install, replace, rollback, unsafe names, full drive, list-based uninstall); installer built with stand-in tools, carries every tool and a small uninstaller, refuses a kit with an unlisted file; per-user, unelevated, non-recursive and opt-in-settings guards; one version source; icon file holds 256/48/32/16. No real install, registry write or shortcut was made by this check.'
