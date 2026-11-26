$FdSrc = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $FdSrc
$ErrorActionPreference='Stop'
Set-Location $FdSrc
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /main:TeleportPureTest /out:.dream-loop\TeleportPureTest.exe TeleportCore.cs ..\tests\TeleportPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Teleport test compile failed'}
& .\.dream-loop\TeleportPureTest.exe .dream-loop\teleport-fixture
if($LASTEXITCODE -ne 0){throw 'Teleport tests failed'}
function Code($name){ return ((Get-Content $name -Raw) -replace '(?m)^\s*//.*$','') }
$core=Code 'TeleportCore.cs'
# The unit is pure: it reads a list and writes one small file the game program is told to look for. It never starts a process, uses the network or touches a memory card.
if($core -cmatch 'System\.Windows|System\.Net|Process|Registry|DllImport|Clipboard|\.mcd|\.gme|rock_neo'){throw 'TeleportCore.cs must stay free of window, network, process, registry and memory card code'}
$g=Get-Content GameRuntime.cs -Raw
foreach($x in @('" --teleport-file "','TeleportRequestPath','r.HasTeleport=')){if(-not $g.Contains($x)){throw "Missing teleport guard in GameRuntime.cs: $x"}}
$pg=Get-Content Program.cs -Raw
# Owner, 2026-11-19: the launcher has one teleport switch and no picker. The places are chosen in the game's pause menu.
if($pg -cmatch 'TeleportCore\.|Teleport go|TeleportRequestPath|TeleportTablePath'){throw 'Program.cs must not have a teleport picker: the switch and its explanation only'}
if(-not $pg.Contains('"teleport-allow"')){throw 'The teleport switch is missing in Program.cs'}
if(-not $g.Contains('File.Delete(TeleportRequestPath(data))')){throw 'An old teleport request must be deleted when a game starts'}
# Builder kit 17: a kit-built game brings its list of teleport places; the launcher copies it into the data folder when the player has none.
foreach($x in @('public static bool TakeBuiltTeleportList(string program,string data){','if(File.Exists(target)||Directory.Exists(target))return false;','if(!File.Exists(Path.Combine(outDir,"rmx-kit-build.json")))return false;','FileAttributes.ReparsePoint|FileAttributes.Directory','StartsWith("# teleport-table.txt:",StringComparison.Ordinal)','if(kind.Id==1)TakeBuiltTeleportList(s.Program,data);')){if(-not $g.Contains($x)){throw "The copy of a kit-built list of teleport places is missing or changed in GameRuntime.cs: $x"}}
if($g.IndexOf('if(kind.Id==1)TakeBuiltTeleportList(s.Program,data);') -lt $g.IndexOf('File.Delete(TeleportRequestPath(data))')){throw 'The list of teleport places must be taken after the folder of the game is prepared'}
if(([regex]::Matches($g,'TeleportTablePath\(data\)')).Count -ne 1 -or $g -cmatch 'File\.(Copy|Replace)\([^)]*teleport'){throw 'The list of teleport places must be written in one place only, and never over an existing file'}
Write-Output 'PASS: teleport list, request line and request file (good, bad and edge lines; atomic write; one request at a time); pure unit; the request path and the feature check are in the launch module; a kit-built list is copied only when the player has none. No game program was started.'
