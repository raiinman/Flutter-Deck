# Gamepad buttons (Mega Man Legends)

## What the player sees

- On the Settings screen, for Mega Man Legends, the row **Gamepad buttons** has the button **Edit gamepad buttons**. It opens the screen **Gamepad buttons: Mega Man Legends** (screen id `Gamepad`).
- That screen has one line for each of the 16 pad buttons. Each line names the Xbox button first and the PlayStation 5 button in the same place second, for example "A (Xbox) / Cross (PlayStation)". Its list offers:
  - **Default**, which keeps what the game does without changes (the list shows what that is);
  - one of the game's 14 buttons;
  - **Camera behind Mega Man** or **Aim, hold** (these work with the third-person controls only);
  - **Nothing**.
- **Stick dead zone** is a slider from 5 to 50 percent of the stick's travel, 20 by default. It is used by the third-person controls only. The original controls turn the stick into the direction buttons with their own fixed limit.
- Warnings:
  - a game button that the player's changes took off the pad (the keyboard keys still give it);
  - an action that more than one pad button gives;
  - lines in the saved file that the game does not understand;
  - camera lines in the file (the Settings screen's camera speed and invert win over them).
- The buttons are **Save gamepad buttons**, **Discard gamepad changes**, **Use the default buttons** (also the default dead zone) and **Back to Settings**. Changes stay on screen until Save. They apply the next time the game starts: Play, or Restart in the in-game menu. The in-game menu does not show "Restart to apply" for a gamepad change, because the start line of the game does not change.
- For Mega Man Legends 2 and The Misadventures of Tron Bonne, the row says that pads work with their usual buttons and that changing them is coming soon. Their programs have no `--pad-map` option. For Mega Man Legends, when the game program is not set up or does not report `pad-map`, the row says so and has no button. The row does not depend on the third-person controls being reported.

## The file

- `games\mml1\pad-map.txt` in the data folder. The game reads it with `--pad-map` (contract 2.8, `rtc_padmap.h` in the Legends 1 kit).
- `PadMap.cs` reads it exactly as the game's loop in `rtc_move_init` and its `padmap_line` do:
  - text mode: a Ctrl-Z byte ends the file, and CR LF becomes LF;
  - `fgets` into 1024 bytes: a line with a zero byte is cut there, and a line with no newline before the end of the file is refused whole and the rest up to the next newline is skipped;
  - for the same button the last line wins;
  - an empty value or a trailing `+` is a mistake;
  - `#` starts a comment anywhere on a line;
  - PlayStation names are accepted on the left of `=`;
  - names are not case sensitive (ASCII only);
  - a line of 512 bytes or more is refused whole;
  - a byte-order mark is not removed, so a first line that starts with one is not understood.
- A button the file does not name keeps the game's default, and that default depends on the third-person controls: with them on, the left trigger aims; with them off, it gives L2. So the editor writes **only the buttons the player changed**. A changed dead zone is written too, and any `look_sensitivity` and `look_invert_y` lines are kept as they were.
- Writing goes through a temporary file. The file it replaces is kept beside it as `pad-map.txt.bak`, as the key map does. The first time the editor replaces a file it did not write (the template, or the player's own), that file is also kept as `pad-map.txt.before-editor`, and later saves never replace it, so hand-written comments and lines survive. It is written without a byte-order mark, with Windows line ends. `SavePadDraft` in `Program.cs` is the only place that writes it.
- The launcher still creates the commented template (`GameRuntime.EnsurePadMap`) when no file exists. The template changes nothing.

## Checks

`check-padmap.ps1`:
- runs the unit tests in `PadMapPureTest.cs`;
- compiles a small C harness around the kit's own `rtc_padmap.h` with the bundled Zig. The harness reads files the way `rtc_move_init` does: `fopen "r"`, `fgets` into 1024 bytes, a line that is too long refused whole;
- runs that harness and `PadMap.cs` over 454 files (hand cases, among them zero bytes, Ctrl-Z, CR LF at the length limit and UTF-16 files found by an independent check, and 400 generated ones with rare bytes sprinkled in), over both default maps, and requires the same 16 actions, camera values, dead zone and count of lines not understood;
- needs an installed Flutter Deck for the kit and Zig, or `FD_PADMAP_KIT` and `FD_ZIG`.

Not tested: a real game session with a changed map, and any real PlayStation 5 pad (see the controller notes in the game contract).
