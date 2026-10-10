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
Icon size, opacity, and selected groups are saved in settings.local.json.
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
