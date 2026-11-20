# Racetrack and residents

The constructed `circuit` building enters `Interior_0a_racetrack_entrance`.
The existing Silver construction quest remains the unlock path. The lobby now
has a ticket clerk, Silver, Bet, the frightened husband and four named Nyuel.
All use the normal town conversation path; Silver's choices and instructions
stay inside the main dialogue window, with the existing Grandpa portrait.

## Original sources

English dialogue was decoded from the local NTSC-U `TOWN/TOWN.BIN`:

| Resident/content | File offsets |
| --- | --- |
| Bet | `0x4E1814–0x4E1F15` |
| Frightened husband | `0x4E1F3D–0x4E2244` |
| Ticket clerk | `0x4E2284–0x4E2740` |
| Silver | `0x4E276A–0x4E28EE` |
| Riding instructions | `0x4E29A6–0x4E2BD6` |
| Four Nyuel names | `0x4E2C0B–0x4E2CC6` |
| New-record response | `0x4E2CF8–0x4E2DB9` |

Portuguese text is a translation. Riding instructions substitute the player's
current bindings. The latest race result selects Bet's response, and the
personal-best flag selects Silver's record response; these branch conditions
are reconstructed rather than a confirmed port of the original event script.

The two captured scenes supply the lobby and track geometry and textures.
The lobby exit is the blue door cell `(22.5, 17.5)`; arrival is two units
inside it at `(22.5, 15.5)`. The race starts at the captured finish line near
`(62.5, 16)`. Racers follow the road, use its two existing hedges and climb the
captured overpasses at X=25 and X=50. Road elevation is 2.5 units across the
bridge decks, with ramps between Z=21–25 and Z=29–33. Horizontal traffic uses
the ground-level underpasses.

`Tools/build_racetrack_nyuel.ps1` rebuilds the five original idle/walk
directions from the Nyuel bank in `DUNGEON.BIN` and the extracted indexed atlas
and chain metadata. It verifies the bank header before rendering. CLUT 1's
embedded rider is omitted so Koh and the other jockeys are separate visuals.
The generated Resources atlas is scoped to the racetrack; the community
monster catalogue is unchanged. Only its idle/walk columns are intended for
use. Regenerate with PowerShell from the project root:

```powershell
./Tools/build_racetrack_nyuel.ps1
```

The clerk and Silver reuse original Aunt/Grandpa directional cels. Bet and the
husband reuse the existing reconstructed Bet/NicoDad models. The exact native
mesh identities and placement scripts for the minor race residents have not
been confirmed. Jockey poses use the reconstructed seated animation.

## Playable behavior

- Silver offers racing, spectating, instructions and leaving.
- Tickets retain the existing 100 G price, six first-two combinations and
  10-ticket limit. Purchase deducts gold once; settlement clears the tickets
  and pays a winning combination once.
- Running accelerates; repeated presses give a short sprint; down reverses;
  left/right change lane; the displayed jump binding jumps over hedges.
- Four riders receive unique ordered results. Same-frame finishes are sorted
  by their estimated crossing time. Returning restores Koh and the lobby NPCs
  clear of the exit trigger. Scene changes clear mounted poses and race state.

Simulation pace, random odds, camera behavior and jumper timing are gameplay
reconstructions, not claimed as exact original-game constants.

## Validation — 2026-11-18

Runtime scripts compile against Unity 6000.6.7f1. An isolated Unity Play Mode
probe loads the actual captured scenes and prefab/texture dependencies, opens
Silver's menu and instructions, purchases two tickets, completes spectator and
player races with injected acceleration input, verifies unique finish places,
single settlement, safe lobby arrival, both bridge elevations, underpasses,
same-frame crossing order and scene cleanup. Lobby and mounted-rider renders
were inspected. The user's open Editor was not interrupted.

Artifacts are in `../Validation/NpcRestaurant/`: `race-passed.txt`,
`UnityProbe/race-playmode.log`, `racetrack-lobby-npcs.png` and
`racetrack-riders-bridge.png`. This is focused feature validation, not a full
project test run or a WebGL build.
