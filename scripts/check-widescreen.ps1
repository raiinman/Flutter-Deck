$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
# Widescreen setting (contracts 2.19 and 2.20, 2026-11-19). The behaviour is tested by ..\scripts\check-runtime.ps1 (..\tests\GameRuntimePureTest.cs); this adds source guards.
$g=Get-Content GameRuntime.cs -Raw
foreach($x in @('Widescreen=false','r.HasWidescreen=kind.Id==1&&fs.Contains("widescreen")','" --widescreen 53"','s.Widescreen=game.GetAttribute("widescreen")=="1"','writer.WriteAttributeString("widescreen",s.Widescreen?"1":"0")','public const string SkHash=')){if(-not $g.Contains($x)){throw "Missing widescreen guard in GameRuntime.cs: $x"}}
# It is off unless the player ticks it, and it is only passed to a program that reports the feature and not with the original picture.
if(-not $g.Contains('if(widescreenOption&&s.Widescreen&&!(pictureOption&&s.OriginalPicture))')){throw 'The widescreen argument must need the option flag, the tick and the sharper picture'}
if(([regex]::Matches($g,'--widescreen')).Count -ne 2){throw '--widescreen must appear only in the argument line and the SkHash comment'}
# The test option of the game (--wide-test) must never be passed by the launcher; the existing runtime check already refuses names starting with --test-, so name it here.
foreach($f in 'GameRuntime.cs','Program.cs','OverlayGameSettings.cs'){if((Get-Content $f -Raw).Contains('--wide-test')){throw "$f must never pass --wide-test"}}
$p=Get-Content Program.cs -Raw
# Owner, 2026-11-19: the switch is always on the Scaling page, first, with its explanation. It is the choice of game 1.
if(-not $p.Contains('"widescreen"') -or -not $p.Contains('IsChecked=runtimes[0].Widescreen,') -or -not $p.Contains('wsBox.IsEnabled=!runtimeLoadFailed&&!GameRunning;') -or -not $p.Contains('runtimes[0].Widescreen=wsBox.IsChecked==true;try{GameRuntime.Save(GameRuntime.SettingsPath,runtimes[0],1);}') -or -not $p.Contains('"widescreen-state"')){throw 'The widescreen switch is missing in Program.cs (always shown; the choice of Mega Man Legends; with a line that says whether it takes effect)'}
if($p -cmatch 'if\([^)\r\n]*\)\s*\{[^\r\n]*Label\("Game picture",20\)'){throw 'The Game picture section must not be inside a condition'}
$sc=$p.Substring($p.IndexOf('void RenderScaling(){'));$sc=$sc.Substring(0,$sc.IndexOf('page.Children.Add(Label("Good to know",20));')+60)
if(-not $sc.Contains('"widescreen"') -or -not $sc.Contains('Label("Game picture",20)') -or $sc.IndexOf('Label("Game picture",20)') -gt $sc.IndexOf('Label("Scale another window",20)') -or $sc.IndexOf('Label("Scale another window",20)') -lt 0 -or $sc.IndexOf('"widescreen"') -gt $sc.IndexOf('Label("Scale another window",20)')){throw 'The widescreen switch belongs in the Scaling page, in the Game picture section, before the window scaling part'}
if(([regex]::Matches($p,'AutomationId\(wsBox,"widescreen"\)')).Count -ne 1){throw 'The widescreen switch must exist once only'}
if(-not $p.Contains('EXPERIMENTAL')){throw 'The widescreen switch and its warning must say EXPERIMENTAL'}
if($p -cmatch 'Widescreen\s*=\s*true'){throw 'Program.cs must not turn widescreen on by itself'}
'PASS: widescreen setting source guards (off by default, needs the feature and the sharper picture, never --wide-test). The behaviour is in ..\scripts\check-runtime.ps1. No game program was started; nobody has looked at the switch in the running launcher.'
