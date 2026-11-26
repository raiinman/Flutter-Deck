$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
# The Settings screen (developer, 2026-11-20): a clearly labelled game selector, then every setting of that game in ONE list in
# alphabetical order, every row in the same text style. What the Settings screen showed before is Advanced Settings.
# This is a check of the SOURCE. It was made stricter after an independent check showed twelve changes it did not catch.
$p=Get-Content Program.cs -Raw
$d=Get-Content DeckState.cs -Raw
$h=$p.IndexOf('  const double SetTitleSize=20,SetHelpSize=15,SetControlSize=17;'); $a=$p.IndexOf('  void RenderGameSettings(){'); $b=$p.IndexOf('  // ---- key map editor (SPEC-keys.md).')
if($h -lt 0 -or $a -lt $h -or $b -lt $a){throw 'The Settings part of Program.cs (its three text sizes, its helpers, RenderGameSettings) is missing or out of order'}
$part=$p.Substring($h,$b-$h)      # the helpers and the page
$page=$p.Substring($a,$b-$a)      # the page alone
$names='SetTitleSize','SetHelpSize','SetControlSize'

# ---- one text style: only the three named sizes, in the helpers and on the page
if($part -cmatch 'FontSize\s*=\s*(?!(?:SetTitleSize|SetHelpSize|SetControlSize)\s*[,;}])'){throw 'The Settings screen sets a text size that is not exactly SetTitleSize, SetHelpSize or SetControlSize'}
if($part -cmatch '\bTextBlock\b|\bnew\s+(?:System\.Windows\.Controls\.)?Label\b|\bRun\b|FontSizeProperty|LayoutTransform|RenderTransform|ScaleTransform|\bViewbox\b|FontStretch'){throw 'The Settings screen must make its texts with Label(text,size) only'}
if($part.Substring($part.IndexOf("`n")) -cmatch 'Set(?:Title|Help|Control)Size\s*[-+*/%]|[-+*/%]\s*Set(?:Title|Help|Control)Size'){throw 'A text size of the Settings screen is used in a sum: use one of the three sizes as it is'}
# every Label(...) call: its second argument must be one of the three names
function ArgsAt([string]$t,[int]$open){ $args2=@(); $depth=0; $cur=New-Object Text.StringBuilder; $i=$open+1
  while($i -lt $t.Length){ $c=$t[$i]
    if($c -eq '"'){ [void]$cur.Append($c); $i++; while($i -lt $t.Length -and $t[$i] -ne '"'){ if($t[$i] -eq '\'){ [void]$cur.Append($t[$i]); $i++ }; [void]$cur.Append($t[$i]); $i++ }; [void]$cur.Append('"'); $i++; continue }
    if($c -eq '(' -or $c -eq '{' -or $c -eq '['){ $depth++ }
    if($c -eq ')' -or $c -eq '}' -or $c -eq ']'){ if($depth -eq 0){ $args2+=$cur.ToString().Trim(); return ,$args2 }; $depth-- }
    if($c -eq ',' -and $depth -eq 0){ $args2+=$cur.ToString().Trim(); [void]$cur.Clear(); $i++; continue }
    [void]$cur.Append($c); $i++ }
  throw 'A Label call of the Settings screen could not be read' }
$labels=0
foreach($m in [regex]::Matches($part,'(?<![A-Za-z_.])Label\(')){ $labels++; $ar=ArgsAt $part ($m.Index+$m.Length-1)
  if($ar.Count -lt 2 -or $names -cnotcontains $ar[1]){throw ("A Label of the Settings screen has no size, or a size that is not one of the three: Label(" + ($ar -join ',').Substring(0,[Math]::Min(70,($ar -join ',').Length)))} }
if($labels -lt 8){throw "Only $labels Label calls were found in the Settings part"}

# ---- one list, sorted by its titles, every row through SettingRow inside add(...)
$s0=$page.IndexOf('    // ---- the settings.'); $s1=$page.IndexOf('    rows.Sort(delegate(KeyValuePair<string,Action<string>> a,KeyValuePair<string,Action<string>> b){return String.Compare(a.Key,b.Key,StringComparison.OrdinalIgnoreCase);});')
if($s0 -lt 0 -or $s1 -lt $s0){throw 'The settings list must be built between its marker line and the sort by title'}
if(-not $page.Substring($s1).Contains('foreach(var r in rows)r.Value(r.Key);')){throw 'Every settings row must be built from the sorted list'}
$list=$page.Substring($s0,$s1-$s0); $around=$page.Substring(0,$s0)+$page.Substring($s1)
if($around -cmatch 'SettingRow\('){throw 'A settings row is built outside the sorted list'}
if($list -cmatch 'page\.Children\.Add\('){throw 'Something is added to the Settings screen from inside the list without a row'}
if($page -cmatch 'SettingRow\((?!t,)'){throw 'Every SettingRow call must take its title from the sorted list: SettingRow(t,...)'}
if($page -cmatch '\b(?:string|var)\s+t\s*=|\(string t\)(?<!delegate\(string t\))'){throw 'The name t is for the title of a list row only'}
if(([regex]::Matches($page,'page\.Children\.Add\(')).Count -ne 8){throw 'The Settings screen adds something outside its rows: only the game selector, the state line, the builder line, the two messages with their buttons and the foot may be added directly (8 places)'}
if($page -cnotmatch 'SetAutomationId\(builder,"settings-build"\);page\.Children\.Add\(builder\);'){throw 'The Settings screen must show the builder line under the state line'}
if($page -cmatch 'new CheckBox'){throw 'A switch of the Settings screen must be made with Toggle(...)'}
if($page -cmatch '(?<![A-Za-z.])Control\(|\bnew\s+(?:System\.Windows\.Controls\.)?(?:Button|ToggleButton|RepeatButton)\b'){throw 'A button of the Settings screen must be made with SetButton(...)'}
if($page -cmatch '(?<![A-Za-z])Row\('){throw 'The Settings screen must build its rows with SettingRow, not with Row'}
if(([regex]::Matches($list,'SettingRow\(t,')).Count -lt 20){throw 'The settings list has fewer than 20 rows'}
if(([regex]::Matches($p,'(?<![A-Za-z])SettingRow\(')).Count -ne ([regex]::Matches($list,'SettingRow\(t,')).Count+1){throw 'SettingRow is called outside the sorted list of the Settings screen (only its definition and the rows of the list may name it)'}

# ---- every setting is changed in one place only: on the Settings screen, not on the setup screen
$r0=$p.IndexOf('  void RenderRuntimeSetup(){'); $r1=$p.IndexOf('  // ---- Settings page')
if($r0 -lt 0 -or $r1 -lt $r0){throw 'RenderRuntimeSetup was not found before the Settings page'}
$setup=$p.Substring($r0,$r1-$r0)
foreach($m in [regex]::Matches($setup,'\bruntime\.(\w+)\s*(?:[-+*/%^|&]|<<|>>|\?\?)?=(?!=)')){ if($m.Groups[1].Value -cne 'AcceptedHash'){throw ("The setup screen changes a game setting again: runtime." + $m.Groups[1].Value)} }
if($setup -cmatch 'new CheckBox\{Content="(?!Download cover art)|new Slider|GameRuntime\.(?:WindowHeights|LookSensitivities|CameraDistances|FarViews)'){throw 'A settings control is on the setup screen again'}
$set=@([regex]::Matches($page,'\bruntime\.(\w+)\s*=(?!=)') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if($set.Count -lt 16){throw "The Settings screen changes only $($set.Count) settings"}
foreach($n in $set){ $all=([regex]::Matches($p,'\bruntime\.'+$n+'\s*=(?!=)')).Count; $here=([regex]::Matches($page,'\bruntime\.'+$n+'\s*=(?!=)')).Count
  if($all -ne $here){throw "runtime.$n is changed in $all places in Program.cs, $here of them on the Settings screen: a setting must be changed on the Settings screen only"} }
foreach($x in @('Mute','OriginalPicture','OriginalMovement','NoRumble','RumbleStrength','MusicVolume','VoiceVolume','EffectsVolume','Teleport','AnalogPad','NoIntroMovie','OriginalControls','LookSensitivity','InvertLookY','CameraDistance','ZTargetOff','Volume','WindowHeight','Scale')){ if($set -cnotcontains $x){throw "This setting is not on the Settings screen: $x"} }

$pins=@('add("Game window size",','add("Game volume",','add("Sound off at start",','if(playCheck.HasPictureOption)add("Original picture",','if(playCheck.HasMovementOption)add("Smooth movement",',
  'add("Controller vibration",','add("Vibration strength",','levelRow("Music level","music",','levelRow("Voice level","voice",','levelRow("Effects level","effects",',
  'if(playCheck.HasTeleport&&game==1)add("Teleport line in the pause menu",','if(playCheck.HasPadType&&game==2)add("Analog stick",','if(playCheck.HasIntroMovie)add("Opening movie",',
  'add("Third-person controls",','add("Camera speed",','add("Invert camera up and down",','if(playCheck.HasCameraDistance)add("Camera distance",',
  'if(playCheck.HasCameraWalls)add("Camera wall collision (experimental)",','if(playCheck.HasFarView)add("Draw distance",','if(playCheck.HasZTarget)add("Target lock-on",',
  'if(playCheck.HasPadMap)add("Gamepad buttons",','add("Keyboard keys",','add("Memory card",','if(game==1)add("Widescreen (experimental)",',
  'if(playCheck.HasDiagnostics)add("Diagnostics recording",','var dgn=Toggle(!runtime.NoDiagnostics);','runtime.NoDiagnostics=dgn.IsChecked!=true;runtime.DiagnosticsNoticeSeen=true;',
  'var mute=Toggle(runtime.Mute);','runtime.Mute=mute.IsChecked==true;','var original=Toggle(runtime.OriginalPicture);','runtime.OriginalPicture=original.IsChecked==true;',
  'var smooth=Toggle(!runtime.OriginalMovement);','runtime.OriginalMovement=smooth.IsChecked!=true;','var rb=Toggle(!runtime.NoRumble);','runtime.NoRumble=rb.IsChecked!=true;',
  'var tpBox=Toggle(runtime.Teleport);','runtime.Teleport=tpBox.IsChecked==true;','var ap=Toggle(runtime.AnalogPad);','runtime.AnalogPad=ap.IsChecked==true;',
  'var im=Toggle(!runtime.NoIntroMovie);','runtime.NoIntroMovie=im.IsChecked!=true;','var tp=Toggle(!runtime.OriginalControls);','runtime.OriginalControls=tp.IsChecked!=true;',
  'var inv=Toggle(runtime.InvertLookY);','runtime.InvertLookY=inv.IsChecked==true;','var cwall=Toggle(runtime.CameraWalls);','runtime.CameraWalls=cwall.IsChecked==true;',
  'var zt=Toggle(!runtime.ZTargetOff);','runtime.ZTargetOff=zt.IsChecked!=true;',
  'if(playCheck.HasRoll)add("Quick roll: tap Circle",','var qr=Toggle(runtime.Roll);','rollRefresh=delegate{qr.IsEnabled=!running&&OverlayGameSettings.RollUsable(runtime,playCheck);};rollRefresh();','runtime.OriginalMovement=smooth.IsChecked!=true;SaveRuntime();if(rollRefresh!=null)rollRefresh();','runtime.OriginalControls=tp.IsChecked!=true;SaveRuntime();if(rollRefresh!=null)rollRefresh();','runtime.Roll=qr.IsChecked==true;',
  'if(playCheck.HasShotAim)add("Locked shots aim at the target",','var sa=Toggle(!runtime.NoShotAim);','runtime.NoShotAim=sa.IsChecked!=true;')
foreach($x in $pins){ if(([regex]::Matches($list,[regex]::Escape($x))).Count -ne 1){throw "A setting of the Settings screen is missing, doubled, under another condition or with another sense: $x"} }
$pg=$p.IndexOf('if(GameRuntime.DiagnosticsNoticeDue(runtime,playCheck)){'); if($pg -lt 0 -or $pg -lt $p.IndexOf('void PlayGame(){') -or $pg -gt $p.IndexOf('var session=GameRuntime.Start(')){throw 'PlayGame must show the diagnostics notice (game contract 2.42) before it starts the game'}
$tpb=$list.IndexOf('if(playCheck.HasThirdPerson){'); if($tpb -lt 0){throw 'The third-person block of the Settings screen is missing'}
foreach($x in @('add("Camera speed",','if(playCheck.HasCameraDistance)add(','if(playCheck.HasCameraWalls)add(','if(playCheck.HasFarView)add(','if(playCheck.HasZTarget)add(')){ if($list.IndexOf($x) -lt $tpb){throw "This camera setting must stay inside the third-person block: $x"} }
# ---- the game selector: every game, by the game table; drawing the page never switches the game; the Library follows
foreach($x in @('for(int id=1;id<=GameRuntime.GameCount;id++)pick.Items.Add(GameRuntime.Game(id).Name);','"settings-game"','"Game these settings are for"','if(pick.SelectedIndex>=0&&pick.SelectedIndex+1!=game){game=pick.SelectedIndex+1;LibraryFollowGame();bodyScroll.ScrollToTop();UpdatePlay();Render();}','heading.Text="Settings: "+Kind.Name;')){if(-not $page.Contains($x)){throw "The game selector of the Settings screen is missing or changed: $x"}}
if($page.IndexOf('pick.SelectedIndex=game-1;') -lt 0 -or $page.IndexOf('pick.SelectedIndex=game-1;') -gt $page.IndexOf('pick.SelectionChanged+=')){throw 'The game selector must be set before its handler is attached'}
$dl=Get-Content DeckLibrary.cs -Raw
if(-not $dl.Contains('void LibraryFollowGame(){libraryOpen=null;libraryFollow=game;libraryFocusKey="builtin:"+game;}') -or -not $dl.Contains('if(libraryFollow!=0){foreach(var known in items)if(known.BuiltIn==libraryFollow){libraryDetailsItem=known;break;}libraryFollow=0;}')){throw 'The Library must follow the game chosen on the Settings screen'}

# ---- the screens
if(-not $p.Contains('readonly string[] screens={"Home","Scaling","Library","Downloads","Music","Settings","Advanced"};')){throw 'The navigation must have Settings and, after it, Advanced'}
if(-not $p.Contains('if(screen=="Settings"){RenderGameSettings();return;}')){throw 'The Settings screen must show the game settings'}
if(-not $d.Contains('name=="Advanced"?"Advanced Settings":name') -or -not $d.Contains('CatalogCategory(string name){return name=="Advanced"?"All"')){throw 'DeckState: the former Settings content must be Advanced Settings'}
if($p.Contains('Navigate("Settings");},"Open feature proposals"')){throw 'The Features button must open Advanced Settings (the feature catalog), not the game settings'}
if(-not $p.Contains('StretchDirection=StretchDirection.DownOnly')){throw 'A navigation name that is too wide must be drawn smaller, not cut'}
# The name of a navigation button sits inside a Viewbox. Code that reads it as the second child of the button's grid makes the launcher fail at start (seen 2026-11-20).
if($p.Contains('(TextBlock)layout.Children[1]')){throw 'A navigation name is read as layout.Children[1]: it is the Child of the Viewbox there'}
if(([regex]::Matches($p,[regex]::Escape('((TextBlock)((Viewbox)layout.Children[1]).Child).Text'))).Count -ne 2){throw 'The Home and Updates badges must write the navigation name through its Viewbox (2 places)'}
# The big diagnostics notice (DeckNotice.cs): the first button is the one that means recording on, and an unanswered notice changes nothing.
$notice=Get-Content -LiteralPath (Join-Path $FdSrc 'DeckNotice.cs') -Raw;$whole=Get-Content -LiteralPath (Join-Path $FdSrc 'Program.cs') -Raw
if(-not $notice.Contains('var first=Control(firstLabel,delegate{answer=true;w.Close();});var second=secondLabel==null?null:Control(secondLabel,delegate{answer=false;w.Close();});')){throw 'DeckNotice.cs: the first button must answer true and the second false'}
# The guide (GameRuntime.cs GuideTopics): every Settings row title it names in quotation marks must be a row of the Settings screen.
$guide=[regex]::Match((Get-Content -LiteralPath (Join-Path $FdSrc 'GameRuntime.cs') -Raw),'(?s)static readonly string\[\]\[\] GuideTopics=\{(.*?)\r?\n  \};').Groups[1].Value
if(-not $guide){throw 'GameRuntime.cs: the guide topics were not found'}
$named=@([regex]::Matches($guide,'\\"([^"\\]+)\\"') | ForEach-Object { $_.Groups[1].Value } | Sort-Object -Unique)
if($named.Count -ne 9){throw "The guide names only $($named.Count) Settings rows: the comparison is not reading it right"}
foreach($n in $named){if($n -eq 'Build my game...'){if(-not (Get-Content -LiteralPath (Join-Path $FdSrc 'DeckLibrary.cs') -Raw).Contains('LibButton("Build my game...",')){throw 'The guide names a Library button that does not exist: Build my game...'};continue};if(-not $whole.Contains('add("'+$n+'",')){throw "The guide names a Settings row that does not exist: $n"}}
if(-not $whole.Contains('GameRuntime.GuideShown(guideFor,guideDue,guideKit);if(guideFor==runtime)SaveRuntime();')){throw 'Program.cs: the guide shown before Play must be marked as shown and saved'}
if(-not $whole.Contains('if((guideDue&GameRuntime.GuideBuild)!=0)return;')){throw 'Program.cs: Play must not start the old game program right after the guide told the player to build the game again'}
if(-not $whole.Contains('SetAutomationId(open,"feature-guide-open");open.IsEnabled=!running;')){throw 'Program.cs: the guide is not opened from Settings while a game runs'}
foreach($pair in @(@('Locked shots aim at the target','HasShotAim'),@('Target switch skips people and signs','HasLockHealth'),@('Flick to switch target','HasLockFlick'),@('Target lock-on','HasZTarget'))){if(-not $whole.Contains('if(playCheck.'+$pair[1]+')add("'+$pair[0]+'",')){throw "The lock-on topic of the guide names the row $($pair[0]), which must be shown for $($pair[1])"}}
if(-not (Get-Content -LiteralPath (Join-Path $FdSrc 'GameRuntime.cs') -Raw).Contains('(c.HasZTarget&&c.HasLockFlick&&c.HasShotAim&&c.HasLockHealth?GuideLock:0)')){throw 'GameRuntime.cs: the lock-on topic must need every feature whose switch it names'}
if(-not $notice.Contains('buttons[0],buttons[1],GameRuntime.DiagnosticsNoticeFirstFocused(reading,on),reading,')){throw 'DeckNotice.cs: the diagnostics notice must pass its buttons in order, and only a notice read again may close on Esc'}
if(-not $whole.Contains('if(!GameRuntime.DiagnosticsNoticeApply(noticeFor,keepOn))return;')){throw 'Program.cs: Play must stop when the diagnostics notice is closed without an answer'}
if(-not $whole.Contains('if(readFor!=runtime||GameRunning||!GameRuntime.DiagnosticsNoticeApply(readFor,again))return;SaveRuntime();')){throw 'Program.cs: the notice read again must change nothing without an answer'}
if(-not $whole.Contains('keepOn=ShowDiagnosticsNotice(false,true);')){throw 'Program.cs: the notice before Play is the first-time notice of a recorder that is on'}
if(-not $whole.Contains('again=ShowDiagnosticsNotice(true,!readFor.NoDiagnostics);')){throw 'Program.cs: the notice read again must be given the true state of the recorder'}
Write-Output ("PASS: Settings screen (source check): game selector from the game table and followed by the Library; one list of " + ([regex]::Matches($list,'SettingRow\(t,')).Count + " row builders sorted by title, rows only inside it; $labels labels and every control in the three named text sizes; $($set.Count) settings each changed on the Settings screen only and none on the setup screen; $($pins.Count) pins of rows, conditions and switch sense; the former content is Advanced Settings. It does not prove how the page looks: see the pictures.")
