# Builds, releases, and updates

GitHub Actions builds on pushes to main, pull requests, and manual dispatch.
The Windows runner publishes a self-contained x64 app, tests native OpenCV,
verifies package extraction and tamper rejection, renders/checks the first-run
screen, and tests update application, user-data preservation, and rollback.
These checks require no Questlog downloads, game, screenshots, credentials, or
local data. CI artifacts are retained for seven days.

To publish an update:

1. Change `VERSION` to the next stable three-part version, such as `0.1.1`.
2. Commit and push the source changes. Check the Windows build result.
3. Create and push the matching tag: `git tag v0.1.1`, then `git push origin v0.1.1`.

The tag workflow builds and tests again, then publishes a stable GitHub Release
with `aion2-map-overlay-win-x64-portable.zip` and `SHA256SUMS`. The release tag
must match VERSION. Ordinary source pushes do not publish a stable release.
Workflow actions are pinned to commits; only the separate release job receives
contents-write permission. No personal access token is required in Actions.

Public ZIPs contain application/runtime files and component notices. Questlog
data/artwork and user screenshots/settings are not included. First-run setup
downloads required Verteron data directly from Questlog and validates its format.
One-time map calibration saves a title crop locally after terrain recognition.
Website outages/schema changes can prevent setup; completed caches remain usable.

## In-app updates

Startup checks make one unauthenticated HTTPS request to GitHub's latest-stable
release endpoint. Users can turn these checks off or check manually. The app
does not include GitHub credentials or query updates in its capture loop.
An update is offered only for a newer stable three-part version with the expected
ZIP and checksum assets from this repository. Clicking Install & restart:

1. Downloads the ZIP to a unique temporary staging folder and checks SHA-256.
2. Validates the manifest, version, file lengths and hashes, and extraction paths.
3. Starts the bundled Windows PowerShell helper, then closes the application.
4. Waits for this instance to exit, backs up replaced files, and installs the new
   application. Local data, captures, and settings cannot be overwritten by the
   manifest. Existing junctions/symlinks in target subdirectories are rejected.
5. Restarts the app. Failure triggers a best-effort rollback and writes
   `update-error.txt` beside the executable. Temporary staging/backup folders are
   retained for recovery rather than recursively deleting user paths.

Keep the portable folder writable and close other instances before updating.
Checksums provide download integrity; they are not Windows code signing.
The first updater-enabled release must be downloaded manually by users of older
local builds. Subsequent releases can be installed from within the app.

An installer is optional later for shortcuts and a managed installation location.
Portable updates already work without elevation or a separate hosting service.
