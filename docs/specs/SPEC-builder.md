# Builder: build my game from my disc

Status, 2026-11-20: BUILT, AND REAL BUILDS HAVE RUN. Play was not pressed: nobody has played a kit-built program yet. Since kit 17 (2026-11-20) the Legends 1 kit also writes the list of teleport places, and the launcher copies it into the game's data folder when the player has none. The newer Legends 1 kits (14 to 18) were built from the disc by running the kit directly, not through the launcher. The paragraph below is the earlier status and is kept as history. Earlier status, 2026-11-19: BUILT IN SOURCE, NO REAL BUILD RUN. The pure tests pass with a stand-in for the build tools. The real builder kit and the real packed Python were run through this code on three error paths that need no disc. No game was built from a disc by this launcher, and no built game was started by it. Another session rebuilt the developer's launcher file from the shared working copy on 2026-11-19 at 09:03, so that file holds this step; the launcher project did not build or check that file. The button does not show for the developer yet: the developer's data folder has no `builder-tools.txt`.

## Background

The game side is specified by its contract BUILD-1 (`LAUNCHER-CONTRACT-BUILD.md` in the builder kit, kit version 12 and later). This document says what the launcher does with it.

## What the player does

1. Library, the Mega Man Legends card, "Build my game...". The button is there only when build tools came with this Flutter Deck.
2. The window names the chosen disc image and says whether a built game is already on this PC.
3. "Build the game". Flutter Deck asks once, then runs the kit's one command without a window. The window shows the step (1 to 5), a progress bar and the last line of the build.
4. The window can be closed; the build goes on. "Cancel the build" ends it. Closing Flutter Deck also ends it.
5. When the build is done and proven, Flutter Deck names the new program as the game program of Mega Man Legends and says so. Play then starts it like every other game program. It reports `multiplayer`, so "Play with a friend..." shows.

## Where the build tools are

