# SPEC: game folder (finding the disc image)

## What the user sees

- The "Game setup and readiness" screen has a row "Game folder" with the button "Choose game folder". The user chooses the folder that holds the disc image.
- The launcher searches that folder and up to three folders below it. It looks for a CUE disc image whose disc holds the file `ROCK_NEO.EXE`. That is the file the game program reads from the disc, so the search does not depend on the file name or the region of the image.
- One image found by a complete search, and no working disc image saved: the launcher sets the found image as the disc image. The user does nothing more. After a search that was not complete (a limit was reached, or deeper folders exist), the launcher sets nothing by itself; the one image is listed with "Use this image".
- Several images found, and no working disc image saved: the launcher lists them, each with "Use this image". It chooses none by itself.
- A saved disc image that works is never replaced by the search. The found images are listed, and the user can change to one with "Use this image".
- The row "Found in the game folder" shows the result in a sentence. "Look again" searches again.
- A BIN, ISO or IMG file with no CUE file beside it is counted in that sentence. The game program needs a CUE disc image, so the launcher cannot use such a file. It does not write a CUE file.
- The buttons are disabled while the game runs.

## When the folder is searched

- When the user chooses the folder.
- When the user presses "Look again".
- When the launcher starts, only if a folder is saved and the saved disc image does not work (for example the image was moved). A saved image that works means no search at start.
- Never while a page is drawn.

## Rules

- The search only reads. It writes nothing into the game folder. It copies, hashes, uploads and starts nothing.
- For each CUE file: the CUE text (64 KiB limit, checked by `LocalSetup.CheckGame` as for a chosen image), then at most nine sectors of the first data track (the volume descriptor and up to eight sectors of the root directory). Reading the disc code for cover art (SPEC-covers.md) reads one more sector, the start of `SYSTEM.CNF`. The sector layout comes from the first `TRACK` line of the CUE file.
- Limits: three folder levels below the chosen folder, 4000 files and folders looked at, 200 CUE files opened, 8 images listed. When a count limit is reached, the sentence says that only part of the folder was searched. When folders exist below the third level, the sentence says that they were not searched. The folder is read one entry at a time.
- Linked folders and linked files are skipped. A chosen folder that is itself a link is refused.
- The folder must be on a drive of this computer (a drive letter). A network address and a mapped network drive are refused.
- A found image is set in one place, `UseDiscImage`. It refuses when the saved setup could not be read, checks the image like a chosen one, changes only the image of Mega Man Legends, and keeps the BIOS value and the values of Mega Man Legends 2.
- The folder is kept in `game-folder.txt` in the launcher's settings folder: the format number `1`, then the folder. The save keeps the file before as `.bak`. Uninstall with "remove settings" removes it.
- No path is written to the session log or to the state line. The log gets the event names `game-folder-searched` and `disc-image-set`.
- Test seam: the variable `FLUTTERDECK_GAMEFOLDER_FILE` moves the setting file. A portable run (`--data-dir`) ignores it, like the other overrides.

## Design points

- "my iso/dump" is read as the Mega Man Legends disc image that Play needs. Other disc images in the folder are not added to the Library. That is an open developer call.
- "detect" is read as "find and use without more steps" only when that is safe: one match and no working saved image.
- Reading a few sectors of the image goes past SPEC-local-setup.md, which allowed file metadata and bounded CUE text only. The read is small and read-only, and it is recorded in DECISIONS.md.
- The setup wizard asks for a BIOS file. A found image is saved without one. Play did not need the BIOS value before this change either (readiness checks the game program and the disc image).
- Mega Man Legends 2 is not searched for: no native game program exists for it, and nothing in this repository names its program file.

## Where the code is

- `DiscFinder.cs`: the setting, the search, the read of the image, the decision and the sentences. No window code.
- `Program.cs`: `FindDisc`, `UseDiscImage`, `ChooseGameFolder`, and the rows in `RenderRuntimeSetup`. Automation ids: `game-folder-choose`, `game-folder-search`, `game-folder-use-N`.
- `DiscFinderPureTest.cs` and `check-discfinder.ps1`: tests with made-up images, and source guards.

## Not built

- A search for other games or for movies in the folder. Several folders. A search of the whole computer.
- Use of an ISO file, or of a BIN file without a CUE file. CHD and other packed formats.
- Watching the folder for changes while the launcher is open.
