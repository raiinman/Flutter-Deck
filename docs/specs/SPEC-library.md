# SPEC: Library list

## What the user sees

an AI assistant operated the developer's launcher of that name with the developer's leave (hover, right click, one item panel, the filter pane, the details view; nothing was started, installed, hidden or removed). The Library follows how that one behaves and is arranged. It copies no art, icon, colour or wording set from it, and the ship frame stays.

1. Cards are the cover only. A card with no cover (an added item) also shows its title, so that such cards do not all look the same. A small heart marks a favourite.
2. Pointing at a card, or moving the keyboard focus into it, shows three controls on it: a heart (favourite), a "more" button, and the main action (Play, Open or Details). The title is the tooltip.
3. A click, a right click and the menu key (on the grid and on the list) all open the item's card, which has every action the menu had (Play or Open, favourite, Hide or Unhide, Remove from library) and more. Pointing at a card shows the heart and the main action.
4. A click on a card opens the item's panel over the grid, and the grid behind it is dimmed. The panel has the picture, the title, a line about the item, the main button, the heart, Hide, and for Mega Man Legends also Setup, Saves, Files and "Picture preset...". Below are Activity (last opened, times opened) and Details (kind, disc code, file). The close button, Escape and a click beside the panel close it.
5. Tabs across the top: Games, Movies, Other, Favorites, each with its count. Beside them: the search box, the view button, "Filter and sort" and "+ Add".
6. "Filter and sort" opens a pane at the right: the order (As added, Alphabetical A-Z, Alphabetical Z-A, Recently opened) and "Show hidden items". Clear puts both back.
7. The view button changes between the grid and the details view. The details view has the titles at the left and the chosen item at the right, with the same content as the panel.
8. A hidden item is not shown and not counted. While a tab has hidden items, a button "Show hidden items (N)" is under its cards, so a hidden item can always be found again.
9. "+ Add" opens the same short form as before.
10. The order, the view, the favourites, the hidden items and "last opened" are remembered. The search, the tab and "Show hidden items" are not.

The earlier game page (the painted picture with the round Play button) is no longer shown. Its controls are in the panel.

## Play from the Library

