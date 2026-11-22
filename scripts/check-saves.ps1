$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\SaveCardsPureTest.exe SaveCards.cs GameRuntime.cs LocalSetup.cs DeckPaths.cs ..\tests\SaveCardsPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Memory card backup test compile failed'}
& .\.dream-loop\SaveCardsPureTest.exe .dream-loop\cards-fixture
if($LASTEXITCODE -ne 0){throw 'Memory card backup tests failed'}
$c=Get-Content SaveCards.cs -Raw
# The unit never deletes a card or a backup, never uses the game's own ".tmp" name, and has no window, network or process-start code.
if($c -cmatch 'System\.Windows|System\.Net|Process\.Start|Registry|Directory\.Delete|File\.Replace|File\.Delete\((?!part\)|path\);\s*\}catch)'){throw 'SaveCards.cs must not delete anything but its own part file, and must stay free of window, network and process code'}
if($c -match '\.tmp|"tmp"|"t"\s*\+|"\.t"'){throw 'SaveCards.cs must never use the game''s .tmp name'}
foreach($g in @('if(!IsRestorable(backupPath,data))throw','new System.Threading.Mutex(false,GameRuntime.GateName(data))','held=gate.WaitOne(0);','if(GameRuntime.CardInUse(data)||','if(File.Exists(card))kept=Path.GetFileName(BackupAs(','try{ReplaceInOneStep(part,card);}','MoveFileEx(part,card,0x1|0x8)','File.Move(part,target); // fails rather than replaces','if(File.Exists(GameRuntime.ImportPath(data))&&!File.Exists(card))throw','if(GameRuntime.FileSha256(part)!=GameRuntime.FileSha256(source))throw','if(GameRuntime.FileSha256(part)!=GameRuntime.FileSha256(source)||new FileInfo(part).Length!=GameRuntime.CardBytes)throw','(info.Attributes&FileAttributes.ReparsePoint)!=0||(new DirectoryInfo(folder).Attributes&FileAttributes.ReparsePoint)!=0','GameRuntime.AlreadyRunning(program)')){if(-not $c.Contains($g)){throw "Missing memory card guard: $g"}}
# Order inside Restore: source check, gate, in-use check, safety copy, and only then the replace.
$order=@('if(!IsRestorable(backupPath,data))throw','held=gate.WaitOne(0);','if(GameRuntime.CardInUse(data)||','kept=Path.GetFileName(BackupAs(','try{ReplaceInOneStep(part,card);}') | ForEach-Object { $c.IndexOf($_,$c.IndexOf('public static string Restore(')) }
for($i=1;$i -lt $order.Count;$i++){if($order[$i] -le $order[$i-1]){throw 'Restore steps are out of order'}}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('SaveCards.cs')){throw 'SaveCards.cs missing from build'}
$p=Get-Content Program.cs -Raw
foreach($g in @('if(screen=="Saves")RenderSaveCards();','backup.IsEnabled=state=="ok"&&!running;','restore.IsEnabled=usable&&!running&&!waiting&&!item.SameAsCurrent;','MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;','"card-status"')){if(-not $p.Contains($g)){throw "Missing Saves screen guard: $g"}}
# The card is replaced from exactly one place, behind the confirmation.
if(-not $p.Contains('SaveCards.Restore(item.Path,GameData,runtime.Program);') -or ([regex]::Matches($p,'SaveCards\.Restore\(')).Count -ne 1 -or ([regex]::Matches($p,'RestoreCard\(')).Count -ne 2){throw 'Restore must have exactly one caller, behind the confirmation'}
'PASS: memory card backup tests (verified copies, restore only from the launcher''s two folders, safety copy first, refused while a game uses the card or a start holds the gate); nothing deleted; restore has one caller behind a confirmation. No screen was drawn and no real card was touched by this check.'