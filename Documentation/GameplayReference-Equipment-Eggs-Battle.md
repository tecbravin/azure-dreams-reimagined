# Tower equipment, eggs, and battle reference

This document records the supplied Azure Dreams PS1 mechanics notes and how
they are represented in the Unity implementation. Probability values shown as
percentages in the source were converted to exact 1/1024 rolls where possible.

## Tower equipment

| Equipment | Modifier | Probability | Curse |
| --- | ---: | ---: | --- |
| Sword or shield | -1 | 17.97% (184/1024) | Always cursed |
| Sword or shield | 0 | 71.09% (728/1024) | No |
| Sword or shield | +1 | 5.47% (56/1024) | No |
| Sword or shield | +2 | 5.47% (56/1024) | No |
| Wand | -1 | 12.5% (128/1024) | Always cursed |
| Wand | 0 | 87.5% (896/1024) | No |

Dark Sword is always cursed independently of its modifier. Equipment found in
the tower remains unidentified until equipped. These rolls are used by the
managed tower loot path and debug-generated tower equipment.

The reference also lists five exceptionally rare items at 1/5000 each:
Bow Gun, Dark Sword, Holy Sword, Roche Fruit, and Trained Wand. The current
managed-floor rare-item adapter already rolls each outcome at 1/5000; this does
not alter quest-locked or floor-specific unique-item rules.

## Crystal-ball charges

Acid Rain Ball (item ID `0x11`) always has one charge. Every other crystal ball
found on a tower floor has 4, 5, 6, or 7 charges with equal probability. The
managed floor-loot adapter normalizes balls supplied by the Core floor data to
these charge values.

## Monster eggs

Tower-spawned eggs begin with 1–40% warmth, uniformly. Warming adds 1–4
percentage points, uniformly. The existing 50% spawn roll was halved to 25% per
eligible floor. Eggs are still excluded from floor 40 and the frog species and
the previously excluded rare species remain absent from random egg rolls.

The per-floor species weights below use integer units out of 32. Percentages
from the supplied notes are rounded display values; a weight of 5 is 15.625%,
shown there as 15.63%.

| Floors | Species weights |
| --- | --- |
| 1–5 | Noise 6; Flame 5; Pulunpa 5; Balloon 4; Troll 4; Blume 3; Cyclone 3; Manoeva 2 |
| 6–10 | Blume 6; Balloon 5; Cyclone 5; Manoeva 4; Clown 3; Dreamin 3; U-Boat 3; Volcano 3 |
| 11–15 | Clown 5; Dreamin 5; U-Boat 5; Volcano 5; Garuda 3; Griffon 3; Kraken 3; Nyuel 3 |
| 16–20 | Garuda 5; Griffon 5; Kraken 5; Nyuel 5; Arachne 3; Picket 3; Weadog 3; Troll 2; Barong 1 |
| 21–25 | Arachne 5; Picket 5; Weadog 5; Block 3; Stealth 3; Unicorn 3; Viper 3; Manoeva 2; Troll 2; Barong 1 |
| 26–30 | Block 5; Stealth 5; Unicorn 5; Viper 5; Mandara 3; Naplass 3; Snowman 3; Zu 3 |
| 31–35 | Mandara 5; Naplass 5; Snowman 5; Zu 5; Glacier 3; Kid 3; Killer 3; Tyrant 3 |
| 36–39 | Killer 7; Glacier 6; Tyrant 6; Golem 5; Kid 5; Maximum 3 |
| 40 | No egg |

## Floor generation observations

- A single-room floor is reported at 3.13% (1/32). Assuming independent
  floor rolls across a 40-floor ascent, the supplied estimate for at least one
  such floor is 70.11%.
- Elevator count is reported as: one 98.44% (252/256), two 1.54% (about
  3.94/256), three 0.024%, and four 0.00038%; maximum four.
- The tower can contain at most 32 traps on a floor. The Unity trap manager
  enforces this cap.

