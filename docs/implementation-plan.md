# Implementation plan

## Frame processing

Use Windows Graphics Capture with D3D11 for GPU-backed frames. Capture the selected display; crop the map area and exclude fixed UI from recognition. Do not record video or save screen images by default. Keep one pending frame and replace it when a newer frame arrives. Exclude the overlay and settings window from capture where supported and verify this before live matching.

Detect the open map with fixed UI cues and region identification. Match terrain against precomputed reference features using OpenCV. Estimate scale and translation with robust outlier rejection, validate spatial coverage and residual error, and reject implausible transforms. Do not let fixed screen text or sidebar elements determine alignment.

Use fast local feature tracking between full matches to follow panning. Reacquire on zoom changes, lost tracking, region changes, or low confidence. Match at reduced resolution; refine coordinates for rendering. Expensive full-map matching must not run on every captured frame.

## Marker drawing

Load category and marker data once per region. Build a spatial grid or tree, invert the current map transform to find the visible reference bounds, and query only those markers. Pan and zoom update coordinates and visibility; they do not fetch new data from a website. Cache decoded icons and use a single custom WPF drawing surface rather than one control per marker.

Draw in a transparent, non-activating, click-through window. Clip icons to the map viewport and exclude game UI regions. Convert capture pixels and WPF device-independent units explicitly, including per-monitor DPI and display origins. Hide immediately when the map closes or registration is stale. Avoid extrapolating through long gaps during fast dragging.

## Performance and accuracy targets

Initial goals, to be measured rather than assumed: tracking updates at 20–30 Hz while the map is moving; idle recognition throttled; at most one pending frame; no disk writes in the frame loop. Rendering remains independent of recognition. Measure capture time, tracking time, frame age, CPU/GPU usage, and registration error. Reduce frame size or frequency based on measured load.

Verify alignment against held-out screenshots at different zoom levels and map positions. Aim for median error below 5 pixels and 95th percentile below 10 pixels at native resolution. Record confidence, loss of tracking, and time to reacquire during quick panning. Suppress icons rather than display a misleading transform. These are acceptance targets, not current results.

## Milestones

1. Inspect licensed datasets and coordinate systems; prepare Verteron reference imagery and existing marker categories.
2. Prove terrain registration on supplied screenshots, including zooms with native icons hidden. Add geometry and held-out accuracy checks.
3. Implement bounded screen capture and map-open detection; verify that the overlay cannot contaminate capture.
4. Add pan tracking, zoom reacquisition, spatial queries, and visible-area drawing. Profile fast dragging.
5. Add runtime category filters, icon size/opacity, collected-state persistence, and local configuration.
6. Publish a self-contained portable folder and test on a Windows machine without the SDK.

The first milestone covers Verteron. Expand to other regions only after registration and coordinate conversion work reliably. Player-arrow detection is optional for this overlay: terrain alignment already places resource markers correctly.

## Optional minimap direction prototype

The gameplay indicator uses separate native-resolution screen crops for the two
supplied minimap presets. A 4096px reference, SIFT features, and robust similarity
registration locate the crop center on the existing 2048px marker reference.
Reject weak inlier coverage, high residuals, or rotation beyond 5 degrees. The
player is assumed to stay centered in the minimap. Runtime preset selection is
required: a partial crop of the larger minimap can still match terrain correctly
while giving the wrong player center if Default is selected.

Query enabled gatherables and retain a previous target while it remains within
10% of the nearest distance to reduce target flicker. Render a north-up ring on
the minimap with a direction pointer and node name. This is straight-line guidance
to a possible spawn; gathering detection, node skipping, obstacle routing, and
camera-relative directions around the main character are not implemented.

Guidance is opt-in and saved locally. Its analysis runs at up to 10 Hz only while
Aion is foreground and the full Verteron map is closed. Disable the toggle to
stop minimap analysis and release the extra matcher on the worker's next active
iteration. Confidence loss or stale frames hide the ring within 500ms. Existing
capture exclusion covers the ring, so it cannot feed back into screen matching.
