# Picture presets (proposed)

Status: **provisional**, proposed by the Prism integration team on 2026-11-16. The file format and the six looks are not developer-approved and may change. Parser, service and six preset files are built and unit-tested. Nothing is connected to the launcher window, the overlay or DSI Prism yet, so no picture changes.

## What it is
A preset is a named picture look: one scaling filter, an optional variant and a sharpness value. The launcher owns small text files in `presets\`. The game builds no filter and no effect. A picture backend applies the look. The intended backend is DSI Prism (see below).

## DSI Prism: open source, its own program, and bundled with Flutter Deck
**Developer decision, 2026-11-16 (stated in the Prism integration project; supersedes the earlier plan to sell Prism on Steam):** Prism will be open source. It is both its own program and a part that ships with Flutter Deck. It should work especially well on the old Mega Man games.

What this changes here: Flutter Deck may start the Prism it ships, so the backend can be a bundled Prism as well as one the player installed. The preset files and the call surface below stay the same. The earlier "detect only, never bundle" assumption is withdrawn.

**Licence decided 2026-11-16 (developer): MIT for Prism's own code.** Third-party parts keep their own notices. Still open and not decided by anyone: a provenance and licence review of Prism's source before it goes in a public repository (Prism's status file says `legalClearanceClaim: false`, and its notes list retired code that must not come back); where Prism's repository lives; and how a bundled copy and a standalone copy of Prism share the one-copy-per-user rule.

What Prism offers today (from Astra's agent, 2026-11-16, read from its source): no command to apply a profile and no way to read the active one from outside; a running scale uses a snapshot, so a change needs stop and start; profiles match a game by program path or file name, then window title, and Prism checks the foreground window once a second; capture of the `RuntimeGpuPresent` window, GDI or Direct3D 11, is not tested. Astra's agent said it will add a local control interface for named profiles and test a window of that class. Details are in .

## File format
One file per preset, `presets\<id>.preset`, UTF-8, at most 4096 bytes. The id is the file name: a-z, 0-9 and hyphen, 1 to 32 characters. Lines are `key=value`; lines starting with `#` and blank lines are ignored. Keys are case-insensitive. Unknown or repeated keys make the file invalid.

| Key | Required | Meaning |
|---|---|---|
| `name` | yes | Shown to the player. At most 32 characters, no control characters. Unique ignoring case. |
| `description` | no | One short line, at most 80 characters. |
| `filter` | yes | `nearest`, `bilinear`, `integer`, `bicubic`, `fsr1`, `nis`, `cas`, `sgsr`, `xbr` or `anime4k`. These are the scaling filters Prism's README lists. |
| `variant` | no | `fsr1`: `original` (default) or `tiled`. `anime4k`: `s` (default), `m`, `l`, `vl`, `ul`. Other filters take none. |
| `sharpness` | no | Whole number 0 to 100. Default 0. Prism's profile has one `Sharpness` value from 0 to 1 (seen in its settings file); a backend would map 0 to 100 onto that. Which filters use it is not known. |

A bad file (unreadable, not UTF-8, too large, invalid, or a name that repeats another preset's name or equals another preset's id) is skipped and reported; the other presets still load. At most 64 presets load. Names and descriptions may not hold control, invisible or text-direction characters. Length counts UTF-16 units. Ids that Windows cannot use as file names (such as `con`) are refused.

## Starting set (proposed; developer to change)
Original (integer), Clean (bicubic, 20), Sharp (CAS, 70), Smooth (bilinear), Painted (Anime4K M, 40), Pixel Art (xBR, 30). Each is a plain choice among Prism's listed scaling filters. The developer showed a reference with six named looks (for example a clean look and a film look); we have not seen it, so these names are guesses. **Developer: please send the six names you want.** No shader or preset file from any other program was read or copied.

**Not in the set:** a film look, scanlines or a CRT look. Prism's README lists no such effects. Adding them needs Prism to offer them first; this is a question in the request file.

## Interface for the overlay team
`FlutterDeck.Presets.PresetService` in `PresetCore.cs`:
- `List()` returns the loaded presets (id, name, description, filter, variant, sharpness).
- `Apply(idOrName, out reason)` applies by id or by name, ignoring case; an id wins over a name that equals it. It returns true only when the backend reports success. On failure, the active preset does not change and `reason` is always a plain sentence the overlay can show. A call made from inside `ActiveChanged` is refused.
- `ActiveId` is the id of the preset that last applied in this run, or an empty string. It is not read back from Prism.
- `ActiveChanged` fires once after each successful apply. A listener that throws is ignored and does not stop the others.
- A backend implements `IPictureBackend`: `Supports(preset, out reason)` and `Apply(preset, out reason)`.
- `UnavailableBackend` is the backend to use when Prism is not installed. It lists normally, refuses every apply with "DSI Prism is not installed." (or a sentence you give it), and never makes a preset active. The launcher keeps working; the overlay can show the sentence or hide the looks.

Changes apply while the game runs: the service holds no game state. Whether the picture changes without a visible gap depends on the backend; Prism today needs a stop and start of scaling.

## Boundaries
- Which preset was last used is not saved yet. Saving it belongs to the launcher's settings and is the launcher project's call.
- No backend exists. The old `scaling\` module (MIT) can show only nearest, bilinear, bicubic and integer, and only for a window the user picks by hand. It is not wired as a backend.
- Capture of the game window on the GDI and Direct3D 11 paths is untested.

## Checks
`check-presets.ps1` builds the core with the .NET Framework compiler (C# 5), runs the checks in `PresetPureTest.cs` (parsing, limits, service, folder loading with temporary files, the six shipped files), and guards the source against process, network and library-loading calls. An independent review found six defects; all are fixed and each has a test. These checks do not show that any picture changed.

## Learned presets and live picker candidate, 2026-11-18

The catalog includes `prism-learn-balanced` and `prism-learn-performance`, with zero sharpness and no variant. The live overlay initially offers only these two looks. The integration mappings retain all existing filters and add Bicubic CAS; Clean uses that filter for its sharpness. Painted and Pixel Art omit ignored sharpness values. The reconciled parser rejects nonzero sharpness for filters that do not consume it. All eight shipped files and 74 parser/service/mapping checks pass.
