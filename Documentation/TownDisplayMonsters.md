# Monsters kept in town

- Before the racetrack is built, two Nyuel accompany `construction_silver` in
  town. Positions are chosen within two units of his actual spawned position,
  using the original ground/wall collision checks and space between the birds.
- Silver's house also has two Nyuel while he is its resident. They occupy the
  clear floor next to him. The existing four named Nyuel at the racetrack remain
  on their established racing/conversation path.
- The monster shop has one Pulunpa inside each captured cage: X 19–21 / Z 10–12
  and X 10–12 / Z 15–17. Their floor is Y 0; the sampled collision height belongs
  to the roof and must not be used as their standing height.

`TowerGameController.InteriorMonsters.cs` spawns the existing monster prefabs
through `SpawnActor`. Nyuel use the recovered directional body atlas already
used at the racetrack, without an embedded rider. Pulunpa use the full existing
community idle/walk sequences and their original palette mapping.

`TownDisplayMonster` alternates short walks and pauses inside a fixed rectangle.
The shop rectangles stay inside the cage footprint with room for the sprite.
The view reports movement through `ActorLocomotionView`, so turning and walking
use the normal monster animation path. These are presentation actors: they are
not registered as tower enemies, inventory familiars or interactive town NPCs.

Setup checks placement IDs to prevent duplicates. The controller owns the
objects in `_generated`, so its normal scene cleanup removes them. No new save
keys, scene overrides, monster prefabs or art assets are required.

Validation uses a separate Unity 6000.6.7f1 Play Mode project: both captured
interiors, changing scenes and returning, visible animation frames, bounded
movement, floor height, no duplicate spawns and no combat colliders. Screenshots
and the result are saved under `../Validation/CaptiveCreatures/`.
