# Actor eligibility and physical reach — implemented predicate layer

## Follow-up: original diagonal side-height predicate

`TowerActorEligibility.CheckDiagonalSideHeight` now ports `azure-decomp2/src/dungeon/level/tile.c:423–451`. `include/modules/dungeon_engine_tile_map_abi_views.h` establishes `map_height_wrapped(u16 x, u16 y, s16 minimum)` and its signed-short return.

- Cardinal directions return success without querying terrain.
- Diagonals query sides -1 then +1, masked to 0..7 after preserving the original signed-short direction assignment.
- A zero side-cell value skips that side's height sample.
- Direction offsets are supplied as raw unsigned-short words. World sample coordinates are narrowed to unsigned16 at the map-height call boundary; the minimum height stays signed16.
- Only a signed sampled height strictly greater than0x200 blocks the diagonal; equality and negative samples do not.
- First blocking side short-circuits. Missing required dependencies are reported explicitly rather than treated as clear terrain; skipped dependencies are not demanded.

This remains an engine-independent query compiled by Unity, **not a fabricated map provider or a newly wired playable terrain check**. Real offset tables and map-height sampling must still be supplied by a consumer. No manager, Core source or published DLL was changed.

Final source-matched execution:71/71 eligibility cases inside180/180 combined EditMode;109/109 original PlayMode assembly alone;137/138 combined PlayMode. The remaining combined failure is still `CoderGeneratedLifecycleTests.Trap_UsesAuthoredFiveDamage_AndClearsTrapFlag` (expects Floor, actual restored Corridor). No independent verdict or correction was returned in the delivery notice. RED/GREEN runs and current137 source/assembly hashes are recorded in `Logs/TowerRunIntegration/terrain-side-*.xml/.log/*-snapshot.json` and `terrain-side-verification.json`. Previous snapshot results below are historical, not relabeled as this follow-up's acceptance.

## New artifacts
- `Assets/AzureDreams/Scripts/TowerActorEligibility.cs`: engine-independent static predicates, compiled with the Unity runtime using the EXISTING Core.dll; no UnityEngine dependency in this file.
- `Assets/Tests/Editor/TowerActorEligibilityTests.cs`:51 cases covering the predicates and their dependency contracts.

## Original-backed behavior
`IsTargetEligible(ActorRecord target, byte linkedX, byte linkedY, Func<int,int,int,ushort> linkedTileFlags)` follows `azure-decomp2/src/dungeon/actor/ability_eligibility.c:554–575`:
1. Add unsigned-byte Hp and signed-short HpDelta as promoted integers; require positive total. Do not wrap to byte/short.
2. For flags1C.PlayerSide0x2000, query the caller-provided linked-entity position `(linkedX-1, linkedY, direction0)` and reject bit2. This query precedes the later flag mask. Do not substitute ActorRecord tile fields for the linked entity's byte coordinates.
3. Reject literal flag mask0x290000.
4. Reject an occupied status0x1B slot, including zero-valued slots (original get_entity_effect_value returns0x100 for that match).

`CanReachPhysicalAttack(...)` ports the physical branch of `choose_usable_ability_or_attack`, same C file181–228/308–316:
- Promoted signed-height difference must have magnitude below0x40.
- Chebyshev adjacency: eight neighbors, not Manhattan-only adjacency; reject same cell and farther cells before querying terrain.
- Use caller's actual rounded facing. Preserve `(u16)((s16)angle >>9)`, signed16 source-height-minus0x20, and signed16 interpretation of the side-height query result.
- This boolean predicate is not the entire ability selector. It does not invent spell chances, ability priority, terrain heights or a mapping of original action8 to the synthetic demo enum.

A missing required map/probe dependency raises ArgumentNullException instead of guessing that the map is clear. Rejected targets/ranges short-circuit without querying those dependencies, matching original ordering.

## Execution verified
- Targeted eligibility suite: **51/51**.
- Full frozen Unity PlayMode: **99/99**.
- Full frozen Unity EditMode: **149/149**, including the51 cases and current manager regressions.
- Zero failures/skips/inconclusive in successful runs. RED/GREEN evidence preserved per behavior in `Logs/TowerRunIntegration/eligibility-*.xml/.log`.
- `eligibility-verified-results.json`: enumerated counts and124 source/assembly hashes matching production. Both full runs used identical source snapshots.

## Architect manager evidence corroborated
Parent independently read `tower-mgr-editmode4.xml` (98/98) and `tower-mgr-playmode3.xml` (93/93) under Architect's scratch directory: every enumerated case passed, no skips/inconclusive. The five reported manager/test SHA256 values matched current production AND the existing v4 archive. Those manager fixes were therefore already included in the99/149 integration/predicate snapshot; they do not require a new v4 package or relabel the separate v3 QA. Architect's own93-test PlayMode snapshot is not the controller-integrated99-test snapshot.

## Snapshot artifact
`Logs/TowerRunIntegration/tower-eligibility-complete-v4.zip`
SHA256 `bafa0661e1ed31facb26eb6044f4f051d9027ab791502184e5436f43cbe886fd`.
Complete input closure:3413 payload files,124 source/assembly files, all Assets/metas, Packages and ProjectSettings. Every member checked in ZIP and after extraction. This new restored folder has not been launched for independent acceptance. The input manifest is inside the archive; version-specific verification file is `tower-eligibility-complete-v4-verification.json`.

## Important integration boundary
These predicates are implemented and tested but **NOT yet wired into demo attack selection**. The playable cardinal demo has not been given a fabricated linked-map query or terrain-height decoder. A real consumer must provide the original linked-position and side-height/occupancy semantics, then cover the full action path before claiming original target-selection behavior. Diagonal movement and all species-specific abilities are not delivered by this predicate module.

No xunit command, source Core build or Core.dll publication was performed. Pending ActorStatusEffects storage work remains separate and permission-blocked; these tests read existing ActorRecord data and do not test/replace the unpublished slot algorithm. No edits to Architect-owned managers or their tests in this slice.

Prior complete v3 remains immutable with its own99/92 run-lifecycle gate. Do not stop or relabel its independent QA because v4 adds predicates and newer manager tests; approval is per snapshot.
