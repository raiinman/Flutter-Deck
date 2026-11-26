# Rebuilds the two original chiptune loops in assets\ from LoopSynth.cs. The result is the same bytes every run.
param([string]$Folder='')
$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
if(-not $Folder){$Folder=Join-Path $FdSrc 'assets'}
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /optimize+ /out:.dream-loop\LoopSynth.exe LoopSynth.cs
if($LASTEXITCODE -ne 0){throw 'Loop synthesiser did not compile'}
& .\.dream-loop\LoopSynth.exe $Folder
if($LASTEXITCODE -ne 0){throw 'Loop synthesiser failed'}
