# Feature Deck specification
## Assumptions/objective
Extend the actual native shell, preserving ship art, into a comprehensive categorized proposal browser. This is a launcher configuration concept, not game implementation. No claim to cover every historical request. Verified community findings can later be attached to individual proposals. Defaults are conservative and classic-first; experimental engine-dependent ideas explicitly marked.
## Commands/structure
Build: powershell -NoProfile -File .\build.ps1
Catalog check: powershell -NoProfile -File .\check-catalog.ps1
FeatureCatalog.cs: data model and declared proposals; Program.cs: native UI; check-catalog.ps1: data/source checks. Existing updater specification remains separate.
## Acceptance
Search and category filters narrow native rows; each feature has stable ID, title, proposal/dependency label and selectable concept options. Choices stay only in memory and survive navigation; reset clears them. Advanced Settings (screen id Advanced; until 2026-11-20 this was the Settings screen) opens Feature Deck. Saves opens its category (Workshop and Gallery were removed on 2026-11-19 on the developer's word). A top-deck Features shortcut does not obstruct existing window controls. Existing updater remains disconnected. No game process, filesystem, network, account or publication code.
## Coverage
Library/profile/onboarding; graphics/display; controls/camera; accessibility; saves/recovery; mods; gameplay/QOL; media/audio; community/challenges; diagnostics/privacy. Separate launcher UI possibilities from engine changes and service dependencies. Rewind, arbitrary framerate, multiplayer and mod compatibility are not promised.
## Verification boundaries
Compile plus stable-ID/category/options/schema/source checks. Native screenshot, control interaction, scaling and accessibility validation require actual permitted runtime. Existing network-policy failure is preserved. Do not run via alternative route to evade it or claim the new UI was observed.

## Compatibility-aware preview revision
No new feature IDs: reuse existing controls/profile/presentation proposals. Every row shows selected game and edition context with support explicitly unknown. Unavailable-fixture mode disables preview selection and explains that it is simulated, not a measured incompatibility. Choices are isolated by game and edition only in memory. Original-style controls preset changes seven existing classic/default control options, not every control; inversion/rumble and edition-specific original behavior are not asserted. No compatibility detection, game launch or remote requests.

## Readability / resizing revision
Developer requests better scrollbar, less text, bigger text, resizable deck. Keep one concise preview/unknown-support notice; move full per-feature explanations into tooltip/accessibility help instead of repeating four lines. Increase row titles and control typography. Use a wider themed scrollbar with a large thumb. Add a visible lower-right native resize grip that scales the ship proportionally; retain maximize/restore and native editable text. No integration changes. Runtime appearance and drag/scroll interactions remain unverified until permitted capture.
