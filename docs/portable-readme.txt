Aion 2 Map Overlay — Verteron test build

Run aion2-map-overlay.exe from this folder. Keep the data folder and DLLs beside it.
No installer, administrator rights, Npcap, or separate .NET installation is needed.
OpenCV uses the Microsoft Visual C++ runtime normally present on gaming PCs;
startup on a clean Windows installation has not yet been tested.

1. Choose the display containing Aion 2.
2. Enable the resources or collectibles you want. Sapphire is enabled initially.
3. Click Start overlay, return to Aion 2, and open the Verteron map.
4. Pan or zoom. Icons follow the terrain when alignment is valid.
5. Return to this window to change filters or Stop. Closing it stops capture.

Supports all 13 gatherable and 2 collectible groups present in the Verteron data.
Click any resource tile to toggle it; enabled tiles have a cyan border and check.
The grid uses three columns, or four when the window is widened.
Tracking targets 30 updates per second, with short visual transitions between
confirmed positions. Large movements snap and lost alignment still hides icons.
Icon size, opacity, and selected groups are saved in settings.local.json.
The app window shows its own CPU use and RAM working set, refreshed each second.
CPU is normalized across all logical processors, like Task Manager's overall scale.
Optional nearest-resource direction:
- Enable "Nearest resource direction" and choose your in-game minimap preset.
- Larger minimap is the initial preset; guidance itself is off by default.
- While the full map is closed, a north-up ring on the minimap points toward the
  nearest enabled gatherable and shows its name. Collectibles are not targeted.
- The player is assumed to be at the minimap center. These two presets are tuned
  to the supplied 16:9 layout. Choose the correct preset; other UI layouts need
  calibration. Rotation beyond 5 degrees or uncertain matches hide the ring.
- Guidance updates at up to 10 Hz using a native-resolution minimap crop.
- Turning the toggle off stops minimap analysis; settings persist between runs.
- The pointer indicates a possible spawn, not a walkable route or availability.
  Gathering is not detected; harvested nodes are not automatically skipped.
- Both supplied screenshots pass matching checks. Live travel, camera changes,
  teleports, and other minimap zoom levels still require testing.
To screenshot the icons: with markers visible, press F8, use Win+Shift+S, then
press F8 again to resume tracking. Screenshot mode freezes markers and pauses
screen analysis. Avoid moving the map while it is frozen. The settings button
offers a 3-second countdown if F8 is already assigned to another application.
Hidden Cube uses a replacement cube drawing because its source icon was unavailable.
Test screenshot creates local annotated Sapphire previews and alignment reports.

This preview is tuned to the supplied English map UI at a 16:9 display ratio.
Other UI scales, aspect ratios, regions, and very fast drags need further testing.
Markers are intentionally clipped away from the sidebar, header, and right controls.
These are possible spawn locations, not live resource availability.

The program observes normal Windows screen images and the foreground window title.
It does not open the game process, read game files or memory, capture network
traffic, inject code, hook rendering, or send input. Live frames are not saved.
This first backend takes downsampled desktop snapshots; it does not encode video.

Attribution: marker data and map/icon references from Questlog.gg; Aion 2 game
content belongs to NCSOFT and respective owners. This local test package has not
been cleared for public redistribution. Do not upload its data/assets publicly.
OpenCvSharp and OpenCV are used for image matching; .NET and WPF provide the UI.
Third-party component notices are in THIRD-PARTY-NOTICES.txt.
