$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $Here 'ResizeGeometry.cs')
function Assert-Near($actual,$expected){if([Math]::Abs($actual-$expected) -gt 0.00001){throw "Expected $expected got $actual"}}
$r=1672.0/941.0
Assert-Near ([ResizeGeometry]::WidthFromPointer(1672,100,100/$r,1200,3000)) 1772
Assert-Near ([ResizeGeometry]::WidthFromPointer(1672,0,0,1200,3000)) 1672
for($i=0;$i -lt 100;$i++){Assert-Near ([ResizeGeometry]::WidthFromPointer(1672,100,100/$r,1200,3000)) 1772}
Assert-Near ([ResizeGeometry]::WidthFromPointer(1672,-9999,-9999,1200,3000)) 1200
Assert-Near ([ResizeGeometry]::WidthFromPointer(1672,9999,9999,1200,3000)) 3000
# Return to the anchor after a clamp must return to the exact starting width.
Assert-Near ([ResizeGeometry]::WidthFromPointer(1672,0,0,1200,3000)) 1672
$a=[ResizeGeometry]::WidthFromPointer(1672,10,5,1200,3000)
$b=[ResizeGeometry]::WidthFromPointer(1672,10.01,5.01,1200,3000)
if($b -le $a -or $b-$a -gt .1){throw 'Discontinuous pointer projection'}
$s=Get-Content (Join-Path $Here 'Program.cs') -Raw
if($s.Contains('pendingWidth+delta') -or $s.Contains('Math.Abs(e.HorizontalChange)')){throw 'Accumulation/axis switching returned'}
'PASS: anchor projection, repeated event idempotence, reversal, clamps and continuity; not measured GUI smoothness.'
