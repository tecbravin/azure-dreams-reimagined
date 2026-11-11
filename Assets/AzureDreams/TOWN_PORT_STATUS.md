# Town port status and source evidence

## Confirmed visual replacement (2026-11-04)

ReimaginedDemo now uses Prefabs/Koh.prefab with the visually identified Koh
walking sheet, preserving the protagonist's gameplay configuration. Source
meshes are hidden; color-key removal preserves the original RGB image. The
single available direction and authored playback timing are documented in
Documentation/ConfirmedSceneVisuals.md. Provisional demo buildings and NPCs
remain pending identity proof. Native graphics-enabled validation: 48/48 cases
passed in Logs/ConfirmedVisualPlayMode.xml; rendered screenshot inspected.

## Community assets integrated and validated (2026-11-04)

Documentation/Community/Integration.md records folder destinations and source
crosswalks. Native Unity import verification passed: 8,457 source references,
44 captured scene models and 2 fonts. The manifest resolves to 6,701 canonical
assets, with every output hash verified. Building-score references include
source bytes from authenticated TOWN.BIN; semantic matches remain provisional.
146 lexical item-text matches and the corrected 46-monster / 1,191-animation
inventory are recorded under Assets/AzureDreams/Data/Community. These assets
are available for scene integration; existing gameplay was not overwritten.

## Community reference update (2026-11-04)

Documentation/Community/README.md indexes the supplied ODS, two PDF compendiums,
archived item guide and sprite collection. Structured references preserve 139
Town/Tower event cells and 16 building-score records with source coordinates.
The sprite inventory validates 4,419 PNGs and 1191 contiguous animation groups
across 46 source monster IDs; timing and identity mapping remain unverified.
Community building IDs differ from model filename IDs (e.g. Koh house LV1), so
no automatic ID join or runtime rule override was made. Fountain donation
edge cases, pool deadlines and building-score prerequisites are now recorded
as source-attributed QA/research candidates. See the linked README for limits.

## Latest geometry and texture recovery (2026-11-04)

Native Unity: 47/47 cases pass in Logs/TownGeometryAndVramPlayMode.xml.
The BIN extractor recovered Town type-0 geometry: 934 face lists and 2,314
vertices, with original packet controls, UVs, TPAGE and CLUT. TownRetailGeometry
ports the view-dependent packet traversal with bounds checks. TownRetailVram
samples indexed/direct PS1 textures and tracks missing source coverage.
The texture extractor replays 39 image/palette uploads from four shared banks
and variant zero, exporting VRAM plus an explicit known-word mask. Both tools
authenticate the resident executable and TOWN.BIN before exporting.
See Documentation/TownTerrainRecovery.md for hashes, source addresses and tests.
Some referenced palettes/uploads remain unresolved. Original meshes, complete
Town composition and NPC bindings are still pending; the visible scene remains
the provisional reconstruction. Passing these tests does not establish parity.
## Latest terrain recovery

The current Unity Town suite passes 40/40 tests in
Logs/TownRetailTerrainPlayMode2.xml. TownRetailTileGrid now ports both direct
and parity-based halfword tile copies, 128x128 queries, and source-bank reads.
Tools/extract_town_layout.py verified every payload byte of 27 raw BIN sectors
and imported the 55,296-byte bank into Resources/Town/Retail, with tile-table
and initial plot/assignment metadata. Documentation/TownTerrainRecovery.md
records the source contracts, offsets, validation and remaining mesh/texture
bindings. Documentation/TownRetailPlotLayout.svg is a source-data diagram.
The demo's visible terrain remains provisional; the recovered data is not yet
bound to a complete retail Town mesh or NPC/script layout.
## Native Unity validation (2026-11-04)

The native Editor is usable. Earlier attempts were blocked by a task-owned
headless BuildTownAssets process (PID 32848) left running without -quit. Its
completed builder was identified from the exact command line, then stopped;
subsequent batch runs now exit automatically.

The actual Unity prefab verifier found Tower_Gate lacked TownGateDoor and its
hinged leaf. RepairTowerGatePrefab now adds those missing elements to the
existing prefab without rebuilding other assets. The repair was applied, and
all 12 prefab checks passed (Logs/TownGateRepairAndVerify2.log).

Unity PlayMode executed TownInteractionIntegrationTests: 31/31 passed in
Logs/TownCurrentPlayMode2.xml. The gate-return test now waits for the original
16-update opening ramp before requiring TowerRunning; it also verifies that
interaction does not skip the ramp.
A separate graphics-enabled Direct3D11 run passed the real keyboard/camera
smoke test (1/1, Logs/TownCurrentVisual.xml), capturing the current scene at
Logs/Movement/demo-town-smoke.png. This validates the current reconstruction's
movement/rendering and Town integration scope, not visual or story parity
with retail. The camera, map, homes and NPC identities remain provisional.
## Latest correction: service +0x2F8 is an audio reservation

