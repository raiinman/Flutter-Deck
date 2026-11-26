$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
foreach($guard in @('Button DeckControl(','b.FontSize=14','b.Padding=new Thickness(6,2,6,2)','Place(DeckControl("Help"','Place(DeckControl("A-"',',1014,174,44,34)',',1120,174,64,34)')){
 if(-not $s.Contains($guard)){throw "Missing compact deck guard: $guard"}
}
'PASS: top-deck compact type, padding, dimensions and dedicated helper source guards; not live UI verification.'
