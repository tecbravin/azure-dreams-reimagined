# Arcade

The constructed amusement center (`arcade`) enters the captured
`Interior_09_arcade_1` scene. Its side mat leads by touch to the captured
`Interior_2a_arcade_2` batting center. The second room's mat returns to the
first room, where the blue doorway returns to town. Arrival positions are
clear of the corresponding triggers. The original scene textures and geometry
are retained, and both rooms use the existing Playroom music.

Each room has an attendant, Bet, a visitor, a boy and a girl. The attendant
offers the local game and optional instructions. Bet reacts to the last score
and a new personal record. The visitor comments on the children; in the
batting room the visitor can reveal Bet's secret practice. Conversations and
choices use the existing town dialogue window and its white font/frame.

## Original references and reconstruction limits

English text was decoded from the local NTSC-U `TOWN/TOWN.BIN`:

| Content | File offsets |
| --- | --- |
| Mole-room residents, invitation and instructions | `0x4B2059–0x4B2712` |
| Batting-room residents, invitation and instructions | `0x4B4011–0x4B4907` |

Portuguese text is translated. Control instructions substitute the player's
configured actions. Resident placement and result-dependent branch conditions
are reconstructed rather than a confirmed port of the original scene scripts.
The attendant/Bet/visitor reuse existing Aunt, Bet and NicoDad visuals. The
children reuse the original young Koh/Nico directional sheets under separate
arcade NPC IDs; they are generic visitors, not additional story appearances.
Runtime copies retain the sheets' native dimensions and remove the magenta
color key; their original import settings otherwise resample the sheets and
invalidate the directional rectangles. Regenerate these copies with
`Tools/build_arcade_children.py` (Pillow).
No high-resolution portrait images were changed.

Gameplay references in `../azure-dreams-decomp-master/src/town/minigame/`:

- `initialize_mogura_minigame.c`: initial count 30 and a four-entry pattern.
- `object/advance_object_spawning_displays_high.c`: rotating spawn pattern and
  `max(16, 42 - presented / 6)` frame spawn interval, and record comparison.
- `initialize_tako_minigame.c` / `update_tako_minigame.c`: three Takopoos and
  ten table-selected volleys.
- `object/update_object_movement_proximity_reactions.c`: 1-point ordinary
  hits, 2-point returns and a 10-point bonus on the tenth return to a target.

The exact native Takopoo timing table and creature animation banks are not
resolved. Candidate sprite reads produced invalid headers/noisy texture pages
and were not imported as game artwork. Soilclaw and Takopoo use lightweight
reconstructed models based on their monster-book appearances, with breathing,
tentacle motion and ink darkening. Their geometry and animation are not claimed
as extracted original models. Mole exposure times, ink speeds, the hit window
and camera framing are reconstructed gameplay tuning.
The original batting sound selectors are used when their clips exist; otherwise
existing retail hit/confirmation sounds provide feedback.

## Playable rules

Hit the Mole has 30 Soilclaw appearances across four holes. Move, run, jump
and attack with the normal configured actions. A target scores once and each
swing can hit at most one target. The captured game-board navigation mask is
closed during ordinary town movement; the minigame uses its raised one- and
two-unit platforms while active. A round ends after all 30 targets resolve.

The batting center launches ten three-ball volleys. Up/down moves Koh along
the batting line. The attack action swings: ordinary contacts score one point;
well-timed contacts return the ball and score two. Each Takopoo darkens with
returned ink. Ten returns blacken it completely and add ten points. Returning
all 30 balls scores 90, including the three bonuses. Missed balls count as
misses. Neither game charges money or awards gold.

Both games show a countdown, score, personal best, results and replay/return
choices. Cancel asks before abandoning an active round. An abandoned round
does not replace the saved last result. Completing a round settles it once.
Scene changes and controller disable release the generated objects/materials
and reset the temporary presentation. Returning from a game restores Koh's
previous position and facing.

Save keys use the existing `TownSaveVariables` capture/restore path:

- `arcade.moles.best`, `.last`, `.played`, `.new_record`
- `arcade.batting.best`, `.last`, `.played`, `.new_record`

## Validation — 2026-11-18

Runtime scripts compile against Unity 6000.6.7f1. A focused isolated Play Mode
probe loads both actual scenes and their prefab/texture dependencies, checks
the five NPC conversations per room and attendant instructions, exercises
movement/jump input, completes both games, verifies 30 mole hits, 30 misses,
best-score preservation, cancellation, touch travel both ways, a 90-point
perfect batting result, blackening/bonuses, replay, cleanup, no gold changes
and return to Koh's prior position. Camera renders of both rooms and gameplay
were inspected. The user's open Editor was preserved.

Reports and previews are under `../Validation/Arcade/`; the Unity log is
`../Validation/NpcRestaurant/UnityProbe/arcade-playmode.log`. This is focused
feature validation, not a full test-suite run or WebGL build.

Compilation also exposed existing blockers elsewhere: an extra parenthesis
in the gym dialogue, a duplicate theater method, a missing core namespace in
fusion presentation and an obsolete Unity object-ID call. Only those blocking
lines were corrected.
