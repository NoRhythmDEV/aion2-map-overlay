"""Validate and split a locally downloaded Questlog response; no network access."""
from collections import Counter, defaultdict
from datetime import datetime, timezone
import hashlib
import json
import math
from pathlib import Path

root = Path(__file__).resolve().parents[1]
source = root / 'data/sources/questlog/markers.en.raw.json'
output = root / 'data/processed/questlog'
raw = source.read_bytes()
markers = json.loads(raw.decode('utf-8-sig'))['result']['data']
if not isinstance(markers, list) or not markers:
    raise ValueError('Expected a nonempty marker array')
worlds = defaultdict(list)
ids = set()
for marker in markers:
    for key in ('id', 'name', 'coordinates', 'mapWorldId', 'mapType', 'mapCategory'):
        if key not in marker:
            raise ValueError(f'Missing field: {key}')
    if marker['id'] in ids:
        raise ValueError(f'Duplicate ID: {marker["id"]}')
    ids.add(marker['id'])
    world = str(marker['mapWorldId'])
    if not world.isdigit():
        raise ValueError('Unexpected map ID')
    if not marker['coordinates']:
        raise ValueError('Empty coordinates')
    for pair in marker['coordinates']:
        if len(pair) != 2 or not all(isinstance(v, (int, float)) and math.isfinite(v) for v in pair):
            raise ValueError('Invalid coordinate pair')
    worlds[world].append(marker)
output.mkdir(parents=True, exist_ok=True)
summary = {}
for world, records in worlds.items():
    (output / f'map-{world}.json').write_text(json.dumps(records, separators=(',', ':')), encoding='utf-8')
    summary[world] = {
        'markers': len(records),
        'types': dict(Counter(m['mapType'] for m in records)),
        'categories': dict(Counter(m['mapCategory'] for m in records)),
        'sapphire': sum(m['name'] == 'Sapphire Gemstone' for m in records),
    }
provenance = {
    'source': 'https://questlog.gg/aion-2/api/trpc/map.getMarkers?input={"language":"en"}',
    'sha256': hashlib.sha256(raw).hexdigest(),
    'preparedAtUtc': datetime.now(timezone.utc).isoformat(),
    'attribution': 'Questlog.gg; game content belongs to its respective owners',
    'redistributionLicense': 'Not established; local inspection only, excluded from Git',
    'coordinateSystem': 'Original Questlog coordinate pairs; no conversion applied',
    'mapNames': 'Not verified; map IDs preserved',
    'totalMarkers': len(markers),
    'worlds': summary,
}
(output / 'manifest.json').write_text(json.dumps(provenance, indent=2), encoding='utf-8')
print(f'Validated {len(markers):,} unique markers across {len(worlds)} map IDs.')
for world in ('1010', '1110'):
    print(f'Map {world}: {json.dumps(summary.get(world))}')
