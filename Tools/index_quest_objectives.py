"""Index quest variables from the workbook's previously extracted Girlfriends sheet."""
import json
import re
from pathlib import Path

data = Path(__file__).resolve().parents[1] / "Assets/AzureDreams/Data/Community"
rows = json.loads((data / "town_event_reference.json").read_text(encoding="utf-8"))
routes = {
    3: ("nico", "Nico", None), 4: ("selfi", "Selfi", None),
    5: ("fur", "Fur", "shared_or_path_1"), 6: ("fur", "Fur", "path_2"),
    7: ("fur", "Fur", "path_3"), 8: ("patty", "Patty", None),
    9: ("vivian", "Vivian", "path_1"), 10: ("vivian", "Vivian", "path_2"),
    11: ("mia", "Mia", None), 12: ("cherrl", "Cherrl", "path_1"),
    13: ("cherrl", "Cherrl", "path_2"), 14: ("temple_alley_casino", None, None),
    15: ("circuit", None, None), 16: ("arcade", None, None),
    17: ("gym", None, None), 18: ("windmills", None, None),
}
npc_aliases = {
    "Nico": "Nico", "Selfi": "Selfi", "Patty": "Patty|Patti",
    "Vivian": "Vivian", "Mia": "Mia|Miya", "Cherrl": "Cherrl",
    "Ghosh": "Ghosh", "Dr. Hal": r"Dr\. Hal", "Hush": "Hush", "Okami": "Okami",
    "Barten": "Barten", "Wookie": "Wookie", "Jorda": "Jorda", "Pen": "Pen",
    "Barry": "Barry", "Issac": "Issac", "Fon": "Fon", "Chero": "Chero",
}
quests = {}
for row in rows:
    column, number = row["source_column"], row["source_row"]
    quest, owner, route = routes[column]
    record = quests.setdefault(quest, dict(quest_id=quest, affected_npc=owner,
        state_keys=[f"quest.{quest}.completed_steps", f"quest.{quest}.complete"], steps=[]))
    if route and f"quest.{quest}.route" not in record["state_keys"]:
        record["state_keys"].append(f"quest.{quest}.route")
    text = row["text"]
    # Match names as words, excluding possessive location names and building labels.
    mentions = [npc for npc, aliases in npc_aliases.items()
                if re.search(r"\b(?:" + aliases + r")\b(?!['’]s (?:[Hh]ouse|store))", text, re.I)]
    if column in (5, 6, 7) and re.search(r"talk to (?:her|Fur)\b", text, re.I):
        mentions.append("Fur")
    if column == 3 and number == 3:
        mentions.append("Nico's father")
    if column == 12 and number == 11:
        mentions.append("Cherrl's mother")
    letter = chr(64 + column)
    cell = f"{letter}{number}"
    record["steps"].append(dict(source_cell=cell, day_label=row["day_label"], location=row["location"],
        route=route, affected_npc=owner, named_npc_mentions=sorted(set(mentions)),
        step_labels=re.findall(r"\[STEP\s+([^\]]+)\]", text, re.I),
        completion_marker=bool(re.search(r"\[\w+ DONE\]", text, re.I)),
        variables=dict(gold_amounts_gp=[int(x) for x in re.findall(r"\b(\d+)G\b", text)],
                       floor_numbers=[int(x) for x in re.findall(r"\b(\d+)F\b", text)],
                       deadline_days=[int(x) for x in re.findall(r"latest at day (\d+)", text, re.I)],
                       minimum_building_score=[int(x) for x in re.findall(r"score is at least (\d+)", text)],
                       minimum_empty_inventory_slots=[int(x) for x in re.findall(r"at least (\d+) empty slot", text)],
                       named_items=[item for item in ("Blue Cape", "Water Medal", "Healing Herb", "Guru's Pot", "Music Box")
                                    if item.casefold() in text.casefold()],
                       bracketed_dialogue=[value for value in re.findall(r"\[([^\]]+)\]", text)
                                           if not re.match(r"STEP\b|Check Note\b|\w+ DONE$", value, re.I)]),
        source_condition=text))
assert len(rows) == 139 and len(quests) == 12
assert sum(len(q["steps"]) for q in quests.values()) == len(rows)
assert len({step["source_cell"] for q in quests.values() for step in q["steps"]}) == len(rows)
out = dict(source="community/AzureDreams_GameObjectives.ods", sheet="Girlfriends",
           scope="Workbook reference. State keys are proposed Unity keys, not retail flags. NPC mentions include supporting participants. Gold and floor numbers retain their conditions in source_condition.",
           quests=list(quests.values()))
(data / "QuestObjectiveVariables.json").write_text(json.dumps(out, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
print(f"{len(quests)} quest lines, {len(rows)} source cells")
