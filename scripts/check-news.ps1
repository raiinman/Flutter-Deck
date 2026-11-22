$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
New-Item -ItemType Directory -Force (Join-Path $Here '.dream-loop') | Out-Null
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\NewsPureTest.exe NewsFeed.cs ..\tests\NewsPureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'News test compile failed'}
& .\.dream-loop\NewsPureTest.exe news-example.xml .dream-loop\news-fixture
if($LASTEXITCODE -ne 0){throw 'News tests failed'}
$s=Get-Content NewsFeed.cs -Raw
foreach($g in @('AllowAutoRedirect=false','UseDefaultCredentials=false','Credentials=null','CookieContainer=null','req.Abort()','8000','MaxBytes=131072','result.Count>=50')){if(-not $s.Contains($g)){throw "Missing guard $g"}}
$p=Get-Content Program.cs -Raw
foreach($g in @('string screen="Home"','newsBusy||newsClosed','TimeSpan.FromMinutes(15)','newsClosed=true','RenderHome()')){if(-not $p.Contains($g)){throw "Missing UI guard $g"}}
'PASS: timeout/credentials/concurrency/startup/closed-state structural guards; no delivery claim.'
