$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\LibraryPureTest.exe LibraryCore.cs LibraryView.cs LibraryOpen.cs ..\tests\LibraryPureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Library test compile failed'}
$t=Join-Path $Here ('.dream-loop\lib-'+[guid]::NewGuid().ToString('N').Substring(0,8))
try{
  & .\.dream-loop\LibraryPureTest.exe $t
  if($LASTEXITCODE -ne 0){throw 'Library checks failed'}
}finally{if(Test-Path $t){Remove-Item -LiteralPath $t -Recurse -Force}}
$c=Get-Content LibraryCore.cs -Raw
# The list module keeps and reads a list. It starts nothing and reaches no network.
if($c -cmatch 'System\.Windows|System\.Net|System\.Diagnostics|Process\.|PlayGame|GameRuntime\.'){throw 'LibraryCore must not start anything or use the network'}
$s=Get-Content Program.cs -Raw
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('LibraryCore.cs')){throw 'LibraryCore.cs missing from build'}
# Open is for an added movie only. Its one start is in LibraryOpen.cs, behind CanOpen and a file check. Program.cs starts no program itself (..\scripts\check-catalog.ps1).
$o=Get-Content LibraryOpen.cs -Raw
if(([regex]::Matches($o,'Process\.Start\(')).Count -ne 1 -or -not $o.Contains('Process.Start(new ProcessStartInfo(item.Path){UseShellExecute=true})')){throw 'A library item must be opened in exactly one place, with no arguments'}
if($o.IndexOf('if(!LibraryCore.CanOpen(item)||!File.Exists(item.Path))return') -lt 0 -or $o.IndexOf('if(!LibraryCore.CanOpen(item)||!File.Exists(item.Path))return') -gt $o.IndexOf('Process.Start(')){throw 'Open must check the kind, the extension and the file first'}
if(($o -replace '(?m)^\s*//.*$','') -cmatch 'GameRuntime|PlayGame|Arguments|Verb|System\.Net'){throw 'LibraryOpen must not touch the game Play path, pass arguments or use the network'}
if(([regex]::Matches($s,'LibraryOpen\.Open\(')).Count -ne 1){throw 'Program.cs must open a library item in exactly one place'}
$body=[regex]::Match($s,'void OpenLibraryItem\(LibraryItem item\)\{[^\r\n]*').Value
if($body.Length -eq 0 -or -not $body.Contains('LibraryOpen.Open(item)') -or $body -cmatch 'PlayGame|GameRuntime|gamePlay'){throw 'Opening a library item must not touch the game Play path'}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('LibraryOpen.cs')){throw 'LibraryOpen.cs missing from build'}
# The Library screen is in DeckLibrary.cs, the same window class as Program.cs. The checks that read Program.cs by name
# (check-runtime, check-catalog, check-covers, check-discfinder) do not read it, so its rules are checked here.
# These are text rules: they stop a careless change, not a determined one. Both kinds of comment are taken out first.
$d=((Get-Content DeckLibrary.cs -Raw) -replace '(?s)/\*.*?\*/','') -replace '(?m)//.*$',''
$v=((Get-Content LibraryView.cs -Raw) -replace '(?s)/\*.*?\*/','') -replace '(?m)//.*$',''
if($v -cmatch 'System\.Windows|System\.Net|System\.Diagnostics|Process\.|PlayGame|GameRuntime'){throw 'LibraryView must not start anything or use the network'}
# It starts no game and no program itself, reaches no network, starts no search and no download.
if($d -cmatch 'PlayGame|GameRuntime|Process\.|System\.Diagnostics|System\.Net|WebRequest|WebClient|HttpClient|CoverArt\.Fetch|DiscFinder\.Scan|LibraryOpen\.|FindDisc|FetchCovers|playCheck\s*=[^=]'){throw 'DeckLibrary.cs must not start anything or use the network'}
# No other way to press or to enable a button: no automation peer, no reflection, no command, no property system calls on IsEnabled.
if($d -cmatch 'AutomationPeer|IInvokeProvider|System\.Reflection|GetMethod|GetField|GetProperty|\.Invoke\(|OnClick|\.Command\b|IsEnabledProperty\s*,\s*(true|false)|ClearValue|SetCurrentValue|CoerceValue|InvalidateProperty'){throw 'DeckLibrary.cs must not press or enable a button in another way'}
# The Play button is named in two statements only, both in PressPlay: the test and the press. Its enabled state is never written here.
if(([regex]::Matches($d,'gamePlay')).Count -ne 3 -or ([regex]::Matches($d,'UpdatePlay')).Count -ne 1 -or ([regex]::Matches($d,'RaiseEvent')).Count -ne 1 -or ([regex]::Matches($d+$s,'\.RaiseEvent\(new RoutedEventArgs\(ButtonBase\.ClickEvent\)\)')).Count -ne 1){throw 'The Play button may be named only in the test and the press of PressPlay'}
# PressPlay is this exact text: take a built-in game (1 or 2), select it, refresh readiness, stop when the Play button is not enabled, press it once.
$press=([regex]::Match($d,'(?s)void PressPlay\(LibraryItem item\)\{.*?\r?\n  \}').Value) -replace '\s',''
if($press -cne 'voidPressPlay(LibraryItemitem){if(item==null||item.BuiltIn<1||item.BuiltIn>runtimes.Length)return;game=item.BuiltIn;librarySelected=-1;UpdatePlay();if(gamePlay==null||!gamePlay.IsEnabled){ShowLibraryItem(item,true);return;}LibraryView.MarkOpened(libraryState,item,DateTime.UtcNow);SaveLibraryState();gamePlay.RaiseEvent(newRoutedEventArgs(ButtonBase.ClickEvent));Render();}'){throw 'PressPlay must be exactly: take a built-in game, select it, refresh readiness, test the Play button, press it once'}
if(([regex]::Matches($d,'PressPlay')).Count -ne 3){throw 'PressPlay has two callers: the main action of a built-in game, and the Play button of its panel'}
if(([regex]::Matches($d,'OpenLibraryItem')).Count -ne 1 -or $d.IndexOf('if(MoviePresent(item)){') -lt 0 -or $d.IndexOf('if(MoviePresent(item)){') -gt $d.IndexOf('OpenLibraryItem(')){throw 'A movie must be opened from one place, after the file check'}
foreach($g in @('LibraryCore.Load(LibraryCore.PathName)','LibraryCore.Save(LibraryCore.PathName,libraryUser=next)','gameOnly.Add(play);','bool lib=DeckState.ShowsLibrary(screen),gamePage=false;','foreach(var item in gameOnly)item.Visibility=gamePage?Visibility.Visible:Visibility.Collapsed;','readonly WrapPanel libraryIcons','BuildLibraryView();','public partial class Deck : Window','libraryGridView.LayoutTransform=new ScaleTransform(UiPreferences.Drawn(contentScale),UiPreferences.Drawn(contentScale))')){if(-not $s.Contains($g)){throw "Missing library guard in Program.cs: $g"}}
foreach($g in @('public partial class Deck {','void RenderLibrary()','LibraryView.Load(LibraryView.PathName)','LibraryView.Save(LibraryView.PathName,libraryState)','if(libraryStateLoadFailed)return;','LibraryView.Arrange(all,libraryState,libraryTab,librarySearch)','"library-search"','"library-add"','"library-open"','"library-remove"','"library-list"','"library-play"','"library-panel"','"library-close"','"library-filter-pane"','"library-view"','"library-tab-"','"library-item-"','"library-action-"','"library-fav-"','"library-show-hidden"','root.IsKeyboardFocusWithinChanged+=','e.Key==Key.Apps','static bool SameItem(LibraryItem a,LibraryItem b)')){if(-not $d.Contains($g)){throw "Missing library guard in DeckLibrary.cs: $g"}}
foreach($f in @('DeckLibrary.cs','LibraryView.cs')){if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains($f)){throw "$f missing from build"};$x=Get-Content $f -Raw;if($x -cmatch '(?i)[A-Z]:\\Users\\|OneDrive'){throw "A local path appears in $f"}}
# No local path and no private file name in the list module or its test.
foreach($f in @('LibraryCore.cs','LibraryOpen.cs','..\tests\LibraryPureTest.cs','..\docs\specs\SPEC-library.md')){$x=Get-Content $f -Raw;if($x -cmatch '(?i)[A-Z]:\\Users\\|OneDrive'){throw "A local path appears in $f"}}
$i=Get-Content InstallCore.cs -Raw;if(-not $i.Contains('"overlay.txt","library.txt"') -or -not $i.Contains('"covers.txt","library-view.txt"')){throw 'Uninstall must list library.txt and library-view.txt with the other settings'}
# Owner, 2026-11-21: no three-dot menu; a click, a right click and the menu key all open the item's card.
if($d.Contains('OpenLibraryMenu') -or $d.Contains('"library-more-"+row') -or $d.Contains('libraryMenu')){throw 'The library has no item menu any more (developer, 2026-11-21)'}
if(([regex]::Matches($d,'MouseRightButtonUp\+=delegate\(object sender,MouseButtonEventArgs e\)\{ShowLibraryItem\(item,true\);e\.Handled=true;\};')).Count -ne 2 -or ([regex]::Matches($d,'if\(MenuKey\(e\)\)\{ShowLibraryItem\(item,true\);e\.Handled=true;\}')).Count -ne 2){throw 'A right click and the menu key must open the card, on the grid and on the list'}
'PASS: library list module compiled alone (kinds, movie file rule, add, duplicates, limits, file round trip, test seam, Open refusals); Open has one guarded site in LibraryOpen.cs, apart from the game Play path; library.txt is in the uninstall list. Source guards only: the Library screen was not run by this check.'
