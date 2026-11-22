# Notices

## Unofficial fan project
Flutter Deck is an unofficial, free fan-made launcher. It is not affiliated with, endorsed by or sponsored by Capcom. Mega Man, Mega Man Legends, Rockman DASH and related names, characters and designs are trademarks or copyrights of Capcom Co., Ltd. The MIT licence in LICENSE grants no rights to any of them.

Flutter Deck contains no game files, BIOS files, game audio or game artwork, and does not download any. Users point it at files they already own.

## Build tools in the installer
The installer also carries the tools that build a game from the player's own disc, placed in the `builder` folder of the install: the builder kits for the three games (MIT licence, Dead Signal Interactive), Python 3.15.2 (the official embeddable package, Python Software Foundation License, `builder\python\LICENSE.txt`) and Zig 0.17.1 (the official package, MIT licence, `builder\zig\LICENSE`; it also contains LLVM, Clang and LLD under the Apache License 2.0 with LLVM Exceptions and mingw-w64 files under the Zope Public License 2.1). Both packages are unchanged. `builder\NOTICES.txt`, `builder\zig\THIRD-PARTY-NOTICES.txt` and `builder\python\THIRD-PARTY-NOTICES.txt` list the licences and where their texts are; the last holds the notices Python's own `LICENSE.txt` lacks (expat, mpdecimal, SQLite, xz, zlib-ng, libtommath, HACL* and the Python documentation's list of incorporated software), each copied unchanged from its source; the source of those two files is in `builder-notices\` in this repository. One table in the Mega Man Legends kit was generated from "The PlayStation 1 Video (STR) Format" by Michael Sabin (MIT licence); that licence text is in `builder\NOTICES.txt`.

## Code
All source code in this repository is licensed under the MIT licence in LICENSE, copyright Dead Signal Interactive. This includes the scaler in `scaling/`, which is derived from DSI Scaling, the developer's own work (see `scaling/PROVENANCE.md`).

## Artwork and music
- `assets/ship.png` is AI-generated artwork made for this launcher. It was not taken from any game. Since 2026-11-20 it shows the ship only: three small pictures of characters inside its screens were painted over.
- The control and lamp sprites in `assets/controls/` are cut from artwork the developer supplied for this launcher (see `assets/CONTROLS-PROVENANCE.md`). The same terms and cautions apply to them as to the ship artwork.
- `assets/FlutterDeck.ico`, the application icon, is AI-generated artwork supplied by the developer and shows the launcher's ship (see `assets/ICON-PROVENANCE.md`). The same terms and cautions apply to it as to the ship artwork.
- `assets/FlutterDeck-DawnLaunch.wav` ("Dawn Launch") and `assets/FlutterDeck-SkywayRun.wav` ("Skyway Run") are original loops written for this launcher and rendered by `LoopSynth.cs`. They use no game recordings; the notes were written new for this project (see `assets/AUDIO-PROVENANCE.md`).
- `assets/FlutterDeck-OriginalLoop.wav` ("Skyward Circuit") is an AI-generated loop made for this launcher. Its record says no game samples or existing game melodies were supplied to make it (see `assets/AUDIO-PROVENANCE.md`).

Dead Signal Interactive makes these files available under the same MIT terms as the code, to whatever extent it holds rights in them. AI-generated works may not be protected by copyright in some countries, and nothing here grants rights to any Capcom design the artwork may resemble.
