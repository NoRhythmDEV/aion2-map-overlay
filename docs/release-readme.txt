Aion 2 Map Overlay - Windows x64 portable

Extract the ZIP into a writable folder, then run aion2-map-overlay.exe.
Keep the accompanying DLLs and runtime files beside the executable.
No installer or separate .NET installation is needed.

This public package contains the application, runtime, and component notices.
It does not include Questlog marker data, map artwork, icons, or user screenshots.
On first run, click Download map data to fetch Verteron markers, map artwork,
and resource icons directly from Questlog. Then click Calibrate map and return
to Aion 2's open Verteron map during the 3-second countdown. Calibration creates
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

Source, release notes, and data setup:
https://github.com/NoRhythmDEV/aion2-map-overlay

Third-party notices: THIRD-PARTY-NOTICES.txt and licenses/.
