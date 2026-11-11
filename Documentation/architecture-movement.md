# Azure Dreams — movement architecture and verification handoff

## Implemented structure

- `TowerGameController.cs` retains scene construction, input, inventory and area transitions.
- `TowerGameController.Movement.cs` adapts the tested pure core to Unity. Town uses continuous, normalized movement with bounded collision substeps. NPC/building/wall footprints block movement. The tower gate switches to the grid-based tower.
- Tower actions read one pre-turn snapshot for player, creature and familiar. The pure resolver commits cell changes; all views interpolate using one shared clock. A frame completing a turn cannot also accept a second action. Wait and successful potion use consume one shared turn. Idle frames do not advance AI. Stairs are processed after presentation completes.
- Competing prefab player/monster transform drivers and Animator root motion are disabled by the demo controller. Generated objects are parented to the controller so their lifetime follows it.
- `AzureDreams.Core/TurnSystem.cs` applies deterministic demo conflict rules: no swaps, higher-priority same-target winner, stable input-order ties, recursive blocking propagation, guards for malformed moves/duplicates. Include all actors, including waiters, exactly once.

**Fidelity boundary:** simultaneous presentation matches the user's requested structure. Current conflict priorities, simplified damage/AI, demonstration map and recruitment are demo decisions, not verified retail PS1 scheduling/mechanics. Town locomotion visual treatment is also presentation, not recovered original animation data.

## Core integration

`Assets/Plugins/AzureDreams.Core.dll` is now consumed explicitly by `AzureDreams.Runtime.asmdef`. `Tools/sync_core.py` runs the core tests and builds/imports the Unity-safe netstandard2.1 assembly. The build property `UnityPort=true` excludes the desktop filesystem-based `SaveSystem.cs` and its System.Text.Json dependency; Unity retains its browser-compatible persistence adapter. Movement/ActorRecord source is shared, not rewritten.

Run the sync script with TMPDIR set to a caller-owned scratch directory. Do not run competing builds/restores of the same core project simultaneously. `Documentation/core-import.json` records the imported DLL SHA-256 and source digests. Re-run the sync after core source changes, before Unity QA.

## Renderer repair

`PsxSpriteRenderer` retains serialized atlas, metadataJson and palettesJson. It supports import-based data and opt-in StreamingAssets downloads. Streaming PNGs are downloaded as raw bytes, decoded into a linear texture, and their owned sources are released on disable/destroy. This avoids treating index bytes as color-managed texture data.

Frame construction uses Color32 buffers and bounded coroutine batches. Active resource leases share frames; an in-flight registry prevents concurrent duplicate baking. Last release destroys generated Sprites/Textures, cancellation destroys partial frames, and generated frame textures discard their CPU-readable copy after upload. The shared lookup retains at most 32 active resource entries; extra resources use noncached owned leases.

Nested RGBA preview arrays are normalized for JsonUtility. Actual RGB555/STP words take precedence when present: word 0 is transparent, STP black is visible. Callback-only parts are skipped, and this CPU preview renderer does not implement full retail callback sequencing or PS1 ABR blending. Editor-time sprite-sheet baking remains a possible future optimization, not part of this runtime repair.

`PsxLowResRender` no longer redirects the scene camera to its own post-effect buffer. It downsamples the actual source and presents to Unity's actual destination, avoiding the previous self-blit/missing-output problem. Its fixed buffer has no depth attachment, is created outside rendering and released on disable. **This is a visual pixelation effect, not reduced scene shading; WebGL speedups are unmeasured.** Linear fog remains a visual setting; shader support and GPU cost still require target profiling.

## Actual developer checks

Executed with Unity 6000.6.7f1 in an isolated copy of Assets/Packages/ProjectSettings; the open user Editor was not closed or used as a concurrent batch project.

| Check | Result |
|---|---|
| Pure core suite | 97 passed, 0 failed, 0 skipped |
| Unity full PlayMode | 63 passed, 0 failed, 0 skipped |
| Unity full EditMode | 37 passed, 0 failed, 0 skipped |
| Focused renderer/GPU/lifecycle subset | 7 passed (subset of EditMode; do not add to totals) |

PlayMode includes 15 controller integration cases covering continuous movement, collision, normalized diagonals, gate/stairs, shared player/creature/familiar progress, idle freeze, wait, reentry lock, snapshot attacks, transitions, and preserved locomotion/pickup/inventory tests.

TDD evidence includes movement RED before the integration (missing controller methods), the fallback-spawn regression, a real low-resolution presentation failure, and duplicate in-flight texture construction before its guard was implemented.

Evidence directory: `C:/Users/tecbr/AppData/Local/hermes/profiles/architect/cache/scratch/`:
- `movement-red3.xml`, `movement-green1.xml`, `movement-green2.xml`
- `lowres-red2.xml`, `inflight-red.xml`, `renderer-green.xml`
- `playmode-full.xml` / `.log`
- `editmode-full.xml` / `.log`

An initial broad EditMode attempt timed out during audio asset reimports; it was not counted as a pass. The subsequent complete full EditMode run finished and produced the passing XML above.

## QA gate and remaining work

Independent execution and visual smoke testing have been requested from `@game-tester`; their report is pending. The counts above cover the executed snapshot; a subsequently added `DemoSceneMovementSmokeTests` and its test-assembly reference update are not included in those counts and are being developed/validated separately. Developer checks do not replace that gate. WebGL/browser execution, memory/frame-time profiling and long-session resource measurements have not been performed in this pass. Original floor generation/geometry, complete sprite animation semantics and verified retail combat remain separate reconstruction work.
