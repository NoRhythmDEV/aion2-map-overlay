# aion2-map-overlay

A Windows overlay that uses the visible Aion 2 map to place resource and collectible icons over their known spawn locations. It is designed to follow the map as you pan and zoom, with a configuration window for choosing categories, adjusting icons, and hiding locations you have collected.

The application will determine map alignment from screen images captured through Windows. It will not read game memory, inspect game files, capture network traffic, inject code, or send input to the game. Marker locations will come from separately sourced map datasets, not from the running game. A marker shows a possible spawn location, not whether a resource is currently available.

## Status

Initial project structure and implementation plan. Screen capture, recognition, dataset import, and overlay rendering are not implemented yet. The current WPF project is only the generated application shell.

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

Portable packaging will target Windows x64 with a self-contained .NET runtime. The executable, native vision dependencies, map data, and settings can live in one portable folder. No installer is planned.

See [the implementation plan](docs/implementation-plan.md) and [data sources](docs/data-sources.md).