- Shipped: a folder `builder` beside the launcher file, with `kit\`, `python\python.exe` and `zig\zig.exe`.
- On a developer's PC: the file `builder-tools.txt` in the data folder names the three places (`kit=`, `python=`, `zig=`, full paths). The `builder` folder wins when both exist.
- Neither: the build is not offered.
- A kit without a readable version, or older than 12, is refused. A place that is not a full path, or a file that is not named `python.exe` or `zig.exe`, is refused.
- The kit, Python and Zig are never inside the source folder. `check-builder.ps1` fails when a folder `builder` is there, because whole-folder snapshot commits would publish it.

## The command and its output

`<python> -B <kit>\build_game.py --cue <the chosen disc image> --out <data folder>\built\mml1 --zig <zig>`

- No console window, no shell. The input stream ends at once. Settings whose names start with `RMX_` or `PYTHON` are taken out of the build's environment.
- Lines that start with `@RMX ` drive the step, the progress bar, the notes and the stop code. A word this launcher does not know changes nothing. Every other line is shown as the last line and kept as "details".
- Each exit code of the contract has its own sentence. A stop also shows the kit's own reason (the line that starts `STOPPED:`).
- The launcher writes two files of its own, both beside the output folder and not in it: `mml1.lock` (held open while a build runs, so two Flutter Decks cannot build into one folder; gone afterwards) and `mml1-last-build.txt` (the text of the last build, for a report; writing stops after the line that passes 2 million characters). The text holds local paths. It is never sent anywhere.

## Before a build starts

- No game runs.
- A disc image for Mega Man Legends is chosen, exists and passes the launcher's own check of a disc image.
- The output folder is not inside a folder that a cloud service copies away (OneDrive and similar, by Windows' own settings and by folder name; the name of the Windows account itself is not looked at). the AI assistant's reading of "never leaves the player's PC". The list of names is not complete.
- About 1.5 GB is free.
- The player says yes to one question.

## The start gate

A program that a build made must not run unless the build's marker file proves it. `BuildCore.StartProblem` is asked in two places: in `PlayGame` right before the one place that starts a game (Play, the overlay's Restart, Host and Join all pass it), and in `UpdatePlay` before `GameRuntime.Readiness`, which runs the game program for a moment with `--identity` to ask what it is. The first computes the SHA-256 on every start; the second may use the launcher's remembered hash of an unchanged file.

- It applies to a program that has a marker file `rmx-kit-build.json` up to three folders above it, and to any program inside the launcher's own output folder. When more than one marker is above the program, each must prove it.
- It goes by the place the program is started from. A copy that the player makes elsewhere has no marker above it. The same holds for a path through a folder link that points below the marker (for example a link to the inner `build\game` folder); a link to the output folder itself still has the marker above it and is checked. Such a file is treated like any other game program that the player chose: the launcher asks the player to confirm an unknown build before it runs it.
- The marker must say `state` `ready` and `selftest` `passed`. The program it names must be a plain relative path inside that folder. The program's SHA-256 is computed on every start and must equal `exe_sha256`. The program being started must be exactly that file.
- The marker is untrusted data. It is read only when it is a flat list of texts and numbers; any other shape is not used.
- So a build that was killed between its two last steps (new program, old marker) does not start, and the "Use this game program build" button on the setup screen cannot accept it either: the gate does not look at the accepted hash.
- While a build runs, Mega Man Legends does not start at all. This also holds for a second Flutter Deck with the same data folder: it sees the gate file that the building one holds open. The Play button in the Library panel stays enabled, as it does for every other problem; pressing it starts nothing and shows the sentence.
- Other game programs (no marker near them) are not touched by this gate.

## After a build

- Only exit code 0 together with a proven marker names the new program. Then `runtime.xml` gets the program and its hash as the accepted hash. When the launcher could not read `runtime.xml` at its start, nothing is written and the window says so.
- "Cancelled" is said only when Cancel really ended a running build. A build that had already finished says its own result. When the build stopped by itself in the same moment, the window says cancelled and adds the build's own reason.
- A failed or cancelled build changes no setting. The kit keeps an earlier program in place until a new one passed its self-test.
- "Use the built game" names a built program again when another program was chosen in between.
- When the kit that came with Flutter Deck is newer than the one that made the program, the window says "build again".

## Hard rules of the contract, and how they are held

- The built program never leaves the PC: no code in the builder files copies, moves or sends it, and they hold no network code (`check-builder.ps1`).
- The session log gets four fixed words (`game-build-started`, `-ready`, `-stopped`, `-cancelled`). No path and no line of the build goes into it.
- The kit folder is not written to: `-B` is given, and the launcher's own two files are elsewhere.
- `--skip-selftest` is nowhere in the launcher's source.
- Cancel ends the whole process tree with Windows' `taskkill`, and does not rely on the kit's own tie between its processes.
- The launcher puts the build into a Windows job that only the launcher holds. When that worked, Windows ends the build and everything under it when the launcher ends in any way, also when it is killed. The kit's own job works inside it. When Windows refuses the job, the build still runs and the window says that a forced end of Flutter Deck would leave it running. A process that the build starts in the first instant, before the job is set, would not be in it; the kit starts none that early.
- The end of a build is not kept waiting by a process that still holds the build's output: after 5 seconds the launcher goes on.

## Uninstall

- `builder-tools.txt` is removed with the other settings.
- The output folder (about 1 GB: a copy of the disc's files, the translated code, the program) is not removed. The window says where it is and that removing Flutter Deck's settings does not remove it. Not built: an uninstall step that offers to delete it.

## Files

- `BuildCore.cs`: pure. Tools, command line, lines and exit codes, marker check, start gate, sync-folder check.
- `BuildRun.cs`: one run of the command.
- `DeckBuilder.cs`: the window and the flow. Same window class as `Program.cs`.
- Two lines in `Program.cs` (the gate before a start, and the gate before the identity question), and one line each in `DeckLibrary.cs` (the button), `build.ps1`, `Foundation.cs` (log words), `InstallCore.cs` (pointer file).
- `check-builder.ps1`, `BuildPureTest.cs`.

## Tests

- `BuildPureTest.cs` (126 checks). The test program copies itself to `python.exe` and plays the kit's command from a plan file. Covered: every launcher line and broken forms of it; a whole build's progress; exit sentences; quoting and the exact command; the flat JSON reader; the marker check with 13 markers that do not prove the program; the start gate; finding the tools; the sync-folder check; a good run (all lines, also letters outside ASCII and the error stream; exact arguments; environment stripped; input ended; text kept; gate file gone); a stopped run; a second build refused; cancel with a child process; a child that outlives the build; a second marker nearer to the program; a Python that does not start; nothing written into the kit folder.
- The real kit (version 12) and the real packed Python, through `BuildCore` and `BuildRun`, with scratch output folders: a missing disc image (code 11), an output folder that is not empty and not ours (code 12), a fake disc image without a game (code 20). The lines, the codes and the kit's reasons arrived as the contract says, the kit's own tie between its processes held (also inside the launcher's job, run again after that was added), and the kit folder was unchanged afterwards.
- By hand with the stand-in: a test program that ran a build was killed; the stand-in and its child ended within 3 seconds and the gate file was gone.
- Real output of the kit, captured by the game project (two good builds of kit 11 and 12, and stops 10, 11 and 12), was replayed once by hand through `BuildCore.Apply`: 226 launcher lines in each good build, no line of an unknown kind, the progress bar only rose and ended full, and each stop gave its code, word and reason. The real marker file of a proven build was read by `BuildCore.Flat` with all nine keys. These captures hold local paths and are not in the repository.
- The whole launcher compiles with the builder files.
- `check-builder.ps1` text guards: 38 broken versions of the code were tried in scratch clones, and each one now makes the check fail. Ten of them passed at first (one in the AI assistant's own try, three found by the first independent checker, six by the second); the guards were tightened for them. The two gate methods are now pinned as whole text, so a change to either needs a change to the check. Text guards can still miss a change that nobody thought of; they are a second line behind the tests.
- An independent checker reviewed the code on 2026-11-19 and found ten problems, none of them a way to run an unproven program from the launcher's own folder: weak guards, a settings file that could be written over after a failed read, a wait without a real limit, a marker nearer to the program, the cloud-folder names, a second launcher during a build, a wrong "cancelled", three sentences in the records, and a missing check of the disc image. All were fixed or the records corrected. A second independent checker confirmed eight fixes and found three more points (guards that could still be fooled, two inexact sentences, a failed job tie that was not shown); those were fixed too, and that last round had no further independent check.
- UI Automation on a scratch build with a separate data folder and the stand-in tools, no mouse and no keyboard (the one question was answered with a window message to the scratch copy): the button is on the Legends 1 panel; the window names the disc image and the state; a stand-in build ran with step, progress bar and last line; Play during the build started nothing; a build that ended with code 0 but without a proving marker changed no setting and said so; a good stand-in build named the program in the scratch `runtime.xml` with its hash; "Cancel the build" ended the stand-in and its child process; closing the launcher during a build ended them too and left no gate file; and with a built program whose marker does not prove it, the launcher started without running that program, "Play with a friend" was not offered, and the status line said to build again. The developer's real settings folder got no built folder and no pointer file.
- The same look again after both rounds of fixes, on a scratch build of the final source: with a copy of the developer's settings and no pointer file the launcher started without an error box, said "Ready to play" and showed no build button; with the pointer file to the stand-in the button showed, a stand-in build ran, the status line said that the game is being built, no "not tied" line appeared, and "Cancel the build" ended the stand-in and its child and said cancelled. A good stand-in build and the unproven-marker case were not repeated in the window after the fixes; the tests cover them.

## Not tested

- A real build from a disc image through the launcher, and a start of a program built that way. The first one is the developer's click.
- The window (`DeckBuilder.cs`) has no test in the check scripts that runs it. It was run by hand through UI Automation with the stand-in (see Tests), and is held by text guards.
- "Copy the details", "Use the built game", the low-space refusal and the cloud-folder refusal in the window.
- Cancel against the real kit in the middle of compiling.
- The shipped layout (`builder` beside the launcher file). Only the pointer file was used with the real tools.
- A start of a kit-built program by the launcher. The game session reported on 2026-11-19 that such a program starts with the launcher's present argument line unchanged (`--exe-from-disc ROCK_NEO.EXE --cue <the player's .cue>`; 600 frames without a window, exit code 0) and that its `--identity` reports contract 2.12 with `multiplayer`. That is the game project's test, not the launcher's.

