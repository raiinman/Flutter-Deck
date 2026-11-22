# Memory card backups and restore

Status, 2026-11-16: built. The backup unit is tested in temporary folders; the Saves screen was read once in a muted test copy without pressing any of its buttons. Back up, Add and Restore have not been pressed in the launcher.

## Developer request (2026-11-16)
That is the whole approval; the rules below are the launcher project's design.

## What changed in principle
Until now the launcher never replaced its memory card. **Restore is the one exception**, under the rules in "Restore rules". Everything else is still copy-only, and nothing is ever deleted.

## What the user gets (Saves screen, Mega Man Legends 1)
- The card's state: present with its last-written time, not created yet, or damaged (wrong size).
- **Back up now**: a verified copy of the card in the launcher's backups folder. Off while a game runs.
- **Add a card file as a backup**: copies a raw card file the user names (exactly 131,072 bytes) into the backups. The user's file is only read. It becomes the card only through Restore.
- **Backups, newest first**: the launcher's backups, and the "card as it was when a game session began" copies the game leaves in the sessions folder. Each has a Restore button. A backup equal to the current card is marked and cannot be restored (it would change nothing).
- The feature proposals for saves still follow below the card section.

## Restore rules
1. The source must be a whole card file directly inside the launcher's backups folder, or a card-at-start file directly inside its sessions folder. No other path is accepted, and no link.
2. It runs inside the same per-card gate as starting the game, and refuses if a game uses the card, if the chosen game program is running at all (also one started outside the launcher), or if a start is in progress. The buttons are also off while a game runs.
3. The current card is copied to the backups first ("card kept before a restore") and the copy is verified. If that copy cannot be made, nothing is restored. A damaged current card is kept the same way.
4. The user confirms in a dialog. Restore has exactly one caller, behind that dialog.
5. A `.gme` save still waiting for the first Play (no card exists yet) blocks restore, because the restored card would silently cancel that import. Once a card exists, a leftover `.gme` blocks nothing.
6. The restored backup itself stays in the list.
7. The card is replaced in one step (a rename over it), so there is no moment without a card and no extra file appears beside it. If another program holds the card for a moment, the step is retried for about two seconds.

## Choices made here
- **Raw cards only for Add.** A `.gme` save is a different format; it is not converted here. A `.gme` can still be copied in before the first Play on the setup and readiness screen, as before.
- **No delete button.** Backups are 128 KB each. Removing them is left to the user.
- **Temporary files** end in `part` with a unique name. The card's name with a `tmp` ending is never used, because the game uses that name for unsaved progress. A part file left by a cut-off restore is removed by the next restore once it is an hour old; this is the only thing the unit deletes.
- **A backup is dated when it was made**, not when the card was last written, so new backups and safety copies are at the top of the list.
- **Known leftovers:** a restore that fails at the last step still leaves its safety copy in the list. "Backups and version history" still appears among the proposals below the card section, because version history is not built.
- **The list shows the newest 60.** Older files stay on disk.
- The Saves screen is still a feature-catalog screen in `DeckState`; the card section is drawn above the catalog.

## Structure and command
- `SaveCards.cs`: card state, backup, add, list, restore. No window code.
- `Program.cs`: `RenderSaveCards`, `RestoreCard`.
- `SaveCardsPureTest.cs` and `check-saves.ps1`. Command: `powershell -NoProfile -File .\check-saves.ps1`

## Verification
- Verified by test, in temporary folders: 47 checks. Exact copies; no backup replaced; fifteen kinds of wrong restore source refused, plus a backup reached through a linked folder; restore keeps the old card first; refused while a lock file names a running process, while the game program itself runs, and while another thread holds the gate, with no copy made; a damaged card kept and repaired; nothing restored when the safety copy cannot be made; no temporary file left; during 40 restores with a second thread watching, the card was never missing and no file with a tmp ending was created; old part files cleared.
- Guarded by source text only, not by a behaviour test: the hash comparisons after each copy (a test cannot make a copy go wrong), and the rule that the unit never writes the game's own temporary name.
- An independent review on 2026-11-16 found 7 problems (the card briefly missing during a restore, a leftover `.gme` blocking restore for good, no check for a game started outside the launcher, backups dated with the card's old time, test gaps, stale wording, small leftovers). They are fixed as described above; the fixes were not reviewed a second time.
- Observed once in a muted test copy, read through UI Automation, with no button of the screen pressed: the card reported as present with its last-written time, "Back up now" and "Add a card file as a backup" enabled, one card-at-start backup listed with its Restore button off because it equals the current card. The settings folder was byte-identical before and after.
- NOT verified: pressing Back up, Add or Restore in the launcher; the confirmation dialog; the screen while a game runs; that the game accepts a restored card (it is a byte copy of a card the game wrote); how the rows look at other text sizes.