The native service binding is now proven, superseding older unresolved-callback
notes below. `menu/window.c` initializes `town_resource_context.field20` to
`D_800D3CC8`; MIPS at TOWN.BIN 0x1FEF4..0x1FEFC confirms the table address
and the store at context +0x20. Its slot 0x2F8 (native call 190, file 0x56860)
points to 0x800C17A0, `town_seq_reserve`, whose body at file 0x44040 stores
pending stream/sequence halfwords. Service-table anchor entries 123 and 125
also match their named native functions. The extraction tool verifies these
instructions and pointers and records the binding in its JSON report.

`TownRetailStoryDispatcher` now documents this audio role and has a typed
`TownRetailAudioState` destination. The pairs do not launch narrative scripts.
`TownRetailAudioState` ports the reservation/update path: -1 preserves values,
-5 clears them, changed streams stop/drain/load before the sequence command,
and the hold flag suppresses reissuing an unchanged sequence for that update.
Pending selections reset after application and the active state is saved.
The sound-driver backend and stream resource mapping remain separate; existing
extracted WAV filenames do not establish an ID-to-track mapping.

Runtime and PlayMode assemblies compile with 0 errors and 18 existing renderer
warnings. `Tools/VerifyTownRetailManaged.ps1` executed all 16 listed pure managed
regression methods successfully against the compiled assemblies, recording their
SHA256 hashes in `Logs/TownRetailManagedResults.json`. This is direct method
execution outside the Unity test lifecycle. Scene/rendering, audible playback,
and native PlayerPrefs behavior remain unverified.

This note separates the playable Unity reconstruction from behavior that is
actually established by the retail decompilation. Do not treat the current
NPC names, dialogue, shop stock, or world coordinates as retail data.

## Current playable reconstruction

- `TowerGameController` generates one outdoor Town with textured ground,
  paths, reconstructed houses, a tower gate, a fountain, a sign, a chest,
  two NPC prefabs, and item pickups.
- The Town supports free movement, NPC conversation, a Rowan-labelled demo
  potion transaction, fountain healing, object interactions, tower entry, and
  return to Town.
- Returning from the tower places the player by the reconstructed gate. This
  pose is provisional; the retail destination position record has not been
  decoded.
- The tower gate now creates its hinged leaf at runtime when the existing prefab
  lacks one, then opens in 16 fixed-angle updates (0x40 through 0x400) before
  entering the tower, matching the progression in town/map/door.c. The map
  transition fires in the same update as the final 0x400 increment, matching
  `open_door_to_target`; it no longer waits an extra Unity frame. This is
  source-backed timing; the reconstructed leaf geometry and exact town spawn are
  still provisional.
- The gate's facing check now quantizes actor/gate bearings to the retail
  12-bit angle circle and applies `((doorAngle - actorAngle) + 0x3FF) < 0x7FF`
  from `check_door_input_and_angle`. The bearing is still inferred from the
  player-to-gate direction because the retail door orientation record has not
  been decoded; collider overlap is also represented by a proximity check.
- `town/map/door.c:check_door_input_and_angle` first calls
  `get_smallest_signed_overlap_axis_amount_two_intersecting_boxes` on the door
  and player actors. A strict Unity `Bounds.Intersects` check cannot be dropped
  in without changing the current return loop: the reconstructed return spawn
  is x=8.5, the gate is x=10, and the fallback CapsuleCollider (radius 0.5)
  ends at x=9.0 while the gate trigger begins at x=9.05. Exact destination-door
  position and actor collision records remain the source-backed next step;
  keep the proximity path until those retail dimensions/coordinates are bound.
- The authored characters currently named Mira and Rowan are not verified
  mappings to retail Town actors.

## Town art provenance audit

A local audit in `../../../extracted_assets/verified_decode/art_validation_20261102/README.md`
changes how the available graphics should be used:

- The Unity `TownGenerated` grass, path, plaster, roofs, wood, stone and water
  are authored placeholder textures, not recovered retail art.
- `../../../extracted_assets/town_slots/` has the wrong provenance: its 35 files match
  DUNGEON slices, not TOWN. Do not feed those files to Town prefabs.
- The corrected 35 modules under
  `../../../extracted_assets/verified_decode/art_validation_20261102/town_scene_modules_corrected/`
  match TOWN.BIN and are classified as scene code/data; visual subresources are
  still unresolved.
