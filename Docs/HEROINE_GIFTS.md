# Held-item gifts in town

Interact with Nico, Selfi, Fur, Mia, Patty, Vivian or Cherrl while holding an inventory item. The original shared offer presents “This is for you.” and “Well... Nothing.” inside the dialogue window. Choosing nothing, cancelling or receiving a refusal preserves the held item. Acceptance removes the held item only after the response, with an identity check against stale callbacks.

## Original evidence

- `azure-dreams-decomp-master/src/town/event/item_query_checks_g01145.c`: `get_group_record_table_entry`, `filter_entry_by_kind`, `classify_listed_entry_type`, `classify_item_check_result`, and the clamped counter increment.
- TOWN overlay RAM `0x800D1038`: seven response-matrix pointers. Each matrix contains 16 script pointers, starting at `0x800D0E78`.
- `0x800D1054`: recipient group IDs 11, 7, 9, 10, 8, 12, 13 (Nico, Selfi, Fur, Mia, Patty, Vivian, Cherrl).
- `0x800D1064`: their existing love flags. Quarrel flags 4602/4603 and Mia flags 1274/1275 also affect response selection.
- `0x800D1074`: item kinds whose sell prices are classified. Thresholds at `0x800D1080` are 100 and 5000 G; unlisted kinds use column 3.
- SLUS scripts `0x8006B32C`–`0x8006C894`: 47 responses, preserving the original English, page breaks, acceptance callback and counter increment. Portuguese translations are separate from the extracted English.
- SLUS `0x8006C894`: shared offer choices. `0x8006BC7A`: Fur's repeat dumplings response after flag 4811. Her first dumplings conversation comes from TOWN.BIN `0x410E6E`–`0x4111D5`.

The native counter only increments where the selected script explicitly calls `0x800A9C18`; it clamps at three. This does not add an invented affection bonus or complete romance quests. Quest kind 13 only allows dumplings (3) and music box (4); other quest objects retain their dedicated handovers. Kind 1 / subtype 8 is excluded by the original filter.

Run `python Tools/extract_heroine_gifts.py` to regenerate `Resources/Town/Retail/heroine-gifts.json` from the local original files. The extractor rejects unknown instructions instead of guessing side effects. `Tools/heroine_gifts_pt.json` supplies the translations. `TownHeroineGiftTests` checks catalog completeness, thresholds, relationship flags and special-item filters.