## Not built

- Shipping: packing the kit, Python (about 10 MB) and Zig (about 100 MB zipped) with Flutter Deck, and their licence files. The game session lists open licence items for the kit.
- A build for Mega Man Legends 2.
- An uninstall step for the output folder.
- A choice of output folder.

## More than one game (2026-11-19, later)

What is built: the build step for Mega Man Legends 2 (builder kit contract BUILD-2). Everything above still holds for
Mega Man Legends; this section says what is per game.

| Item | Mega Man Legends | Mega Man Legends 2 |
|---|---|---|
| Contract | BUILD-1 | BUILD-2 |
| Output folder | `built\mml1` | `built\mml2` |
| Gate file and kept text | `built\mml1.lock`, `built\mml1-last-build.txt` | `built\mml2.lock`, `built\mml2-last-build.txt` |
| Kit folder when shipped | `builder\kit` | `builder\kit2` |
| Line in `builder-tools.txt` | `kit=` | `kit2=` (and `kit2-manifest-sha256=` to pin it) |
| Game in the marker | no `game` key, or `SLUS-00603` | `SLUS-01140`, always |
| Oldest kit version | 13 | 1 |
| Time and space said to the player | 2 to 25 minutes, up to 2 GB | 6 to 60 minutes, up to 2 GB |

