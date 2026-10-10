# aion2-map-overlay

A Windows overlay that uses the visible Aion 2 map to place resource and collectible icons over their known spawn locations. It follows the map as you pan and zoom, with a configuration window for choosing categories and adjusting icons.

The application determines map alignment from screen images captured through Windows. It does not read game memory, inspect game files, capture network traffic, inject code, or send input to the game. Marker locations come from separately sourced map datasets, not from the running game. A marker shows a possible spawn location, not whether a resource is currently available.

## Status

First local Verteron test build: 4,733 gatherable and collectible locations across
15 independently selectable groups. Includes terrain matching, optical-flow pan/zoom
tracking, a spatial index, click-through drawing, map-open detection, and saved settings.
Uses 14 downloaded icons and a replacement drawing for Hidden Cube.

The current capture backend uses downsampled GDI desktop snapshots, bounded to one
frame at a time, targeting 20 updates/second while tracking. It does not record video.
GPU-backed capture remains a planned improvement. It pauses outside an Aion window
and suppresses stale or unregistered markers. The first preview is tuned to the
supplied English Verteron screenshots at 16:9; rapid live dragging and other display
layouts still require user testing. Collected-state editing is not implemented yet.

Map data, reference artwork, and supplied screenshots are local and excluded from
Git. The public repository does not include a ready-to-run dataset or public binary.

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

Verification tools are built into the executable:

```powershell
aion2-map-overlay.exe --verify OUTPUT_FOLDER SCREENSHOT_1 SCREENSHOT_2
aion2-map-overlay.exe --self-check OUTPUT_FOLDER SCREENSHOT
```

The first command prepares the local map-title template from the first supplied
screenshot and writes Sapphire previews and matching reports. The second checks
coordinate conversion, spatial queries, simulated panning/zooming, map-close
detection, desktop capture, icons, and overlay capture exclusion. Use quoted paths
when they contain spaces. These commands explicitly write local diagnostic files.

See [the implementation plan](docs/implementation-plan.md) and [data sources](docs/data-sources.md).
