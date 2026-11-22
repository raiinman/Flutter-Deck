using System;
using System.Collections.Generic;

public sealed class Feature {
  public string Id,Category,Title,Dependency;
  public string[] Options;
  public string Description {
    get {
      switch(Id){
        case "library-onboarding": return "Implemented local setup references and bounded metadata checks. This does not identify the game revision or connect a native runtime.";
        case "audio-soundtrack": return "Implemented local PCM WAV playback with original loop and session playlist; not an included game soundtrack.";
        case "input-style": return "Classic preserves the proposed original movement style; twin-stick and camera-relative modes need game-specific implementation.";
        case "input-orbit": return "Orbit would decouple camera rotation from movement; camera collision and controls differ by game and edition.";
        case "input-lock": return "Keep classic lock-on as a baseline; target cycling requires a separately verified targeting implementation.";
        case "input-invert": return "Horizontal and vertical axes are proposed independently. Original defaults must be verified for each edition.";
        case "input-deadzone": return "Tune stick thresholds without assuming the same device range or response curve across controllers.";
        case "input-speed": return "Preview camera sensitivity presets; frame-rate-independent response is not implemented.";
        case "input-remap": return "Action mappings require conflict detection and a recoverable reset before integration.";
        case "input-glyphs": return "Prompt artwork selection is separate from real device detection and mapping.";
        case "input-mouse": return "Mouse look needs camera integration; selecting it here does not capture the mouse.";
        case "input-gyro": return "Research-only motion aiming; no device support or game compatibility established.";
        case "input-rumble": return "Strength choices are conceptual; no vibration command is sent.";
        case "input-hotplug": return "A future reconnect flow must preserve mappings and avoid surprise game input.";
        default: return "Design scope: "+Title+". Required work: "+Dependency+". No support is verified for either game or any edition.";
      }
    }
  }
}
public static class FeatureCatalog {
  // 82 proposals and 2 links to implemented launcher tools. No game-runtime integration.
  public const string Data=@"library-profile|Library|Per-game profiles|Launcher UI|Classic;Modern;Custom
library-favorites|Library|Favorites and collections|Launcher UI|All games;Favorites;Custom collection
library-onboarding|Library|Owner-supplied game setup wizard|Implemented local references; game identity unknown|Open setup
library-integrity|Library|Game version and integrity report|File integration deferred|Not checked;Verification concept
library-offline|Library|Offline-first library|Launcher architecture|Offline-first;Online features opt-in
library-resume|Library|Continue from last session|Game integration required|Disabled;Resume concept
library-history|Library|Playtime and session history|Game integration required|Disabled;Local-only concept
library-bigpicture|Library|Controller-friendly couch layout|Launcher UI|Desktop;Couch concept
library-notes|Library|Per-game notes and tags|Storage deferred|Disabled;Local notes concept
gfx-resolution|Graphics|Resolution and render scale|Engine work required|Original;1080p;1440p;2160p;Custom
gfx-aspect|Graphics|Aspect ratio and ultrawide|Engine work required|Original 4:3;16:9;21:9;32:9
gfx-window|Graphics|Display mode|Game integration required|Windowed;Borderless;Fullscreen
gfx-rate|Graphics|Framerate target with timing validation|Experimental engine work|Original timing;60 FPS target;120 FPS target;Uncapped research
gfx-pacing|Graphics|VSync and frame pacing|Engine work required|Default;VSync;Low latency concept
gfx-fov|Graphics|Field of view|Engine work required|Original;Wider;Custom
gfx-filter|Graphics|Texture filtering|Engine work required|Nearest;Bilinear;Anisotropic
gfx-style|Graphics|Faithful versus enhanced presentation|Engine work required|Faithful;Enhanced;Per-feature
gfx-aa|Graphics|Antialiasing and image sharpness|Engine work required|Original;AA concept;Sharpen concept
gfx-hud|Graphics|HUD scale and safe area|Engine work required|Original;Large;Ultrawide-safe
gfx-crt|Graphics|Optional CRT and scanline effects|Engine work required|Off;Subtle CRT;Scanlines
gfx-hdr|Graphics|HDR and color calibration|Experimental engine work|SDR;HDR research
input-style|Controls|Classic versus modern movement|Engine work required|Classic;Twin-stick;Camera-relative
input-orbit|Controls|Right-stick camera orbit|Engine work required|Classic;Free orbit
input-lock|Controls|Lock-on and target switching|Engine work required|Classic lock-on;Target cycle concept
input-invert|Controls|Independent camera inversion|Engine work required|Neither;Vertical;Horizontal;Both
input-deadzone|Controls|Stick dead zones and response|Engine work required|Default;Small;Large;Custom curve
input-speed|Controls|Camera speed and sensitivity|Engine work required|Default;Slow;Fast;Custom
input-remap|Controls|Keyboard/controller action remapping|Input integration deferred|Default;Custom mapping concept
input-glyphs|Controls|Controller button glyphs|Game integration required|Auto;Xbox;PlayStation;Nintendo;Keyboard
input-mouse|Controls|Mouse look and relative input|Engine work required|Classic;Mouse-look concept
input-gyro|Controls|Gyro aiming|Experimental input work|Off;Gyro research
input-rumble|Controls|Rumble strength|Input integration deferred|Off;Low;Full
input-hotplug|Controls|Device hot-plug and reconnect|Input integration deferred|Prompt;Automatic concept
access-text|Accessibility|Text and subtitle sizing|Engine work required|Original;Large;Extra large
access-contrast|Accessibility|High-contrast HUD and focus|Engine/UI work required|Default;High contrast
access-color|Accessibility|Color-vision-friendly cues|Engine work required|Default;Shape-plus-color cues
access-motion|Accessibility|Reduced camera shake and motion|Engine work required|Original;Reduced;Off
access-flash|Accessibility|Flash-intensity reduction|Engine work required|Original;Reduced-flash concept
access-hold|Accessibility|Hold/toggle input alternatives|Engine work required|Hold;Toggle;Per-action
access-caption|Accessibility|Speaker labels and sound captions|New authored content required|Original;Expanded captions concept
access-reader|Accessibility|Screen-reader-friendly launcher|Launcher UI validation required|Native labels;Expanded descriptions
access-focus|Accessibility|Pause on focus loss|Engine work required|Original;Pause concept
save-slots|Saves|Named slots and save preview|Save integration deferred|Original;Named-slot concept
save-backup|Saves|Backups and version history|File integration deferred|Disabled;Manual backup;Rolling backups
save-auto|Saves|Autosave at safe checkpoints|Engine work required|Original;Checkpoint autosave concept
save-recover|Saves|Corruption detection and recovery|Save integration deferred|Not checked;Recovery concept
save-import|Saves|Import/export legacy saves|Compatibility research required|Disabled;Import preview;Export preview
save-cloud|Saves|Optional cloud sync and conflicts|Service integration deferred|Off;Manual conflict resolution concept
save-rewind|Saves|Rewind and instant snapshots|Experimental engine work|Off;Research only
save-separate|Saves|Separate modded save profiles|Save/mod integration deferred|Shared concept;Separate profiles
mods-profiles|Mods|Per-game mod profiles|Mod runtime deferred|Vanilla;Modded profile concept
mods-order|Mods|Load order and dependency graph|Mod runtime deferred|Manual concept;Dependency-aware concept
mods-conflicts|Mods|Conflict and compatibility warnings|Mod runtime deferred|Not checked;Conflict preview
mods-revert|Mods|Safe disable and vanilla recovery|File integration deferred|Disabled;Recovery concept
mods-trust|Mods|Package permissions and provenance|Security implementation deferred|Block unknown;Review explicitly
mods-textures|Mods|Optional texture/model packs|Asset rights and engine work|Original;Owner-provided pack concept
mods-localization|Mods|Language and translation packs|Content rights and engine work|Original;Translation-pack concept
mods-authoring|Mods|Documented mod SDK and validation|Engineering tooling deferred|Not available;SDK plan
qol-skip|Gameplay|Skippable cutscenes and repeat dialogue|Engine work required|Original;After-first-view concept
qol-map|Gameplay|Map markers and optional objective hints|Engine/content work required|Original;Optional hints
qol-retry|Gameplay|Quick retry and safe checkpoint restart|Engine work required|Original;Quick retry concept
qol-camera|Gameplay|Camera collision and occlusion tuning|Engine work required|Original;Improved-camera concept
qol-difficulty|Gameplay|Difficulty and assist presets|Game design/engine work|Original;Assist concept;Challenge concept
qol-ngplus|Gameplay|New Game Plus and challenge rules|Game design/engine work|Original;NG+ concept
qol-speedrun|Gameplay|Speedrun timer and practice rules|Engine/tool integration deferred|Off;Timer concept;Practice research
qol-randomizer|Gameplay|Seeded randomizer|Experimental game-design work|Off;Research only
media-photo|Media|Photo mode and free camera|Engine work required|Off;Photo-mode concept
media-gallery|Media|Screenshot gallery and export|File integration deferred|No files read;Gallery concept
media-spoilers|Media|Spoiler-safe artwork and galleries|Content curation required|Spoiler-safe;All art concept
media-capture|Media|Capture hotkeys and clean HUD|Engine/OS integration deferred|Disabled;Screenshot concept;Clip concept
audio-mix|Audio|Music/dialogue/effects mixer|Engine work required|Original mix;Custom mix concept
audio-range|Audio|Dynamic range presets|Engine work required|Original;Headphones;Night concept
audio-soundtrack|Audio|Local music player and playlists|Implemented original loop and local PCM WAV; no game soundtrack|Open music
audio-language|Audio|Voice/subtitle language pairing|Content rights and engine work|Original;Independent language concept
community-achievements|Community|Optional achievements|Engine/service integration deferred|Off;Local achievements concept
community-guides|Community|Spoiler-controlled guides|Authored/licensed content required|Off;Hints;Full guide concept
community-presence|Community|Optional rich presence|Service integration deferred|Off;Opt-in concept
community-challenges|Community|Challenges and leaderboards|Service/security work deferred|Off;Local challenge concept
community-multiplayer|Community|Co-op or multiplayer exploration|Major experimental engine work|Not promised;Research-only idea
diag-report|Diagnostics|Privacy-reviewed diagnostic report|File integration deferred|No collection;Manual review concept
diag-verify|Diagnostics|Compatibility and build provenance|Engineering integration deferred|Not checked;Report concept
diag-safe|Diagnostics|Safe mode without mods|Mod/runtime integration deferred|Disabled;Safe-mode concept
diag-privacy|Diagnostics|Telemetry consent and data controls|Service/storage deferred|Off by default;Explicit opt-in only
diag-performance|Diagnostics|Frame-time and performance overlay|Engine integration deferred|Off;Overlay concept";
  public static readonly List<Feature> All=Load();
  static List<Feature> Load(){var list=new List<Feature>();foreach(var line in Data.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries)){var c=line.Split('|');list.Add(new Feature{Id=c[0],Category=c[1],Title=c[2],Dependency=c[3],Options=c[4].Split(';')});}return list;}
}
