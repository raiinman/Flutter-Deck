$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
New-Item -ItemType Directory -Force (Join-Path $Here '.dream-loop') | Out-Null
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\PreflightPureTest.exe LocalSetup.cs Preflight.cs ..\tests\PreflightPureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Preflight test compile failed'}
& .\.dream-loop\PreflightPureTest.exe .dream-loop\preflight-fixture
if($LASTEXITCODE -ne 0){throw 'Preflight checks failed'}
$s=Get-Content Program.cs -Raw
foreach($g in @('RenderPreflight()','Preflight.Check(localSetup.Game','Preflight.Check(localSetup.Bios','item.Id=="library-onboarding"','item.Id=="audio-soundtrack"','play.IsEnabled=false','game=target.SelectedIndex+1;OpenSetup();','Control("Open Help"')){if(-not $s.Contains($g)){throw "Missing preflight guard $g"}}
'PASS: preflight and actual-tool navigation guards, Play starts disabled and is enabled only by the readiness check (see ..\scripts\check-runtime.ps1); no GUI test.'
