# Data sources

Downloaded community YAML files are stored locally under data/sources/community-map,
with source revision and attribution in provenance.json. Prepared coordinate indexes
are under data/processed/community-map. Both directories are excluded from Git.

Run `python scripts/prepare-community-markers.py` after downloading the sources to
rebuild the indexes. This script extracts only flat coordinate and category fields;
it is not a general YAML parser and does not import nested descriptions or images.

## Verified coverage, 11 October 2026

The user supplied Questlog's public `map.getMarkers` tRPC endpoint with language=en.
Downloaded its response to data/sources/questlog/markers.en.raw.json and validated
88,048 unique records across 425 map IDs. Run
`python scripts/prepare-questlog-markers.py` to create per-map JSON and a manifest
under data/processed/questlog. The manifest records a SHA-256 and original source.
Map 1010 contains exactly 19,990 records, including 2,733 gatherables and 192
Sapphire Gemstone records. Preserve original coordinates and icon references;
map identity and orientation were subsequently confirmed as described below.
The response includes icon paths, not downloaded icon images.
Redistribution permission has not been established; these files remain local
and ignored by Git. This supersedes the earlier unsuccessful export inspection below.

Subsequent inspection of Questlog's public map configuration confirms 1010 is
Verteron (World_L_A). Its 8×8 sectors are 102,000 world units each. The image's
upper-left corner is world (-408000,+408000); lower-right is (+408000,-408000).
The reference image is 8192×8192, resized to 2048×2048 for feature matching.
Coordinate conversion is implemented in QuestlogData.LoadVerteron.
Use `python scripts/fetch-questlog-assets.py` to prepare the ignored local assets.
It downloads website data only and does not access the running game.

Revision 89ce3984d569ff1a03ee6311d064231211212981 contains 841 World_L_A
markers, 832 World_D_A markers, and 199 Abyss_Reshanta_A markers. World_L_A
includes 560 monolithMaterial and 118 hiddenCube records, plus location markers.
Its category definitions include gatheringSapphireGemstone, but the downloaded
marker files contain no gathering records. Category definitions alone must not
be presented as available spawn coordinates.

InteractiveMap's public Verteron options JSON exposes categories including Sapphire,
but the Sapphire category's plain data array is empty. A complete coordinate export
has not been obtained. Questlog's marker coordinate export also remains unverified.
The downloaded alternative is therefore a partial dataset, not parity with either site.

- https://github.com/aion2-interactive-map/aion2-interactive-map — candidate downloadable dataset. Its README licenses public/data under CC BY-NC 4.0, with attribution required for noncommercial reuse. Verify individual files, coverage, coordinate conventions, and notices before import. Its source code has a separate GPL-3.0 licence; using data does not require copying its code.
- https://interactivemap.app/aion2/maps/verteron — comparison source for category coverage. Public visibility alone does not establish an export or redistribution licence.
- https://questlog.gg/aion-2/en/map — comparison source; no supported data export has been verified.

Keep provenance, source revision, licence, attribution, and any coordinate conversions with each prepared dataset. Do not claim complete parity with another site's database until counts and locations have been compared. Do not merge datasets without checking duplicates and usage terms.
