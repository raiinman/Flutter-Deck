$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
foreach($g in @('var resizeGhost=new Image{Source=art','var ghostOverlay=new Window','Interval=TimeSpan.FromMilliseconds(16)','ghostScale.ScaleX=pendingWidth/1672.0','ghostTimer.Stop();ghostOverlay.Hide()','IsHitTestVisible=false')){
 if(-not $s.Contains($g)){throw "Missing spatial resize guard: $g"}
}
if($s -match 'DragDelta\+=.*?\{[^\r\n]*?(?<!pending)Width=pendingWidth'){throw 'Live window resize reintroduced'}
'PASS: ghost overlay/throttle/cleanup source guards; not GUI or FPS verification.'
