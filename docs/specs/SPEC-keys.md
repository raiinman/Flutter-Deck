# Keyboard key map editor

Status, 2026-11-16: built. The key map unit is tested; the editor was driven once in a muted test copy through UI Automation without saving. Pressing a real key ("Press a key") and the Save button have not been exercised in the launcher.

## Developer request (2026-11-16)
The developer agreed to a key remapping screen as the next launcher feature, and that a later in-game overlay will reuse it.

## What the user gets
On the game setup and readiness screen, under "Keyboard keys": one row for each of the game's fourteen buttons (Up, Down, Left, Right, Cross, Circle, Square, Triangle, L1, R1, L2, R2, Start, Select).

- Each row has a list of every key name the game accepts, and a "Press a key" button. After "Press a key", the next key pressed becomes that button's key. Esc cancels.
- If the chosen key is already on another button, the two buttons swap keys, and the status line names every button that changed. A swap gives the other button the key this button had. Only when this button had no key at all (possible in a hand-written file) is the other button left without one; it is then listed as missing and Save stays off.
- Nothing is written until "Save keys". "Discard key changes" reloads the saved map. "Use the default keys" shows the defaults; they also need Save.
- Save is off while any button has no key.
- Saved keys apply the next time Play is pressed. The game reads its key map only when it starts.
- A gamepad is not changed here. The game fixes its own gamepad layout.

## Rules taken from the game side's launcher contract (section 4)
- File format: one line per button, `BUTTON = KEY [, KEY ...]`, up to four keys, `#` starts a comment.
- A map file replaces the game's defaults entirely, so all fourteen buttons are always written.
- The game keeps Tab, F11 and F12 for itself. The editor refuses them, by name and by code.
- A key can be written as a virtual-key code, `0xNN`.

## Choices made here
- **Pressed keys.** Letters, digits, numpad digits, arrows and the named control keys are written by name. Any other pressed key (punctuation, function keys other than F11 and F12) is written as its `0xNN` code. Reason: the contract lists punctuation names but not which physical key each one means, and a code is exact. The list still offers the contract's punctuation names for anyone who wants them.
- **One key per button in the editor.** Setting a key replaces all keys of that button. Extra keys on buttons the user does not touch are kept.
- **Existing file.** The file is read as it is. Lines the editor does not understand are counted and reported; saving writes only the fourteen button lines. The previous file is kept beside the map with `.bak` added.
- **Written shape.** One leading comment line, fourteen lines, CRLF, no byte-order mark: the shape the game side tested.
- **First Play.** `GameRuntime.EnsureKeys` still writes the default map when none exists. It is unchanged.
- **Key repeats are ignored** while waiting for a key, so the Enter that pressed "Press a key" is not taken as the key. Waiting ends without a change when the window loses focus.
- **One identity per key.** A code that has a plain name is written as the name (0x5A becomes Z). Keys are compared by their code, so a key chosen by name and the same key pressed count as one key. For the punctuation names this comparison assumes the usual codes of a US keyboard, because the contract does not state them; it is used only to swap and to warn.
- **Unsaved edits stay while the launcher is open.** Leaving the screen and coming back shows the same unsaved edits and the last status line. A change made to the file by something else in the meantime is not noticed until Discard or a restart.
- **Saving while the game runs is allowed**, because the game reads the map only when it starts (stated by the game side's contract, not tested here).
- **On a closed list, the arrow keys and typed letters change the key at once**, as any Windows list does. Nothing is written until Save.
- Known limits of "Press a key", from code reading: AltGr on some keyboards arrives as Left Ctrl first and is recorded as Left Ctrl; use the list for such keys.

## The overlay's editor
The in-game overlay has its own panel for the same fourteen buttons (SPEC-overlay.md). It edits this editor's draft through callbacks and saves through the same `SaveKeyDraft`, so there is still one draft and one save path.

## Structure and command
- `KeyMap.cs`: parse, edit, format, load and save. No window code, so the overlay can share it.
- `Program.cs`: `RenderKeyEditor`, `CaptureKey`, `SetKey`, `LoadKeyDraft`. The map is saved from exactly one place.
- `KeyMapPureTest.cs` and `check-keys.ps1`. Command: `powershell -NoProfile -File .\check-keys.ps1`

## Verification
- Verified by test: 43 checks of the key map unit (contract parsing, refused keys, swap rules, written shape, backup on save, refusal to write a map with a missing key). The defaults equal the ones `GameRuntime` writes.
- Observed once in a muted test copy, through UI Automation, with no Save pressed: fourteen lists and fourteen "Press a key" buttons; Cross changed to K; Cross changed to Z swapped with Square; "Press a key" entered its waiting state; defaults; discard. (In the run after the review fixes the waiting state ended by itself before the script cancelled it, which fits the new rule that waiting ends when the window loses focus; that rule was not tested on its own.) The settings folder was byte-identical before and after. One capture of the rows was taken.
- Reported by the game-side session on 2026-11-16, from its reading of the game source (not tested here): the punctuation names are the usual US virtual-key codes this editor assumes (COMMA 0xBC, PERIOD 0xBE, SLASH 0xBF, SEMICOLON 0xBA, QUOTE 0xDE, LBRACKET 0xDB, RBRACKET 0xDD, BACKSLASH 0xDC, MINUS 0xBD, EQUALS 0xBB); a `0xNN` code from 1 to 255 is accepted with no special cases; the written file shape is fine; Alt+Enter is also reserved for full screen. The game acts on keys only while its own window is in front.
- NOT verified: pressing a real key in the launcher (needs real keyboard input); the Save button in the launcher (the write itself is covered by the unit test); that the game accepts every code the editor can write; how the rows look at other text sizes.
