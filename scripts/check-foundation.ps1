$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $FdSrc 'DeckPaths.cs'),(Join-Path $FdSrc 'LocalSetup.cs'),(Join-Path $FdSrc 'Foundation.cs') -ReferencedAssemblies System.Xml,System.Core
$t=Join-Path $FdSrc ('.dream-loop\foundation-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory $t | Out-Null
$p=Join-Path $t 'prefs.txt'
$v=[LauncherPreferences]::Load($p)
if($v.Width -ne 1672 -or $v.Game -ne 1){throw 'Defaults'}
$v.Width=99999;$v.Game=2;$v.Topmost=$true
[LauncherPreferences]::Save($p,$v);$r=[LauncherPreferences]::Load($p)
if($r.Width -ne 3000 -or $r.Game -ne 2 -or -not $r.Topmost){throw 'Roundtrip/bounds'}
[IO.File]::WriteAllText($p,'corrupted')
if([LauncherPreferences]::Load($p).Width -ne 1672){throw 'Corrupt fallback'}
$l=New-Object SafeSessionLog
$l.Add('setup-saved');$l.Add('C:\private\game.bin');$l.Add('secret exception')
if($l.Text.Contains('private') -or $l.Text.Contains('secret') -or -not $l.Text.Contains('setup-saved')){throw 'Log allowlist'}
for($i=0;$i -lt 200;$i++){$l.Add('library-opened')}
if($l.Count -gt 50){throw 'Unbounded log'}
# Remembered choices (SPEC-choices.md): music on or off, volume, mute, repeat, shuffle, track, scaling engine and filter.
$cp=Join-Path $t 'sub folder\choices.txt'
$d=[DeckChoices]::Load($cp)
if(-not $d.MusicOn -or $d.Volume -ne 20 -or $d.Muted -or -not $d.Repeat -or $d.Shuffle -or $d.Track -ne '' -or $d.ScalingEngine -ne 0 -or $d.ScalingFilter -ne 0){throw 'Choices defaults'}
if(Test-Path $cp){throw 'Loading choices must not create a file'}
$d.MusicOn=$false;$d.Volume=65;$d.Muted=$true;$d.Repeat=$false;$d.Shuffle=$true;$d.Track='Skyway Run';$d.ScalingEngine=1;$d.ScalingFilter=3
[DeckChoices]::Save($cp,$d);$e=[DeckChoices]::Load($cp)
if($e.MusicOn -or $e.Volume -ne 65 -or -not $e.Muted -or $e.Repeat -or -not $e.Shuffle -or $e.Track -ne 'Skyway Run' -or $e.ScalingEngine -ne 1 -or $e.ScalingFilter -ne 3){throw 'Choices round trip'}
if([DeckChoices]::Format($e) -ne [DeckChoices]::Format($d) -or [IO.File]::ReadAllText($cp) -ne [DeckChoices]::Format($d)){throw 'Choices file text is not the formatted text'}
$bytes=[IO.File]::ReadAllBytes($cp);if($bytes[0] -eq 0xEF){throw 'Choices file must have no byte order mark'}
$d.Volume=0;[DeckChoices]::Save($cp,$d);if([DeckChoices]::Load($cp).Volume -ne 0){throw 'Volume zero must be kept'}
if(-not (Test-Path ($cp+'.bak')) -or [DeckChoices]::Load($cp+'.bak').Volume -ne 65){throw 'The earlier choices file must be kept as the backup'}
if(@(Get-ChildItem (Split-Path $cp) -Filter '*.tmp').Count -ne 0){throw 'No temporary file may stay'}
# out-of-range values are written and read as the defaults
$d.Volume=250;$d.ScalingEngine=7;$d.ScalingFilter=-1;$d.Track=("x"*200)+"`r`nmusic=1";[DeckChoices]::Save($cp,$d);$e=[DeckChoices]::Load($cp)
if($e.Volume -ne 20 -or $e.ScalingEngine -ne 0 -or $e.ScalingFilter -ne 0 -or $e.Track.Length -ne 80 -or $e.MusicOn){throw 'Choices bounds, track length, or a track title that tries to add a line'}
# hand-made files: each bad value falls back by itself; unknown keys and lines are ignored
[IO.File]::WriteAllText($cp,"1`r`nmusic=0`r`nvolume=loud`r`nmuted=yes`r`nrepeat=0`r`nfuture=thing`r`nnonsense`r`n=5`r`nvolume=101`r`nshuffle=1`r`ntrack=  Dawn Launch  `r`nscaling-engine=1`r`nscaling-filter=9`r`n")
$e=[DeckChoices]::Load($cp)
if($e.MusicOn -or $e.Volume -ne 20 -or $e.Muted -or $e.Repeat -or -not $e.Shuffle -or $e.Track -ne 'Dawn Launch' -or $e.ScalingEngine -ne 1 -or $e.ScalingFilter -ne 0){throw 'Choices hand-made file'}
[IO.File]::WriteAllText($cp,"1`r`nvolume=-5`r`nvolume=+5`r`nvolume= 40 `r`n");if([DeckChoices]::Load($cp).Volume -ne 40){throw 'Signed numbers are refused, a plain number is read'}
[IO.File]::WriteAllText($cp,"1`r`nmusic=0`r`n");$e=[DeckChoices]::Load($cp);if($e.MusicOn -or $e.Volume -ne 20 -or -not $e.Repeat){throw 'A file with only some lines keeps the other defaults'}
foreach($bad in @('',"2`r`nmusic=0`r`n","music=0`r`n",'corrupted',("1`r`nmusic=0`r`n"+('#'*2000)))){[IO.File]::WriteAllText($cp,$bad);$e=[DeckChoices]::Load($cp);if(-not $e.MusicOn -or $e.Volume -ne 20){throw 'An empty, unknown-version, headless, corrupt or oversized choices file must give the defaults'}}
[IO.File]::WriteAllBytes($cp,[byte[]](0..255));if(-not [DeckChoices]::Load($cp).MusicOn){throw 'A binary choices file must give the defaults'}
$dirAsFile=Join-Path $t 'is-a-folder.txt';New-Item -ItemType Directory $dirAsFile | Out-Null
if(-not [DeckChoices]::Load($dirAsFile).MusicOn){throw 'A folder in place of the file must give the defaults'}
$threw=$false;try{[DeckChoices]::Save($dirAsFile,$d)}catch{$threw=$true};if(-not $threw -or @(Get-ChildItem $t -Filter '*.tmp').Count -ne 0){throw 'A save that cannot replace the file must fail and leave no temporary file'}
# a held file is read on a later try; a file held for longer gives the defaults and is not damaged
[IO.File]::WriteAllText($cp,"1`r`nmusic=0`r`nvolume=47`r`n")
Add-Type -TypeDefinition 'public static class HoldFile{public static void For(string path,int ms){var s=System.IO.File.Open(path,System.IO.FileMode.Open,System.IO.FileAccess.Read,System.IO.FileShare.None);var t=new System.Threading.Thread(delegate(){System.Threading.Thread.Sleep(ms);s.Dispose();});t.Start();}}';[HoldFile]::For($cp,50)
$e=[DeckChoices]::Load($cp);Start-Sleep -Milliseconds 120;if($e.MusicOn -or $e.Volume -ne 47){throw 'A file held for a moment must be read on a later try'}
$hold=[IO.File]::Open($cp,'Open','Read','None');try{if(-not [DeckChoices]::Load($cp).MusicOn){throw 'A file held throughout gives the defaults'}}finally{$hold.Dispose()}
if([DeckChoices]::Load($cp).Volume -ne 47){throw 'A failed read must leave the file alone'}
# two open launchers: a save writes only what this copy changed, on top of what is in the file now
$onDisk=New-Object DeckChoices;$onDisk.MusicOn=$false;$onDisk.Volume=5;$onDisk.Track='Skyway Run'
$mine=New-Object DeckChoices;$base=[DeckChoices]::Copy($mine);$mine.Volume=60;$mine.Shuffle=$true
$m=[DeckChoices]::Merge($onDisk,$base,$mine)
if($m.MusicOn -or $m.Volume -ne 60 -or -not $m.Shuffle -or $m.Track -ne 'Skyway Run' -or -not $m.Repeat){throw 'Merge must keep the other copy''s Stop and track and add only this copy''s changes'}
if($onDisk.Volume -ne 5 -or $mine.MusicOn -ne $true){throw 'Merge must not change its inputs'}
$m=[DeckChoices]::Merge($onDisk,$mine,$mine);if([DeckChoices]::Format($m) -ne [DeckChoices]::Format($onDisk)){throw 'With nothing changed, merge gives the file as it is'}
$all=New-Object DeckChoices;$all.MusicOn=$false;$all.Muted=$true;$all.Repeat=$false;$all.Shuffle=$true;$all.Volume=1;$all.ScalingEngine=1;$all.ScalingFilter=2;$all.Track='Dawn Launch'
if([DeckChoices]::Format([DeckChoices]::Merge((New-Object DeckChoices),(New-Object DeckChoices),$all)) -ne [DeckChoices]::Format($all) -or [DeckChoices]::Format([DeckChoices]::Copy($all)) -ne [DeckChoices]::Format($all)){throw 'Merge and Copy must carry every value'}
# the test-only location: only a full path on a local drive to a .txt file is accepted
$real=Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FlutterDeck\choices.txt';$before=[Environment]::GetEnvironmentVariable('FLUTTERDECK_CHOICES_FILE','Process')
try{
  [Environment]::SetEnvironmentVariable('FLUTTERDECK_CHOICES_FILE',$null,'Process');if([DeckChoices]::PathName -ne $real){throw 'Default choices location'}
  foreach($no in @('relative\choices.txt','choices.txt',(Join-Path $t 'choices.xml'),'   ','C:choices.txt','\choices.txt','\\server\share\choices.txt','/choices.txt','C:/x/choices.txt')){[Environment]::SetEnvironmentVariable('FLUTTERDECK_CHOICES_FILE',$no,'Process');if([DeckChoices]::PathName -ne $real){throw "Override '$no' must be ignored"}}
  [Environment]::SetEnvironmentVariable('FLUTTERDECK_CHOICES_FILE',$cp,'Process');if([DeckChoices]::PathName -ne $cp){throw 'Override with a full .txt path'}
}finally{[Environment]::SetEnvironmentVariable('FLUTTERDECK_CHOICES_FILE',$before,'Process')}
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
foreach($g in @(
 'choices=DeckChoices.Load(DeckChoices.PathName);audio.Muted=false;audio.Repeat=false;audio.Shuffle=false;audio.Volume=choices.Volume/100.0;',
 'Closing+=delegate{FlushChoices();audio.Dispose();',
 'audio.Toggle();choices.MusicOn=audio.Playing;NoteChoices();}',
 'audio.Changed+=UpdateMusic;audio.Changed+=NoteChoices;',
 'volumeSlider=volume;volume.Value=choices.Volume;volume.ValueChanged+=',
 'SyncChoices();choicesBase=DeckChoices.Copy(choices);choicesLoading=false;',
 'void NoteTrack(){if(audio.Index>=0&&audio.Index<bundledTracks)choices.Track=audio.Tracks[audio.Index].Title;NoteChoices();}','audio.Previous();NoteTrack();','audio.Next();NoteTrack();','audio.Select(tracks.SelectedIndex,audio.Playing);NoteTrack();','DeckChoices.Save(path,DeckChoices.Merge(DeckChoices.Load(path),choicesBase,choices));choicesBase=DeckChoices.Copy(choices);',
 'if(choicesLoading)return;SyncChoices();if(DeckChoices.Format(choices)==DeckChoices.Format(choicesBase))return;',
 'engine.SelectedIndex=choices.ScalingEngine;','filter.SelectedIndex=choices.ScalingFilter;filter.IsEnabled=engine.SelectedIndex==1;',
 'choices.MusicOn=true;choices.Track=bundledTracks>0?audio.Tracks[0].Title:"";choices.ScalingEngine=0;choices.ScalingFilter=0;UpdateMusic();FlushChoices();')){if(-not $s.Contains($g)){throw "Choices UI guard $g"}}
# "Music on" changes only where the user presses Play, Pause or Stop (three places), in Reset, and nowhere else.
if(([regex]::Matches($s,'choices\s*\.\s*MusicOn\s*=[^=]')).Count -ne 4 -or ([regex]::Matches($s,'\bchoices\s*=')).Count -ne 2 -or ([regex]::Matches($s,'choices\.Track=')).Count -ne 2 -or ([regex]::Matches($s,'audio\.Stop\(\);choices\.MusicOn=false;NoteChoices\(\);')).Count -ne 2){throw 'Music on/off must be set only by Play/Pause, the two Stop buttons and Reset'}
if(([regex]::Matches($s,'DeckChoices\.Save\(')).Count -ne 1){throw 'Choices are saved in one place'}
if($s -match 'featureChoices[^;]*DeckChoices|DeckChoices[^;]*featureChoices'){throw 'Feature preview choices are not saved'}
$inst=Get-Content (Join-Path $FdSrc 'InstallCore.cs') -Raw;if(-not $inst.Contains('"runtime.xml","choices.txt"')){throw 'Uninstall must remove choices.txt with the other settings'}
foreach($g in @('play.IsEnabled=false','RenderHelp()','SaveWindowPreferences()','ResetLauncherPreferences()')){if(-not $s.Contains($g)){throw "Foundation UI guard $g"}}
'PASS: preference defaults/roundtrip/bounds/corruption, remembered choices (defaults, round trip, bounds, hand-made and damaged files, test-only location) and bounded allowlisted logs; UI source guards only: no window was opened and no real settings file was read or written.'
