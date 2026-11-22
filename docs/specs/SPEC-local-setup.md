# Local setup integration
Developer now authorizes finding local game/BIOS candidates and wiring feasible launcher functions. Supersedes simulated-only setup scope, not game execution/network/publication restrictions.

Allowed: user-triggered native file chooser; read file metadata and bounded CUE text; verify referenced tracks exist within the selected CUE directory; store per-game references in the current user's LocalApplicationData/FlutterDeck/setup.xml. No payload copies, hashes, uploads, installs or game execution. BIOS length/extension checks are shape checks, not identity, provenance, compatibility or legal proof. Both references required by developer setup policy; no verified technical BIOS runtime requirement asserted.

MML1 and MML2 references kept separate. Existing local config preserved if valid. Save transaction stages XML beside config and retains previous config backup. No private paths in source/docs/packages. Runtime game settings stay proposals. Launcher-only preferences may alter this window, never system settings or game files. Actual GUI runtime remains blocked; independent bounded helper tests are not native UI validation.
