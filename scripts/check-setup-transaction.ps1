# Transaction regression: draft must not leak to saved state or other game.
$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $FdSrc 'DeckPaths.cs'),(Join-Path $FdSrc 'LocalSetup.cs') -ReferencedAssemblies System.Xml,System.Core
$base=[SetupState]::new();$base.Game[0]='original-1';$base.Bios[0]='bios-1';$base.Game[1]='original-2';$base.Bios[1]='bios-2'
$d=[LocalSetup]::Copy($base);$d.Game[0]='abandoned';$d.Bios[0]=''
if($base.Game[0] -ne 'original-1'){throw 'Draft aliases baseline'}
$d=[LocalSetup]::Copy($base)
if($d.Game[0] -ne 'original-1'){throw 'Cancel leaked'}
$d.Game[0]='abandoned';$d.Game[1]='new-2'
$merged=[LocalSetup]::MergeSelected($base,$d,1)
if($merged.Game[0] -ne 'original-1' -or $merged.Game[1] -ne 'new-2' -or $base.Game[1] -ne 'original-2'){throw 'Cross-game commit leakage'}
try{[LocalSetup]::MergeSelected($base,$d,3);throw 'Index accepted'}catch{if($_.Exception.Message -eq 'Index accepted'){throw}}
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
if($s.Contains('localSetup.Game[setupTarget]=picker.FileName')){throw 'Browse mutates saved state'}
if(-not $s.Contains('LocalSetup.Save(LocalSetup.ConfigPath,candidate);localSetup=candidate')){throw 'Commit before successful save'}
'PASS: draft isolation, discard, selected-game merge and commit-order guards; no UI claim.'
