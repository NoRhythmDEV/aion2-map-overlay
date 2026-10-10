# First prototype validation

The terrain matcher registered all four supplied screenshots, with 232–1,134
inlier feature matches. Median feature residuals were 0.25–0.42 pixels in the
1600-pixel-wide processing frame. Full matching took approximately 150–170 ms
in the initial run. These residuals measure matched terrain features, not
independently surveyed spawn-position accuracy.

The self-check compares 100 indexed region queries against brute-force queries,
checks the expected 4,733 gathering/collection records and 192 Sapphire records,
and verifies a coordinate round trip. A synthetic 25/-12 pixel pan tracked in
approximately 7–8 ms with a translation error below 0.01 pixels. An 8% zoom was
recovered within 0.02 percentage points. These controlled transformations do
not establish performance during arbitrary live dragging.

Local desktop snapshot capture measured approximately 17–20 ms at 1600×900.
Map-close blank-frame rejection, 15 icon groups, settings rendering, and Windows
capture-exclusion acceptance are checked. Live resource availability is not
observable through the static marker data.

Still to test: live game rapid pans and zooms, false positives over other UI,
per-monitor DPI combinations, other aspect ratios, other map regions, extended
CPU/GPU use, and portable startup on a machine without .NET installed.
