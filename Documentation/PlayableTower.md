# Playable procedural tower

Open Assets/AzureDreams/Scenes/ReimaginedDemo.unity and enter Play Mode.
Approach the east gate in town, face it and press E. Procedural generation is
now enabled in the saved scene. WASD moves; Shift+direction aims; F attacks;
Space waits; E interacts/uses a carried potion; R returns to town. On floor 2,
E near the visitor talks instead of using a potion. Step onto the exit stairs
to advance. The camera follows the player's presented position in the tower.

## Implemented

- Every production-scene tower floor uses the procedural 64x64 generator.
- Floor 2 uses procedural terrain but suppresses core random monster creation,
  runtime creature views, and monster occupancy flags. It creates one event
  visitor near entry, away from stairs and existing items/traps where possible.
- Visitor dialogue is interactive and does not consume a combat turn. The
  visitor does not block narrow corridors and is not a targetable combat actor.
- Leaving floor 2 through the actual stair action advances the run to floor 3,
  removes the visitor and restores normal generated monster spawns.
- TowerDamageLedger keeps player/familiar damage keyed to each pre-turn enemy
  index. Shared-target hits combine; different targets no longer share damage.
  All queued effects apply before defeated creatures are removed.
- Koh's shader no longer uses an SRP-only pass tag in this Built-in scene.
  A pixel-level test confirms that the hero actually renders in tower captures.

The parameterless core Generate() remains compatible and populates monsters.
Generate(bool populateMonsters) adds event-floor control; TowerFloorManager
selects false for floor 2. Existing authored-layout test fixtures remain
available, while the saved playable scene selects generated floors.

## Validation

Unity 6000.5.6f1 with D3D11, isolated validation copy:
- Logs/PlayableTowerFinalEditMode.xml: 190/190 passed.
- Logs/PlayableTowerFinalPlayMode.xml: 182/182 passed.
- Logs/EventFloorCoreSync.log: 123/123 pure core tests passed; Unity-safe DLL rebuilt.

New coverage tests separate/shared damage targets and reset, floor-2 layouts
across three seeds, zero monster records/occupancy flags, NPC interaction,
real stair progression 1->2->3, saved scene configuration, and visible hero
pixels in the rendered views. No tests were skipped or disabled.
Screenshots: Logs/TowerPlayable/tower-floor-1.png and tower-floor-2-event.png.

## Remaining fidelity work

This is a playable tower implementation, not a complete retail port. Terrain
meshes and some actor/item/trap visuals are proxies. Heights, encounter tables,
monster abilities, item/trap effects and combat values still include documented
authored fallbacks. The run seed remains the existing deterministic demo seed.
The floor-2 visitor has a placeholder visual and authored dialogue: original
visitor selection, story conditions and event scripts are not yet bound.
Koh's supplied walking sheet has one direction and authored timing. Source
art/geometry exports remain available for subsequent visual integration.
