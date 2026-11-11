# Tower delivery: implementation ownership and acceptance

## User priorities recovered directly from Architect's conversation

Source: Architect Bot Chat message2347, session local-5260107c3b975fc4102f67a8333dadc8b7cf454c110a2db9ce7ecf192bead59d.

1. Koh player prefab using Koh art, including jump, lift objects, run and NPC interaction.
2. Pulunpa prefab with original species data, behavior, movement pattern, animations and level-stat progression.
3. Integrate these into a working tower, maximizing implemented original behavior rather than independent unused helpers.

The original implementation is the behavior specification. Match original mode restrictions, integer semantics, RNG consumption and action timing; do not assume all listed actions are interchangeable across town and tower. Unknown assets/formulas remain explicitly unresolved, not replaced by invented retail claims.

## Ownership directed by Hermes

- Architect: new Koh/Pulunpa components, definitions, prefab variants and focused tests; propose exact exclusive paths before shared edits. Start with a runnable Pulunpa vertical slice, then Koh action slices. Reuse verified art; do not overwrite existing art assets. Source functions, integration API, executed tests and artifact paths accompany each delivery.
- Hermes: existing TowerGameController*.cs, action/input wiring, shared integration and sole publication of Core.dll. Keep current floor/run/item manager contracts stable; negotiate any shared/Core changes explicitly.
- Existing independent QA remains separate from implementation validation. A message receipt is not acceptance, a written pseudo-tool call is not execution, and an author's green isolated test is not integrated approval.

Architect instructions were queued using delivery ids cab88b18558bdd2ff8a6ef1ca11c24ed5985a88c26baba13e539826aea4f6b22 and57e2f64bb2f9b1287e88723083dc2c71dafe898f82b4230b279914289b30ccf3. The second message corrects the initial generic manager suggestion to prioritize the user's Koh/Pulunpa request. No acceptance or implementation completion has been verified yet.

## End-to-end gate

- Load the actual scene and enter tower from town.
- Generate a walkable floor with reachable entry/exit and preserved placement metadata.
- Exercise player commands and Pulunpa behavior through the shared action/presentation flow, not a substitute simulation.
- Collect items exactly once, preserve item identity, trigger traps according to their implemented effect, and restore recorded terrain.
- Change floor without inheriting destroyed enemy state or leaking procedural resources.
- Die/return to town with one-time inventory/reward transfer and correct saved town progress.
- Verify controller APIs plus real input; record source/assembly hashes, XML/logs and asset dependencies. Rendering, WebGL and original-rule parity are separate gates.

## Follow-up executed by Hermes: generated-floor camera

Architect's first delivery reply contained a literal JSON pseudo-message, not artifact paths or execution evidence. It does not establish acceptance or implementation. The later Koh/Pulunpa assignment remains pending; no duplicate message was sent in response to that receipt.

Added `TowerGameController.Camera.cs`: generated-floor camera follows the actual interpolated player view in `LateUpdate`, preserving the current authored offset. It does not advance the simulation or change town/authored-demo camera behavior. This is an authored Unity presentation fix, not a claim of porting the original PSX camera equations.

Observed missing-follow RED, then investigated a cross-frame assertion mismatch: UnityTest yield-null resumes before LateUpdate. A late-execution-order observation-only test component now records camera/player offsets after the real controller LateUpdate, without manually invoking/replacing the camera path. Three regressions cover follow beyond demo bounds, stopping follow on return to town and preserving the authored-demo fixed camera.

Final matching138-source/assembly input snapshot:112/112 original PlayMode,180/180 combined EditMode,140/141 combined PlayMode. The sole combined failure remains the Floor-versus-Corridor trap assertion above. Evidence: `Logs/TowerRunIntegration/generated-camera-verification.json`, `generated-camera-final-*.xml/.log/*-snapshot.json`, plus the RED/focused runs. DLL hashes unchanged.

## Follow-up executed by Hermes: first verified Pulunpa spawn slice

A separate delegated executor created the first concrete Pulunpa implementation after the Architect's two replies contained only literal JSON text and no artifact or execution evidence. This is tracked as delegated execution, not as Architect completion.

Files and hashes:
- `Assets/AzureDreams/Scripts/Actors/PulunpaSpawn.cs` — `ba24376e93b40725e1793b00f53dc1aca5655bdef2561642f9eace4f0153643c` (after the resist-sleep bit correction to `0x4000`)
- `Assets/Tests/Editor/PulunpaSpawnTests.cs` — `0c57656d3c2a06865e1ab42e4af4f576285770e54a131f7c6ceb3063592d41c9`
- `Assets/Tests/PulunpaSpawnRuntimeTests.cs` — `f5cff3620c73f9ed4b6827c5209e051a5f6ec12269e79d288c8da2d65a7148a8`

API: `PulunpaSpawn.Initialize(flags, kindId, variant, spawnValue, RetailRng rng, variantCode)` creates a fresh `ActorRecord`, sets species `PULUNPA`, applies kind-based ally/enemy flags, performs the original sleep roll with exact RNG consumption, selects frame/callback/variant, and sets raw bytes `0x9A=0xFF`, `0x9C=-1`, `0xA8=0`. It exposes `Record` and `LastResult`; it does not advance turns, register nodes, invent attributes or animations, or replace the unpublished status-slot algorithm.

Original references and branch coverage:
- `spawn_pulunpa.c:create_actor_grid_object` lines 56–129.
- `kind = flags & 3`: kind 1 sets ally/party flags; kind 2/3 sets ally flags; kind 0 with `(flags & -4) == 0` performs the sleep roll, otherwise normal path.
- Sleep roll consumes one RNG only when `!(flags14 & ACTOR_RESIST_SLEEP)`, then a second RNG and sets `SLEEP` slot 3 to `(random & 0x3F) | 0x20` only when the first random is odd.
- Frame/callback selection uses `variantCode < 10` for normal resume/frame set 1 versus variant action/frame set 2 plus byte A7=1.

Validation in the isolated clone:
- Focused EditMode: 7/7.
- Focused PlayMode runtime: 1/1 using a real GameObject.
- Combined EditMode: 187/187.
- Original PlayMode assembly: 113/113.
- Combined PlayMode: 141/142, sole failure `CoderGeneratedLifecycleTests.Trap_UsesAuthoredFiveDamage_AndClearsTrapFlag` (Floor expected, Corridor restored). DLL hashes unchanged.
- Evidence: `Logs/TowerRunIntegration/pulunpa-spawn-verification.json`, `pulunpa-corrected-*.xml/.log/*-snapshot.json`.

Visual binding and animation tables remain unresolved, not represented by a proxy prefab. Koh is not implemented. The component is ready for controller integration but has not been wired into `TowerGameController` yet.

## Verified baseline and remaining gaps

- 109/109 original PlayMode assembly.
- 180/180 combined EditMode, including71 eligibility/terrain cases.
- Combined PlayMode137/138: remaining CoderGeneratedLifecycleTests.Trap_UsesAuthoredFiveDamage_AndClearsTrapFlag expects Floor while the manager restores Corridor. Review pending; no fully green combined gate claimed.
- Eight-direction facing/front attacks are connected. Movement remains cardinal.
- Original diagonal side-height query is ported/tested, but its real map-height/offset provider is not wired into the playable attack path.
- Generated floors remain opt-in; flat terrain, authored damage/spawns/trap effects and unsupported mid-run saves must not be described as original-complete behavior.

Evidence for this baseline: Logs/TowerRunIntegration/terrain-side-verification.json and corresponding XML/log/snapshot files. Subsequent deliveries must freeze and validate their own combined snapshot rather than reuse these totals.