- Twelve TOWN VRAM uploads are decoded and hash-verified under
  `../../../extracted_assets/verified_decode/art_validation_20261102/town_vram_raw/`,
  but their bpp, TPAGE, CLUT and semantic use are not bound. Existing RGB555
  previews are diagnostic only and must not be assigned as final NPC or terrain
  textures.

## Verified retail event data found locally

The extracted US `TOWN.BIN` is at `../../../extracted_assets/files/TOWN/TOWN.BIN`.
The decompilation documents these event subfiles as loading into
`0x8001DCD0`:

| File offset | Extent | What the text scan establishes | What remains unknown |
|---|---:|---|---|
| `0x2D2000` | `0x2000` bytes | Header word is `0x0B`. The first command is `SET_ACTOR_MOTION` (`0x0F`) for actor 11, motion 3; decoded text then covers park/town-improvement dialogue and response branches. Scene 0x2AB800's event table at `0x8001C18C` loads this subfile into `0x8001DCD0`. | Which retail resident owns actor 11, event prerequisites, and whether this event runs during the outdoor starting Town. |
| `0x2D9800` | `0x1000` bytes | Header word is `0x0F`; it is not proven to be an actor ID. The first command is `CALL_NATIVE` (`0x4C`) to `0x8001E98C`, followed by page-break/end. Decoded text covers a monster-tamer encounter, familiar recruitment choices, and a tower tutorial. Scene 0x2AB800's event table at `0x8001C1AC` loads this subfile into `0x8001DCD0`. | The native routine's branch/flag effects, actor/object placement, choice-result wiring, and required story flags. |

The offsets are corroborated by
`../../../azure-dreams-decomp-master/docs/evidence/town_prototypes_run7/owner_audit.md`
and the authenticated row-base records for `town_ovl_2d3f0c` and
`town_ovl_2da5e4`. The text itself uses fullwidth Shift-JIS glyphs with
interleaved bytecode; scanning decoded strings alone is not enough to bind
dialogue to an actor or reconstruct event control flow.

## Shop and building fidelity limits

- The decompilation establishes item prices are category/record based, with
  separate buy and sell values and optional category callbacks. It does not
  support one universal fixed potion price.
- The Unity Rowan offer (one “Healing Draught” at 20 coins) is demo content;
  its actor identity, stock, exact item mapping, price, and dialogue are not
  established by the evidence located so far.
- Retail building placement checks resident flags, record kinds, assigned
  land slots, dimensions, and pending palette updates. The current static Inn,
  Market, and Residence are visual stand-ins; Town level is not sufficient
  evidence to reconstruct their unlock rules.

## Next source-backed steps

1. Bind each loaded event subfile to its scene module and actor/event table.
2. Decode the entry conditions and choice branches before assigning retail
   dialogue or NPC identity in Unity.
