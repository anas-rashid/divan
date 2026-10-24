"""web/public/maps/city-names.json: the admin statistics' city names (admin/stats.astro) in Urdu script, keyed by
"<country>|<English name>" as DB-IP gives them. From Wikidata's labels (CC0):
  Palestine (with Israel's area) and the Arab countries: the Arabic names (Urdu writes them the same; owner 2026-10-25)
  everywhere else: the Urdu names
Cities of 100,000 people or more (Palestine and Israel's area: 5,000 or more), with their English name and an alias or two
as keys. A city without one keeps DB-IP's English name.
  python3 city-names.py
"""
import csv, io, json, time, urllib.parse, urllib.request
from pathlib import Path

ARAB = 'AE BH DZ EG IQ JO KW LB LY MA OM QA SA SD SY TN YE MR SO DJ KM PS IL'.split()
UA = {'User-Agent': 'DivanBot/1.0 (https://divan.pk)', 'Accept': 'text/csv'}

def sparql(q):
    url = 'https://query.wikidata.org/sparql?' + urllib.parse.urlencode({'query': q})
    for attempt in range(3):
        try:
            return list(csv.DictReader(io.StringIO(urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180).read().decode())))
        except Exception:
            time.sleep(10)
    raise SystemExit('Wikidata did not answer')

def query(lang, countries, kind, pop):
    where = f'VALUES ?cc {{ {" ".join(f"{chr(34)}{c}{chr(34)}" for c in countries)} }}' if countries else ''
    return f'''SELECT ?cc ?en ?alias ?name WHERE {{
      {where} ?c wdt:P31/wdt:P279* wd:{kind}; wdt:P1082 ?pop; wdt:P17 ?country. ?country wdt:P297 ?cc.
      FILTER(?pop >= {pop}) ?c rdfs:label ?en. FILTER(lang(?en) = "en") ?c rdfs:label ?name. FILTER(lang(?name) = "{lang}")
      OPTIONAL {{ ?c skos:altLabel ?alias. FILTER(lang(?alias) = "en") }} }}'''

names = {}
def add(rows, keep=lambda cc: True):
    for r in rows:
        if not keep(r['cc']): continue
        for en in (r['en'], r.get('alias') or ''):
            if en: names.setdefault(f"{r['cc']}|{en}", r['name'])
# Palestine and Israel's area first: every town of 5,000 (human settlements), in Arabic
add(sparql(query('ar', ['PS', 'IL'], 'Q486972', 5000)))
add(sparql(query('ar', [c for c in ARAB if c not in ('PS', 'IL')], 'Q515', 100000)))
add(sparql(query('ur', [], 'Q515', 100000)), keep=lambda cc: cc not in ARAB)
out = Path(__file__).parent.parent / 'public/maps/city-names.json'
out.write_text(json.dumps(dict(sorted(names.items())), ensure_ascii=False, separators=(',', ':')))
print(len(names), 'names')
