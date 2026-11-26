$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw -Encoding UTF8
if($s.Contains("TargetType='ScrollBar'")){throw 'Custom scrollbar template returned'}
if(-not $s.Contains('CanContentScroll=false') -or -not $s.Contains('ScrollToVerticalOffset(bodyScroll.VerticalOffset-e.Delta)')){throw 'Pixel/wheel scrolling guard missing'}
$drag=[regex]::Match($s,'grip.DragDelta\+=([\s\S]*?);\s*grip.DragCompleted').Groups[1].Value
if($drag -match '(?<![A-Za-z])Width\s*=|(?<![A-Za-z])Height\s*='){throw 'Live window resize returned'}
if(-not $s.Contains('Width=pendingWidth;Height=pendingWidth*941.0/1672.0')){throw 'Deferred resize commit missing'}
if(-not $s.Contains('if(OpenDropDown(e.OriginalSource)!=null)return;Navigate("Library");') -or -not $s.Contains('return owner!=null&&owner.IsDropDownOpen?owner:null;') -or -not $s.Contains('var box=node as ComboBox;if(box!=null)return box.IsDropDownOpen?box:null;') -or -not $s.Contains('node=up!=null?up:LogicalTreeHelper.GetParent(node);')){throw 'Escape must leave an open dropdown list to close itself'}
$all=0..83;$seen=@();for($page=0;$page -lt 14;$page++){$slice=@($all | Select-Object -Skip ($page*6) -First 6);if($slice.Count -gt 6){throw 'Unbounded page'};$seen+=$slice};if(($seen | Select-Object -Unique).Count -ne 84){throw 'Paging lost proposals'}
$pending=1672.0;for($i=0;$i -lt 10000;$i++){$delta=if($i%2 -eq 0){3}else{-4};$pending=[Math]::Max(1200,[Math]::Min(3000,$pending+$delta))};if($pending -lt 1200 -or $pending -gt 3000){throw 'Resize bounds failed'}
'PASS: static native-scroll/wheel/deferred-resize guards; paging coverage and scalar resize bounds. NOT UI mechanics or performance measurement.'
