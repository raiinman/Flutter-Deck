# SPEC: cover art by disc code

## The rule that changed

- Before: no downloaded art and no newly bundled art.
- Now: downloaded art is allowed. The developer removed that part in the words above.
- Unchanged: no cover picture is bundled. No picture is in the program file, the source, the repository or a release. The news rule "never fetch remote images" (SPEC-news.md) is a separate rule and is unchanged.

## What the user sees

- The Library card of a built-in game shows the cover of the user's own disc image, when the launcher has one. The whole cover is shown, not cut to the card shape, so a dark band can show above and below it.
- Without a cover the card shows the plain game symbol.
- The "Game setup and readiness" screen has a switch "Download cover art" (off at first) and, when it is on, a row "Cover for your disc image" with a sentence and the button "Look for covers again".
- With the switch off no download is started and the cards show the plain game symbol. A download that is already in progress when the switch is turned off (at most 16 seconds) can still finish. Pictures already kept stay on the disk and are used again when the switch is turned on.

## How it works

- The disc code comes from the saved CUE disc image of the game: the `BOOT` line of the disc's `SYSTEM.CNF` file, for example `SLUS_006.03` gives `SLUS-00603` (`DiscFinder.DiscCode`). One more sector of the image is read for this.
- The picture comes from one fixed address: `https://raw.githubusercontent.com/xlenore/psx-covers/main/covers/default/<code>.jpg`.
- The launcher looks for a cover when it starts, when a disc image is set from the game folder or saved in the setup wizard, when the switch is turned on, and when "Look for covers again" is pressed. One lookup runs at a time; a request made during a lookup runs when that lookup ends. A picture that is already kept is not downloaded again.
- The picture is kept as `covers\<code>.jpg` in the launcher's data folder. A portable run (`--data-dir`) keeps it in that folder.
- When the collection has no picture for a code, an empty file `covers\<code>.none` remembers that, so the launcher does not ask again at every start. "Look for covers again" forgets it and asks once more.
- A failed download (no connection, a refused answer) is not remembered. The next start tries again.

## Rules

- Only the disc code is sent, as part of the address. No account, name, path or file is sent. The client name is `FlutterDeck-Covers/1`.
- The code must match four capital letters, a hyphen and five digits, with nothing after it, before an address or a file name is made from it.
- One request site, HTTPS only, one fixed host. No redirect is followed. No setting, variable or argument can change the address.
- The answer must have status 200, be at most 3 MiB, and start like a JPEG picture. Anything else keeps nothing.
- The download runs on a background thread. The window does not wait for it.
- Uninstall with "remove settings" removes `covers.txt`, and the files in `covers` that are named by a disc code.
- No path and no disc code is written to the session log. The log gets the event name `covers-checked`.

## Design points

- The collection is `xlenore/psx-covers` on GitHub. A lookup on 2026-11-18 found that it serves a JPEG file for `SLUS-00603` and for `SLUS-01140`. Its page states no licence. The covers are the publishers' art. The launcher fetches one for the user's own disc and keeps it on the user's computer only; it does not pass any on.
- The switch is off at first (developer, 2026-11-18). Nothing is sent until the user switches it on. A missing setting file, or one that is not understood, also means off.
- Only the two built-in games can have a cover, because only they have a disc image. Items the user adds to the Library have no disc image and keep their symbol.
- The whole cover is shown (letterboxed) instead of being cut to the tall card.

## Where the code is

- `CoverArt.cs`: the code pattern, the address, the download, the kept files and the switch. No window code.
- `DiscFinder.cs`: `DiscCode` and `CodeFromSystemCnf`.
- `Program.cs`: `FetchCovers` (the one download place), `CoverNote`, `CoverImage`, a first line in `LibraryCover`, and two rows in `RenderRuntimeSetup`. Automation ids: `covers-download`, `covers-again`.
- `CoverArtPureTest.cs` and `check-covers.ps1`: tests with no network, and source guards.

## Not built

- Covers for items the user adds. A picture the user chooses from a file. The 3D cover set of the collection.
- The picture is decoded on the window thread when the Library is drawn; a picture of 3 MiB with very large dimensions could pause the window for a moment. A second collection when the first has no picture. A check that the picture shows the right game.
- Removing kept pictures from inside the launcher.
