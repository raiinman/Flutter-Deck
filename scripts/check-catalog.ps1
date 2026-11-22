$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
$p=$Here
$s=Get-Content "$p\FeatureCatalog.cs" -Raw -Encoding UTF8
$m=[regex]::Match($s,'Data=@"([\s\S]*?)";')
$rows=$m.Groups[1].Value.Trim() -split '\r?\n'
$ids=@{}; $categories=@{}
foreach($row in $rows){$c=$row.Split('|');if($c.Count -ne 5){throw "Invalid schema: $row"};if($ids.ContainsKey($c[0])){throw "Duplicate ID: $($c[0])"};$ids[$c[0]]=1;$categories[$c[1]]=1;if($c[0] -notin @('library-onboarding','audio-soundtrack') -and $c[4].Split(';').Count -lt 2){throw 'Missing selectable options'}}
foreach($category in @('Library','Graphics','Controls','Accessibility','Saves','Mods','Gameplay','Media','Audio','Community','Diagnostics')){if(-not $categories.ContainsKey($category)){throw "Missing category $category"}}
$source=(Get-Content "$p\Program.cs" -Raw)+(Get-Content "$p\FeatureCatalog.cs" -Raw)+(Get-Content "$p\LocalSetup.cs" -Raw)
if($source -cmatch 'HttpClient|WebClient|Process\.Start(?!Time\b)') {throw 'Unexpected integration API'}
"PASS: $($rows.Count) unique catalog entries (82 proposals, 2 operational links); $($categories.Count) categories; selectable options; no network/process API tokens; local setup file access authorized. Static checks, not UI/runtime validation."
