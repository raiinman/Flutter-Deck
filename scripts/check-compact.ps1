$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
foreach($guard in @('Place(panel,350,281,1168,420)','Header="Advanced / window options"','IsExpanded=false','child.MinHeight=36','FontSize=18','page.Children.Add(results);page.Children.Add(pager)','IsPressedProperty','IsEnabledProperty')){
 if(-not $s.Contains($guard)){throw "Missing compact layout: $guard"}
}
'PASS: compact layout structure, collapsed advanced controls, result-first ordering, button state guards. Not a visual test.'
