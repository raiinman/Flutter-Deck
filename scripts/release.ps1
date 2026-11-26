# Prepares a signed Flutter Deck release on this PC. It publishes nothing: no upload, no commit, no push.
# Usage:  powershell -NoProfile -ExecutionPolicy Bypass -File ..\scripts\release.ps1 -KeyPath <private key file> -Tools <build tools folder> -Notes "What changed"
# -Tools: the folder of build tools the installer carries (three kits, python.zip, zig.zip; see ..\scripts\build-setup.ps1).
# Before running: set Version and Sequence in Updater.cs (Sequence must go up by at least 1 every release).
param([Parameter(Mandatory=$true)][string]$KeyPath,[Parameter(Mandatory=$true)][string]$Tools,[string]$Notes='')
$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
if(Get-Process FlutterDeck -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $FdSrc 'FlutterDeck.exe') }){throw 'Close Flutter Deck first: the build replaces the FlutterDeck.exe in this folder.'}
if(-not (Test-Path -LiteralPath $KeyPath)){throw 'Private key file not found.'}
if((Resolve-Path -LiteralPath $KeyPath).Path.StartsWith($FdSrc,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep the private key outside this repository folder.'}
if($Notes.Length -gt 2000){throw 'Notes must be 2000 characters or fewer.'}
if($Notes -match '[\x00-\x08\x0B\x0C\x0E-\x1F]'){throw 'Notes contain a control character that cannot be published. Use plain text.'}
$u=Get-Content Updater.cs -Raw
$version=[regex]::Match($u,'public const string Version="(\d{1,4}\.\d{1,4}\.\d{1,4})";').Groups[1].Value
$sequence=[regex]::Match($u,'public const int Sequence=([1-9]\d{0,8});').Groups[1].Value
$publicKey=[regex]::Match($u,'public const string PublicKey="(<RSAKeyValue>.*?</RSAKeyValue>)";').Groups[1].Value
$prefix=[regex]::Match($u,'public const string DownloadPrefix="([^"]+)";').Groups[1].Value
if(-not $version -or -not $sequence -or -not $publicKey -or -not $prefix){throw 'Could not read Version, Sequence, PublicKey or DownloadPrefix from Updater.cs.'}
if(Test-Path ..\update\manifest.xml){
  $previous=[int]([xml](Get-Content ..\update\manifest.xml -Raw)).update.sequence
  if([int]$sequence -le $previous){throw "Sequence $sequence is not higher than the published manifest's $previous. Raise Sequence (and Version) in Updater.cs first."}
}
# Always the public build (developer, 2026-11-20): "Play with a friend..." greyed out until multiplayer works. There is no other way to make a release.
& powershell -NoProfile -ExecutionPolicy Bypass -File ..\scripts\build.ps1 -PublicV1
if($LASTEXITCODE -ne 0){throw 'Build failed.'}
foreach($check in Get-ChildItem ..\scripts\check-*.ps1 | Sort-Object Name){
  & powershell -NoProfile -ExecutionPolicy Bypass -File $check.FullName | Out-Null
  if($LASTEXITCODE -ne 0){throw "Check failed: $($check.Name). Nothing was prepared."}
}
$out=Join-Path $FdSrc ".dream-loop\release\v$version"
if(Test-Path $out){throw "Release folder already exists: $out. Remove it yourself if you mean to prepare this version again."}
New-Item -ItemType Directory -Force $out | Out-Null
try{
Copy-Item FlutterDeck.exe (Join-Path $out 'FlutterDeck.exe')
$exe=Join-Path $out 'FlutterDeck.exe'
$length=(Get-Item $exe).Length
$sha=(Get-FileHash $exe -Algorithm SHA256).Hash
$url="${prefix}v$version/FlutterDeck.exe"
$manifest="<update schema='1' product='flutterdeck' channel='stable' platform='win-x64'>`n<version>$version</version>`n<sequence>$sequence</sequence>`n<file name='FlutterDeck.exe' length='$length' sha256='$sha' url='$url'/>`n<notes>$([System.Security.SecurityElement]::Escape($Notes))</notes>`n</update>`n"
$bytes=(New-Object Text.UTF8Encoding($false)).GetBytes($manifest)
$rsa=New-Object System.Security.Cryptography.RSACryptoServiceProvider
$rsa.PersistKeyInCsp=$false
try{
  $rsa.FromXmlString([IO.File]::ReadAllText((Resolve-Path -LiteralPath $KeyPath).Path))
  if($rsa.PublicOnly){throw 'That file holds only a public key. The private key is needed to sign.'}
  $sha256=New-Object System.Security.Cryptography.SHA256CryptoServiceProvider
  $signature=$rsa.SignData($bytes,$sha256)
}finally{$rsa.Dispose()}
$verify=New-Object System.Security.Cryptography.RSACryptoServiceProvider
$verify.PersistKeyInCsp=$false
try{$verify.FromXmlString($publicKey);$ok=$verify.VerifyData($bytes,(New-Object System.Security.Cryptography.SHA256CryptoServiceProvider),$signature)}finally{$verify.Dispose()}
if(-not $ok){throw 'This key does not match the public key built into the launcher. Installed copies would reject the release.'}
# Run the launcher's own acceptance code on exactly what would be published, before anything is written.
# Updater.cs needs DeckPaths.cs (the data folder); the two are compiled together, as ..\scripts\check-updater.ps1 also does.
Add-Type -Path (Join-Path $FdSrc 'Updater.cs'),(Join-Path $FdSrc 'DeckPaths.cs') -ReferencedAssemblies System.Xml
$sigText=[Convert]::ToBase64String($signature)
$accepted=[Updater]::Accept($bytes,[Updater]::DecodeSignature([Text.Encoding]::ASCII.GetBytes($sigText)),[Updater]::PublicKey)
if($accepted.Version -ne $version -or $accepted.Sequence -ne [int]$sequence -or -not [Updater]::FileMatches($exe,$accepted)){throw 'The launcher would not accept this manifest for this file.'}
# The installer is built around the exact launcher file whose checksum was signed above.
& powershell -NoProfile -ExecutionPolicy Bypass -File ..\scripts\build-setup.ps1 -LauncherPath $exe -Tools $Tools | Out-Null
if($LASTEXITCODE -ne 0){throw 'Setup build failed.'}
$setup=Join-Path $out "FlutterDeckSetup-$version.exe"
Copy-Item (Join-Path $FdSrc '.dream-loop\setup\FlutterDeckSetup.exe') $setup
# The zip is the whole program, as Setup installs it, build tools included (developer, 2026-11-21). It is laid out by the installer's own code from the installer just built.
$zip=Join-Path $out "FlutterDeck-$version-win-x64.zip"
& powershell -NoProfile -ExecutionPolicy Bypass -File ..\scripts\build-zip.ps1 -SetupPath $setup -LauncherPath $exe -OutZip $zip | Out-Null
if($LASTEXITCODE -ne 0){throw 'Zip build failed.'}
# The signed files are written last, so a refusal above leaves the published pair untouched.
New-Item -ItemType Directory -Force ..\update | Out-Null
[IO.File]::WriteAllBytes((Join-Path $FdSrc '..\update\manifest.xml'),$bytes)
[IO.File]::WriteAllText((Join-Path $FdSrc '..\update\manifest.sig'),$sigText,(New-Object Text.UTF8Encoding($false)))
}catch{
  if(Test-Path $out){Remove-Item -Recurse -Force $out}
  throw "Nothing was prepared. $($_.Exception.Message)"
}
"Prepared Flutter Deck $version (release $sequence). Nothing has been published."
"  Launcher file : $exe"
"  Installer     : $setup"
"  Release zip   : $zip"
"  Signed files  : update\manifest.xml and update\manifest.sig"
"  SHA-256       : $sha"
''
'To publish, in this order:'
"  1. On GitHub, create release tag v$version and attach all three files above. The launcher file must keep the name FlutterDeck.exe: installed copies download it by that name. Point new testers at the installer."
'  2. Only after that, commit and push update\manifest.xml and update\manifest.sig. Installed copies start offering the update once those two files are on main.'
'Upload the exact launcher file listed above. A rebuilt copy has a different checksum and installed copies will refuse it.'
