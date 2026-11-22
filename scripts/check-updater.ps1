$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\UpdaterPureTest.exe Updater.cs ..\tests\UpdaterPureTest.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Updater test compile failed'}
& .\.dream-loop\UpdaterPureTest.exe .dream-loop\updater-fixture
if($LASTEXITCODE -ne 0){throw 'Updater tests failed'}
$u=Get-Content Updater.cs -Raw
foreach($g in @('AllowAutoRedirect=false','UseDefaultCredentials=false','Credentials=null','CookieContainer=null','req.Abort()','MaxManifestBytes=16384','MaxPayloadBytes=67108864','rsa.VerifyData(manifest,sha,signature)','var info=Accept(manifest,signature,publicKeyXml);','if(!FileMatches(stagedPath,info))','RedirectAllowed(next)','hop>=4','DtdProcessing=DtdProcessing.Prohibit','https://raw.githubusercontent.com/raiinman/Flutter-Deck/main/update/manifest.xml','https://github.com/raiinman/Flutter-Deck/releases/download/')){if(-not $u.Contains($g)){throw "Missing updater guard: $g"}}
# The source, key and install target must not be redirectable at run time, and the private key must never be in the source.
if($u -cmatch 'GetEnvironmentVariable|GetCommandLineArgs|AppSettings|Registry|runas|Verb\s*='){throw 'Updater must not take its source or key from the environment, arguments, registry or elevation'}
if($u -cmatch '<P>|<D>|<InverseQ>|PRIVATE KEY'){throw 'Private key material in Updater.cs'}
if(-not ($u -cmatch 'public const string Version="\d{1,4}\.\d{1,4}\.\d{1,4}";') -or -not ($u -cmatch 'public const int Sequence=[1-9]\d{0,8};')){throw 'Release identity missing'}
$p=Get-Content Program.cs -Raw
foreach($g in @('InitializeUpdates();','if(updatePrefs.Auto)CheckForUpdate(false);','Updater.Install(exe,staged,found.Manifest,found.Signature,Updater.PublicKey,Updater.Sequence)','SetAutomaticUpdateCheck(auto.IsChecked==true)','Updater.Offers(found.Info,Updater.Sequence,updatePrefs.Hold,manual)','MessageBoxButton.OKCancel','"update-auto"','"update-status"')){if(-not $p.Contains($g)){throw "Missing update UI guard: $g"}}
if($p.Contains('SIMULATED')){throw 'Simulated update states must not ship'}
# Install only ever starts from the button on the Updates screen.
if(([regex]::Matches($p,'InstallUpdate\(\)')).Count -ne 2){throw 'InstallUpdate must have exactly one caller'}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('Updater.cs')){throw 'Updater.cs missing from build'}
$a=if(Test-Path ..\.gitattributes){Get-Content ..\.gitattributes -Raw}else{''}
if(-not $a.Contains('update/manifest.xml -text') -or -not $a.Contains('update/manifest.sig -text')){throw 'Signed files must be stored byte-exact (.gitattributes)'}
# ..\scripts\release.ps1 loads the launcher's own acceptance code to test a manifest before it is written. That load must keep
# working when Updater.cs gains a need for another file, so it is tried here on every run, in a process of its own.
$load="Add-Type -Path '$Here\Updater.cs','$Here\DeckPaths.cs' -ReferencedAssemblies System.Xml -ErrorAction Stop; if([Updater]::Sequence -lt 1){exit 3}"
& powershell -NoProfile -ExecutionPolicy Bypass -Command $load | Out-Null
if($LASTEXITCODE -ne 0){throw '..\scripts\release.ps1 could not load the updater code: Updater.cs needs a file that is not given to Add-Type'}
$rel=Get-Content ..\scripts\release.ps1 -Raw
if(-not $rel.Contains("Add-Type -Path (Join-Path `$Here 'Updater.cs'),(Join-Path `$Here 'DeckPaths.cs') -ReferencedAssemblies System.Xml")){throw '..\scripts\release.ps1 must load Updater.cs together with DeckPaths.cs'}
if(Test-Path ..\update\manifest.xml){
  Add-Type -Path (Join-Path $Here 'Updater.cs'),(Join-Path $Here 'DeckPaths.cs') -ReferencedAssemblies System.Xml -ErrorAction Stop
  $info=[Updater]::Accept([IO.File]::ReadAllBytes("$Here\..\update\manifest.xml"),[Updater]::DecodeSignature([IO.File]::ReadAllBytes("$Here\..\update\manifest.sig")),[Updater]::PublicKey)
  "Published manifest verifies: $($info.Version) (release $($info.Sequence))."
}
'PASS: updater pure tests; bounded, credential-free, no-redirect manifest fetch; signature before parse and again at install; fixed source and key; install has one click-driven caller; no simulated states. No network request made and no real launcher replaced by this check.'
