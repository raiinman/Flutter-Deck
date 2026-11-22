$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force (Join-Path $Here '.dream-loop') | Out-Null
$out=Join-Path $Here '.dream-loop\AudioPureTest.exe'
& "$fx\csc.exe" /nologo /target:exe /out:$out /reference:"$fx\WPF\PresentationCore.dll" /reference:"$fx\WPF\WindowsBase.dll" DeckAudio.cs ..\tests\AudioPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Audio helper compilation failed'}
# Every bundled loop: 30.000 seconds, 44,100 Hz, real sample levels.
$loops='FlutterDeck-OriginalLoop.wav','FlutterDeck-DawnLaunch.wav','FlutterDeck-SkywayRun.wav'
foreach($loop in $loops){
 & $out (Join-Path 'assets' $loop) ..\README.md
 if($LASTEXITCODE -ne 0){throw "Audio tests failed for $loop"}
}
# The two newer loops are rendered by LoopSynth.cs; the committed files must be exactly what it produces.
$render=Join-Path $Here '.dream-loop\loops-check'
New-Item -ItemType Directory -Force $render | Out-Null
& powershell -NoProfile -ExecutionPolicy Bypass -File (Join-Path $Here '..\scripts\make-loops.ps1') -Folder $render | Out-Null
if($LASTEXITCODE -ne 0){throw 'Loop synthesiser failed'}
foreach($loop in 'FlutterDeck-DawnLaunch.wav','FlutterDeck-SkywayRun.wav'){
 if((Get-FileHash (Join-Path $Here "assets\$loop")).Hash -ne (Get-FileHash (Join-Path $render $loop)).Hash){throw "$loop is not what LoopSynth.cs renders"}
}
# The synthesiser reads no audio file: the loops come from the notes written in it and nothing else.
$synth=Get-Content LoopSynth.cs -Raw
if($synth -cmatch 'File\.Read|File\.Open|OpenRead|ReadAll|FileStream|StreamReader|BinaryReader|System\.Net|\.wav"\)\s*;\s*//\s*read'){throw 'LoopSynth.cs must not read any audio or network source'}
$a=Get-Content DeckAudio.cs -Raw
if(-not $a.Contains('public const double StartVolume=.2;') -or -not $a.Contains('double volume=StartVolume;')){throw 'Music must start at 20% volume'}
$s=Get-Content Program.cs -Raw
foreach($g in @("Value='20' IsMoveToPointEnabled='True' ToolTip='Music volume'",'Loaded+=delegate{if(choices.MusicOn&&audio.Index>=0&&!audio.Playing&&!GameRunning)audio.Toggle();};','if(audio.Playing){musicPausedForGame=true;audio.Toggle();}','playButton=MusicIcon("play",delegate{musicPausedForGame=false;audio.Toggle();choices.MusicOn=audio.Playing;NoteChoices();}','stopButton=MusicIcon("stop",delegate{musicPausedForGame=false;audio.Stop();choices.MusicOn=false;NoteChoices();}','catch{ended=false;}','"FlutterDeck-DawnLaunch.wav","Dawn Launch"','"FlutterDeck-SkywayRun.wav","Skyway Run"','"FlutterDeck-OriginalLoop.wav","Skyward Circuit"','audio.Toggle()','audio.Next()','audio.Previous()','audio.Volume=volume.Value/100','audio.ToggleMute()','audio.Levels(waveform.Count)','audio.Dispose()')){
 if(-not $s.Contains($g)){throw "Missing audio binding: $g"}
}
'PASS: three bundled loops decode at 30.000 s and 44,100 Hz; the two newer loops are exactly what LoopSynth.cs renders; music starts with the launcher (at 20% the first time, and only if the user did not leave it off) and gives way to a running game; transport bindings present. Nothing was played and nobody has listened as part of this check.'
