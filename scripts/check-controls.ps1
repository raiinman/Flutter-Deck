$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
Add-Type -AssemblyName System.Drawing
# The ship artwork is not edited for controls: new controls are drawn over it. It was changed ONCE, on 2026-11-20, on the owner's word ("get rid of any images that dont affect our app but also would get me in liability trouble"): the three pictures with game characters inside its screens were painted over with a plain dark panel. This is its hash since then. The file before that change (hash 5CF478AD...) must not come back.
if((Get-FileHash assets\ship.png).Hash -eq '5CF478ADE54D255D1EBC12347CE3975F6A50FEF31EEF6D2E0C13BB8E8EFEC427'){throw 'assets\ship.png is the old picture with the game characters in its screens; it was replaced on 2026-11-20 and must not come back'}
if((Get-FileHash assets\ship.png).Hash -ne 'F9DD2580BC142FBEEDBE465505F144E96CC83061CEA36AF148D5B58519865E77'){throw 'assets\ship.png was changed; controls must be drawn over it, not into it'}
$states='normal','hover','pressed','disabled'
$need=@('play','pause','stop' | ForEach-Object { $k=$_; $states | ForEach-Object { "$k-$_" } })+@('previous','next','shuffle','repeat','speaker','mute','settings','save','folder','gamepad','grid','download','images','music','help','setup','minimize','maximize','close','reset','scale-down','scale-up','features','menu','lamp-off','lamp-dim','lamp-on','lamp-bright')
$have=@(Get-ChildItem assets\controls\*.png | ForEach-Object BaseName)
foreach($n in $need){if($have -notcontains $n){throw "Missing sprite: $n"}}
foreach($n in $have){if($need -notcontains $n){throw "Unexpected sprite: $n"}}
foreach($n in $need){
  $img=[System.Drawing.Bitmap]::FromFile((Join-Path $FdSrc "assets\controls\$n.png"))
  try{
    if($img.PixelFormat -ne [System.Drawing.Imaging.PixelFormat]::Format32bppArgb){throw "$n is not a 32-bit image with transparency"}
    $expect=if($n -like 'lamp-*'){208}elseif($n -match '^(play|pause|stop)-'){100}else{144}
    if($img.Height -ne $expect){throw "$n has height $($img.Height), expected $expect"}
    if($n -notlike 'lamp-*' -and $img.GetPixel([int]($img.Width/2),[int]($img.Height/2)).A -lt 250){throw "$n has no opaque face"}
  }finally{$img.Dispose()}
}
# The four lamp frames share one window, so they line up when swapped.
if(@('off','dim','on','bright' | ForEach-Object { $i=[System.Drawing.Image]::FromFile((Join-Path $FdSrc "assets\controls\lamp-$_.png"));"$($i.Width)x$($i.Height)";$i.Dispose() } | Select-Object -Unique).Count -ne 1){throw 'Lamp frames differ in size'}
$b=Get-Content ..\scripts\build.ps1 -Raw
if(-not $b.Contains('"/resource:assets\controls\$($_.Name),S-$($_.BaseName)"') -or -not $b.Contains('@sprites')){throw '..\scripts\build.ps1 must embed every sprite as S-<name>'}
$s=Get-Content Program.cs -Raw
foreach($g in @('GetManifestResourceStream("S-"+name)','if(Sprite(name)==null&&Sprite(name+"-normal")==null)return;','d.Ring.Visibility=b.IsKeyboardFocused?Visibility.Visible:Visibility.Collapsed;','IsKeyboardFocusedProperty','d.Art.Opacity=!ownStates&&state=="disabled"?0.4:1;','if(!SystemParameters.ClientAreaAnimation||Sprite("lamp-dim")==null)return;','foreach(double cx in new[]{285.0,597.0,1070.0,1397.0})','Place(stopButton,866,646,74,50)','Place(new Border{Background=Ink(PodInk),IsHitTestVisible=false},940,646,546,50);Place(volume,940,646,448,48);','b.Template=BareTemplate();b.FocusVisualStyle=null;','"Stop music"')){if(-not $s.Contains($g)){throw "Missing sprite control guard: $g"}}
# Every dressed control still has a real accessible name; the art never replaces it.
foreach($name in @('"Decrease content text size","scale-down"','"Increase content text size","scale-up"','"Reset content text size to 100 percent","reset"','"Help and safe session log","help"','"Minimize","minimize"','"Maximize or restore","maximize"','"Close Flutter Deck","close"')){if(-not $s.Contains($name)){throw "Top-deck control lost its name or sprite: $name"}}
# Top-deck click areas must not overlap: each starts at or after the end of the one before it.
$deck=[regex]::Matches($s,'Place\(DeckControl\(.*?\),(\d+),174,(\d+),34\);') | ForEach-Object { [pscustomobject]@{X=[int]$_.Groups[1].Value;W=[int]$_.Groups[2].Value} } | Sort-Object X
if($deck.Count -ne 7){throw "Expected 7 dressed top-deck controls, found $($deck.Count)"}   # 9 until 2026-11-20, when the owner had Features and Setup removed
if($s.Contains('Label("FLUTTER DECK"') -or $s.Contains('"Open feature proposals","features"') -or $s.Contains('"Open local setup wizard","setup"')){throw 'The top deck has no FLUTTER DECK text and no Features or Setup buttons (developer, 2026-11-20)'}
for($i=1;$i -lt $deck.Count;$i++){if($deck[$i].X -lt $deck[$i-1].X+$deck[$i-1].W){throw "Top-deck controls overlap at x $($deck[$i].X)"}}
# Sprites travel inside the launcher only: nothing new for the installer or updater to carry.
foreach($f in 'InstallCore.cs','..\scripts\build-setup.ps1','..\scripts\release.ps1','Updater.cs'){if((Get-Content $f -Raw) -match 'assets\\controls'){throw "$f must not handle sprites; they are embedded in the launcher"}}
"PASS: $($need.Count) sprites present, sized and transparent; lamp frames registered; sprites embedded by the build; focus ring, disabled fade, reduced-motion lamps, toggle mark and accessible names guarded; ship art is the file of 2026-11-20 (no game characters). No screen was drawn by this check: how it looks, and hover and pressed states, are not covered."
