$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\DeckStatePureTest.exe DeckState.cs ..\tests\DeckStatePureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'State test compile failed'}
& .\.dream-loop\DeckStatePureTest.exe
if($LASTEXITCODE -ne 0){throw 'State checks failed'}
$d=Get-Content DeckState.cs -Raw
if($d -cmatch 'System\.Windows|System\.IO|System\.Net|File\.|Process\.'){throw 'DeckState must stay pure'}
$s=Get-Content Program.cs -Raw
foreach($g in @('if(!DeckState.IsScreen(name))return;','DeckState.Heading(screen)','DeckState.ShowsLibrary(screen)','DeckState.ShowsTransport(screen)','DeckState.CatalogCategory(screen)','DeckState.Snapshot(','AutomationProperties.SetItemStatus(this,','PublishState()')){if(-not $s.Contains($g)){throw "Missing state guard: $g"}}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('DeckState.cs')){throw 'DeckState.cs missing from build'}
$a=Get-Content DeckAudio.cs -Raw
foreach($g in @('Failed=true','if(Failed){Select(Index,true);return;}','var p=new MediaPlayer();player=p;','if(p!=player)return;','DispatcherTimer{Interval=TimeSpan.FromSeconds(LoadSeconds)}')){if(-not $a.Contains($g)){throw "Missing audio state guard: $g"}}
# Navigate() silently ignores unknown names, so every literal target and nav entry must be in the screen table.
$ids=[regex]::Matches([regex]::Match($d,'Ids=\{([^}]*)\}').Groups[1].Value,'"([^"]+)"') | ForEach-Object { $_.Groups[1].Value }
if(@($ids).Count -ne 12){throw 'Screen table not found in DeckState.cs'}   # 11 since 2026-11-20: Advanced (Advanced Settings) joined; 12 since 2026-11-22: Gamepad (the gamepad button editor)
$targets=@([regex]::Matches($s,'Navigate\("([^"]+)"\)') | ForEach-Object { $_.Groups[1].Value })+@([regex]::Matches([regex]::Match($s,'screens=\{([^}]*)\}').Groups[1].Value,'"([^"]+)"') | ForEach-Object { $_.Groups[1].Value })
if($targets.Count -lt 20){throw 'Navigate targets not found in Program.cs'}
foreach($t in $targets){if($ids -cnotcontains $t){throw "Navigate target not in screen table: $t"}}
# Back and Forward (developer, 2026-11-20): every screen change goes through Navigate, which remembers it; Back and Forward do not.
foreach($g in @('void Navigate(string name){if(!DeckState.IsScreen(name))return;keyCapture=null;history.Visit(screen,name);ShowScreen(name);}','string to=history.Back(screen);if(to!=null)ShowScreen(to);','string to=history.Forward(screen);if(to!=null)ShowScreen(to);','"nav-back"','"nav-forward"','backButton.IsEnabled=history.CanBack;forwardButton.IsEnabled=history.CanForward;','MouseButton.XButton1','MouseButton.XButton2','Key.BrowserBack','e.SystemKey==Key.Left')){if(-not $s.Contains($g)){throw "Missing Back and Forward guard: $g"}}
if(([regex]::Matches($s,'(?<![A-Za-z])screen=(?!=)')).Count -ne 2 -or -not $s.Contains('string screen="Home";') -or -not $s.Contains('screen=name;Render();bodyScroll.ScrollToTop();}')){throw 'The screen may be changed only in ShowScreen, so that Back and Forward see every change'}
'PASS: pure state module compiled alone; UI reads screen routes and publishes the token-only snapshot; every literal Navigate target is a known screen; Back and Forward see every screen change; failed-audio retry source guard present. Source guards only: no GUI or audio playback executed.'