Python and Zig are named once and serve every kit.

Rules:

- **The button** shows on a game's card only when that game's kit is named. A pointer file with `kit=` only offers the
  build of Mega Man Legends only. One exception: a pointer file that cannot be read at all shows the button for every
  game, with the problem said in the window. Changed for Mega Man Legends: a `builder` folder without `kit\`, or a
  pointer file without `kit=`, now shows no button (before, it showed the button with "Repair or reinstall").
- **Kit 13 for Mega Man Legends.** Kit 12 let a crafted `.cue` file make the built game open a file anywhere. The
  launcher builds with kit 13 and later only. A game that kit 12 built still starts (with the player's own disc image
  it is sound); the build window asks for a new build.
- **A kit is the kit of its game.** The game line in the kit's `expected.json` must be the game's own line. A Legends 2
  kit must have that line. A kit in the place of another game's kit is refused before anything runs.
- **The pin.** Every Legends 2 kit so far says version 1, so the version number cannot tell a good kit from an early
  one with known faults. The place that names the kit can pin the SHA-256 of its `kit-manifest.json`; a kit whose list
  differs is refused. Without a pin the launcher does not protect against an early kit. For a shipped launcher the pin
  must be in the launcher itself; that is not built.
- **The start gate keeps the games apart.** A built program starts only as the game that its marker names. The built
  programs of the later kits do not refuse another game themselves (they only fail to find their program on the disc),
  so this check is the one that matters. Every marker above the program must name the game being started.
- **Anything inside the folder `built` needs a marker.** A file there whose marker is gone, or is another game's, does
  not run, whatever game it is chosen for. A file there runs only from the folder of its own game (`built\mml2` holds
  Legends 2 only, whatever a marker in it says).
- **The true place of the program counts.** Before the checks, the launcher asks Windows for the true place of the
  program file and of the folder `built`, behind every junction, symbolic link and substituted drive. So a folder link
  into a build folder is checked as the file it leads to. A program whose true place cannot be found does not start.
  A share of this PC or a mapped drive is another name of the same folder: the launcher walks up from the program and
  compares each folder with `built` by the number of its disk and its number on that disk, so such a name does not get
  past the check. A name whose top lies below `built` (a share of a build folder) and a hard link are known by the
  file itself: the launcher compares the program with each program file in `built\<game>\build\game`. So a built
  program under any such name starts as its own game only. A copy is another file and stays the player's own choice.
- **One rule while a build runs:** no game starts while any build runs, in this Flutter Deck or in another one.
- **One build at a time** in one Flutter Deck. A build in another Flutter Deck is looked for before the question and
  again after the yes. Two Flutter Decks that pass that moment together can still build two different games at once;
  each build has its own folder, and no game starts meanwhile.
- **The game of a build is fixed when it starts.** Its folder, disc image, settings slot and sentences come from that
  game, also when the player selects another card while it runs.
- **After a build** the launcher also compares the kit's own `@RMX game` line with the game of the build. A Legends 2
  kit must print that line; the first kit prints none.

Tests added: 126 more checks in `BuildPureTest.cs` (252 in all, with the Files button): the four combinations of two built games and two
games to start, marker game values, a deleted marker, a file directly in `built`, a copied build folder, two markers
that name different games, both kits' real `expected.json` shapes, the pin, the pointer and shipped layouts, and a
stand-in build of the second game, folder links (junctions) into a build folder and to the data folder, a marker
planted deep inside a build folder, and a game built by an older kit. `check-builder.ps1` pins the new gate text.

Tried with the real Legends 2 kit and the real packed Python, without a disc: missing `.cue` (11), output folder not
empty (12), a fake disc image (20), another game's marker in the folder (12), a wrong pin, and kits named for the
wrong game. The kit folder was unchanged afterwards. Real build output that the game project captured (five whole
builds) ran through the parser with no unknown line, and a real finished marker was read: accepted as Legends 2,
refused as Legends 1.

Not tested: a real build of Legends 2 through the launcher; a start of a kit-built Legends 2 program by the launcher.

Not built: The Misadventures of Tron Bonne. The launcher knows two games only (two cards, two settings slots, two
disc image slots). A build button for a third game needs that game in the launcher first.

Independent check (2026-11-19): the first check confirmed the main rule for a program opened by its real path and
found eight points. All are fixed or written down above: a folder link got past the gate (fixed: the true place
counts); the folder name did not tie a build to its game (fixed); five changes to the source switched a rule off
unnoticed (the guard now pins them); two Flutter Decks could build at once (narrowed, see above); the `@RMX game`
check was skipped when the line was missing (fixed for Legends 2); the reader of `expected.json` read inner objects
(fixed: top level only); two mismatches between this file and the code (written down above).

Known limit of the kit file check: the kit comes with the launcher and is trusted as far as its own files go. A kit
that is changed on purpose can write any marker; the pin of `kit-manifest.json` is what notices a changed kit.

## The Files button (2026-11-19)

- **Where:** "Files" on a game's card in the Library, and the folder icon on the game screen. Before, both only said
  "Game files is not connected yet."
- **What it does:** it shows the build folder of that game (`built\mml1` or `built\mml2` in the data folder) in the
  file explorer of Windows. That folder holds the marker, the game program (`build\game`), the translated code
  (`recomp-out`), the copy of the disc's files (`disc`) and the compiler's cache.
- **When there is no such folder:** the button says that Flutter Deck has not built this game on this PC, points to
  "Build my game..." when the kit is there, and opens nothing. A game program that the player chose by hand has no
  folder here.
- **Rules:** only this one folder is ever opened, and only when it exists and its marker is this game's. No path from
  the marker is used. The folder's name has no extension, so Windows cannot run a file that takes its place. Nothing
  is copied or sent. The session log gets the fixed word `game-files-opened`.
- **Files:** `GameFilesOpen.cs` (the one place that asks Windows to show a folder), `BuildCore.FilesFolder`,
  `OpenGameFiles` in `DeckBuilder.cs`.
- **Tested:** seven checks in `BuildPureTest.cs`. In a scratch launcher: without a built game the
  message shows; with a stand-in build folder a file explorer window opened at that folder.
- **Not tested:** the button with a real built game.

Second independent check (2026-11-19): three more points, all fixed. A share path of this PC got past the gate
(fixed: folders are compared by their numbers on the disk, see above). Four changes to the source switched a rule off
unnoticed (fixed: `check-builder.ps1` now pins the whole text of the methods that decide, by a SHA-256 of each). The
sentence that a trailing backslash makes a path "a folder only" was wrong (fixed: the rule is the name without an
extension). The fixes of this second round had no further independent check.

Known limits: a file that is not a program in `build\game` (for example one in the compiler's cache), reached by a
share whose top lies below `built`, is not seen as inside `built`. A folder link put in the place of `built\mml1` by someone who can write the data folder makes the Files
button show the folder it leads to. `DeckBuilder.cs` is not compiled by `check-builder.ps1`; the full build compiles
it, and its deciding methods are pinned as text.

## Real builds through the launcher, 2026-11-19 evening

Run in the developer's real Flutter Deck (launcher file of 20:07), driven by UI Automation and window messages, without
mouse or keyboard. Play was never pressed. Before each build the card, the build window and the question were
compared with the game's name.

| Game | Kit | Time | Result |
|---|---|---|---|
| Mega Man Legends | 13 (replaced the kit 12 build of the afternoon) | 1.5 minutes | ready, self-test passed, 0.89 GB |
| Mega Man Legends 2 | 1 (pinned) | 7.3 minutes | ready, self-test passed, 1.27 GB |
| The Misadventures of Tron Bonne | 1 (pinned) | 5.5 minutes | ready, self-test passed, 0.84 GB |

After each build the launcher named the built program for that game, and the card said "Ready to play" (so the
launcher also asked each built program what it is, and each answered as its own game). Checked afterwards with the
launcher's own code: each marker is ready and names its game; each built program passes the start gate as its own
game and is refused as the two others; the three kit folders are unchanged. "Files" on each card opened that game's
build folder in the file explorer.

Still not done by anyone: Play of a kit-built program from the launcher, and playing any of the three kit-built
programs.
