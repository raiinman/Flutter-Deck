$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$s=Get-Content Program.cs -Raw
foreach($g in @('screens={"Home","Scaling","Library","Downloads"','symbols={"\uE80F","\uE740","\uE7FC"','287+i*46,192,41','SkinScrollThumb(bar)','thumb.Template=template','BuildScrollArrowTemplates();','Control.BorderBrushProperty,Ink("#102638")','arrowButton.Template=arrowTemplate','ScrollBar.LineUpCommand','ScrollBar.LineDownCommand','Thumb.IsDraggingProperty','PanningMode=PanningMode.VerticalOnly','CanContentScroll=false','ScrollToVerticalOffset(bodyScroll.VerticalOffset-e.Delta)')){if(-not $s.Contains($g)){throw "Missing scroll/navigation guard: $g"}}
if($s.Contains('Place(DeckControl("Scaling"')){throw 'Top-deck Scaling duplicate'}
if($s.Contains('new ControlTemplate(typeof(System.Windows.Controls.Primitives.ScrollBar))') -or $s.Contains('new System.Windows.Controls.Primitives.Track')){throw 'Native scrollbar mechanics replaced'}
$previousEnd=0
0..7 | ForEach-Object {$top=287+$_*46;if($top -lt $previousEnd){throw 'Menu overlaps'};$previousEnd=$top+41}
if($previousEnd -gt 658){throw 'Menu escapes backing'}
'PASS: eight separated menu entries, Scaling second and no top duplicate, thumb and line-arrow skin on the native scrollbar, and native scroll/panning guards. No actual GUI/input test.'
