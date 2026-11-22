# Music capability

Current behaviour (2026-11-16) is in the last section, "Start-up music and bundled loops", and in SPEC-controls.md. Since 2026-11-17 the launcher remembers music on or off, volume, mute, repeat, shuffle and the bundled track between runs (SPEC-choices.md), so "starts playing" and "20%" in that section describe the first run only. The sections before it are earlier states kept as history.

Original bundled 30-second composition, no game audio samples. Real local native MediaPlayer transport, volume/mute, seek, repeat, and sample-driven waveform bars tied to player position. No autoplay, network, upload, game integration or audio capture.
User may explicitly add bounded PCM16 mono/stereo WAVs to a session-only playlist; no scan/copy/persistence. Previous/next/shuffle disabled for one track with clear help. Repeat restarts current track; no claim of gapless MediaPlayer looping. Wave bars are sample amplitudes, not hardware output measurement.
Test PCM parser limits, silence/nonzero waveform, transport playlist math where independent; compilation. Live audio-device output, timing, controls and audible loop seam need permitted observation. Do not bypass denied GUI route.

## Icon-only refinement (2026-11-15)
Developer screenshot showed native text labels stacked over illustrated transport. Use opaque icon-only native transport buttons with vector play/pause, previous/next, shuffle, repeat and volume/mute state; keep accessible names and tooltips. Remove Settings/Saves/Files captions under art icons. Game Play retains a transparent disabled hit target and honest tooltip/help/readiness, without a visible unavailable box. No playback or GUI validation performed in this pass.

## Artwork-first correction
Supersedes the vector-button treatment above: existing transport artwork remains visible, with completely transparent native hit targets. No duplicate icon, text, button background, border or hover box is painted. Playback/mute/repeat/shuffle state is exposed through tooltips/accessibility and existing playback-status line, not a second visual control. Waveform and live volume slider remain functional native surfaces.

## Load behaviour (2026-11-16)
Each track open uses a new MediaPlayer and only the current player's events are honoured. A reused player could stay silent when asked to reopen a file it had held before, which left the same file listed twice, or a retried file, stuck at Loading. Loading now always ends: a file that neither opens nor fails within 10 seconds (DeckAudio.LoadSeconds) becomes "Audio unavailable". Play after a failed open reopens the track. The lower-deck level box reads Stopped, Paused, Loading, Audio unavailable or No track when nothing is audible.
Known edges: pausing while a track is still loading ends as Stopped; a file slower than 10 seconds to open shows "Audio unavailable" until Play is pressed again.
Verified with muted headless probes of DeckAudio only (duplicates, failed-open retry, forced no-event deadline, rapid track changes). check-state.ps1 guards the source pattern; it does not play audio. No sound was auditioned and the behaviour was not observed in the running launcher.

## Start-up music and bundled loops (2026-11-16)
- Three bundled loops, in this order: Dawn Launch, Skyway Run, Skyward Circuit. Each is loaded by itself, so a missing file does not lose the others.
- Music starts playing when the launcher window has loaded. The loops play in order; after the last one the first starts again; a playlist of one track plays it again. Repeat and shuffle saved by an older build are ignored at start.
- The starting volume is 20% (`DeckAudio.StartVolume`), and the lower-deck slider starts at the same value. This is the launcher's music; the game's own volume is a separate setting on the setup and readiness screen. Since 2026-11-17 the volume is remembered between sessions (SPEC-choices.md); 20% is the first-run value.
- The level bars now show the music itself, not the volume setting, so they stay readable at 20%. They are flat when muted or at zero volume.
- When Play starts a game the launcher's music pauses, and it resumes when the game ends, if it was playing. If the user presses play, pause or stop for the music while the game runs, that choice stands and nothing resumes by itself. Music does not start with the launcher if a game is already running.
- The two new loops are rendered by `LoopSynth.cs` (`make-loops.ps1`); `check-audio.ps1` requires the committed files to be exactly what it renders.
- Verified: files decode at 30.000 s and 44,100 Hz with no clipping; checks pass; the installer build includes all three loops. Observed once in a muted test copy of the launcher, read through UI Automation: music playing on start, three tracks, slider at 20. Not verified: nobody has listened to the new loops; the pause and resume around a game (code reading and review only).
