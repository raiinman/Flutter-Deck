# Local launcher foundation
## Objective
Make local setup status and launcher behavior reliable and understandable without claiming a runnable native game.
## Modules / acceptance
- local-setup: empty, missing/moved, metadata-invalid and reference-ready distinguishable; both game and BIOS required by developer policy. Files never moved/copied. CUE metadata validation remains bounded. No rights/identity/compatibility claim.
- launcher-preferences: persist text (existing), normal width/aspect, always-on-top and selected MML1/2. Bad/missing config safe defaults. Reset launcher preferences leaves setup paths intact. No automatic path publication.
- readiness-help: Play disabled for both titles because no verified native runtime is connected. Explain exact next step for references and runtime. Reopen setup selects current game. Help explains resize, sizing, local-only status. Session log accepts only fixed event names, no free text/paths/raw exceptions.
- native-shell: preserve compact ship and body. Recheck on explicit library entry (not polling). First run with no references opens setup. Config failure does not overwrite setup silently. Scroll resets on page navigation; dropdown wheel should not scroll body while popup open.
## Verification
powershell -NoProfile -File ./check-foundation.ps1; ./build.ps1; existing helper/layout tests. GUI acceptance remains pending existing blocked-route boundary: no bypass, no claimed measured smoothness/visuals.
## Exclusions
Game runtime validation/launch, updater network, auto discovery, cloud logs, asset copies, account wiring, OS settings. Reference-ready is not playable.