3. Resolve the retail door destination/map record and position record before
   replacing provisional Town placement and return coordinates. In
   `town/map/door.c`, `emit_door_position_effect` and
   `trigger_door_interaction_effect` pass the position record's fields at
   offsets `+0x02` and `+0x06` into the player-position initializer; the map
   destination is selected separately by `change_door_map` from its selection
   mode and map table. `town_scene_loader_receipt.json` confirms the outdoor
   entry is area `0x0B`, room `0x01`, loading `TOWN.BIN+0x2AB800`; its listed
   `door_actor_records` belong to area `0x0C` interior descriptors and must not
   be reused for the outdoor Town gate. The receipt's 33 descriptor-array
   entries are all area `0x0C`, so it does not expose an outdoor gate record;
   continue through the startup actor/event setup for module `0x2AB800`.
   `read_map_cell_at_world_position`
   divides signed world coordinates by `0x40`, while
   `write_square_position_offsets` lays out cells at `0x40` intervals. This
   suggests (but does not yet prove for the door's position record) 64 retail
   coordinate units per map tile. The outdoor gate record and its actual
   coordinate values still need binding. The available scene-slot routines
   register objects into slots `24`, `25`, `30`, `38`, `46`, `57`, `62`, `69`,
   `83`, and `84` using imported setup tables, but their receipts do not map
   those slots to a Town resident or gate. Slot numbers alone are not actor
   identities; the setup tables and outdoor scene roster still need decoding.
4. Decode building/item tables and callbacks before claiming shop stock,
   prices, or construction unlock parity.

The scene-module evidence resolves the former “which module loads it” gap, but
does not identify actor/person table entries or prove event prerequisites.
Module `0x2AB800` is the initial Town scene module (loaded at `0x80016000`,
entry `0x8001A484`; the documented initial scene is area 11 / room 1). Its
event tables are a promising reverse-engineering path, but should not yet be
wired to the reconstructed Mira/Rowan NPCs.

Until those bindings are recovered, keep the current content labelled as
reconstruction/demo data and avoid presenting guessed values as retail facts.

### Current Unity reconstruction follow-up

Town rendering currently uses the authored `TownGround`, `TownPathTile`,
house, fence, gate, fountain, and NPC prefabs with the generated Town materials.
The three house placements and path branches remain a readable reconstruction;
the retail outdoor actor roster and building coordinates are not yet decoded.
Returning from the tower rebuilds these generated objects while retaining the
controller's runtime inventory and restoring the player at the gate.

The local `extracted_assets/town_textures/` dump contains raw Town texture pages
including 4bpp pages, but this pass did not find the matching CLUT/palette
binding for the outdoor scene. They display as invalid colors without that
binding and therefore are not safe to assign to the generated materials. The
scene-loader receipt confirms the initial outdoor module, but known descriptor
records currently cover interior areas; exact outdoor placement and palette
selection remain unresolved.

Progress now loads the canonical `SaveSystem` values first and falls back to
the Town event variables when no canonical save exists. The familiar name and
potion count are mirrored with gold and town level in the extended Town save,
so town events and the main save stay aligned after purchases, pickups, and
tower returns. Gold and town-level events also update the canonical save while
in Town; tower events defer that write until inventory has been transferred
back. The two current demo pickups now have separate one-shot save IDs,
so rebuilding the Town after a tower trip no longer respawns already collected
items. Their placement and contents are still reconstructed demo data, not
confirmed retail Town pickups. These changes have not been verified in a
running Unity Editor.

## Existing automated Town integration evidence

The repository already contains earlier Unity Test Framework result XMLs under
`Logs/TownIntegration/`; these are historical results, not a fresh run in the
current Editor session. The latest broad named result files inspected,
`final-editmode.xml` and `final-playmode.xml`, report 37/37 and 76/76 passing
tests respectively. A separate `full-playmode-green.xml` reports 74/74. The
red/green iteration files document earlier fixed regressions (gate entry,
conversation blocking, fountain registration, object interactions, and NPC
patrol). The current Unity CLI still cannot launch tests because it reports a
project lock with unknown PID, so these results do not validate the latest
gate runtime fallback or current working tree.

The current gate integration assertions were updated to require map transition
on the same update as the 16th `0x40` opening step, including the diagonal
approach path. They have not been rerun: `unity status` currently reports no
Editor instance with the Pipeline package installed.

## Town environment composition pass

The procedural Town now instantiates the existing `Fence_Section` prefab as
two short frontage segments at the Inn and Market. The middle openings line up
with the current footpaths at x=2 and x=8. This adds readable lot boundaries
without closing the authored placeholder routes. The Town ground, path tile,
three house prefabs, and Mira/Rowan NPC prefabs were already present in
`Resources/Town`; the house and character meshes/materials are reconstructed
project assets, while the grass/path materials use the local Town textures.

These frontage positions still belong to the reconstructed 11x11 demo layout.
They are not claims about retail PS1 coordinates: the outdoor map's exact
placement records and CLUT binding remain unresolved. Unity scene/Play Mode
visual validation is still pending because the project reports an unknown-PID
Editor lock.

## Map data distinction from the decompilation

Further source tracing found `town_tile_grid` at `0x80162004`, cleared and
indexed as a 128-by-128 halfword grid. Its observed consumers include
`initialize_building_graphics`, `initialize_building_shared_buffer`, and
`copy_variant_building_tiles`; the graphics initializer uploads building
texture/palette buffers and loads packed building resources. This is evidence
for the Town building/land-management subsystem, not a recovered 11-by-11
outdoor street layout. The retail `town_map_set` also allocates eleven linked
map objects, but its object types/positions do not directly provide the
outdoor placement records. Those datasets must not be substituted for the
Unity demo paths or house coordinates.

A fresh `unity test` attempt for `TownInteractionIntegrationTests` still exits
before launching tests with “project is already open in a running Editor
(PID ?).” The current process inventory has no visible Unity Editor process;
`unity status` also reports no Pipeline-connected instance. No new Play Mode
result was produced for the environment composition pass.

The map return integration test now also waits for deferred object destruction
and checks that the rebuilt Town contains exactly one ground, one Inn, one
Market, and four frontage fence segments before re-entering the gate. The
runtime and PlayMode test assemblies compile successfully with the temporary
MSBuild inclusion for the stale project file's omitted `TownGateDoor.cs`:
0 errors and 18 existing `PsxSpriteRenderer` unassigned-field warnings. The
temporary target was removed; this compile does not replace a Unity PlayMode
run.

The existing `Town_UsesTexturedTerrainHouseAndNpcPrefabs` PlayMode test now
also checks all four Inn/Market frontage fence prefabs and verifies that their
positions leave the path center cells open. The test assembly and runtime
assembly compile successfully with `dotnet build` when a temporary MSBuild
target adds `TownGateDoor.cs`, which the stale generated runtime `.csproj`
currently omits. The temporary target was removed after the build. This is
compile evidence only; Unity PlayMode behavior remains unverified.

The retail `event_fountain_chk` source only reads the global fountain-event
flag `D_800133C4`; it does not establish that Town use restores health or mana.
The current full-recovery fountain remains a playable adapter with unverified
retail effect rules. The decomp symbol map binds town call-service slot 205 to
this function. No named writer or caller script with confirmed outdoor-Town
ownership was found in the current source pass, so the returned flag's exact
lifecycle and gameplay meaning are still open.

The retail event scheduler `select_next_event_state` is story-flag driven: it
prioritizes several one-time story branches (including `F_sero_end`,
`F_s04_niko_step`, and `F_veldil_dungeon_deatta`), then enabled town entries,
before falling back to other story states. The `0x2D9800` event subfile contains
full retail recruitment dialogue, two explicit familiar-join responses, and a
tower tutorial, but its actor placement and story-flag wiring are unresolved.
This is a concrete source-backed path for the next event port; it must not yet
be assigned to the provisional Mira/Rowan actors.

The native call target is in the loaded event bank itself: `0x8001E98C` maps
to TOWN.BIN file offset `0x2DA4BC` (`0x2D9800 + 0xCBC`). That span begins with
MIPS code, not more dialogue bytes. The caller's choices therefore depend on
an embedded native routine as well as the script opcodes. The scene-loader
receipt records `0x2AB800` for area `0x0B` / room `0x01`, and also observes it
cached while the current room is `0x03`; module residency alone does not prove
that the familiar event is scheduled in the outdoor room.

Disassembling the 60-word routine at `0x8001E98C` shows indirect calls through
the Town service table at offsets `+0x80`, `+0x220`, `+0x2E0`, and `+0x2E4`;
it also reads/modifies byte `+3` of a returned record before another callback.
`shared/town_services.h` currently leaves `+0x80`, `+0x2E0`, and `+0x2E4` in
padding because their roles and initializer bindings are not yet named. The
next source step is to trace those service-table entries and the record they
mutate; without that contract, the choice outcomes cannot be reproduced safely
in the Unity dialogue state machine.

The decompiled `event_state_dispatch_g00731.c` adds a more precise scheduler
contract than the earlier high-level `select_next_event_state` summary. It
advances a cursor modulo seven, resets seven table-selected event states and
three fixed event states, then tests the two pool-event flags (`Fp_bibian_pool_event`
and `Fp_mia_pool_event`). If both are clear, it scans up to seven rows in
`D_80019088`, comparing each row's condition flag and enabled value, dispatches
the matching row's event, and applies two trailing fixed event states. The
routine has a special fallback around cursor index five. At the time of this
source note the Unity Town had no corresponding scheduler; the exact row data
was decoded below and the deterministic algorithm has since been ported. Its
NPC dialogue and shop are still local demo interactions.

One adjacent helper, `call_scene_service_220_d_2.c`, demonstrates that service
slot `+0x220` is called with `(2, 13, 0, 0)` in that specific helper. This does
not establish that the `0x2D9800` familiar script uses the same semantic call
or identify slots `+0x80`, `+0x2E0`, and `+0x2E4`; the table initializer remains
untraced. Keep the native familiar choices out of the provisional Unity NPC
flow until the returned record's byte `+3` and callback effects are mapped.

The seven `D_80019088` rows are now decoded directly from `TOWN/TOWN.BIN` at
file offset `0x521088` (the decomp overlay map places the 56-byte table at this
offset). Each row is four little-endian halfwords: event, condition flag,
enabled, padding:

| Row | Event | Condition flag | Enabled |
| ---: | ---: | --- | ---: |
| 0 | `0x0607` | `0x11FA` (`Fp_niko_kenkatyu`) | 0 |
| 1 | `0x0608` | `0x11FB` (`Fp_far_kenkatyu`) | 0 |
| 2 | `0x0609` | `0x1472` (`Ft_selfy_love`) | 1 |
| 3 | `0x060A` | `0x11FD` (`Fp_cheriru_kangofu`) | 1 |
| 4 | `0x060B` | `0x11FE` (`Fp_bibian_pool`) | 1 |
| 5 | `0x060C` | `0x11FF` (`Fp_mia_pool`) | 1 |
| 6 | `0x060D` | `0x147A` (`Ft_paty_love`) | 1 |

This decodes the schedule inputs but does not prove event `0x0607`–`0x060D`
are all outdoor-town scenes: the dispatcher is in the Town overlay, but its
event payloads may transfer to interior or story scenes. The native eligibility
comparison is `(conditionFlagIsClear != enabled)`, not a generic “flag set means
run” rule. The dispatcher also checks both pool event flags before scanning.
Unity still has no retail script VM or handlers for these seven IDs, so the
ported scheduler cannot yet trigger playable retail scenes.
The decompilation's current C call graph contains no caller of
`advance_town_events_and_dispatch_entry` outside its own translation unit. Its
runtime scheduling point is therefore unresolved in this source set; Unity must
not run this cycle automatically on every Town entry until the state-machine
caller/trigger is recovered. The two love-condition symbols are named flags,
but their threshold/set rules and corresponding event scenes are still not
decoded.

The deterministic scheduler core is now ported to
`Scripts/TownRetailEventScheduler.cs`: it carries the seven retail rows,
increments/wraps the cursor, issues the ten retail reset calls, applies the
two-pool-event guard, uses the original clear-vs-enabled comparison, and emits
the trailing fixed events with the cursor-five exception. Its backend is an
explicit interface for flag queries, reset-state calls, and script dispatch.
The scene does not invoke it yet because its exact retail scheduling point and
the backend mappings to the missing script runtime remain unknown. This avoids
triggering story/interior events at an invented time while making the recovered
algorithm available to the forthcoming integration.

Compilation check: `AzureDreams.Runtime` and
`AzureDreams.Reimagined.PlayModeTests` compile with 0 errors and 18 existing
`PsxSpriteRenderer` unassigned-field warnings after temporarily adding the new
scheduler and the previously omitted `TownGateDoor.cs` to the generated
runtime project. The generated `.csproj` was restored after the build. This is
compile validation only; Unity Play Mode was not run.

Four scheduler regression tests have been added to
`Assets/Tests/TownInteractionIntegrationTests.cs`: retail row dispatch/order,
pool-event short-circuit after resets, enabled-condition flag behavior, and
the complete-scan cursor-five exception. They compile in the PlayMode test
assembly, but were not executed because this environment's Unity Editor test
runner is still unavailable.

Additional retail references clarify that these flags participate in a wider
event/state system. `select_next_event_state` gives priority to `F_sero_end`
and `F_s04_niko_step`; `select_event_data_and_fallback` sets `F_sero_end` for
event code `0x25`; `F_veldil_dungeon_deatta` gates entry-selection/shop paths;
and `Ft_paty_love` is passed to an event cue when selector 8 is used. These
usages do not identify which of scheduler events `0x0607`–`0x060D` invoke those
paths, nor establish their outdoor actor/scene association. The Scheduler's
read-only row view is now wrapped so consumers cannot mutate the recovered
retail data at runtime.

`select_event_data_table` is a separate path from the seven-row cursor. Its
selector modes return raw argument-pair tables rooted at `D_800178F8`,
`D_800178FC`, and `D_80017918`; direct bytes in `TOWN.BIN` at `0x51F8F8`,
`0x51F8FC`, and `0x51F918` begin respectively with pairs `(0x0014, 0x2631)`,
`(0x0014, 0x2610)`, and `(0x0028, 0x8FBF)`. Modes 9 and 10 select the second
pair root; modes 3 through 8 select the third. Mode 2 indexes the first table
by `selected_entry * 4`. The service at `+0x2F8` receives the selected two
signed halfwords. These are callback arguments, not identified dialogue IDs;
the callback's handler/data mapping is still required before they can become
Unity story events.

The service slot `+0x2F8` is a generic town-scene callback in the callers
available in the decompilation, rather than a direct dialogue function:
`invoke_callback_event_11` passes `(0x11, 0x200)`, and
`check_town_event_town_callback` / `set_town_pointer_town_callback` pass
`(0x0F, 0x200)` on their respective conditions. `start_dispatch_event` passes
the selected signed pair. Other fallback paths call `(0x10, 0x200)` and
`(0x0E, 0x200)`. The callback is thus a command interface used by multiple
scene branches, not a unique dialogue call. No service-table initializer or
callback registry was found in this source pass, so these command pairs still
cannot be mapped to retail scenes. Keep `select_next_event_state` (story
priority and entry selection) distinct from
`advance_town_events_and_dispatch_entry` (seven-row cursor and flag
conditions); the decompilation has not yet exposed their caller relationship
or a Unity-safe invocation point.

The story-priority branch of `select_next_event_state` is now ported as
`Scripts/TownRetailStoryEventSelector.cs`. It preserves the observed order for
the Sero/Niko and S04/Niko branches, Veldil saved-entry and return modes,
enabled-entry cycling, and the egg/Niko-love fallback. Its backend exposes
the two distinct retail flag-operation sites separately and accepts the
retail entry-query operations. It deliberately selects state only: the
callback pairs and story scripts are still unmapped, so this selector is not
yet called from Town entry/return.

Four selector tests now cover story priority, Veldil saved-entry mode,
enabled-entry fallback cycling, and the egg/Niko-love fallback; together with
the four scheduler tests they compile in the PlayMode assembly. The latest
Runtime and PlayMode assembly build completed with 0 errors and the same 18
existing `PsxSpriteRenderer` warnings. Unity did not execute the tests.

## Reusable Town art and Editor validation

`Assets/Editor/TownAssetPrefabBuilder.cs` already provides the Editor menu command
`Azure Dreams/Town/Build Town Terrain, Houses & NPC Prefabs`. It generates the
point-filtered 32x32 reconstruction textures and reusable `Resources/Town`
prefabs for ground, path tiles, three house types, fence sections, tower gate,
fountain, sign, chest and the Mira/Rowan placeholder NPCs. The runtime Town
loads these prefabs by resource name and uses the matching house/NPC instances
instead of primitive fallbacks when available. The builder also provides
`Azure Dreams/Town/Verify Town Prefabs & Textures` to inspect required prefab
components, geometry and texture import settings.

These assets make the Town repeatable and editable in Unity, but they remain a
visual reconstruction: no TOWN.BIN texture page or CLUT has yet been mapped to
these materials, and NPC identity/placement is not established by retail data.
The Unity CLI could not find a connected Editor in this environment, so the
Editor prefab verifier and a visual scene capture were unavailable in this
pass. Existing compile checks cover the C# runtime and PlayMode test assemblies;
they do not substitute for opening the scene in Unity.

The story selector port is in `Scripts/TownRetailStoryEventSelector.cs`.
It mirrors the branch order and state-byte updates in
`src/town/scene/event/start_dispatch_event_g01230.c`, while exposing flag
operations and enabled-entry queries as an injected backend because their
Unity save/event mappings are not yet recovered. Four selector regression
cases were added alongside the four scheduler cases in
`Assets/Tests/TownInteractionIntegrationTests.cs`; they compile but remain
unexecuted while no Unity Editor test runner is available.

## Current compile check (2026-11-04)

The current Runtime and PlayMode test sources compile with `dotnet build
AzureDreams.Reimagined.PlayModeTests.csproj --no-restore`: 0 errors and 18
existing CS0649 warnings in `Rendering/PsxSpriteRenderer.cs`. The generated
Runtime project omits `TownGateDoor.cs`, `TownRetailEventScheduler.cs`, and
`TownRetailStoryEventSelector.cs`; they were included temporarily for this
compile and the Unity-generated project file was restored afterward.

A previous Unity PlayMode result in `Logs/TownPrefabFocused.xml` passed the
single test `Town_UsesTexturedTerrainHouseAndNpcPrefabs`, but that report is
older than the current test source and must not be read as validation of the
current complete Town suite. A fresh batch verification could not be confirmed
because Unity exited before creating the requested log. The prefab verifier and
current PlayMode tests therefore remain pending in a functioning Editor run.

The `TownAssetPrefabBuilder` summary now accurately states that its terrain,
building and NPC appearances use authored reconstruction art. No recovered raw
TOWN VRAM preview is represented as a verified NPC texture.

## Recovered selection table for module 0x3E5000

`Tools/extract_town_selection_table.py` now authenticates the US TOWN.BIN SHA256
and checks the actual MIPS instructions in the table consumers before reading
data. Its output is `Documentation/TownRetailSelectionTable.json`.
The load mapping is supported by
`azure-decomp2/config/link/town/ll34_3e5000_80016000.json`.

The count/cycle/priority helpers reference records at RAM 0x8001791C
(file 0x3E691C), cycle bytes at 0x80017BB0 (file 0x3E6BB0), and priority
bytes at 0x80017BC0 (file 0x3E6BC0). Each of eight records is 12 bytes;
selection reads signed flags at +4 and +6. The cycle is
`1,2,3,6,5,1,2,3,6,4,1,2,3,6,7,0`; priority is `1,2,3,6,0`.
Repeated IDs in the cycle must be preserved, and EntryIndex is an index into
that weighted list, not an entry ID.

`TownRetailStoryEventSelector` now implements all four helpers directly from
`count_enabled_town_entries.c`, `find_first_enabled_entry_id.c`,
`find_first_enabled_paired_entry.c`, and `advance_to_next_enabled_entry.c`.
The backend now supplies only flag queries and the two retail flag operations.
`TownRetailSelectionTable.CreateForScene3E5000()` supplies the recovered flags
and orders for this module. Other scene instances require their own binding;
this factory is not automatically applied to all Town scenes.
Managed cycle searches are bounded to one complete order and reject inconsistent
data instead of allowing the retail loop to hang indefinitely.

Selector tests now exercise the paired-flag operation, table-backed fallback
cycling, and the recovered cycle's entry-4 position (index 9) versus the narrower
priority subset. These checks use the recovered data where available; synthetic
flag IDs in other algorithm tests are explicitly fixture inputs.
The flag operation semantics, event-script dispatcher, scene invocation point,
and map/actor bindings remain unresolved, so the live scene does not yet invoke
this selector. Current validation does not prove a retail Town gameplay loop.

After the table recovery and helper integration, Runtime and the PlayMode test
assembly compiled successfully again: 0 errors, 18 existing CS0649 warnings.
The extraction script executed successfully against the authenticated source.
The selector regression tests compile; Unity has not executed the updated tests.

## Retail bitmap backend implemented

The helper semantics are now established for the 0x3E5000 module, superseding
prior notes that left its flag operations unresolved. MIPS at file offsets
0x3ED578, 0x3ED5F0, and 0x3ED670 implements set, clear, and query respectively.
The resident C counterparts are town/player/state/set_script_flag_bit.c,
clear_script_flag_bit.c, and get_script_flag_mask.c. The helpers use words of
32 bits through town_scene_root+0x18. Query ID 0 is constant false and ID 1
constant true; set ignores ID 0 and clear ignores ID 1.

Scripts/TownRetailScriptFlags.cs implements these operations and supplies the
story selector backend. Its persistent factory uses tracked TownSaveVariables
keys, unsigned masks are preserved through signed PlayerPrefs integers, and the
existing Town save/clear lifecycle flushes/removes those words. No retail save
file layout or bitmap allocation extent is claimed. The selector remains
uninvoked in the demo scene until its retail invocation/dispatch mapping is known.

The extraction tool now verifies representative bitmap-helper instructions as
well as table references; Documentation/TownRetailSelectionTable.json was
regenerated successfully against the authenticated TOWN.BIN.
Runtime and PlayMode test assembly compilation passed with 0 errors and 18
existing renderer CS0649 warnings; the generated project was restored.
A direct execution of the compiled Runtime DLL with injected in-memory bitmap
storage passed constant queries, 31/32-bit word boundaries, isolated clearing,
and the Sero/Niko sequence (mode 9 once, morning flag set, conflict cleared,
then fallback mode 0). These are managed logic checks outside the Editor.
The two equivalent regression cases were added to the Unity test assembly but
have not been executed by Unity. PlayerPrefs persistence and scene integration
still need an Editor/runtime check.

## Module-specific story dispatcher

`TownRetailStoryDispatcher` now ports start_dispatch_event,
dispatch_enabled_message_coordinates, and dispatch_active_event_27 from the
0x3E5000 module, sharing the selection state and the new retail flag backend.
DispatchState is the observed byte at +1; callback order preserves when that
byte advances, and the enabled-message path sets flag 1032 afterward.
The injected callback receives signed service +0x2F8 arguments.

MIPS in select_event_data_table at file 0x3E8AFC references RAM 0x800178F8,
0x800178FC, and 0x80017918. File 0x3E68F8 contains pairs (-1,-1), (9,512),
(11,512), (10,512), (13,512), (15,512), (12,512), (16,512), (39,512).
The extractor now checks the address instructions and exports all nine pairs.
This supersedes using the different 0x51E000 module's byte contents for this
module. The callback arguments are scene-specific and are not dialogue IDs.
The callback service implementation and mapping to scene/event scripts remain
unrecovered; dispatch cannot yet be attached to the demo Town entry lifecycle.

Dispatcher validation: Runtime and PlayMode assembly compilation passed with
0 errors and 18 existing renderer warnings. Direct execution of the compiled
Runtime DLL passed phase gating, callback order/arguments, completion guard,
setting flag 1032, and the active 0x27/0x200 path. The extraction with callback
references/pairs completed successfully. These are managed checks outside Unity;
no visual/runtime scene verification or full retail gameplay parity is claimed.
