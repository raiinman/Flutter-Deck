# Flutter Deck native desktop shell
## Correction and assumptions
Developer explicitly rejects browser mockup: deliver an actual Windows desktop executable. Use installed C#/WPF .NET Framework toolchain, no WebView/browser. Approved ship artwork is text-free decoration; all labels/buttons are live native UI. No game/save-file/accounts/network wiring. Preserve earlier browser artifact separately, do not represent it as this application.
## Objective
A launchable native shell matching red/blue Flutter ship with content between upper and lower decks. One capability: offline UI shell; fixture panels are not implemented game systems.
## Commands
Build: powershell -NoProfile -File .\build.ps1
No executable self-test command is provided in this visual revision.
Run: .\FlutterDeck.exe
## Structure
Program.cs native UI; assets/ship.png embedded decoration; build.ps1; tasks/plan.md and todo.md; README.md.
## Style
C# 5-compatible clear methods, native WPF controls. Example: `void Navigate(string screen) { currentScreen = screen; Render(); }`.
## Testing
Compiler result, source scope checks, native-window visual and interaction review where available. Confirm launch guard and controls have no file/network/process action. Separate self-test from observed UI results.
## Boundaries
Always: real editable text, keyboardable controls, explicit preview labels. Ask first: actual games, accounts, disk save access, native integrations, distribution signing. Never: game launch, user file scan, download service, persistence or credentials.
## Acceptance
Windows .exe builds and runs independently of browser; six panels navigate; MML1/2 selection; guarded launch; close/minimize/maximize; live red/blue text and controls; ship upper and lower deck visible; no backend.
## Open decisions
Compilation is available. Live UI verification is blocked by the preserved execution-session network-policy failure. Native game contracts and signing deferred.

## Dream Loop revision
Developer supplied reference replaces approximate proportions. Match its ship framing, narrow icon navigation, two illustrated cards and large hero; remove exterior sky with alpha. Keep genuine native WPF controls and editable labels. Transparent custom window with close/minimize/drag affordances; no backend. Exact Pro skill read; supplied target locked in .dream-loop/target.png. Live screenshot judge required; no score until screenshot exists.

## Compact body acceptance (developer screenshot revision)
More reading space, not merely smaller text: enlarge non-Library body by 69 design units, collapse advanced/context/window controls by default, place results before paging. Preserve top/lower ship decks, local references, proposed/unverified distinction and adjustable/resettable body size. Compact native button states and consistent nav gaps. Source/helper acceptance passed; live visual acceptance pending.

## Scrollbar and navigation refinement
Scaling is second in the left menu, removed from the upper deck. Seven 46-unit native rows separated by 7-unit gutters fit the existing left bay; opaque backing masks the old illustrated six-row layout.
Scrollbar skin preserves the native ScrollBar template, Track, arrows, paging commands, range/viewport/value binding, drag handling and ScrollViewer. Only the native thumb's visual template is recolored blue/teal with high-contrast hover/drag highlight, and (2026-11-16) the four native line-arrow buttons, found by their scroll command, get a dark face with a cream arrow, cyan on hover; the buttons, their commands and repeat behaviour stay native and the page areas in the track are untouched; the scrollbar border brush is set to the track colour so the theme's pale side lines do not show; resource brushes tint supporting native surfaces where the Windows theme honors them. Existing pixel-wheel and touch panning retained. Actual theme appearance, mouse/touch/keyboard interactions remain unverified without allowed live GUI evidence.
