# Control and lamp sprites provenance

- The 40 PNG files in `controls\` were cut on 2026-11-16 by `make-sprites.ps1` (repository root) from the developer-supplied "Flutter Deck Visual Controls Pack v1" (`Flutter-Deck-Controls-Art-v1.zip`, SHA-256 `2A0E7F8697CB856B6190C2E29365B2816E4B8B650E18F075D031A6B99310105B`).
- The pack's four sheets, with the hashes its own manifest gives and the script verifies: `transport-states.png` dda95919..., `lamp-states.png` d73d8472..., `utility-controls.png` 40185198..., `supplementary-controls.png` b3086a21.... The sheets are not stored in this repository; rerunning the script needs the pack.
- What the pack says about itself (its own handoff note, not established by this work): generated with an image generator using the existing ship launcher artwork only as a style reference; the sheets contain control glyphs and fixtures only, no character artwork; it grants no third-party trademark or character rights.
- The sprites are crops and downscales of that art, nothing drawn or added. Each is cropped to its opaque button frame; the lamp frames are cut to one shared window so they register.
- `ship.png` was not changed by this work.
- Terms: the root `NOTICE.md` covers these files with the ship artwork.
- Not established: the developer has not yet looked at the sprites in the running launcher.
