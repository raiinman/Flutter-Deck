$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\KeyMapPureTest.exe KeyMap.cs GameRuntime.cs LocalSetup.cs DeckPaths.cs ..\tests\KeyMapPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Key map test compile failed'}
& .\.dream-loop\KeyMapPureTest.exe .dream-loop\keys-fixture
if($LASTEXITCODE -ne 0){throw 'Key map tests failed'}
$k=Get-Content KeyMap.cs -Raw
# The key map unit only reads and writes the one text file it is given: no window code, network or processes.
if($k -cmatch 'System\.Windows|System\.Net|Process\.|Registry|Environment\.GetFolderPath'){throw 'KeyMap.cs must stay free of window, network, process and registry code'}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('KeyMap.cs')){throw 'KeyMap.cs missing from build'}
$p=Get-Content Program.cs -Raw
foreach($g in @('if(keyCapture!=null){CaptureKey(e);return;}','if(e.IsRepeat){e.Handled=true;return;}','e.Key==Key.System?e.SystemKey','KeyInterop.VirtualKeyFromKey(','KeyMap.FromVirtualKey(','keyDraft.Save(GameRuntime.KeysPath(KeyData));','keyDraftFor=KeyData;try{keyDraft=KeyMap.Load(GameRuntime.KeysPath(KeyData));','if(keyDraft==null||!String.Equals(keyDraftFor,KeyData,StringComparison.OrdinalIgnoreCase)){LoadKeyDraft();keyNote=','void NeedKeyDraft(){if(keyDraft==null||!String.Equals(keyDraftFor,KeyData,StringComparison.OrdinalIgnoreCase))LoadKeyDraft();}','string KeyData {get{return GameRuntime.DataFolderFor(OwnGameRunning?sessionGame:game);}}','save.IsEnabled=keyDirty&&keyDraft.Missing().Count==0;','"Save keys"','"Discard key changes"','"Use the default keys"','if(!DeckState.IsScreen(name))return;keyCapture=null;','Deactivated+=delegate{if(keyCapture!=null){keyCapture=null;','e.Key==Key.DeadCharProcessed?e.DeadCharProcessedKey','var save=Control("Save keys",delegate{','void SaveKeyDraft(){')){if(-not $p.Contains($g)){throw "Missing key editor guard: $g"}}
# The map is written in exactly one place, by the Save button.
if(([regex]::Matches($p,'keyDraft\.Save\(')).Count -ne 1){throw 'The key map must be saved from exactly one place'}
'PASS: key map tests (parse, edit, format, save with backup); the unit has no window, network or process code; the key map is written only by the launcher method SaveKeyDraft (the setup screen Save button and the overlay editor both call it), ignores key repeats and refuses the keys the game keeps. No key was pressed and no screen was drawn by this check.'