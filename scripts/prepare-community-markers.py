"""Prepare coordinate indexes from the downloaded community YAML files.

This reads only the flat scalar fields needed by the overlay. Original YAML
remains authoritative for descriptions, images, and other nested metadata.
Unknown or changed record layouts fail validation instead of silently dropping data.
"""
import collections
import json
import math
from pathlib import Path
import re

root = Path(__file__).resolve().parents[1]
source = root / 'data/sources/community-map'
output = root / 'data/processed/community-map'
output.mkdir(parents=True, exist_ok=True)
provenance = json.loads((source / 'provenance.json').read_text(encoding='utf-8-sig'))
summary = {}
for map_id in ('World_L_A', 'World_D_A', 'Abyss_Reshanta_A'):
    text = (source / f'{map_id}.yaml').read_text(encoding='utf-8-sig')
    blocks = re.findall(r'^- category:.*?(?=^- category:|\Z)', text, re.M | re.S)
    records = []
    for block in blocks:
        fields = dict(re.findall(r'^(?:- |  )(category|id|name|region|subtype|x|y):\s*([^\r\n]*)', block, re.M))
        for key in ('category', 'id', 'subtype', 'x', 'y'):
            if key not in fields:
                raise ValueError(f'{map_id}: missing {key}')
        for key in ('x', 'y'):
            fields[key] = float(fields[key])
            if not math.isfinite(fields[key]):
                raise ValueError('Non-finite coordinate')
        records.append(fields)
    expected = len(re.findall(r'^- ', text, re.M))
    if len(records) != expected or not records:
        raise ValueError(f'{map_id}: unsupported layout or missing records')
    if len({r['id'] for r in records}) != len(records):
        raise ValueError(f'{map_id}: duplicate marker IDs')
    counts = dict(sorted(collections.Counter(r['subtype'] for r in records).items()))
    dataset = {'mapId': map_id, 'coordinateSystem': 'upstream map coordinates; orientation requires verification against tiles',
               'provenance': provenance, 'markers': records}
    (output / f'{map_id}.json').write_text(json.dumps(dataset, indent=2), encoding='utf-8')
    summary[map_id] = {'total': len(records), 'categories': counts}
(output / 'summary.json').write_text(json.dumps(summary, indent=2), encoding='utf-8')
print(json.dumps(summary, indent=2))
