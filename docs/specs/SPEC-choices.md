# Remembered choices

Status, 2026-11-17: built. The settings unit is tested in temporary folders, and a scratch copy of the launcher was driven through UI Automation with a test settings file. Nobody has used it with the real settings file yet.

## Developer request (2026-11-17)
"make so the launcher remembers my choices so i don't have to keep going back and stoping the music or change a setting i already changed"

The developer named the music.

## What is remembered now
Stored in `choices.txt` in the launcher's settings folder (`%LOCALAPPDATA%\FlutterDeck`):
- **Music on or off.** If the user paused or stopped the music, the launcher starts silent next time. If music was playing, it starts playing.
- **Volume, repeat and shuffle.** (Mute: the button was removed on 2026-11-18. The `muted` line stays in the file format, is always written as 0, and is ignored when read by the launcher.)
- **The track**, when it is one of the bundled loops and the user picked it (Next, Previous or the playlist). A track the playlist moved on to by itself is not remembered.
- **The scaling engine and GDI filter** on the Scaling screen.

Already remembered before this change, each in its own file: window width, always-on-top and the selected game (`window.txt`); text size (`text-size.txt`); the game program, game window size, game volume, game sound off and "play the original picture" (`runtime.xml`); the automatic update check (`updates.txt`); the setup references (`setup.xml`); the key map (`keys.ini`).

## What is deliberately not remembered
- **WAV files the user added to the playlist.** The playlist stays session-only, as before: the launcher does not store the name or location of the user's files. If such a track is selected at closing, the last bundled track is the one remembered.
- **The preview options on the feature screens.** They are not real settings, and saving them would make them look real.
- **The screen that was open.** The launcher starts on Home.
- **The scaling source window and output display.** Windows and displays change between runs.
- **The playback position.**

## Rules
1. "Music on" is what the user last asked for with Play, Pause or Stop. It is not read from the player. The pause for a running game, a track that fails to open, the end of the playlist and closing the launcher all stop the player without the user asking, and none of them changes the choice.
2. The remembered values are applied before any track is opened, so nothing is heard at the wrong volume or while muted.
3. A choice is saved about 0.4 seconds after it changes, and again at closing. A dragged volume slider is saved once, when it rests. Saving does not depend on the window closing normally.
4. Starting the launcher and changing nothing writes nothing. A first run creates no file.
5. A missing, oversized (over 1,024 bytes), unreadable or unknown-version file gives the defaults. A single bad value gives that value's default. Unknown lines are ignored.
6. Defaults: music on, volume 20%, not muted, repeat on, shuffle off, first track, DWM engine, Integer filter. These are the first-run behaviour the developer chose on 2026-11-16.
7. The file is written to a uniquely named temporary file and then put in place; the earlier file is kept as `choices.txt.bak`.
8. "Reset launcher settings" (Help screen) puts these choices back to the defaults at once. It does not start or stop the music that is playing and does not change the track that is playing; the first track is the one remembered for the next start.
9. Removing settings at uninstall removes `choices.txt` and its backup.
10. If saving fails, the choice still applies for the session and the session log records `preferences-save-failed`. Known limit, shared with the other settings files: a `choices.txt` or backup marked read-only makes every save fail.
11. A save reads the file again and writes only the values this copy of the launcher changed since it last loaded or saved. With two launchers open, one cannot undo a choice made in the other that it did not touch. If both change the same value, the last save wins.
12. A file that another program holds for a moment is tried three times (about 0.1 seconds) before the defaults are used. A start that fell back to the defaults does not write them over the file: only values changed afterwards are written.

## File format
Text, CRLF, no byte order mark. First line `1` (version), then `key=value` lines: `music`, `volume` (0 to 100), `muted`, `repeat`, `shuffle` (0 or 1), `track` (a bundled title, at most 80 characters), `scaling-engine` (0 or 1), `scaling-filter` (0 to 3).

## Test-only location
If the environment variable `FLUTTERDECK_CHOICES_FILE` holds a full path on a local drive (drive letter, colon, backslash) to a `.txt` file, that file is used instead. Network paths and paths relative to a drive or folder are refused. It exists so a test copy of the launcher can be driven without writing to the user's real settings. Any other value is ignored. It moves this one file only.

## Structure and command
- `Foundation.cs`: `DeckChoices` (load, save, format). No window code.
- `Program.cs`: `SyncChoices`, `NoteChoices`, `FlushChoices`; the load beside the text-size load; the Play, Stop, Shuffle and Repeat handlers; `RenderScaling`; `ResetLauncherPreferences`.
- Tests in `check-foundation.ps1`. Command: `powershell -NoProfile -File .\check-foundation.ps1`

## Verification
- Verified by test, in temporary folders: defaults and no file created by loading; round trip; exact file text and no byte order mark; volume zero kept; backup kept; no temporary file left; out-of-range values; a track title that tries to add a line; hand-made, partial, empty, unknown-version, corrupt, oversized and binary files; a folder in place of the file; a failed save; a file held for a moment and one held throughout; the merge of two launchers' changes; the test-only location, with nine refused forms.
- An independent review on 2026-11-17 found 6 problems (two open launchers overwriting each other's choices; the file rewritten at every track change when Repeat is off; a held file read as defaults and later written over; the test-only variable accepting drive-relative and network paths; Reset not resetting the track; read-only files). The first five are fixed as described in the rules; the read-only case is recorded as a known limit. The fixes were not reviewed a second time.
- Guarded by source text only: where "music on" is set (Play or Pause, the two Stop buttons, Reset), one save call, the load order at start, the uninstall list.
- Observed on 2026-11-17, on the build with the review fixes, in a scratch copy of the launcher with a test settings file, read and driven through UI Automation, each run ended by ending the process (not by closing the window). The file was muted throughout, so no sound was made:
  - started with music off, muted, volume 7: launcher showed stopped, muted, slider at 7, and did not rewrite the file;
  - Repeat off, Shuffle on, Next track and volume 33 were in the file within about a second and were shown again after a restart;
  - Play was saved as music on and the next start was playing; Stop was saved as music off and the next start was stopped; Play then Pause was saved as music off;
  - a remembered track (Skyward Circuit) was the one playing after a restart;
  - scaling engine GDI and filter Bicubic were saved and shown again after a restart;
  - The developer's real settings folder was byte-identical before and after, and no real `choices.txt` was created.
- NOT verified: anything with the real settings file; the result by ear (that a remembered volume sounds right from the first moment); the choice surviving a real game session (music pauses for the game: code reading and source guards only); Reset launcher settings pressed in the launcher (not pressed in the scratch copy either, because it also writes the real window and text-size files); closing the window normally; two copies of the launcher open at once (the last one to save wins).
