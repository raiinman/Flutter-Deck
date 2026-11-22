# Changelog

All notable changes to Flutter Deck are listed here. The newest version is first.
Flutter Deck is a beta, so every version is called a Beta for now.

## 0.4.1 Beta - in progress, not released yet

What is here is what has been built so far. It can change before release.

### Added
- **Gamepad buttons screen (Mega Man Legends).** In Settings, the row "Gamepad buttons" opens a screen where you choose what each button of an Xbox or PlayStation 5 pad does, and how far a stick must move before the game reacts (the stick dead zone). Before, this was only possible by editing a text file. You can save, discard your changes, or go back to the default buttons. Changes apply the next time the game starts. Mega Man Legends 2 and The Misadventures of Tron Bonne say that changing their buttons is coming soon.
- **Mega Man Legends: fire and lock on while skating.** On the Jet Skates you can now fire the buster and lock on to an enemy, and the lock keeps the camera on the target while you skate.
- **Mega Man Legends: easier skates.** Hold Circle to put the skates on, let go and keep skating, and press Circle again to take them off. This needs modern movement, which is on by default. The first time you play, one message box explains it.
- **Mega Man Legends: three new switches** in Settings and in the in-game settings: Shoot on the Jet Skates, Lock on while skating, and Hold Circle to start skates, press Circle to stop. All three are on by default and can be switched off.
- **Mega Man Legends: new lock-on marker, on by default.** The lock-on marker in the style of Mega Man Legends 2 is now on by default. Players who had switched it off keep it off, and it can be switched back to the single arrow in Settings. There is also a setting for switching target with a flick of the right stick.
- **Message board with real notices.** The Home screen now reads its notices from a public file kept by the developer, so news can reach you without a new launcher. The built-in fallback notes were out of date (they said updates and a live publisher were not connected) and are corrected.
- **Mega Man Legends: overlay controls.** Pause, Full screen, Take screenshot, Mute and the game volume bar in the in-game overlay now work and show the game's real state, instead of being greyed out. Games you built before need **Build my game** once more.
- **Mega Man Legends: widescreen edges.** With widescreen on, the white gaps at the left and right edges of outdoor scenes are fixed. Build your game again to get it.
- Games you built with 0.4.0 need **Build my game** once more to get the skates, lock-on, overlay and widescreen changes.

### Planned (not finished yet)
- **Update notes:** a launcher update replaces the launcher only. When a version brings new game features, its update notes will say that you need the new Setup or zip and then press Build my game again.

### Changed
- Nothing yet.

### Fixed
- Nothing yet.

## 0.4.0 Beta - 2026-11-21

The first public beta.

- **Play** Mega Man Legends, Mega Man Legends 2 and The Misadventures of Tron Bonne, built from your own US disc images. No game files are included.
- **Build my game** builds the game from your disc on your PC, with progress and plain error messages.
- **Library** shows your games in a grid. Covers can come from your own disc code (off until you turn them on).
- **Play with a friend**: host or join with an invite code, with a direct connection and help with your router.
- **In-game overlay**: a pill at the top of the game window with pause, restart, stop, full screen and a settings menu. Hotkey Ctrl+Alt+O.
- **Settings** for each game, including the picture, the keyboard keys, the volume and the camera.
- **Mega Man Legends** has modern movement and third-person controls, lock-on with a flick of the right stick to switch target, a camera distance setting, Xbox and PlayStation 5 pad support with vibration, an experimental widescreen setting (the enemy health bar, fades and pause menu follow the wide picture, and the title and card menus keep their original shape), farther world drawing, a Teleport line in the pause menu, and the opening movie.
- **Mega Man Legends 2** has modern movement and a free camera on the right stick, with look speed, invert and distance settings, and an analog pad option.
- **The Misadventures of Tron Bonne** plays with a plain digital pad and draws a sharper, high-resolution picture. More features are coming soon.
- **Picture presets** with DSI Prism, a key editor, memory card saves (back up and restore) and music.
- **Updates and installer**: a signed self-updater and a per-user installer that does not ask for administrator rights. The release also comes as a portable zip.
