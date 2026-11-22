# Game 3: The Misadventures of Tron Bonne

Status, 2026-11-19: in the launcher source. In the launcher it is labelled features coming soon: saving, the sound and the opening movie are not yet proven.

## Background

The game side's contract is `LAUNCHER-CONTRACT-TRON.md`, version T-2, in the Tron Bonne source folder of the game project. This file says how the launcher follows it.

## What the player sees

- A third card in the Library, "The Misadventures of Tron Bonne", marked "Game · features coming soon" (until 2026-11-20 "Game · early build"; changed on the developer's ruling for version 1), with a short note of
  its limits. The setup and readiness screen for it shows the longer note (`game3-limits`).
- Setup, Saves, the key editor, Files and "Build my game..." work for it as for the other games. "Play with a
  friend...", teleport, smooth movement, third-person controls, lock-on, vibration and the analog stick are not
  offered: its program does not report them.
- Play is switched on when its program and disc image are in place, as for the other games. The game session advised
  a first play from its own start file before the launcher's Play is used; that is the developer's choice.

## How the three games are kept apart

| Item | Mega Man Legends | Mega Man Legends 2 | Tron Bonne |
|---|---|---|---|
| Game number | 1 | 2 | 3 |
| Data folder (card, keys, sessions) | `games\mml1` | `games\mml2` | `games\tron` |
| Program read from the disc | `ROCK_NEO.EXE` | `SLUS_011.40` | `TRON.EXE` |
| `game=` answer | `Mega Man Legends (USA) SLUS-00603` | `Mega Man Legends 2 (USA) SLUS-01140` | `The Misadventures of Tron Bonne (USA) SLUS-00896` |
| `program=` answer | `rock_neo_rt` | `mml2_rt` | `tron_rt` |
| Entry in `runtime.xml` and `setup.xml` | `id="1"` | `id="2"` | `id="3"` |
| Window title the overlay looks for | `rock_neo_rt live` | `mml2_rt live` | `tron_rt live` |
| Build folder and kit line | `built\mml1`, `kit=` | `built\mml2`, `kit2=` | `built\tron`, `kit3=` |
| Save file import (`.gme`) | yes | no | no |

Rules:

- **One table of games.** `GameRuntime.Game(id)`, `GameRuntime.GameCount` and `GameRuntime.IsGame(id)` are the source.
  Every place that holds one thing for each game (settings, disc images, found discs and programs, cover codes, build
  tools) has three slots and takes the slot from the game's own number.
- **No line may fold the games into two.** Before, many lines said "game 2, else game 1". Such a line would give Tron
  Bonne the settings, the card or the disc image of Mega Man Legends. `check-runtime.ps1` fails when a launcher file
  holds one of those forms. A number that is no game loads default settings and saves nothing.
- **Each slot starts only its own game.** Slots 2 and 3 need the exact `game=` line and `program=` name. Slot 1 refuses
  a program that says it is Legends 2 or Tron Bonne. A known program of another game is refused by its fingerprint
  before it is run.
- **The start line** for game 3 is exactly the one of contract T-2, section 3. Options that the Tron Bonne program
  refuses are never passed: the launcher passes an option only when the program's own feature list names it, and the
  analog pad option is for game 2 only.
- **One game at a time** across all three: a start is refused while either other game's card is in use.
- **Settings file:** saving one game keeps the other two entries. When the file is there but cannot be read, nothing
  is saved, whichever game the save is for (before, a save of game 1 wrote over such a file).
- **The builder:** as SPEC-builder.md says, with a third row. A built Tron Bonne program starts only as game 3.

## Known limits

- **An older Flutter Deck drops game 3.** A launcher built before this change that saves `runtime.xml` or `setup.xml`
  writes entries 1 and 2 only. Do not run an older copy on the same data folder after setting up Tron Bonne.
- The painted game page still shows the Legends art; only the title text changes for game 3.
- With the settings a new game slot starts with, the Tron Bonne program shows its first-start question about the
  picture. The contract marks that window as not tested for this game.
- Covers: a cover is looked up from the disc code of the Tron Bonne image like for the other games; not tried.
- The game's own limits (saving, sound, the movie, the logo screens, 4 of 62 code programs run) are in the contract.

## Tests

- `GameRuntimePureTest.cs`: three games and no other number; the exact Tron Bonne start line and the options that are
  never on it; slot checks in both directions for all three games; one settings file for three games (each save keeps
  the other two, game 3 first, unreadable file refused, numbers 0 and 4 refused and read as defaults).
- `BuildPureTest.cs` (268 checks): the third game's table row; all nine combinations of built program and game to
  start, with and without the true-place lookups; other games' markers in the Tron Bonne folder; three kits side by
  side, each refused for the other games.
- `LibraryPureTest.cs`: three built-in cards.
- `check-runtime.ps1`: the guard against two-game lines, the overlay's third title, the three counts of games.

Tried without pressing Play:

- A scratch launcher with the real Tron Bonne program and disc image: the card shows "Ready to play" (the launcher
  asked the program what it is, as it does for every game), the setup screen shows the limits, the other two cards
  are unchanged, and the settings file kept its other entry.
- The real Tron Bonne kit and the real packed Python through the launcher's build code, without a disc: missing
  `.cue` (11), output folder not empty (12), a fake disc image (20), a Legends 2 marker in the folder (12), and the
  Legends 2 kit named as the Tron Bonne kit (refused). The kit folder was unchanged. Real build output of four whole
  builds had no unknown line, and a real finished marker was accepted as Tron Bonne and refused as both Legends games.

Not tested: Play of Tron Bonne from the launcher, a real build of it through the launcher, its saves screen and key
editor with a real game, the overlay on its window, a cover for it.

## 2026-11-19 evening: built in the real launcher

The developer's disc image for game 3 was set by the launcher's own folder search. Tron Bonne was then built through the
real launcher (5.5 minutes, self-test passed); the launcher uses the built program and the card says "Ready to play".
See SPEC-builder.md, "Real builds through the launcher". Play was not pressed; nobody has played the game.
