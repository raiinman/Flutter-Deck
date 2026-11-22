$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
$wpf="$fx\WPF"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
# 1. Pure decisions: geometry, hover timing, hotkey rules, settings, feature gating. No window.
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\OverlayPureTest.exe OverlayCore.cs ..\tests\OverlayPureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Overlay pure test compile failed'}
& .\.dream-loop\OverlayPureTest.exe .dream-loop\overlay-fixture
if($LASTEXITCODE -ne 0){throw 'Overlay pure tests failed'}
# 1b. The launcher's side of the Game settings panel: rows from what the game program reports, changes, and "restart to apply".
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\OverlayGameSettingsTest.exe OverlayCore.cs OverlayGameSettings.cs ..\tests\OverlayGameSettingsTest.cs GameRuntime.cs LocalSetup.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Overlay game settings test compile failed'}
& .\.dream-loop\OverlayGameSettingsTest.exe .dream-loop\overlay-game-settings-fixture
if($LASTEXITCODE -ne 0){throw 'Overlay game settings tests failed'}
$gs=Get-Content OverlayGameSettings.cs -Raw
if($gs -cmatch 'Process|File\.|Directory\.|Registry|Http|WebClient|SendMessage|PostMessage|\.Save\('){throw 'OverlayGameSettings.cs must stay pure: it changes a settings object and starts, reads and writes nothing'}
# 2. The window against a stand-in game window parked off screen. No real mouse or keyboard input.
$sprites=@(Get-ChildItem assets\controls\*.png | Sort-Object Name | ForEach-Object { "/resource:assets\controls\$($_.Name),S-$($_.BaseName)" })
& "$fx\csc.exe" /nologo /target:exe /platform:x64 /out:.dream-loop\OverlayHostTest.exe @sprites /reference:"$wpf\PresentationFramework.dll" /reference:"$wpf\PresentationCore.dll" /reference:"$wpf\WindowsBase.dll" /reference:"$fx\System.Xaml.dll" /reference:"$wpf\UIAutomationProvider.dll" /reference:"$wpf\UIAutomationTypes.dll" /reference:System.Drawing.dll /reference:System.Windows.Forms.dll OverlayCore.cs GameOverlay.cs KeyMap.cs ..\tests\OverlayHostTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Overlay window test compile failed'}
$env:FLUTTERDECK_OVERLAY_TRACE="$PWD\.dream-loop\overlay-test-trace.txt" # the tests must not replace the real trace file in the temp folder
& .\.dream-loop\OverlayHostTest.exe "$PWD\.dream-loop\overlay-host-fixture"
if($LASTEXITCODE -ne 0){throw 'Overlay window tests failed'}
# 3. The pill draws its icons from path data (the kit's 24 grid): no picture files are embedded for it.
if((Get-Content ..\scripts\build.ps1 -Raw).Contains('S-toolbar-')){throw '..\scripts\build.ps1 must not embed toolbar pictures: the pill draws vector icons'}
# 4. Source guards.
$core=Get-Content OverlayCore.cs -Raw
$win=Get-Content GameOverlay.cs -Raw
if($core -cmatch 'System\.Windows|System\.Net|Process\.|Registry|DllImport'){throw 'OverlayCore.cs must stay free of window, network, process and native code'}
# No keyboard or mouse hook, no input injection, no talking to the game by messages, no process start, no network.
if($win -cmatch 'SetWindowsHookEx|SendInput|keybd_event|mouse_event|SetCursorPos|PostMessage|PostThreadMessage|CloseMainWindow|Process\.Start|HttpClient|WebClient|WebRequest|Registry'){throw 'GameOverlay.cs uses an API the overlay must not use (hooks, input injection, messages to the game, processes, network)'}
if($win -cmatch 'File\.|Directory\.|KeyMap\.(Load|Parse|Defaults)|SetSingle\(|\.Copy\(\)|(?<!settings)\.Save\('){throw 'GameOverlay.cs must not read, change or write the key map itself: it edits the launcher draft through OverlayHost callbacks (only the overlay settings may be saved here)'}
if(([regex]::Matches($win,'StreamWriter\(|FileInfo\(|FileStream\(')).Count -ne 2){throw 'GameOverlay.cs may only open files in the trace (one FileInfo, one StreamWriter)'}
if(-not $win.Contains('host.SaveKeys')){throw 'The overlay key editor must save through host.SaveKeys'}
if($win -cmatch 'SendMessage'){throw 'GameOverlay.cs must not send window messages'}
if(-not $win.Contains('OverlayNative.RegisterHotKey(')){throw 'The hotkey must use RegisterHotKey'}
foreach($g in @('WS_EX_NOACTIVATE','MA_NOACTIVATE','ShowActivated=false','HWND_TOPMOST','new IntPtr(-3)','0x4000u','string problem=(mods&ModifierKeys.Windows)!=0?"Do not use the Windows key.":HotkeyRules.Check(')){if(-not (($win+$core).Contains($g))){throw "Missing overlay guard: $g"}}
# Keys the game keeps are refused by name and by code.
foreach($g in @('vk==0x09||vk==0x7A||vk==0x7B||vk==0x0D')){if(-not $core.Contains($g)){throw "Missing reserved-key guard: $g"}}
$b=Get-Content ..\scripts\build.ps1 -Raw
foreach($f in @('OverlayCore.cs','GameOverlay.cs')){if(-not $b.Contains($f)){throw "$f missing from ..\scripts\build.ps1"}}
$p=Get-Content Program.cs -Raw
foreach($g in @('GameOverlay.Attach(','overlay.Dispose();')){if(-not $p.Contains($g)){throw "Missing overlay wiring in Program.cs: $g"}}
if(([regex]::Matches($p,'GameOverlay\.Attach\(')).Count -ne 1){throw 'The overlay must be started from exactly one place'}
if(([regex]::Matches($p,'OverlayGameSettings\.ForSession\(')).Count -ne 1){throw 'Program.cs must build the overlay game settings in exactly one place (OverlayGameSettings.ForSession)'}
if($p -cmatch 'OverlayGameSettings\.(Build|Set|Differs)\('){throw 'Program.cs must reach the overlay game settings only through OverlayGameSettings.ForSession'}
# Per-monitor screen scale: the manifest is embedded by ..\scripts\build.ps1 and the program says it targets .NET Framework 4.8 (WPF needs both).
if(-not $b.Contains('/win32manifest:FlutterDeck.manifest')){throw '..\scripts\build.ps1 must embed FlutterDeck.manifest'}
if(-not (Get-Content FlutterDeck.manifest -Raw).Contains('PerMonitorV2')){throw 'FlutterDeck.manifest must declare PerMonitorV2'}
if(-not (Get-Content AppInfo.cs -Raw).Contains('TargetFrameworkAttribute(".NETFramework,Version=v4.8"')){throw 'AppInfo.cs must carry the .NET Framework 4.8 target attribute'}
# No local game paths in committed overlay files.
foreach($f in @('OverlayCore.cs','GameOverlay.cs','..\tests\OverlayPureTest.cs','..\tests\OverlayHostTest.cs','..\docs\specs\SPEC-overlay.md')){$t=Get-Content $f -Raw;if($t -cmatch '(?i)[A-Z]:\\Users\\|OneDrive|\\run\\'){throw "A local path appears in $f"}}
'PASS: overlay decisions (geometry, hover timing, hotkey rules, settings, feature gating); the overlay window against a stand-in game window parked off screen (placement, no-activate style, buttons through automation peers, hotkey message, key change rules, keyboard wait, release on dispose); source guards (RegisterHotKey only, no hook, no input injection, no window messages sent from the overlay, no process or network code in the overlay; Stop reaches the game only through the launcher RequestClose, which sends the game a close request). No real mouse or keyboard input was used and no real game was started: real hover, a real hotkey press, real focus behaviour over the game and DPI scales above 100% are not covered.'
