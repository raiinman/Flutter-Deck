$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
New-Item -ItemType Directory -Force .dream-loop | Out-Null
& "$fx\csc.exe" /nologo /target:exe /out:.dream-loop\PadMapPureTest.exe PadMap.cs GameRuntime.cs LocalSetup.cs DeckPaths.cs ..\tests\PadMapPureTest.cs
if($LASTEXITCODE -ne 0){throw 'Gamepad map test compile failed'}
& .\.dream-loop\PadMapPureTest.exe .dream-loop\padmap-fixture
if($LASTEXITCODE -ne 0){throw 'Gamepad map tests failed'}
$k=Get-Content PadMap.cs -Raw
if($k -cmatch 'System\.Windows|System\.Net|Process\.|Registry|Environment\.GetFolderPath'){throw 'PadMap.cs must stay free of window, network, process and registry code'}
if(-not (Get-Content ..\scripts\build.ps1 -Raw).Contains('PadMap.cs')){throw 'PadMap.cs missing from build'}
$p=Get-Content Program.cs -Raw
foreach($g in @('padDraft.Save(GameRuntime.PadMapPath(PadData));','void SavePadDraft(){','"Save gamepad buttons"','"Discard gamepad changes"','"Use the default buttons"','add("Gamepad buttons",')){if(-not $p.Contains($g)){throw "Missing gamepad editor guard: $g"}}
if(([regex]::Matches($p,'padDraft\.Save\(')).Count -ne 1){throw 'The gamepad map must be saved from exactly one place'}
# The game's own reader (rtc_padmap.h of the Legends 1 kit, compiled with the bundled Zig) and PadMap.cs must read every corpus file the same.
$kit=@($env:FD_PADMAP_KIT,(Join-Path $env:LOCALAPPDATA 'Programs\Flutter Deck\builder\kit'))|Where-Object{$_ -and (Test-Path -LiteralPath (Join-Path $_ 'runtime-core\rtc_padmap.h'))}|Select-Object -First 1
$zig=@($env:FD_ZIG,(Join-Path $env:LOCALAPPDATA 'Programs\Flutter Deck\builder\zig\zig.exe'))|Where-Object{$_ -and (Test-Path -LiteralPath $_)}|Select-Object -First 1
if(-not $kit -or -not $zig){throw 'The comparison with the game reader needs the Legends 1 kit and Zig (an installed Flutter Deck, or FD_PADMAP_KIT and FD_ZIG)'}
$c=@'
#include <stdio.h>
#include "rtc_padmap.h"
/* Reads a file exactly as rtc_move_init does (fopen "r", fgets into 1024 bytes, a too-long line refused whole) over both default maps. */
static int one(const char *path, PadMap *m) {
    FILE *f = fopen(path, "r"); char line[1024]; int bad = 0;
    if (!f) return -1;
    while (fgets(line, sizeof line, f)) {
        int r, toolong = strchr(line, '\n') == 0 && !feof(f);
        if (toolong) { int ch; while ((ch = fgetc(f)) != EOF && ch != '\n') ; }
        r = toolong ? -1 : padmap_line(m, line);
        if (r < 0) bad++;
    }
    fclose(f); return bad;
}
int main(int argc, char **argv) {
    int a, i;
    for (a = 1; a < argc; a++) {
        PadMap m0 = padmap_default(), m1 = padmap_default_tps(); int bad = one(argv[a], &m0); one(argv[a], &m1);
        for (i = 0; i < PHY_COUNT; i++) printf("%x ", (unsigned)m0.act[i]);
        for (i = 0; i < PHY_COUNT; i++) printf("%x ", (unsigned)m1.act[i]);
        printf("%d %d %d %d\n", m0.look_sens, m0.invert_y, m0.deadzone, bad);
    }
    return 0;
}
'@
$src=Join-Path $Here '.dream-loop\padmap_ref.c';$exe=Join-Path $Here '.dream-loop\padmap_ref.exe'
[IO.File]::WriteAllText($src,$c)
& $zig cc -O1 -I (Join-Path $kit 'runtime-core') -o $exe $src
if($LASTEXITCODE -ne 0){throw 'The game reader harness did not compile'}
$corpus=Join-Path $Here '.dream-loop\padmap-corpus'
& .\.dream-loop\PadMapPureTest.exe corpus $corpus
$files=@(Get-ChildItem -LiteralPath $corpus -File|Sort-Object Name|ForEach-Object{$_.FullName})
$ref=@(& $exe @files);$ours=@(& .\.dream-loop\PadMapPureTest.exe dump @files)
if($ref.Count -ne $files.Count -or $ours.Count -ne $files.Count){throw "The comparison printed $($ref.Count) and $($ours.Count) lines for $($files.Count) files"}
$diff=0;for($i=0;$i -lt $files.Count;$i++){if($ref[$i] -cne $ours[$i]){$diff++;if($diff -le 5){Write-Output ("DIFF "+(Split-Path $files[$i] -Leaf)+"`n  game: "+$ref[$i]+"`n  ours: "+$ours[$i])}}}
if($diff){throw "PadMap.cs reads $diff of $($files.Count) files differently from the game"}
"PASS: gamepad map tests; PadMap.cs reads all $($files.Count) corpus files exactly as the game's reader (rtc_padmap.h from $kit, compiled with Zig) over both default maps; the map is written only by SavePadDraft. No key was pressed and no screen was drawn by this check."
