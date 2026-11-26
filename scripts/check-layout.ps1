$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $FdSrc 'DeckPaths.cs'),(Join-Path $FdSrc 'UiPreferences.cs')
if([UiPreferences]::Clamp(0.1) -ne 0.8 -or [UiPreferences]::Clamp(9) -ne 1.2 -or [UiPreferences]::Clamp([double]::NaN) -ne 1){throw 'Scale bounds failed'}
$t=Join-Path $FdSrc ('.dream-loop\text-test-'+[guid]::NewGuid().ToString('N')+'\size.txt')
[UiPreferences]::Save($t,0.9);if([Math]::Abs([UiPreferences]::Load($t)-0.9) -gt 0.001){throw 'Preference roundtrip failed'}
[UiPreferences]::Save($t,1);if([UiPreferences]::Load($t) -ne 1){throw 'Reset failed'}
if(-not(Test-Path ($t+'.bak'))){throw 'Preference backup absent'}
# 100% is drawn at 1.2 times the first design size; a file from an older build is converted (old 1.20 to 1.00; old 1.00 and below to 0.80).
if([Math]::Abs([UiPreferences]::Drawn(1)-1.2) -gt 0.0001 -or [Math]::Abs([UiPreferences]::Drawn(9)-1.44) -gt 0.0001){throw 'Drawn size failed'}
if((Get-Content $t -Raw) -ne 'v2 1.00'){throw 'New values must carry the v2 mark'}
foreach($case in @(@('1.20',1.0),@('1.10',0.9),@('1.00',0.8),@('0.80',0.8),@('v2 1.20',1.2),@('v2 0.90',0.9),@('v2 junk',1.0),@('junk',1.0),@('',1.0),@('NaN',1.0))){[IO.File]::WriteAllText($t,$case[0]);if([Math]::Abs([UiPreferences]::Load($t)-$case[1]) -gt 0.0001){throw "Text size file '$($case[0])' must load as $($case[1])"}}
$s=Get-Content (Join-Path $FdSrc 'Program.cs') -Raw
foreach($guard in @('LayoutToolbar(context)','LayoutToolbar(bar)','LayoutToolbar(pager)','LayoutToolbar(actions)','LayoutToolbar(localActions)','TextAlignment=TextAlignment.Center','child.Width=Double.NaN','child.Margin=new Thickness(6)','SetContentScale(contentScale-0.1)','SetContentScale(contentScale+0.1)','SetContentScale(1)','new ScaleTransform(UiPreferences.Drawn(contentScale),UiPreferences.Drawn(contentScale))')){if(-not $s.Contains($guard)){throw "Missing explicit layout/scale guard: $guard"}}
if($s.Contains('page.LayoutTransform=new ScaleTransform(contentScale,contentScale)')){throw 'The page must be drawn at the base size times the chosen percent'}
'PASS: explicit grid/spacing/alignment source guards, scale bounds, isolated preference roundtrip/reset/backup, old-file conversion. No visual or interaction claim.'
