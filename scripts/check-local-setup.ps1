$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Add-Type -Path (Join-Path $FdSrc 'DeckPaths.cs'),(Join-Path $FdSrc 'LocalSetup.cs') -ReferencedAssemblies System.Xml,System.Core
$t=Join-Path $FdSrc ('.dream-loop\tests-'+[Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force $t | Out-Null
[IO.File]::WriteAllBytes((Join-Path $t 'track.bin'),[byte[]](1,2,3))
[IO.File]::WriteAllText((Join-Path $t 'game.cue'),'FILE "track.bin" BINARY')
[IO.File]::WriteAllText((Join-Path $t 'escape.cue'),'FILE "..\outside.bin" BINARY')
[IO.File]::WriteAllBytes((Join-Path $t 'bios.bin'),[byte[]]::new(524288))
if([LocalSetup]::CheckGame((Join-Path $t 'game.cue')) -ne ''){throw 'Valid CUE fixture rejected'}
if([LocalSetup]::CheckGame((Join-Path $t 'escape.cue')) -eq ''){throw 'Traversal accepted'}
if([LocalSetup]::CheckBios((Join-Path $t 'bios.bin')) -ne ''){throw 'Valid BIOS shape rejected'}
if([LocalSetup]::CheckBios((Join-Path $t 'track.bin')) -eq ''){throw 'Invalid BIOS shape accepted'}
$state=[SetupState]::new();$state.Game[0]='fixture-game';$state.Bios[1]='fixture-bios';$cfg=Join-Path $t 'test.xml'
[LocalSetup]::Save($cfg,$state);$loaded=[LocalSetup]::Load($cfg)
if($loaded.Game[0] -ne 'fixture-game' -or $loaded.Bios[1] -ne 'fixture-bios'){throw 'Roundtrip mismatch'}
[LocalSetup]::Save($cfg,$state);if(-not (Test-Path ($cfg+'.bak'))){throw 'Backup absent'}
'PASS: bounded synthetic metadata and configuration tests. No UI/game runtime validation.'
if([LocalSetup]::ReferenceStatus('','') -notlike 'Setup needed*'){throw 'Empty readiness'}
if([LocalSetup]::ReferenceStatus((Join-Path $t 'missing.bin'),(Join-Path $t 'bios.bin')) -notlike '*missing or moved*'){throw 'Missing readiness'}
if([LocalSetup]::ReferenceStatus((Join-Path $t 'game.cue'),(Join-Path $t 'bios.bin')) -notlike 'References ready; native runtime not connected*'){throw 'Ready/runtime distinction'}
if([LocalSetup]::ReferenceStatus((Join-Path $t 'escape.cue'),(Join-Path $t 'bios.bin')) -notlike 'Reference needs attention*'){throw 'Invalid readiness'}
[IO.File]::WriteAllText($cfg,'<flutterDeck schema="1"><game image="ignored"/><game id="2" image="kept"/></flutterDeck>')
if([LocalSetup]::Load($cfg).Game[1] -ne 'kept'){throw 'Malformed id handling'}
'PASS: empty/missing/invalid/reference-ready states and absent ID tolerance.'
