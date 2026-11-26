# Builds the portable Flutter Deck zip from a built installer (developer, 2026-11-21). The zip holds what Setup installs: the launcher, its assets, the shipped cheat-code list, LICENSE, NOTICE.md and the builder folder
# (the three kits, Python, Zig and their notices), laid out by the installer's own code, so "Build my game..." works from the unzipped
# folder. Left out: the uninstaller and builder\installed-files.txt (a portable copy is removed by deleting its folder).
# Usage: ..\scripts\build-zip.ps1 -SetupPath <FlutterDeckSetup.exe built by ..\scripts\build-setup.ps1> -LauncherPath <the FlutterDeck.exe inside it> -OutZip <zip to write>
# Run it in a new PowerShell process (..\scripts\release.ps1 does): it loads the installer program to use its code.
param([Parameter(Mandatory=$true)][string]$SetupPath,[Parameter(Mandatory=$true)][string]$LauncherPath,[Parameter(Mandatory=$true)][string]$OutZip)
$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
$SetupPath=(Resolve-Path -LiteralPath $SetupPath).Path;$LauncherPath=(Resolve-Path -LiteralPath $LauncherPath).Path
if(Test-Path -LiteralPath $OutZip){throw "The zip already exists: $OutZip"}
Add-Type -AssemblyName System.IO.Compression,System.IO.Compression.FileSystem
$top='FlutterDeck'
$work=Join-Path ([IO.Path]::GetTempPath()) ('fd-zip-'+[Guid]::NewGuid().ToString('N').Substring(0,8))
$stage=Join-Path $work $top
New-Item -ItemType Directory -Force $work | Out-Null
try{
  $asm=[Reflection.Assembly]::LoadFrom($SetupPath)
  $installer=$asm.GetType('Installer',$true);$payloadType=$asm.GetType('SetupPayload',$true)
  $builder=$payloadType.GetMethod('Builder').Invoke($null,$null)
  if($null -eq $builder){throw 'This installer holds no build tools.'}
  $un=Join-Path $work 'uninstaller.exe'
  $s=$asm.GetManifestResourceStream('Uninstaller');$f=[IO.File]::Create($un);try{$s.CopyTo($f)}finally{$f.Dispose();$s.Dispose()}
  $open=[Func[int,IO.Stream]]{param($i) $asm.GetManifestResourceStream('P'+$i)}.GetNewClosure()
  $install=$installer.GetMethods()|Where-Object{$_.Name -eq 'Install' -and $_.GetParameters().Count -eq 5}
  [void]$install.Invoke($null,[object[]]@([string]$stage,$open,[string]$un,$builder,$null))
  Remove-Item -LiteralPath (Join-Path $stage 'Uninstall Flutter Deck.exe') -Force
  Remove-Item -LiteralPath (Join-Path $stage 'builder\installed-files.txt') -Force
  if((Get-FileHash -LiteralPath (Join-Path $stage 'FlutterDeck.exe')).Hash -ne (Get-FileHash -LiteralPath $LauncherPath).Hash){throw 'The launcher inside the installer is not the given launcher file.'}
  foreach($need in 'assets\FlutterDeck-OriginalLoop.wav','codes\memory-codes-list.txt','LICENSE','NOTICE.md','builder\kit\build_game.py','builder\kit2\build_game.py','builder\kit3\build_game.py','builder\python\python.exe','builder\zig\zig.exe','builder\NOTICES.txt','builder\zig\THIRD-PARTY-NOTICES.txt','builder\python\THIRD-PARTY-NOTICES.txt'){if(-not (Test-Path -LiteralPath (Join-Path $stage $need))){throw "Missing in the zip folder: $need"}}
  $files=@(Get-ChildItem -LiteralPath $stage -Recurse -File -Force)
  # Exactly the files Setup installs (its Payload and its builder list), nothing more and nothing less.
  $expected=New-Object 'Collections.Generic.HashSet[string]' ([StringComparer]::OrdinalIgnoreCase)
  foreach($n in $installer.GetField('Payload').GetValue($null)){[void]$expected.Add($n)}
  foreach($kv in $installer.GetMethod('BuilderFiles').Invoke($null,[object[]]@($builder))){[void]$expected.Add('builder\'+$kv.Key)}
  foreach($n in 'NOTICES.txt','zig\THIRD-PARTY-NOTICES.txt','python\THIRD-PARTY-NOTICES.txt'){[void]$expected.Add('builder\'+$n)}
  $actual=@($files|ForEach-Object{$_.FullName.Substring($stage.Length+1)})
  $extra=@($actual|Where-Object{-not $expected.Contains($_)});$missing=@($expected|Where-Object{$actual -notcontains $_})
  if($extra.Count -or $missing.Count){throw "The zip folder is not exactly what Setup installs. Extra: $($extra -join ', ') Missing: $($missing -join ', ')"}
  $fs=[IO.File]::Open($OutZip,'CreateNew');$za=New-Object IO.Compression.ZipArchive($fs,[IO.Compression.ZipArchiveMode]::Create)
  try{foreach($x in $files){[void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($za,$x.FullName,($top+'/'+$x.FullName.Substring($stage.Length+1).Replace('\','/')),[IO.Compression.CompressionLevel]::Optimal)}}finally{$za.Dispose();$fs.Dispose()}
  $longest=($files|ForEach-Object{$_.FullName.Length-$stage.Length-1}|Measure-Object -Maximum).Maximum
  "Built $OutZip ($((Get-Item -LiteralPath $OutZip).Length) bytes, $($files.Count) files, longest path inside $top\ $longest characters) around launcher SHA-256 $((Get-FileHash -LiteralPath $LauncherPath).Hash)"
}catch{
  if(Test-Path -LiteralPath $OutZip){Remove-Item -LiteralPath $OutZip -Force}
  throw
}finally{
  if(Test-Path -LiteralPath $work){Remove-Item -LiteralPath $work -Recurse -Force}
}
