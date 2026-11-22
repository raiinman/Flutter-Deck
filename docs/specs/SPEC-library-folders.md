# Library folders: fill the Library from a folder

Status, 2026-11-18: GAMES ONLY. The launcher now passes `gamesOnly` to the search: it lists disc images and sets game programs, and it lists no movie, show or album. The tabs are as before ("Games", "Movies", "Other", "Favorites"; no "Art" tab). The parts of this document about movies, shows, art and their covers describe code that is in the source and tested, but switched off. Built in source and in scratch builds. Not in the developer's launcher file yet. The pure tests pass; the screens were driven once through UI Automation with a made-up media folder. Nobody has used it with a real media folder through the screens.

## What the user does

1. Library, "+ Add", "Add a folder...". The user chooses a folder once. Up to 8 folders; one may not lie inside another.
2. Flutter Deck searches the folder on a background thread and fills the Library. It searches again at each start, and when the user presses "Look again".
3. "Stop using" takes a folder off the list after a question. Its items leave the Library. No file is changed.

## What the search finds

- **The two built-in games.** A `.cue` disc image that holds `ROCK_NEO.EXE` is Mega Man Legends; one that holds `SLUS_011.40` is Mega Man Legends 2. It becomes that game's disc image only when the game's saved image does not work, exactly one such image was found, and the search was not cut short.
- **Game programs.** A file named `rock_neo_rt*.exe` or `mml2_rt*.exe` whose SHA-256 is a known build. It becomes that game's program only when it is the only known one found for that game and the game has no program setting, or its file is gone. With several known Legends 2 programs, the one this build names is preferred. An acknowledged build is never changed, and nothing is acknowledged by a search.
- **Other disc images.** Any other `.cue` is listed in Games as "Game · disc image". Flutter Deck cannot start it.
- **Movies.** A video file (the extensions of SPEC-library.md). It opens like a movie the user added: Windows picks the program.
- **Shows.** A video whose name looks like an episode (`S01E02`, `1x02`, `Episode 3`) belongs to a show. The show is the folder the episode is in, or that folder's parent when the folder is a season folder. The library folder itself is never a show. A show is one item in "Movies & TV"; its panel lists the episodes, and each opens like a movie.
- **Art.** A folder with at least two pictures, no video and no disc image, in which pictures are most of the files, is one album in the "Art" tab. Its panel shows up to 48 small pictures inside the launcher; choosing one shows it larger. No other program is started for a picture.
- **Covers.** A picture with the same name as a movie or disc image is its cover. A picture named cover, poster, folder, front or boxart is the cover of a show, or of the only movie or disc image in its folder.

## Limits and refusals

- 8 folder levels, 60,000 entries, 400 disc images, 12 program files to hash, 5,000 found items, 300 episodes or pictures listed for one item. When a limit stops the search, the sentence on the screen says so.
- Not searched: a network drive, a linked folder, hidden and system folders, folders whose name starts with a dot, `node_modules`, `__pycache__`.
- The search only reads. It never writes, moves, renames or deletes anything in the folder, and it starts no program.

## What is kept

- `library-folders.txt`: the format number 1, then one folder on each line.
- `library-found.txt`: the format number 1, then kind, title, file or folder, cover and library folder on each line, with tabs between. Found items are never written to `library.txt`, whose format is unchanged.
- A search with a problem (the folder is gone, the drive is not connected) changes nothing. A complete search replaces the items of its folder. A search that the entry limit cut short only adds.
- A found item cannot be removed from the Library; it can be hidden. Favourites, hidden items and "last opened" work as for other items (`library-view.txt`).
- Uninstall removes both files with the other settings.

## Rules that the checks hold

- `MediaFolders.cs` has no program start, no network and no write other than its two settings files (`check-folders.ps1`).
- A search never changes the chosen game, never acknowledges a build, and sets a disc image or a game program only for the game slot it names, and not while a game runs.
- A movie or an episode is opened from the one place as before (`LibraryPrimary`, after the file check; `check-library.ps1`).

## Not built

- Covers by disc code for other disc images (only the two built-in games get a downloaded cover).
- Disc images without a `.cue` file.
- Watching the folder while the launcher is open (it looks again at start and on "Look again").
- "Open file location".

## Tests

- `MediaFoldersPureTest.cs` (58 checks): a scratch tree with games, programs, movies, a show with seasons, a flat show, an album, tool folders; covers; 50 movies stay 50 movies; episode names; problems change nothing; complete and cut-short searches; the two files; lines that are refused.
- Read-only run on the developer's own game folder, counts and time only: 4,681 entries in about a quarter of a second, complete, both Legends disc images found.
- UI Automation run of a scratch build with a made-up media folder: tab counts, cards with covers, the panels of a found game, movie, show and album, the larger picture, the folder list. The media folder was unchanged afterwards and `library.txt` was not written.
- Not tested: the folder picker; "Stop using"; "Look again" pressed on screen; opening a movie or an episode (Open is never pressed in a test); a real media folder with thousands of files on screen; a drive that is unplugged; automatic setting of a real disc image or game program through the screens.
