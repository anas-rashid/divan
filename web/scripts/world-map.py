"""web/public/maps/world.json: the admin statistics' world map (admin/stats.astro), one SVG path per country (ISO code),
in degrees (x = longitude, y = -latitude), to 0.1 degree. From Natural Earth (public domain), 1:50m:
  ne_50m_admin_0_countries.geojson and ne_50m_admin_0_breakaway_disputed_areas.geojson
  (github.com/nvkelso/natural-earth-vector, geojson/), next to this script or in the folder given.
Owner's choices (2026-10-25): Palestine one country with Israel's area (PS, merged, no inner border); '_JK' is IIOJK
(Jammu and Kashmir under Indian administration, and the Siachen Glacier), drawn over India as disputed. Antarctica left out.
  python3 world-map.py [folder]      (needs shapely: pip3 install shapely)
"""
import json, sys
from pathlib import Path
from shapely.geometry import shape, mapping
from shapely.ops import unary_union

src = Path(sys.argv[1] if len(sys.argv) > 1 else Path(__file__).parent)
countries = json.load(open(src / 'ne_50m_admin_0_countries.geojson'))['features']
disputed = json.load(open(src / 'ne_50m_admin_0_breakaway_disputed_areas.geojson'))['features']

def ring(r):
    pts, last = [], None
    for lon, lat in r:
        p = (round(lon * 10) / 10, round(-lat * 10) / 10)
        if p != last: pts.append(p); last = p
    if len(pts) < 3: return ''
    f = lambda v: '%g' % v
    s, (px, py) = 'M' + f(pts[0][0]) + ' ' + f(pts[0][1]), pts[0]
    for x, y in pts[1:]:
        dx, dy = round((x - px) * 10) / 10, round((y - py) * 10) / 10
        s += 'l' + f(dx) + (' ' if dy >= 0 else '') + f(dy); px, py = x, y
    return s + 'z'

def path(geom):
    g = mapping(geom)
    polys = g['coordinates'] if g['type'] == 'MultiPolygon' else [g['coordinates']]
    return ''.join(ring(p[0]) for p in polys)

shapes = {}
for ft in countries:
    p = ft['properties']; cc = p.get('ISO_A2_EH') or p.get('ISO_A2')
    if not cc or cc in ('-99', 'AQ'): continue
    shapes.setdefault(cc, []).append(shape(ft['geometry']))
shapes['PS'] += shapes.pop('IL')
out = {cc: path(unary_union(gs)) for cc, gs in shapes.items()}
out['_JK'] = path(unary_union([shape(f['geometry']) for f in disputed if f['properties']['BRK_NAME'] in ('Jammu and Kashmir', 'Siachen Glacier')]))
out = {k: v for k, v in out.items() if v}
(Path(__file__).parent.parent / 'public/maps/world.json').write_text(json.dumps(out, separators=(',', ':')))
print(len(out), 'shapes')
