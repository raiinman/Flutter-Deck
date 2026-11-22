# Current Flutter Deck capabilities
- Implemented local: native ship shell, adjustable launcher size/text, persisted launcher preferences, transactional game/BIOS references, bounded metadata preflight, local WAV music and waveform, Help.
- Implemented reader, deployment pending: Home/changelog/public notice receiver; package has no publisher endpoint. Older copies need upgrade; no push while closed.
- Implemented, not yet live: signed launcher self-update with a startup-check toggle, manual check, click-to-install, restart and rollback (SPEC-updater.md). No release is published and the repository is private, so no copy can update yet; a live download and install has never been run.
- Implemented, run for real once on the developer's PC (one install and uninstall, 2026-11-16): application icon and file details, and a per-user Setup with Start Menu and optional desktop shortcuts, an installed-apps entry and an uninstaller (SPEC-installer.md). Not run on any other PC. Files are not publisher-signed, so Windows SmartScreen will warn testers.
- Experimental: independent DWM/GDI scaler; no live game compatibility or performance claim.
- Proposed: 82 catalog entries covering engine controls, graphics, saves, mods, gameplay and services. Selections are in-memory previews, not applied settings.
- Two catalog entries route to real Setup and Music instead of fake selectors.
- Implemented, not yet started for real from the launcher: Play for Mega Man Legends 1 starts the user's own native game program per the game side's contract, with window size, sound-off and a launcher-owned key map and memory card (SPEC-play.md). Tested with a stand-in program only. Mega Man Legends 2 has no native program.
- Game identity/region/revision detection, controller remapping, volume, filters, full screen and game updates are not implemented/verified.

Preflight checks only existing local references and bounded metadata. It does not hash payloads, repair files or prove a native boot. User files and setup data are not included in packages.
