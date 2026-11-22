# Multiplayer: play Mega Man Legends with a friend

Status, 2026-11-19: BUILT, NOT USED FOR REAL. The pure tests pass, and the window was opened twice in scratch builds through UI Automation (before and after the nine fixes). Nobody has hosted or joined a game from the launcher. No real router was asked by any test. On that day the developer's own settings named a game program without the feature word, so the button does not show until the developer chooses the multiplayer build.

## Background

The game side is specified by its contract MP-1 (`LAUNCHER-CONTRACT-MP.md` in the game project's multiplayer folder). This document says what the launcher does with it.

## What the player does

1. Library, the Mega Man Legends card, "Play with a friend...". The button is there only when the chosen game program reports the feature word `multiplayer`. Mega Man Legends 2 never has it.
2. **Host:** "Host a game". Flutter Deck asks first, then asks the router to send UDP port 47400 to this PC, starts the game, and shows an invite code with a "Copy the code" button. The friend gets the code by the host's own means.
3. **Join:** paste the code, "Join the game". No router work.
4. **Check my router:** looks only, changes nothing, and says what hosting would need.
5. While the game runs, the window shows one sentence about the connection (waiting, looking, connected, stopped answering). To stop, the player closes the game.

## What multiplayer is (from the contract)

Each player runs a full game. Each game draws a second Mega Man from the other player's position. Enemies, items, damage and the story are not shared. The other player is drawn only while both games are in play in the same stage and area. Two players.

## The invite code

`RMX-` and 39 signs in groups of five. It holds a version number, the host's IPv4 address, the port, a key of 16 random bytes and a check byte, exactly as contract section 4 says. `MpCore.Encode` and `MpCore.Decode`.

- Reading ignores spaces, dashes and case, and reads `O` as `0` and `I`, `L` as `1`.
- Refused: a wrong length, a sign outside the alphabet, a wrong check byte, a version other than 1, a port under 1024. the AI assistant's additions, not in the contract: an address that starts with 0 or is 224 or higher, and a last sign with spare bits set. A code made by the game side's reference script never has these.

## Where the secrets are

- The key, the game setting that holds it and the invite code live in memory only, in one object, from the press of Host or Join until that game has ended.
- The setting goes to the game through the environment of the game process (`RMX_MP`), together with the place of the status file (`RMX_MP_STATUS`). It is never on the command line, in a settings file, in the session log or in a message.
- The invite code reaches the clipboard only when the player presses "Copy the code".
- An ordinary Play removes both variables from the game's environment, so a value that the launcher itself was started with cannot switch multiplayer on.

## The router

`RouterPort.cs` speaks to the router directly (UPnP Internet Gateway Device), like the game side's reference script.

- It finds this PC's address and gateway, asks the gateway for its port service, and asks who holds UDP port 47400.
- If no device holds the port, or this PC holds it, it adds the rule (4 hours; without a time limit when the box takes only that) and checks that the box kept it. The record is written first, without a time limit, and gets the real limit only after the box confirmed the rule.
- It asks the box for its internet address. A public address: done. An address in 100.64 to 100.127: the provider shares the address and hosting cannot work; the rule is taken away again. Another private address: there is a second box. Flutter Deck finds it with one echo packet that may travel two steps, asks it in the same way, and maps the port to the router's outer address.
- When a box does not answer, refuses, or does not say who holds the port, the window shows the steps to do by hand with the right addresses, and a box for the public address.
- A typed address may be a local one. The window then says that only someone on the same network can join.

Rules that the checks hold:

- Only the gateway and one second box are contacted, both at a local address. Nothing on the internet is contacted; the public address comes from the boxes.
- A box is believed only on its own address. An answer from another address, a page or control address on another address, a redirect, a proxy and a document type in the box's XML are refused. Answers are limited to 512 KB and the search to a few seconds.
- A port that another device holds is never taken or removed.
- A rule is removed only while it still points at the address this launcher asked for.
- No firewall rule is changed. Windows asks its own question when the game first listens, and the player answers it.

## When the rule is taken away again

- The game ends: both rules are removed on a background thread, the outer box first.
- The overlay's Restart: the same session goes on with the same key, and the rules stay.
- Hosting is cancelled, the window is closed before the game started, hosting cannot work, or the game does not start: the rules made so far are removed.
- The launcher is closed while the hosted game runs: the rules stay for that game. Each rule is written to `multiplayer-rules.txt` (box, control address, service, port, client, time, time limit; no key) before the box is asked. At the next start of the launcher, and when the window is opened, rules in that file are removed, unless a game of Mega Man Legends is running.
- One router job runs at a time on the computer, also across two Flutter Decks. A clean-up never touches a rule while a hosting is prepared, and it leaves a rule younger than 15 minutes alone.
- A rule's record is dropped only when the box removed the rule or clearly holds no rule of ours. A box that refuses, or does not say, keeps its record for a later try.
- A router answer that arrives after the player closed the window is not used: the rules are removed, also when the window is open again.
- A box that cannot be reached or refuses: the player is told in the window, and the sentence stays until the next hosting. A rule with a time limit ends by itself after 4 hours. A rule without one stays in the record for 60 days of tries.
- Uninstall removes `multiplayer-rules.txt` with the other settings. It does not ask a router.

## Files

- `MpCore.cs`: pure. Invite code, settings, status line, address kinds, manual steps, record of rules.
- `RouterPort.cs`: the router questions.
- `DeckMultiplayer.cs`: the window and the flow. Same window class as `Program.cs`.
- `GameRuntime.cs`: `Start` takes the setting and puts it into the game's environment; `RuntimeCheck.HasMultiplayer`.
- `Program.cs`: passes `FriendSetting()` to `Start`, ends the multiplayer session with its game, and cleans left rules at start.
- `check-multiplayer.ps1`, `MpPureTest.cs`; more cases in `GameRuntimePureTest.cs`.

## Tests

- `MpPureTest.cs` (115 checks): the contract's example code both ways; 300 random round trips; every one-sign mistake and every swap of two neighbours in the example refused; the two settings; the status line; address kinds; the record of rules; and the router questions against a stand-in box that listens on this PC only (one box, two boxes, a refusal, a box that keeps nothing, a port held by another device, a shared provider address, answers that must not be believed).
- Compared once by hand with the game side's reference functions: 200 of 200 codes the same, and read the same both ways.
- `GameRuntimePureTest.cs`: the stand-in game receives the setting in its environment, the command line and the data folder hold no key, a program without the feature word is refused, Legends 2 is refused, an ordinary game gets no setting.
- UI Automation, scratch build, separate data folder, with the game side's multiplayer build chosen: the button is on the Legends 1 card, the window opens, a code of the wrong length and a mistyped code are refused with their sentences, and no game started. The developer's real settings were unchanged. This look was before the nine fixes.
- The same through UI Automation on a scratch copy of the 2026-11-19 03:28 build, after the fixes: the button is on the Legends 1 card, the window opens with Host, Check and Join, a code with one wrong sign is refused with its sentence, no game started and no record of rules was written. Host and Check were not pressed. The build of 2026-11-19 04:32 (from commit `d499f26`, with the merged overlay work) was started the same way: the Details view of the Library shows the button on the Legends 1 panel; the window was not opened in that build.
- After the fixes: the whole launcher compiled, and a scratch build was started minimized with one expired rule in its record. The record was gone a few seconds later, with no box asked. This ran the router gate, the question to the window thread and the forgetting of a rule.

## Not tested

- Hosting and joining from the launcher. The first real one is the developer's click.
- Any real router: finding it, adding a rule, removing a rule, the second box, the echo packet. The game side tested finding both boxes on the developer's line with its script; this C# code has only met the stand-in box.
- A game between two PCs, and anything across the internet.
- Two games on one PC cannot both be started from one launcher data folder: the memory card is in use. A one-PC test needs the game side's own start files for the second game.
- The status sentence on screen with a real game, and the overlay's Restart during a session.
- The flow of the window (`DeckMultiplayer.cs`) has no test that runs it: Host, the typed address, Cancel, closing the window during the router work, the end of a game. It is held by text guards in `check-multiplayer.ps1` and by reading. An independent checker read it on 2026-11-18 and found nine problems, which were fixed; the fixes had no second independent check.

## Not built

- The friend's game program: it must be built from the friend's own disc, and that builder does not exist (game side).
- Presence ("is my friend online"), names, chat, more than two players.
- A choice of port. It is 47400.
- IPv6.
