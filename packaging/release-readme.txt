Aion 2 Map Overlay - Windows x64 portable

Extract the ZIP into a writable location. Open the aion2-map-overlay folder,
then run aion2-map-overlay.exe.
Keep the accompanying DLLs and runtime files beside the executable.
No installer or separate .NET installation is needed.

This public package contains the application, runtime, and component notices.
It does not include Questlog marker data, map artwork, icons, or user screenshots.
On first run, click Download map data to fetch Verteron markers, map artwork,
and resource icons directly from Questlog. Open the Verteron map, click Calibrate
map, read the instructions, then click OK and switch back to Aion 2 during the
3-second countdown. Calibration creates
a small local title reference after verifying the terrain. No game input is sent.
An internet connection is needed for setup; downloaded data is cached afterward.
If you already use a local test build, you can instead copy its entire data folder
beside this executable, and optionally copy settings.local.json, to skip setup.

The app checks GitHub Releases for newer stable versions on startup by default.
You can disable startup checks, or use Check for updates manually.
Install & restart downloads and verifies the portable package, closes the app,
and replaces application files. It preserves data and settings.local.json.
If replacement fails, the helper attempts to restore the previous files and
records the error in update-error.txt beside the executable.

The app observes normal Windows screen images and the foreground window title.
It does not read game memory or files, capture game network traffic, inject code,
or send game input. Update checks/downloads contact GitHub over HTTPS.
Known spawn locations do not imply that a resource is currently available.

The optional nearest-resource ring follows camera direction from the minimap view
cone. A node straight ahead points up on the ring. Guidance hides if the cone is
obscured or ambiguous; minimap icons can still be shown independently.
For screenshots of the map or gameplay overlays: F8, Win+Shift+S, then F8 again.
Screenshot mode freezes tracking until you resume.

For the supported larger minimap layout, click Larger minimap setup in the app
and Copy to clipboard. In Aion 2: ESC > Edit HUD > Load > Import Code, paste,
then Import and save. The preset imports the full HUD layout.

Temporary screen capture failures retry automatically while hiding stale icons.
Repeated failures stop tracking; details are in logs/overlay.log beside the app.

Source, release notes, and data setup:
https://github.com/NoRhythmDEV/aion2-map-overlay

Third-party notices: THIRD-PARTY-NOTICES.txt and licenses/.
