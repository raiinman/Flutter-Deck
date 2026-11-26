$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
# The data folder (DeckPaths.cs): the usual folder, the --data-dir option, every settings file under that one root, and
# the option a launcher gives its new copy when it restarts itself after an update.
& "$fx\csc.exe" /nologo /target:exe /main:DeckPathsPureTest /out:.dream-loop\DeckPathsPureTest.exe /reference:System.Xml.dll ..\tests\DeckPathsPureTest.cs DeckPaths.cs Updater.cs Foundation.cs GameRuntime.cs LocalSetup.cs OverlayCore.cs UiPreferences.cs
if($LASTEXITCODE -ne 0){throw 'Data folder test compile failed'}
& .\.dream-loop\DeckPathsPureTest.exe
if($LASTEXITCODE -ne 0){throw 'Data folder tests failed'}
$u=Get-Content Updater.cs -Raw; $p=Get-Content Program.cs -Raw
# A restart after an update passes on the data folder option and nothing else.
if(-not $p.Contains('Updater.Relaunch(ExePath,DeckPaths.RestartArguments);') -or ([regex]::Matches($p,'Updater\.Relaunch\(')).Count -ne 1){throw 'Program.cs must restart itself in one place, with DeckPaths.RestartArguments'}
if(-not $u.Contains('if(arguments==null||(arguments.Length>0&&!arguments.StartsWith("--data-dir \"",StringComparison.Ordinal)))throw new ArgumentException(') -or -not $u.Contains('new System.Diagnostics.ProcessStartInfo(exePath,arguments){UseShellExecute=false,')){throw 'Updater.Relaunch must refuse every option but the data folder option'}
'PASS: paths'
