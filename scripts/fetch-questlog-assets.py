"""Fetch website assets for local inspection/testing, never from the game.

No account, cookies, or authentication are used. Files remain Git-ignored.
This does not establish permission to redistribute them publicly.
"""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
from urllib.request import Request, urlopen

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('--refresh', action='store_true', help='Refresh the existing local marker response')
args = parser.parse_args()
root = Path(__file__).resolve().parents[1]
source = root / 'data/sources/questlog'
data = root / 'data/processed/questlog'
source.mkdir(parents=True, exist_ok=True)

def download(url, path):
    if path.exists() and not args.refresh:
        return
    with urlopen(Request(url, headers={'User-Agent': 'aion2-map-overlay-local-test'}), timeout=60) as response:
        payload = response.read()
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix(path.suffix + '.tmp')
    temporary.write_bytes(payload)
    temporary.replace(path)

download('https://questlog.gg/aion-2/api/trpc/map.getMarkers?input=%7B%22language%22%3A%22en%22%7D', source / 'markers.en.raw.json')
subprocess.run([sys.executable, str(root / 'scripts/prepare-questlog-markers.py')], check=True)
download('https://cdn.questlog.gg/aion-2/map/v3/World_L_A.webp', data / 'reference/World_L_A.webp')
markers = json.loads((data / 'map-1010.json').read_text(encoding='utf-8'))
groups = {}
for marker in markers:
    if marker['mapType'] in ('gatherable', 'collectable'):
        groups.setdefault(marker['mapType'] + '|' + marker['name'], marker)
icons = {}
for key, marker in groups.items():
    path = marker.get('icon')
    if marker['name'] == 'Empyrean Trace':
        path = '/map/icon/monolith.webp'
    if not path:
        continue  # Hidden Cube has an application-drawn fallback.
    asset = path if path.endswith('.webp') else path.rsplit('.', 1)[0] + '.webp'
    name = re.sub('[^a-zA-Z0-9]+', '-', marker['name']).lower() + '.webp'
    download('https://cdn.questlog.gg/aion-2' + asset, data / 'icons' / name)
    icons[key] = name
(data / 'icons/manifest.json').write_text(json.dumps(icons, indent=2), encoding='utf-8')
(data / 'reference/provenance.json').write_text(json.dumps({
    'mapWorldId': '1010', 'name': 'Verteron',
    'referenceSource': 'https://cdn.questlog.gg/aion-2/map/v3/World_L_A.webp',
    'sha256': hashlib.sha256((data / 'reference/World_L_A.webp').read_bytes()).hexdigest(),
    'worldBounds': {'left': -408000, 'right': 408000, 'top': 408000, 'bottom': -408000},
    'sectors': [8, 8], 'sectorSize': 102000,
    'redistributionLicense': 'Not established; local testing only',
}, indent=2), encoding='utf-8')
print(f'Prepared {len(groups)} category groups and {len(icons)} source icons locally.')
print('Map-title template still requires --verify with an English Verteron screenshot before packaging.')
