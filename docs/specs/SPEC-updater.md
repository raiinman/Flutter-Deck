# Updater specification

Status, 2026-11-16: built and wired, not yet live. The repository is private and has no release, so every installed copy currently reports "could not reach the update source". It starts working when the developer makes the repository public and publishes a signed release. Nothing in this work changed visibility or published anything. The earlier simulated preview panel is removed.

## Developer decisions (2026-11-16)
- Update source: this repository, `raiinman/Flutter-Deck`, once the developer makes it public.
- Trust: releases signed with the developer's private key. The key was generated on the developer's PC and is kept outside the repository; only the public half is in `Updater.cs`.
- Launcher updates only. Game files, BIOS, saves, music, mods and settings are never touched. Game updates are a separate, unbuilt product.

## What the launcher does
- Shows its own version and release number on the Updates screen (`Updater.Version`, `Updater.Sequence`).
- When "Check for updates when Flutter Deck starts" is on (the default), makes one check at startup. "Check now" checks on demand. There is no timer and nothing runs while the launcher is closed.
- A check fetches two small files over HTTPS with no credentials, cookies or redirects: `update/manifest.xml` and `update/manifest.sig` from the main branch on `raw.githubusercontent.com`. Nothing about the user, games or settings is sent.
- The manifest is used only if its exact bytes carry a valid RSA signature (3072-bit key, SHA-256, PKCS#1 v1.5) from the built-in public key. It must then name this product, the stable channel, win-x64, schema 1, a version, a release sequence, and one file called `FlutterDeck.exe` with a length (at most 64 MB), a SHA-256 and a download address that is exactly this repository's release download for that version's tag. Numbers must be plain ASCII digits. Anything else is ignored and reported as not trusted.
- An update is offered only when its sequence is higher than the running build's. Version text is for display; ordering uses the sequence, so an old signed manifest cannot roll a newer install back.
- Nothing downloads or installs without the user pressing "Download and install". Release notes are shown as plain text.
- Download: follows at most four redirects, each to `github.com` or a `githubusercontent.com` host over HTTPS; stops if more bytes arrive than the signed length; keeps the file only if length and SHA-256 match.
- Install (`Updater.Install`, the only place the launcher file is replaced): re-checks the signature, manifest and file, renames the running `FlutterDeck.exe` to `FlutterDeck.exe.old`, and moves the new file into place. An earlier rollback copy is set aside first and deleted only after the swap worked; if any step fails the previous names are restored. Read-only flags on these files are cleared. The swap runs on the window's own thread after the download, so closing the window cannot interrupt it. It never asks for elevation; if the launcher cannot replace its own file (unwritable folder, file held by another program) that is reported and the working launcher stays in place.
- "Restart now" starts the new file and closes the old one. The new copy gets the same data folder: a launcher that was started with `--data-dir` passes that one option on (`DeckPaths.RestartArguments`); no other option is ever passed. Between an install (or a rollback) and the restart, "Check now" and "Download and install" are disabled. "Roll back" puts `FlutterDeck.exe.old` back after a confirmation; the release that was left is then not offered again automatically, only through "Check now". The `.old` file is not itself re-verified at rollback: anyone able to replace it could already replace the launcher.
- Leftover `.new`, `.rolledback` and `.old.keep` files are removed at the next start (best effort; a file another program holds open stays until a later start).
- The toggle and the held release are stored in `updates.txt` in the launcher's local application data folder.
- Update phases are published in the state snapshot as `update=` (see SPEC-state.md): off, idle, checking, current, available, unreachable, untrusted, downloading, ready, install-failed.

## No run-time redirection
The address, product, platform and public key are constants. No file, environment variable, registry value or command-line argument can point a shipped launcher at another source or key; `check-updater.ps1` guards this. Tests reach the same code through overloads that take a key and scratch paths.

## Releasing (developer-run)
1. Raise `Version` and `Sequence` in `Updater.cs` (the first public build is 0.3.0, release 1).
2. Run `powershell -NoProfile -ExecutionPolicy Bypass -File .\release.ps1 -KeyPath <private key file> -Tools <build tools folder> -Notes "What changed"`. It builds, runs every check, signs, and refuses if the key does not match the built-in public key or the sequence did not go up. It writes `update\manifest.xml`, `update\manifest.sig`, and under `.dream-loop\release\v<version>\` the exact launcher file, the installer built around that file (`FlutterDeckSetup-<version>.exe`, see SPEC-installer.md) and the release zip `FlutterDeck-<version>-win-x64.zip`: the whole program as Setup installs it, build tools included, laid out by the installer's own code (`build-zip.ps1`; developer, 2026-11-21). Unzipped (near the top of a drive is safest: its longest path is 100 characters inside `FlutterDeck\`), its launcher finds the build tools beside it for all three games (checked 2026-11-21 with the launcher's own tool lookup, `BuildCore.Find`; no game was built from an unzipped copy). It uploads, commits and pushes nothing.
3. On GitHub create release tag `v<version>` and attach that exact `FlutterDeck.exe` (installed copies download it by that name), the installer and the zip. New testers use the installer; the zip is the same program without installing (no Start Menu entry, no installed-apps entry; removed by deleting its folder).
4. Only then commit and push the two files in `update\`. Installed copies begin offering the update once they are on main.

Builds are not byte-reproducible, so the file uploaded must be the one the script produced. `.gitattributes` stores the two signed files byte-exact; a line-ending conversion would break the signature.

## Limits and deferred work
- Only `FlutterDeck.exe` is updated. A release that needs new files beside it (for example a changed bundled loop) needs a fresh zip download; archive updates are deferred. Example: an install that updates itself from a version without Dawn Launch and Skyway Run does not receive those two files, and plays the loop it already has. No such install exists yet (no release has been published).
- If the private key is lost, installed copies cannot be given signed updates and testers must download a new build by hand. Key rotation is not built.
- The launcher file has no Windows publisher (Authenticode) signature, so Windows SmartScreen may warn testers on first run. Code signing is a separate developer decision.
- No health check after restart and no automatic rollback; rollback is manual.
- The manifest and its signature are two files fetched separately. For a few minutes after a release is pushed, GitHub's cache can serve a mismatched pair, which the launcher reports as "failed its signature or format check"; it clears by itself. A server answer other than success is reported as unreachable.
- Prerelease channels, private-repository access and proxies needing credentials are not supported.

## Verification
Command: `powershell -NoProfile -File .\check-updater.ps1`
- Verified by pure tests on scratch files with a throwaway key: signature accept and reject cases, manifest rules, download-address and redirect allowlists, the offer rule, install, refused installs leaving the launcher untouched, a failed swap keeping the earlier rollback copy, read-only files, rollback, cleanup, preferences.
- Verified in a scratch copy with the real key: `release.ps1` signs a manifest the launcher's code accepts (it now runs that code on the manifest before writing it), refuses a wrong or unreadable key and notes with control characters while leaving nothing behind, and refuses a sequence that did not go up.
- Observed in a scratch build through UI Automation: startup check reports unreachable (the real address returns 404 while the repository is private), the toggle saves off and on, "Check now" runs, and the install, restart and rollback buttons are disabled when there is nothing to act on.
- The independent check on 2026-11-16 found no way to get an unsigned or altered file installed; its findings in the screen flow, release script and file handling were fixed and are covered by the tests above. The pending-restart behaviour of the buttons is from code reading and tests of the file operations, not from a live install.
- NOT verified, because nothing is published: a successful live check, a real download through GitHub's redirects, an install and restart of the real launcher, rollback from the screen, and behaviour on other PCs, behind proxies or in read-only folders.

## Rehearsal on the developer's PC, 2026-11-19

So nothing was published and no repository was changed; the updater was proven on this PC, and three faults that would have stopped a first release were fixed.

What was run:

- **A whole update in a scratch copy.** Two scratch launchers were built from the present source, release 1 (0.3.0)
  and release 2 (0.3.1). For this rehearsal only, the scratch copies had a throwaway key and the address of a feed on
  `127.0.0.1` in place of the real key and the GitHub addresses, and the "HTTPS, github.com" rule of the download
  address was set to that local address. Driven without mouse or keyboard, release 1: found release 2 by itself at
  start; "Download and install" fetched the file, checked signature, length and SHA-256, and swapped the files
  (`FlutterDeck.exe.old` kept); "Restart now" started release 2 with the same data folder; "Roll back" (after its
  question) put release 1 back; after the next restart release 2 was not offered again by itself (`hold=2`), and
  "Check now" offered it; at the next start the leftover file was gone. The developer's real data folder was untouched.
- **The real release script with the real key**, in a scratch copy of the repository: build, every check, signing,
  the launcher's own acceptance of the manifest, the installer and the portable zip. `check-updater.ps1` then verified
  the signed pair. The scratch output was deleted afterwards; it was never the release to publish.

Faults found and fixed:

1. "Restart now" started the new copy without its options, so a launcher with its own data folder (`--data-dir`)
   came back with the usual data folder. Fixed (see above). `DeckPathsPureTest.cs` tests it; no check script ran that file before, so `check-paths.ps1` was added for it.
2. `release.ps1` loaded `Updater.cs` alone, but that file needs `DeckPaths.cs` since the data-folder work. Every
   release stopped with "Nothing was prepared". The same line in `check-updater.ps1` would have failed every run of
   that check once a manifest was published. Both fixed; the check now tries the load on every run.
3. `build-setup.ps1` compiled the installer without `DeckPaths.cs`, so the installer did not compile. Fixed;
   `check-installer.ps1` now builds the installer around a stand-in file on every run.

Still NOT verified, because nothing is published: a check against the real GitHub addresses, a download through
GitHub's redirects, the HTTPS and host rules on a live answer (they are covered by the pure tests only), other PCs,
proxies, read-only folders, and Windows SmartScreen on the unsigned files.

## Going live (when the developer decides where updates come from)

The updater reads its files without a login, so the place must be public. Two ways:

- **A public releases repository** (the source stays private): create it, change `ManifestUrl`, `SignatureUrl` and
  `DownloadPrefix` in `Updater.cs` and the matching pins in `check-updater.ps1`, then release as below, committing
  `update\manifest.xml` and `update\manifest.sig` to that repository instead of this one.
- **This repository made public:** no code change. Before that, note that three old commits name the developer's Windows
  account folder; a public history cannot be taken back.

Then, each release: raise `Version` and `Sequence`; run `release.ps1` with the key; create the GitHub release
`v<version>` with the three prepared files; only after that push the two signed files. The first release (0.3.0,
release 1) gives testers the installer; installed copies see an update from release 2 on.

Before the first release, find out whether any copy has left this PC. Every build since 2026-11-16 calls itself "0.3.0 (release 1)", also the test package in the Downloads folder. A copy that someone else already has would answer a first release 1 with "you have the latest version". If one is out, make the first public release number 2.