The single-room and elevator-count distributions are retained here as
reference data: the current project delegates their placement to the existing
Core dungeon generator, whose binary-facing API is intentionally not replaced
by this Unity change. The trap cap is applied in the Unity manager.

## Status-duration notes

These are the supplied duration observations. Definite, directly mapped item
effects are applied; entries marked `?` remain reference-only because the
source itself marks them uncertain or does not identify a unique Unity action.

| Source | Effect | Turns | Implementation |
| --- | --- | ---: | --- |
| Poison herb | Poison | 8 | Existing behavior |
| Paralyze herb | Paralyze | 8 | Existing behavior |
| Sleep herb | Sleep | 8 | Existing behavior |
| Tumna fruit | Frog | 32 | Applied |
| Leolam fruit | MP use prevented | 16 | Applied |
| Laev fruit | MP use doubled | 32–63 | Applied; uniform inclusive range |
| Limit fruit | Angry | 96 | Applied |
| Mahell seed | Double movement | 8 | Applied |
| Slow seed | Half movement | 10 | Applied |
| Water ball | Fire protection | 12? | Reference only; association is uncertain |
| Seal trap | Sealed | 16? | Reference only; duration is uncertain |
| Blume | Brainwash | 16? | Reference only; duration is uncertain |

## Battle and stats

### Luck

Luck influences hit chance, critical chance, evasion for units that are not
incapacitated, and trap success. Incapacitated targets do not dodge. The notes
report that ordinary unit Luck does not increase with level, except Barong.
Koh's Luck cycles from 64 at tower turn 0 to 96 at turn
1024, down to 32 at turn 3072, then repeats at turn 4096. The implementation
uses a smooth sinusoidal curve through those reference points, matching the
reported slowdown near the extrema; the source does not provide a literal
lookup table or equation.

Critical chance before accuracy/evasion is resolved is:

- Normal: `(floor(Luck / 64) + 1) / 128`.
- Special critical flag (used by Killer's Atrocious trait):
  `(floor(Luck / 2) + 1) / 128`.
- Limit fruit's Angry status adds 50% of the base critical chance, as tracked
  separately by the Unity item-effect adapter.

The existing Core combat routine already uses these Luck thresholds and
skips dodge rolls for incapacitated targets.

### Physical weapon accuracy

The Core combat hit check derives chance from the attacker's accuracy baseline
and Luck. The Unity adapter now supplies the documented accuracy baseline for
the attacker's equipped weapon before calling that check:

| Weapon | Accuracy | Damage |
| --- | ---: | ---: |
| Koh unarmed | 126 | 0 |
| Gold Sword | 124 | 1 |
| Copper Sword | 118 | 2 |
| Iron / Steel Sword | 116 | 3 / 4 |
| Fire / Blizzard / Gulfwind / Vital Sword | 120 | 5 |
| Dark Sword | 96 | 10 |
| Holy Sword | 120 | 7 |
| Seraphim Sword | 124 | 8 |
| Wooden / Trained / Life / Paralyze / Money / Scarlet / Stream / Gulf / Seal Wand | 124 | 1 |
| Troll Hammer | 116 | 1 |
| Troll Bow Gun | 112 | 2 |
| Troll Sword | 120 | 8 |
| Non-Troll monster | 124 | 0 |
| Beldo | 128 | 32 |

The source says Bow Gun accuracy decreases with distance but gives no step
size; the Unity adapter uses a four-point accuracy reduction per tile beyond
the adjacent tile. This is an explicit approximation. The Core routine already
applies the leather-shield penalty and skips the regular accuracy check against
asleep, bound, or blind targets, while allowing the separate evasion check.

Accuracy and evasion are resolved before critical damage, so a critical roll
does not turn a miss or dodge into a hit.

## Source caveats

The pasted reference's “Monster Spawns” section only contains `TK`; it provides
no usable spawn table. No enemy-spawn changes are derived from that section.
