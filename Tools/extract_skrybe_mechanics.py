"""Extract mechanics reference data from the local Skrybezero archive."""
from email import policy
from email.parser import BytesParser
from hashlib import sha256
from pathlib import Path
import json
import re
import sys

project = Path(__file__).resolve().parents[1]
workspace = project.parent
sys.path.insert(0, str(workspace / ".tools/community_readers"))
from bs4 import BeautifulSoup

source = next((workspace / "community").glob("*Skrybezero*.mhtml"))
raw = source.read_bytes()
archive = BytesParser(policy=policy.default).parsebytes(raw)
guide = next(pre.get_text() for part in archive.walk()
             if part.get_content_type() == "text/html"
             for pre in BeautifulSoup(part.get_content(), "html.parser").find_all("pre")
             if "Game mechanics guide v1.0" in pre.get_text())
sections = {}
for block in re.split(r"(?m)^---\s*$", guide):
    match = re.match(r"\s*(\d+)\.0\s+([^\n]+)\n(.*)", block, re.S)
    if match:
        sections[int(match[1])] = dict(title=match[2].strip(), text=match[3].strip())
assert set(sections) == set(range(1, 12))

def named_values(section):
    return [{"name": name, "value": int(value)} for name, value in
            re.findall(r"\b([A-Za-z][A-Za-z-]+):[ \t]+(\d+)\b", sections[section]["text"])]

encounters = []
floor_rows = list(re.finditer(r"(?m)^F(\d+):\s*", sections[8]["text"]))
for index, row in enumerate(floor_rows):
    end = floor_rows[index + 1].start() if index + 1 < len(floor_rows) else len(sections[8]["text"])
    entries = []
    for name, percent, fixed in re.findall(r"([A-Za-z][A-Za-z -]*(?:\s*\([^()\d%]*\)|\s*\(Lv\d+\))?)\s*\((?:(\d+)%|just (\d+))\)",
                                          sections[8]["text"][row.end():end].split("*I suspect")[0]):
        entries.append(dict(name=name.strip(), reported_percent=int(percent) if percent else None,
                            fixed_count=int(fixed) if fixed else None))
    encounters.append(dict(floor=int(row[1]), entries=entries,
                           reported_total=sum(e["reported_percent"] or 0 for e in entries)))
assert [row["floor"] for row in encounters] == list(range(1, 40))
assert all(row["entries"] for row in encounters)
assert encounters[24]["entries"][-1] == dict(name="Picket", reported_percent=None, fixed_count=1)
assert len(encounters[16]["entries"]) == 6
eggs = []
for name, line in re.findall(r"(?m)^([A-Za-z][A-Za-z-]+):\s*(F\d+[^\n]*)", sections[7]["text"]):
    for low, high, percent in re.findall(r"F(\d+)-(\d+)\s*\((\d+)%\)", line):
        eggs.append(dict(species=name, first_floor=int(low), last_floor=int(high), reported_percent=int(percent)))
assert len(eggs) == 63, len(eggs)
data = dict(source=source.name, sha256=sha256(raw).hexdigest(), author="Skrybe", version="1.0",
            runtime_policy={"random_monster_excluded_floors": [2, 40], "reference_only": True},
            monster_encounters=encounters, egg_rates=eggs, base_luck=named_values(1),
            direct_spell_power=named_values(3), ball_spell_learning_minimum_charges=10,
            ball_spell_learning_exclusions=["Acid Rain"],
            rare_items={"approximate_one_in": 5000, "names": ["Bow Gun", "Dark Sword", "Holy Sword", "Roche Fruit", "Trained Wand"]}, sections=sections)
out = project / "Assets/AzureDreams/Data/Community/SkrybeMechanics.json"
out.parent.mkdir(parents=True, exist_ok=True)
out.write_text(json.dumps(data, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(len(encounters), "floors,", len(eggs), "egg rates,", len(data["base_luck"]), "luck stats")

