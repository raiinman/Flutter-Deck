# Builds the installer from the FlutterDeck.exe that is in this folder right now.
# Output: .dream-loop\setup\FlutterDeckSetup.exe (ignored by git). Run ..\scripts\build.ps1 -PublicV1 first.
# -Tools: the folder holding the build tools the installer carries (developer, 2026-11-20: Python, Zig and the three kits in
# the installer). It must hold exactly: kit\, kit2\ and kit3\ (the builder kits for Mega Man Legends, Mega Man Legends 2
# and The Misadventures of Tron Bonne, each exactly as its kit-manifest.json lists), python.zip (the official Python
# embeddable package for Windows 64-bit, unchanged) and zig.zip (the official Zig package for x86_64 Windows, unchanged).
# The tools are not in this repository; pass their folder on this PC. Python and Zig are not trimmed (developer, 2026-11-20).
param([string]$LauncherPath='',[Parameter(Mandatory=$true)][string]$Tools,[switch]$AnyToolZips)
$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
if(-not $LauncherPath){$LauncherPath=Join-Path $Here 'FlutterDeck.exe'}
# Only a public build may go into an installer (developer, 2026-11-20): its launcher shows "Play with a friend..." greyed out. A public build carries
# the mark below (an assembly attribute compiled only with ..\scripts\build.ps1 -PublicV1); a launcher without it is refused, whoever runs this script.
if(Test-Path -LiteralPath $LauncherPath -PathType Leaf){$lb=[IO.File]::ReadAllBytes($LauncherPath);$mark=[byte[]](0x12)+[Text.Encoding]::ASCII.GetBytes('FlutterDeckEdition')+[byte[]](0x09)+[Text.Encoding]::ASCII.GetBytes('public-v1');$found=$false;for($i=0;$i -le $lb.Length-$mark.Length -and -not $found;$i++){if($lb[$i] -eq $mark[0]){$found=$true;for($j=1;$j -lt $mark.Length;$j++){if($lb[$i+$j] -ne $mark[$j]){$found=$false;break}}}};if(-not $found){throw 'This launcher is not a public build. Build it with ..\scripts\build.ps1 -PublicV1 (..\scripts\release.ps1 does that) and run this again.'}}
foreach($f in @($LauncherPath,'assets\FlutterDeck-OriginalLoop.wav','..\LICENSE','..\NOTICE.md','assets\FlutterDeck-DawnLaunch.wav','assets\FlutterDeck-SkywayRun.wav','assets\FlutterDeck.ico','builder-notices\NOTICES.txt','builder-notices\ZIG-THIRD-PARTY-NOTICES.txt','builder-notices\PYTHON-THIRD-PARTY-NOTICES.txt')){if(-not (Test-Path -LiteralPath $f)){throw "Missing $f"}}
# ---- the build tools ----
if(-not (Test-Path -LiteralPath $Tools -PathType Container)){throw "The tools folder was not found: $Tools"}
$Tools=(Resolve-Path -LiteralPath $Tools).Path
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$games=@(@{Folder='kit';Game='Mega Man Legends (USA) SLUS-00603'},@{Folder='kit2';Game='Mega Man Legends 2 (USA) SLUS-01140'},@{Folder='kit3';Game='The Misadventures of Tron Bonne (USA) SLUS-00896'})
$out=Join-Path $Here '.dream-loop\setup'
New-Item -ItemType Directory -Force $out | Out-Null
$kitZips=@()
foreach($g in $games){
  $kit=Join-Path $Tools $g.Folder
  if(-not (Test-Path -LiteralPath (Join-Path $kit 'kit-manifest.json'))){throw "$($g.Folder): no kit-manifest.json in $kit"}
  # Exactly the files the kit's manifest lists, each with its listed SHA-256, and nothing else (no caches, no logs).
  $manifest=Get-Content -LiteralPath (Join-Path $kit 'kit-manifest.json') -Raw | ConvertFrom-Json
  $listed=@{};foreach($pr in $manifest.PSObject.Properties){$listed[$pr.Name.Replace('/','\')]=$pr.Value}
  # -Force: hidden and system files too (a .git folder, desktop.ini, a hidden cache must refuse the kit, not slip in).
  $onDisk=@(Get-ChildItem -LiteralPath $kit -Recurse -File -Force | ForEach-Object { $_.FullName.Substring($kit.Length+1) } | Where-Object { $_ -ne 'kit-manifest.json' })
  foreach($rel in $onDisk){if(-not $listed.ContainsKey($rel)){throw "$($g.Folder): $rel is not in its kit-manifest.json"}}
  foreach($rel in $listed.Keys){$f=Join-Path $kit $rel;if(-not (Test-Path -LiteralPath $f -PathType Leaf)){throw "$($g.Folder): $rel is missing"};if((Get-FileHash -LiteralPath $f -Algorithm SHA256).Hash -ne $listed[$rel].ToUpperInvariant()){throw "$($g.Folder): $rel does not match its kit-manifest.json"}}
  foreach($need in 'build_game.py','expected.json','THIRD-PARTY-NOTICES.txt'){if(-not $listed.ContainsKey($need)){throw "$($g.Folder): the kit has no $need"}}
  $expected=Get-Content -LiteralPath (Join-Path $kit 'expected.json') -Raw
  if(-not $expected.Contains('"game": "'+$g.Game+'"') -and -not $expected.Contains('"game":"'+$g.Game+'"')){throw "$($g.Folder) is not the kit for $($g.Game)"}
  $zip=Join-Path $out "tool-$($g.Folder).zip";if(Test-Path -LiteralPath $zip){Remove-Item -LiteralPath $zip -Force}
  # The zip is made from the manifest's list (and the manifest), never from whatever else is in the folder.
  $fs=[IO.File]::Open($zip,'CreateNew');$za=New-Object IO.Compression.ZipArchive($fs,[IO.Compression.ZipArchiveMode]::Create)
  try{foreach($rel in @($listed.Keys)+'kit-manifest.json'){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za,(Join-Path $kit $rel),$rel.Replace('\','/'),[IO.Compression.CompressionLevel]::Optimal)}}finally{$za.Dispose();$fs.Dispose()}
  $kitZips+=$zip
  "$($g.Folder): $($listed.Count+1) files, kit-manifest.json SHA-256 $((Get-FileHash -LiteralPath (Join-Path $kit 'kit-manifest.json') -Algorithm SHA256).Hash)"
}
# Python and Zig: the official zips, carried unchanged. A look inside makes sure each is the right kind of package.
$py=Join-Path $Tools 'python.zip';$zig=Join-Path $Tools 'zig.zip'
foreach($f in $py,$zig){if(-not (Test-Path -LiteralPath $f -PathType Leaf)){throw "Missing $f"}}
# Pinned: the notices name these exact versions. Python 3.15.2 embeddable package for Windows 64-bit (python.org) and
# Zig 0.17.1 for x86_64 Windows (SHA-256 as published in ziglang.org/download/index.json). A new version changes the pins
# and the notices together. -AnyToolZips skips the pins (used only by ..\scripts\check-installer.ps1 with its tiny stand-ins).
if(-not $AnyToolZips){
  if((Get-FileHash -LiteralPath $py -Algorithm SHA256).Hash -ne 'D012F3AA29CF07436DAE8AEA1FEC0FD195C2612A0B059B1F915AE0AFB9AD1F62'){throw 'python.zip is not the pinned Python 3.15.2 embeddable package (amd64)'}
  if((Get-FileHash -LiteralPath $zig -Algorithm SHA256).Hash -ne 'B03F477A1943673E02ACA0F570E8030DFFD536CE1F45CBC4D3326EC31159562E'){throw 'zig.zip is not the pinned Zig 0.17.1 package for x86_64 Windows'}
}
$z=[IO.Compression.ZipFile]::OpenRead($py);try{$names=@($z.Entries|ForEach-Object{$_.FullName})}finally{$z.Dispose()}
foreach($need in 'python.exe','LICENSE.txt'){if($names -notcontains $need){throw "python.zip has no $need at its top: it is not the Python embeddable package"}}
if($names -contains 'THIRD-PARTY-NOTICES.txt'){throw 'python.zip already holds THIRD-PARTY-NOTICES.txt, the name Setup adds'}
$z=[IO.Compression.ZipFile]::OpenRead($zig);try{$names=@($z.Entries|ForEach-Object{$_.FullName.Replace('\','/')})}finally{$z.Dispose()}
$tops=@($names|ForEach-Object{$_.Split('/')[0]}|Sort-Object -Unique)
if($tops.Count -ne 1 -or $names -notcontains "$($tops[0])/zig.exe" -or $names -notcontains "$($tops[0])/LICENSE"){throw 'zig.zip is not the Zig package: it must hold one folder with zig.exe and LICENSE in it'}
if($names -contains "$($tops[0])/THIRD-PARTY-NOTICES.txt"){throw 'zig.zip already holds THIRD-PARTY-NOTICES.txt, the name Setup adds'}
"python.zip SHA-256 $((Get-FileHash -LiteralPath $py -Algorithm SHA256).Hash)"
"zig.zip    SHA-256 $((Get-FileHash -LiteralPath $zig -Algorithm SHA256).Hash)"
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$exe=Join-Path $out 'FlutterDeckSetup.exe'
$common=@('/nologo','/target:winexe','/platform:x64','/win32icon:assets\FlutterDeck.ico','/resource:assets\FlutterDeck.ico,Icon',"/reference:$fx\System.IO.Compression.dll","/reference:$fx\WPF\PresentationFramework.dll","/reference:$fx\WPF\PresentationCore.dll","/reference:$fx\WPF\WindowsBase.dll","/reference:$fx\System.Xaml.dll")
$sources=@('Setup.cs','InstallCore.cs','Updater.cs','DeckPaths.cs')
# The uninstaller: the same program without the payload, so the copy in the install folder (and the one each uninstall
# leaves in the temp folder) stays small. Setup places it in the install folder.
$small=Join-Path $out 'uninstaller.exe'
& "$fx\csc.exe" @common "/out:$small" @sources
if($LASTEXITCODE -ne 0){throw 'Uninstaller compilation failed'}
# Resource order must match Installer.Payload in InstallCore.cs: P0 launcher, P1 loop, P2 LICENSE, P3 NOTICE, P4 and P5 the two newer loops.
# Resource names of the tools must match SetupPayload in Setup.cs.
& "$fx\csc.exe" @common "/out:$exe" "/resource:$LauncherPath,P0" /resource:assets\FlutterDeck-OriginalLoop.wav,P1 /resource:..\LICENSE,P2 /resource:..\NOTICE.md,P3 /resource:assets\FlutterDeck-DawnLaunch.wav,P4 /resource:assets\FlutterDeck-SkywayRun.wav,P5 "/resource:$small,Uninstaller" "/resource:$($kitZips[0]),ToolKit1" "/resource:$($kitZips[1]),ToolKit2" "/resource:$($kitZips[2]),ToolKit3" "/resource:$py,ToolPython" "/resource:$zig,ToolZig" /resource:builder-notices\NOTICES.txt,ToolNotices /resource:builder-notices\ZIG-THIRD-PARTY-NOTICES.txt,ToolZigNotices /resource:builder-notices\PYTHON-THIRD-PARTY-NOTICES.txt,ToolPythonNotices @sources
if($LASTEXITCODE -ne 0){throw 'Setup compilation failed'}
foreach($f in $kitZips+$small){Remove-Item -LiteralPath $f -Force}
"Built $exe ($((Get-Item $exe).Length) bytes) around launcher SHA-256 $((Get-FileHash -LiteralPath $LauncherPath).Hash)"
