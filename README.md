# aion2-map-overlay

A Windows overlay that uses the visible Aion 2 map to place resource and collectible icons over their known spawn locations. It follows the map as you pan and zoom, with a configuration window for choosing categories and adjusting icons.

The application determines map alignment from screen images captured through Windows. It does not read game memory, inspect game files, capture network traffic, inject code, or send input to the game. Marker locations come from separately sourced map datasets, not from the running game. A marker shows a possible spawn location, not whether a resource is currently available.

## Status

First local Verteron test build: 4,733 gatherable and collectible locations across
15 independently selectable groups. Includes terrain matching, optical-flow pan/zoom
tracking, a spatial index, click-through drawing, map-open detection, and saved settings.
Uses 14 downloaded icons and a replacement drawing for Hidden Cube.

The current capture backend uses downsampled GDI desktop snapshots, bounded to one
frame at a time, targeting 30 updates/second while tracking. Marker drawing uses
a 24 ms transition between confirmed positions, with immediate resets on lost
alignment and large movements. It does not record video.
GPU-backed capture remains a planned improvement. It pauses outside an Aion window
and suppresses stale or unregistered markers. The first preview is tuned to the
supplied English Verteron screenshots at 16:9; rapid live dragging and other display
layouts still require user testing. Collected-state editing is not implemented yet.

Download the Windows x64 portable ZIP from [GitHub Releases](https://github.com/NoRhythmDEV/aion2-map-overlay/releases).
Extract it to a writable folder and run `aion2-map-overlay.exe`. On first run,
click **Download map data**, then **Calibrate map** and return to Aion 2's open
Verteron map during the countdown. This creates a local title reference after
terrain validation. Existing users can copy their `data` folder and
`settings.local.json` from a previous local build to skip setup.

Map data and artwork download directly from Questlog for local use; they are not
included in the public ZIP or source repository. Screenshots remain local.
After setup, tracking works from the cache without ongoing website downloads.

The app checks GitHub Releases on startup by default and offers **Install &
restart** for newer stable versions. Startup checks can be disabled. Updates
verify the ZIP checksum and individual file hashes, preserve local data/settings,
and attempt rollback if file replacement fails. GitHub hosts the update files;
there is no separate update server. See [release maintenance](docs/releases.md).

Settings use a three-column resource grid, switching to four columns in a wider
window. Click a resource tile to toggle it; enabled tiles have a cyan border and
check mark. Search and bulk category controls work with the same saved filters.

An optional **Nearest resource direction** toggle adds a north-up ring around the
player's minimap position while the full map is closed. It targets enabled
gatherables, using native-resolution minimap terrain matches at up to 10 Hz.
Choose Default or Larger minimap to match the in-game preset; the larger preset
is initially selected and guidance starts disabled. The player is assumed to be
at the minimap center. Uncertain matches hide the indicator. This first version
has been checked against the two supplied screenshots; live movement and other
UI layouts still need testing. It does not detect gathering or live availability.

## Structure

```text
src/
  Aion2MapOverlay.App/       WPF overlay and settings interface
  Aion2MapOverlay.Capture/   Windows screen capture and frame lifetime
  Aion2MapOverlay.Vision/    Map detection, matching, and pan/zoom tracking
  Aion2MapOverlay.Core/      Coordinates, marker models, and settings
  Aion2MapOverlay.Data/      Dataset import, local cache, and spatial lookup
tests/                      Accuracy and data validation tests
assets/icons/               Licensed marker artwork
data/sources/               Original datasets, excluded from Git
data/processed/             Prepared map data, excluded from Git
scripts/                    Build and portable packaging
docs/                       Architecture, milestones, and data sources
```

## Build

Install the .NET 8 SDK with Windows desktop build support, then run:

```powershell
dotnet build Aion2MapOverlay.sln -c Release
```

For a local portable folder, prepare the ignored map assets described in
`docs/data-sources.md`, then run `scripts/publish-portable.ps1`. The Windows x64 build
includes the .NET runtime. Keep its DLLs and data folder beside the executable;
no installer is needed. Do not publicly redistribute the included Questlog data
without establishing permission.

For the public application-only package, run `scripts/package-release.ps1`.
It builds a self-contained ZIP, update manifest, and SHA256SUMS, then runs
offline smoke tests and updater replacement/rollback checks.

Verification tools are built into the executable:

For screenshots with markers, open the in-game map and press **F8** to freeze the
overlay, use **Win+Shift+S**, then press **F8** again to resume tracking. Keep the
map still while frozen. The settings window also has a screenshot-mode button
with a three-second countdown. Capture pauses during this mode so recognition
does not see its own icons; normal tracking excludes the overlay from capture.

```powershell
aion2-map-overlay.exe --verify OUTPUT_FOLDER SCREENSHOT_1 SCREENSHOT_2
aion2-map-overlay.exe --self-check OUTPUT_FOLDER SCREENSHOT
aion2-map-overlay.exe --check-minimap OUTPUT_FOLDER GAMEPLAY_SCREENSHOT_1 GAMEPLAY_SCREENSHOT_2
```

The first command prepares the local map-title template from the first supplied
screenshot and writes Sapphire previews and matching reports. The second checks
coordinate conversion, spatial queries, simulated panning/zooming, map-close
detection, desktop capture, icons, and overlay capture exclusion. Use quoted paths
when they contain spaces. These commands explicitly write local diagnostic files.

See [the implementation plan](docs/implementation-plan.md) and [data sources](docs/data-sources.md).