- Every Play control of the Library (the card's main action, the menu, the panel) goes through one method, `PressPlay`. It selects Mega Man Legends, lets `UpdatePlay` work out readiness, and then presses the one Play button of `Program.cs`, and only while that button is enabled.
- When the button is not enabled, nothing is pressed. The panel opens and shows the reason.
- The Library never changes whether Play is enabled, and it does not call `PlayGame`. `check-library.ps1` checks this file, because the other checks read `Program.cs` only.
- "Last played from here" and "Times started from here" count presses of an enabled Play in the Library. They do not measure play time.

## The saved view

- `library-view.txt` in the settings folder: the format number `1`, then `sort=`, `view=`, and one line for each favourite, hidden item and opened item. `library.txt` and its format are unchanged.
- A file that is too large or of another format is not replaced: the launcher then keeps the view for this run only.
- Uninstall with "remove settings" removes it.
## Kinds

| Kind | What is kept | What the launcher does |
| --- | --- | --- |
| Game (built in) | nothing; it is part of the program | selects game 1 or 2; Play as in SPEC-play.md |
| Game (added) | title | listed only |
| Movie | title and the full path of a video file | Open hands the file to the Windows program for its type |
| Other | title | listed only |

These points can change:

- A movie is a video file on this computer. The launcher has no player of its own.
- A game that the user adds has no Play path. Only the built-in Mega Man Legends can be started.
- An added item has no picture of its own yet. It shows a small symbol for its kind.

## Rules

- `LibraryCore.cs` holds the list. It starts nothing and uses no network (check-library.ps1).
- Open exists in one place, `LibraryOpen.cs`, called only by `OpenLibraryItem` in `Program.cs`. `Program.cs` starts no program itself (check-catalog.ps1). Open runs only for an added movie whose path is a full local path with a video extension (`.mp4 .mkv .avi .mov .wmv .webm .m4v .mpg .mpeg`) and whose file exists. A changed list file cannot make it start a program.
- Open never goes through the game Play path. The round Play button of the earlier game page is never shown now. The Play controls of the Library are described under "Play from the Library". The Library never changes whether Play is enabled. Its one enable site is unchanged (check-runtime.ps1).
- Added items are kept in `library.txt` in the launcher's settings folder: one line for each item (kind, title, file, with tabs between), after a first line `1`. It is saved through a temporary file with a `.bak` copy. A line that is not valid is left out when the file is read.
- Limits: 500 added items, 120 characters in a title, 520 in a path, 512 KiB for the file. The same kind, title and file cannot be added twice.
- If the file could not be read at start (a read error, a file over the size limit, or a first line that is not `1`), the launcher shows the built-in items and refuses to save, so the file is not replaced. A list that would be over the size limit is not saved. A single line that is not valid is left out when the file is read, and is gone after the next save.
- Remove deletes the list entry only, after a question. No file is deleted.
- The launcher does not copy, change or upload a movie file. `library.txt` is removed with the other settings when the user asks the uninstaller to remove settings. "Reset launcher settings" does not clear the Library.
- When a movie is opened and the launcher music is playing, the music is paused. It does not start again by itself.
- For tests only: the variable `FLUTTERDECK_LIBRARY_FILE` with a full local path to a `.txt` file replaces the real list file.

## Where it is

- `LibraryCore.cs`, `LibraryOpen.cs`, `LibraryPureTest.cs`, `check-library.ps1`.
- `Program.cs`: the fields after `panel`; the block "Library icons and pages"; the Library screen itself is in `DeckLibrary.cs` (the same window class), and the saved view is in `LibraryView.cs`; `RenderLibrary`, `RenderLibraryList`, `LibraryTile`, `LibraryPicture`, `LibraryBackButton`, `RenderLibraryItem`, `OpenLibraryItem`, `SaveLibrary`, `RemoveLibraryItem`, `RenderLibraryAdd`; the `gameOnly` list in `RenderScreen`.

## Verified, 2026-11-17, an AI assistant

- `check-library.ps1`: the list module alone (kinds, the movie file rule, add, repeats, limits, file round trip, the test variable), Open refusing each item that is not an added movie with a video file that exists, and the source guards.
- A scratch build with a test list of 10 added items, driven by UI Automation with no mouse or keyboard: the icon grid, the filter with counts, the search (two matches, then none), the order by title, the pages of both built-in games (Play and the cog shown), of a movie (Play and the cog hidden) and of a movie whose file is missing (Open disabled); Back; the navigation button returning to the grid; the Add form; Add with no file (refused with a message); Add of a listed game (written to the test file and selected); the same title again (refused); Cancel. The real settings folder was unchanged and no real `library.txt` was made.
- Captures at 100% and at 120% text size: no overlap; the form scrolls at 120%. At 120% five icons fit in a row.

## Not verified

- Open (never pressed: it starts another program). Remove. The file picker. A real video file.
- A movie on a network drive that is not connected: the launcher looks for each movie file when it draws the grid, which may make the screen slow. A mapped drive letter is accepted as a movie path.
- Mouse wheel and keyboard in the grid with a real mouse and keyboard. Scrolling the grid by hand.
- The screen in the developer's own launcher with the real settings.

## Not built

- Pictures for added items: every added item of a kind shows the same symbol. Editing an item. Starting a game with one click on its icon (as an AI assistant recalls, the program the developer named does that; that program was not looked at in this work. Here a click opens the page, and Play is on the page). Collections, play time, a double click on a card, and "Open file location". A Play path for another game. A player inside the launcher.
- Escape and the "Open library" button on Home go to the Library as it was left (a page or the Add form may still be open). The Library button in the navigation column always shows the cards: it closes an open panel and the filter pane.
- The state line for UI Automation still shows `game=1` or `game=2` when an added item is selected.

## 2026-11-18: what was and was not tested of the card, panel and details work

- Tested in a scratch build with a separate data folder and no game program, driven by UI Automation with no mouse or keyboard: tabs and counts; the card controls after focus; the heart; the menu rows for a game, a movie and a hidden item; the panel and its buttons; Play with no game program (nothing started, the reason shown) from the panel and from the card; hide, "Show hidden items" and unhide; the order Z-A and Clear; the details view; the Add form. Captures at 100% and at the largest text size.
- Not tested: a real pointer over a card, a real right click, the menu key, the mouse wheel, a click beside the panel, Escape. Open and Remove were not pressed. Play with a ready game was not pressed: the first real Play stays with the developer.
- Not built: double click on a card, "Open file location", play time, collections, the order by play time or by purchase date.
