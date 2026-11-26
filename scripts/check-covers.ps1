$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\CoverArtPureTest.exe CoverArt.cs ..\tests\CoverArtPureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Cover art test compile failed'}
$t=Join-Path $FdSrc ('.dream-loop\cov-'+[guid]::NewGuid().ToString('N').Substring(0,8))
try{
  & .\.dream-loop\CoverArtPureTest.exe $t
  if($LASTEXITCODE -ne 0){throw 'Cover art checks failed'}
}finally{if(Test-Path $t){Remove-Item -LiteralPath $t -Recurse -Force}}
$c=(Get-Content CoverArt.cs -Raw) -replace '(?m)^\s*//.*$',''
# One request site, one fixed HTTPS address, no redirect, no credentials, and nothing that could change the host.
if(([regex]::Matches($c,'WebRequest\.Create\(')).Count -ne 1 -or -not $c.Contains('var req=(HttpWebRequest)WebRequest.Create(uri);req.AllowAutoRedirect=false;req.UseDefaultCredentials=false;req.Credentials=null;req.CookieContainer=null;')){throw 'Cover art must have one request site with no redirect and no credentials'}
if(([regex]::Matches($c,'https?://')).Count -ne 1 -or -not $c.Contains('const string Prefix="https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/";') -or -not $c.Contains('public const string Host="raw.githubusercontent.com";')){throw 'Cover art must use its one fixed address'}
if($c -cmatch 'GetEnvironmentVariable|Environment\.GetCommandLineArgs|Process\.|System\.Diagnostics|WebClient|HttpClient|\.Headers|\.Method="(POST|PUT)|GetRequestStream|System\.Windows'){throw 'Cover art must not take an address from outside, send data, start anything or hold window code'}
# The code is checked before an address or a file name is made from it, and the request uses only that address.
if($c.IndexOf('Uri uri=Address(code);') -lt 0 -or $c.IndexOf('Uri uri=Address(code);') -gt $c.IndexOf('WebRequest.Create(') -or $c.IndexOf('if(!IsCode(code))throw') -lt 0 -or $c.IndexOf('if(!IsCode(code))throw') -gt $c.IndexOf('new Uri(Prefix+code+".jpg")') -or ([regex]::Matches($c,'new Uri\(')).Count -ne 1){throw 'The disc code must be checked before the address is made'}
if(([regex]::Matches($c,'if\(!IsCode\(code\)\)throw')).Count -ne 3){throw 'Every address and file name must be made from a checked code'}
# Only a checked JPEG picture is kept, and only through Store.
if(([regex]::Matches($c,'File\.WriteAllBytes\(')).Count -ne 1 -or $c.IndexOf('if(!IsJpeg(data))throw') -lt 0 -or $c.IndexOf('if(!IsJpeg(data))throw') -gt $c.IndexOf('File.WriteAllBytes(')){throw 'Only a checked picture may be kept'}
$b=Get-Content ..\scripts\build.ps1 -Raw;if(-not $b.Contains('CoverArt.cs')){throw 'CoverArt.cs missing from build'}
if($b -cmatch 'covers\\|\.jpg'){throw 'No cover picture may be built into the program'}
$s=Get-Content Program.cs -Raw
# The launcher downloads from one place, on a background thread, and only while the switch is on.
if(([regex]::Matches($s,'CoverArt\.Fetch\(')).Count -ne 1){throw 'Program.cs must download a cover in exactly one place'}
$fetch=[regex]::Match($s,'void FetchCovers\(bool again\)\{[^\r\n]*').Value
if($fetch.Length -eq 0 -or $fetch.IndexOf('if(!coversOn)') -lt 0 -or $fetch.IndexOf('if(!coversOn)') -gt $fetch.IndexOf('QueueUserWorkItem') -or $fetch.IndexOf('QueueUserWorkItem') -gt $fetch.IndexOf('CoverArt.Fetch(') -or -not $fetch.Contains('Dispatcher.BeginInvoke') -or -not $fetch.Contains('if(!coversOn)break;string result=CoverArt.Fetch(folder,code);') -or -not $fetch.Contains('{coverPending=true;return;}')){throw 'Covers must be downloaded on a background thread, only while the switch is on'}
foreach($g in @('CoverArt.LoadEnabled(CoverArt.SettingPath)','CoverArt.SaveEnabled(CoverArt.SettingPath,coversOn)','"covers-download"','"covers-again"','ImageSource CoverImage(string code)')){if(-not $s.Contains($g)){throw "Missing cover guard: $g"}}
# No cover picture and no local path in the source or the records; the kept pictures are in the uninstall list.
if(@(git ls-files '*.jpg' '*.jpeg' 'covers/*' 2>$null).Count -gt 0){throw 'A cover picture is tracked in the repository'}
foreach($f in @('CoverArt.cs','..\tests\CoverArtPureTest.cs','..\docs\specs\SPEC-covers.md')){$x=Get-Content $f -Raw;if($x -cmatch '(?i)[A-Z]:\\Users\\|OneDrive'){throw "A local path appears in $f"}}
$i=Get-Content InstallCore.cs -Raw;if(-not $i.Contains('"game-folder.txt","covers.txt"') -or -not $i.Contains('Path.Combine(full,"covers")')){throw 'Uninstall must list covers.txt and the kept covers'}
# Owner, 2026-11-20: no default picture for a built-in game. The cards of Mega Man Legends 1 and 2 used two painted pictures cut from the ship art; they are gone.
if($s -cmatch 'CroppedBitmap|libraryCrops|libraryTallCrops'){throw 'Program.cs cuts a default picture for a Library card again: a built-in game has no default picture'}
if(-not $s.Contains('// No default picture for a built-in game (developer, 2026-11-20')){throw 'The note that a built-in game has no default picture is missing in LibraryCover'}
'PASS: cover art module compiled alone (code pattern, fixed address, picture check, kept files, remembered miss, switch); one request site, one fixed HTTPS address, no redirect; one guarded download place in Program.cs; no picture in the build or the repository; covers are in the uninstall list. Nothing was downloaded by this check.'
