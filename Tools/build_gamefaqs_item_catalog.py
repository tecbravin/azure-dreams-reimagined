"""Convert the locally archived Items List into factual item records."""
from pathlib import Path
import json
import re

project = Path(__file__).resolve().parents[1]
guide = project / "Documentation/Community/ItemsGuide.txt"
categories = ("wand sword troll_weapon shield crystal fruit herb seed scroll bell ball "
              "loupe glasses sand money miscellaneous egg").split()
items = []
section = 0
for line in guide.read_text(encoding="utf-8").splitlines():
    heading = re.match(r"^(\d+)\.\s*[A-Z][A-Z ]+-", line)
    if heading:
        section = int(heading[1])
        continue
    if not section or not line or line[0].isspace():
        continue
    parts = re.split(r"\s{2,}", line.strip())
    if section == 17:
        pairs = zip(parts[::2], parts[1::2])
        fields = [(name, price, None) for name, price in pairs if price.isdecimal()]
    else:
        stat_pattern = r"(?:(?P<stat>\d+|N/A)\s+)" if section <= 4 else ""
        row = re.match(r"^(?P<name>.+?)\s+" + stat_pattern
                       + r"(?P<price>\d+(?:-\d+)?|N/A|\?)\s{2,}\S", line)
        if not row:
            continue
        fields = [(row["name"], row["price"], row.groupdict().get("stat"))]
    for label, price, stat in fields:
        name = label.rstrip("~#!*")
        marks = label[len(name):]
        category = categories[section - 1]
        record = dict(key=category + "." + re.sub(r"[^a-z0-9]+", "_", name.lower()).strip("_"),
                      name=name, category=category)
        if price.isdecimal():
            record["resale_gp"] = int(price)
        elif "-" in price:
            record["coin_gp_min"], record["coin_gp_max"] = map(int, price.split("-"))
        else:
            record["resale_gp"] = None
        if stat and stat.isdecimal():
            record["defense" if section == 4 else "attack"] = int(stat)
        if "~" in marks: record["rarity"] = "rare"
        if "#" in marks: record["rarity"] = "exceptionally_rare"
        if "!" in marks: record["unique"] = True
        items.append(record)

assert len({item["key"] for item in items}) == len(items)
assert {item["category"] for item in items} == set(categories)
assert sum(item["category"] == "egg" for item in items) == 35
target = project / "Assets/AzureDreams/Data/Items/Community/GameFAQsItemCatalog.json"
target.write_text(json.dumps({"source": "Azure Dreams Items List, Incossee v1.1 (local GameFAQs archive)",
                              "items": items}, indent=2) + "\n", encoding="utf-8")
from collections import Counter
print(len(items), dict(Counter(item["category"] for item in items)))
