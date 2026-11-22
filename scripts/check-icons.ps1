$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$s=Get-Content Program.cs -Raw
foreach($g in @('MusicIcon("previous"','MusicIcon("play"','MusicIcon("next"','hit.SetValue(Border.BackgroundProperty,Brushes.Transparent)','FocusVisualStyle=null','play.IsEnabled=false','Control("",delegate{},true,"Play unavailable:')){if(-not $s.Contains($g)){throw "Missing icon behavior: $g"}}
foreach($g in @('var caption=Label(name','Control("Play unavailable"','Control("Mix"','Control("Loop"','Control("Mute"','((TextBlock)playButton.Content)')){if($s.Contains($g)){throw "Unwanted visible label: $g"}}
foreach($g in @('Dress(playButton,"play",PodInk);','Dress(stopButton,"stop",PodInk);','SetSprite(playButton,audio.Playing?"pause":"play");','hit.AppendChild(new FrameworkElementFactory(typeof(ContentPresenter)));')){if(-not $s.Contains($g)){throw "Missing sprite transport behavior: $g"}}
# Owner, 2026-11-21: no shuffle or repeat buttons; the speaker mutes and unmutes, the five bars show and set the volume.
$pc=Get-Content Program.cs -Raw
if($pc.Contains('MusicIcon("shuffle"') -or $pc.Contains('MusicIcon("repeat"') -or $pc.Contains('shuffleButton') -or $pc.Contains('repeatButton')){throw 'The shuffle and repeat buttons were removed (developer, 2026-11-21)'}
foreach($g in @('muteButton=VolumeIconButton("music-mute","Mute music")','muteButton.Click+=delegate{audio.ToggleMute();UpdateVolumeIcon();NoteChoices();};','hit.Click+=delegate{volumeSlider.Value=20*level;};','volume.ValueChanged+=delegate{if(audio.Muted)audio.ToggleMute();UpdateVolumeIcon();};','int lit=audio.Muted?0:(int)Math.Ceiling(volumeSlider.Value/20.0-0.0001);','for(int i=1;i<=5;i++)')){if(-not $pc.Contains($g)){throw "Missing volume icon guard: $g"}}
Write-Output 'PASS: transport buttons keep their transparent hit area, accessible names and no visible captions, and now wear the owner-supplied sprites (see ..\scripts\check-controls.ps1). Structural checks only; no GUI executed.'
