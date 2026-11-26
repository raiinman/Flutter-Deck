# Changelog

All notable changes to Flutter Deck are listed here. The newest version is first.
Flutter Deck is a beta, so every version is called a Beta for now.

## 0.4.2 Beta - 2026-11-26

Most Mega Man Legends changes need the new Setup or zip, and then **Build my game** once more. The in-app update replaces the launcher only.

### Added
- **Mega Man Legends: quick dodge roll.** A quick tap of Circle rolls out of the way, and a hit in the first moment of the roll does no harm. Off by default, with a switch in Settings and in the in-game settings. While it is on, holding Circle does not put the Jet Skates on until you press the walk switch (the left stick press on a gamepad; the Gamepad buttons screen can move it). The walk switch is off each time the game starts, and the keyboard has none. Pressing Circle while walking rolls at once, so stop first to talk or to use a door. Pressing R2 cancels a queued second roll.
- **Mega Man Legends: locked shots aim at the target.** Shots fired with lock-on start out aimed straight at the target, so shots can now hit enemies high above Mega Man. On by default, with a switch.
- **Mega Man Legends: cheat codes you can switch while playing.** Flutter Deck comes with a list of codes, and you can turn them on and off from the in-game overlay.
- **Mega Man Legends: Teleport menu names.** Nine places have better names, mostly taken from the game's own island map, in the game and in the launcher.
- **Mega Man Legends: more detail farther away.** With the farther view distance, models such as the town sign keep their detail much farther away.
- **Mega Man Legends: diagnostics record.** The game keeps a record of the last few minutes of play on your own PC (the game's memory, its log and the buttons you pressed), to help find bugs. On by default. Before the first Play a large notice explains it in plain words and lets you keep it on or turn it off; Settings has the switch and a button to read the notice again. Nothing is sent anywhere.
- **Mega Man Legends: guide "How to use the new features".** Short steps inside Flutter Deck for building your game again, the quick roll with the walk switch and the Jet Skates, cheat codes, Teleport and lock-on. It opens once before Play and again from Settings, and lists only what your game has.
- **All three games: see which builder built your game.** Settings and the Build screen show it, and tell you when to build the game again.

### Changed
- **Mega Man Legends: lock-on no longer picks a backup target of its own** when the game finds none, so it no longer falls back onto things like boxes. Switching target with the right stick can still reach things the game did not choose. A new switch, "Target switch skips people and signs" (on by default), leaves out things without health there, such as most town people, signs and destroyed enemies.
- **Mega Man Legends: lock-on marker position.** The marker sits on the exact point your shots fly to.
- **In-game overlay:** the word "Tab" no longer shows beside the Unlimited speed row.

### Fixed
- **Mega Man Legends: camera after a cutscene or a door.** The camera eases back behind Mega Man instead of staying low.
- **Mega Man Legends: save robot choice list.** Down no longer turns into Up with the third-person controls.

## 0.4.1 Beta - 2026-11-22

Game features need **Build my game** once more after you install this version. The in-game overlay still shows the word "Tab" beside the Unlimited speed row in this release; Tab itself does nothing in play.

### Added
- **Gamepad buttons screen (Mega Man Legends).** In Settings, the row "Gamepad buttons" opens a screen where you choose what each button of an Xbox or PlayStation 5 pad does, and how far a stick must move before the game reacts (the stick dead zone). Before, this was only possible by editing a text file. You can save, discard your changes, or go back to the default buttons. Changes apply the next time the game starts. Mega Man Legends 2 and The Misadventures of Tron Bonne say that changing their buttons is coming soon.
- **Mega Man Legends: fire and lock on while skating.** On the Jet Skates you can now fire the buster and lock on to an enemy, and the lock keeps the camera on the target while you skate.
- **Mega Man Legends: easier skates.** Hold Circle to put the skates on, let go and keep skating, and press Circle again to take them off. This needs modern movement, which is on by default. The first time you play, one message box explains it.
- **Mega Man Legends: three new switches** in Settings and in the in-game settings: Shoot on the Jet Skates, Lock on while skating, and Hold Circle to start skates, press Circle to stop. All three are on by default and can be switched off.
- **Mega Man Legends: new lock-on marker, on by default.** The lock-on marker in the style of Mega Man Legends 2 is now on by default. Players who had switched it off keep it off, and it can be switched back to the single arrow in Settings. There is also a setting for switching target with a flick of the right stick.
- **Message board with real notices.** The Home screen now reads its notices from a public file kept by the developer, so news can reach you without a new launcher. The built-in fallback notes were out of date (they said updates and a live publisher were not connected) and are corrected.
- **Mega Man Legends: overlay controls.** Pause, Full screen, Take screenshot, Mute and the game volume bar in the in-game overlay now work and show the game's real state, instead of being greyed out. Games you built before need **Build my game** once more.
- **Mega Man Legends: widescreen edges.** With widescreen on, the white gaps at the left and right edges of outdoor scenes are fixed. Build your game again to get it.
- **Mega Man Legends: Tab no longer speeds up the game.** Pressing the Tab key used to switch the game to unlimited speed, so tabbing in and out while streaming could speed it up. Now Tab does nothing in play. Unlimited speed is still there when you want it: it is a row in the in-game overlay menu. Build your game again to get it.
- Games you built with 0.4.0 need **Build my game** once more to get the skates, lock-on, overlay, widescreen and Tab changes.

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
