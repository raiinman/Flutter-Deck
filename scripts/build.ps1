param([switch]$PublicV1)
$Here = (Resolve-Path (Join-Path $PSScriptRoot '..\src')).Path # this script is in scripts/; the program is in src/
Set-Location $Here
$ErrorActionPreference='Stop'
Set-Location $Here
$fx="$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319"
# -PublicV1: the public version 1 build (developer, 2026-11-20): "Play with a friend..." is shown greyed out. Without it (the owner's own build) it works.
$defines=@(); if($PublicV1){ $defines+='/define:FD_V1' }
# Control and lamp sprites (cut by ..\scripts\make-sprites.ps1) travel inside the program, like the ship art.
$sprites=@(Get-ChildItem assets\controls\*.png | Sort-Object Name | ForEach-Object { "/resource:assets\controls\$($_.Name),S-$($_.BaseName)" })
& "$fx\csc.exe" /nologo /target:winexe /platform:x64 $defines /out:FlutterDeck.exe /win32icon:assets\FlutterDeck.ico /win32manifest:FlutterDeck.manifest /resource:assets\ship.png,Ship /resource:news-example.xml,News @sprites /reference:"$fx\WPF\PresentationFramework.dll" /reference:"$fx\WPF\PresentationCore.dll" /reference:"$fx\WPF\WindowsBase.dll" /reference:"$fx\System.Xaml.dll" /reference:System.Web.Extensions.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll Program.cs TeleportCore.cs DeckLibrary.cs LibraryView.cs DeckPrismBackend.cs KeyMap.cs SaveCards.cs PresetCore.cs OverlayCore.cs GameOverlay.cs OverlayGameSettings.cs Preflight.cs NewsFeed.cs DeckAudio.cs FeatureCatalog.cs LocalSetup.cs UiPreferences.cs LibraryCore.cs LibraryOpen.cs DiscFinder.cs CoverArt.cs MediaFolders.cs ResizeGeometry.cs Foundation.cs DeckState.cs Updater.cs AppInfo.cs GameRuntime.cs MpCore.cs RouterPort.cs DeckMultiplayer.cs BuildCore.cs BuildRun.cs DeckBuilder.cs GameFilesOpen.cs scaling\DsiNative.cs scaling\Profile.cs DeckPaths.cs
if($LASTEXITCODE -ne 0){throw 'Compilation failed'}
