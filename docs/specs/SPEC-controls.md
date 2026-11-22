# Sprite controls and lamps

Status, 2026-11-16: wired and seen in off-screen captures of test builds. Hover, pressed and keyboard-focus looks have not been seen (they need real mouse and keyboard input), and the developer has not yet looked at the result.

## Developer request (2026-11-16)
This reverses the earlier removal of painted overlays from the transport buttons and answers the long-parked question about icons painted into the ship art: they are now covered by the developer's own control art.

## Where the art comes from
`make-sprites.ps1 -Pack <folder>` cuts 40 sprites out of the pack's four sheets into `assets\controls\`. It checks each sheet's hash first, crops each sprite to its own opaque frame (not to the equal grid cell, and without the neighbouring glow), and cuts the four lamp frames to one shared window around their cap and cage so they line up when swapped. The sheets themselves are not in the repository; see `assets\CONTROLS-PROVENANCE.md`. `build.ps1` embeds every sprite in the launcher as `S-<name>`, so the installer and the updater carry nothing new.

`assets\ship.png` is never edited. New controls are drawn over it, and `check-controls.ps1` pins its hash.

## What is dressed
- Music strip: previous, play/pause (one button, one slot), next, a new Stop button, shuffle and repeat. The speaker/mute button was removed on 2026-11-18 at the developer's request; a mute saved by an older build is dropped at start. Each sits on an opaque backing in the strip's own colour, which covers the icon painted underneath. Stop took its slot from the volume slider, which is now shorter (940 to 1244 instead of 870 to 1244 on the 1672-wide design).
- Library: settings, saves and files, drawn square and slightly taller than the painted tiles so they cover them.
- Top deck: features, setup, text smaller, text larger, reset, help, minimize, maximize, close. They were words on a translucent plate; they are now icons. The Features and Setup click areas were narrowed and Setup moved left (now 790 to 874 and 876 to 936) so the Setup icon stays clear of a porthole and the two areas do not overlap; `check-controls.ps1` fails if any two top-deck areas overlap.
- Four ceiling lamps over the painted ones, at the pack's stated centres.
- Not used yet: gamepad, grid, download, images, music and menu (the left navigation keeps its existing glyphs so the eight entries stay consistent), and the lamp frames off and bright.

## States
- Play, pause and stop have the pack's own normal, hover, pressed and disabled art.
- Every other control has normal art only, so the launcher adds: a light wash on hover, a dark wash when pressed, and a fade when disabled.
- Keyboard focus always shows a cream ring, separate from hover. A dressed button uses a bare template, so the older plate, cyan border and second fade of the text buttons do not appear behind the art.
- Shuffle and repeat show "on" with a bar under the button, not with glow, so it does not rely on colour alone.
- An opaque backing in the strip's colour (940 to 1486 on the 1672-wide design) covers their painted glyphs and the painted speaker and bars; the volume slider is longer (940 to 1388). Drawn over that place, in the art's colours: a speaker (`music-mute`) that mutes and unmutes the music, with a red slash while muted, and five bars (`volume-bar-1` to `volume-bar-5`) that light up to the volume (one bar per 20%, none while muted); clicking bar N sets the volume to N x 20%. Moving the slider or clicking a bar unmutes. The mute is remembered like the other music choices but, as before, dropped at the next start.
- Accessible names, automation ids and tooltips are unchanged; the top-deck icons no longer show words, so their existing tooltips now carry the name for mouse users.
- If a sprite is missing from a build, that button stays as it was and keeps working.

## Lamps
Each lamp shows the "on" frame. Every 4 to 7 seconds one lamp, chosen at random, dips to the "dim" frame for 100 to 160 milliseconds. There is no full off, and no rhythm. If Windows is set to reduce animation, the lamps stay steady.

## Structure and command
`Sprite`, `Dress`, `Redress`, `SetSprite`, `SetMark` and `AddLamps` in `Program.cs`; `make-sprites.ps1`; `assets\controls\`. Check: `powershell -NoProfile -File .\check-controls.ps1`

## Verification
- Verified by check: all 40 sprites exist with the expected sizes and transparency, lamp frames share one size, the build embeds them, the guards for focus ring, disabled fade, reduced motion, toggle mark and accessible names hold, and the ship art is unchanged.
- Seen in off-screen captures of test builds at 1200 by 675, Library screen (the last one after the review fixes): all transport sprites, Stop, the three library icons, nine top-deck icons and four lamps in place; the repeat bar shown; the repeat bar inside the strip; the Setup icon clear of the porthole; no painted icon showing through except one faint one-pixel mark between shuffle and repeat. The developer pointed out a painted scrap at the slider's left corner; a solid backing under the slider now covers it (seen gone in a further capture).
- NOT verified: hover, pressed and focus looks; the disabled look of every control; play/pause and speaker/mute swapping on screen; the lamp dip in motion; other window sizes and text sizes; whether the developer likes it.
