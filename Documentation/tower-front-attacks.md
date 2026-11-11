# Unity tower front attacks: eight-direction command slice

## Original reference and scope

Read directly: `azure-decomp2/src/dungeon/player/position/begin_front_tile_attack.c:66–93`.
The original indexes eight `dir_step` entries using `((u16)facing >> 9) & 7`, selects the front target before entering its attack sequence, and still appends the attack action when no target is found. The physical branch of `ability_eligibility.c:216–226` independently confirms Chebyshev adjacency.

This slice ports the eight neighboring command directions into the existing Unity action adapter. It does NOT claim to implement the original side-height probe, allied-target suppression, species abilities, damage arithmetic, or original scheduling.

## Changes

- `TowerGameController.Actions.cs`: facing and explicit front attacks accept the eight nonzero unit offsets. Zero, out-of-range and extreme integer inputs are rejected without turning or advancing the clock.
- `TowerGameController.cs`: Shift + two directional keys preserves both axes for aiming. F attacks the selected direction. Ordinary movement retains its existing cardinal projection because the published `TurnSystem` explicitly rejects diagonal movement intents.
- Pre-turn target selection is preserved: an enemy entering the attacked diagonal during resolution is not retroactively hit.
- Facing costs no turn; an accepted swing, including a diagonal empty swing, costs one shared turn. Busy input cannot enqueue a second attack.
- `TowerActorActionsIntegrationTests.cs`: five new tests cover a diagonal hit, all eight empty-swing directions, malformed/extreme inputs, incoming-target snapshot behavior, and actual InputSystem Shift+W+D then F wiring. The old malformed-input fixture now uses (2,1), since (1,1) is deliberately supported.

No manager, source Core, published DLL, scene or prefab was edited in this slice. Generated floors remain opt-in; their current Core grid is 64x64, not 32x32.

## Executed verification

All execution used the isolated `azure-tower-run-integration` clone, never the main Editor. Final clone identity: companyName `Hermes Coder QA`, productName `Tower Run Integration QA`. Company name was aligned only in the clone to satisfy the additional acceptance fixtures' explicit safety guard; the production preferences were not cleared.

- Observed RED then GREEN for diagonal API attack and real diagonal keyboard aiming.
- Focused actor action suite: 28/28. A later source comment changed its hash; the complete assembly below includes these same tests against the final sources.
- Original PlayMode assembly, alone in a fresh process: **109/109**.
- Combined EditMode assemblies: **160/160**.
- Combined PlayMode assemblies: **137/138**, one failure, no skips/inconclusive. **This is not an all-green combined acceptance.**
- Remaining failure: `CoderGeneratedLifecycleTests.Trap_UsesAuthoredFiveDamage_AndClearsTrapFlag` expects the triggered cell to become `Floor`, while the manager restores its recorded `Corridor` terrain. Health reduction, trigger count and trap-flag assertions preceding that assertion pass. The manager retains source terrain in `_trapTerrain`; this test and the manager were not modified. Review request queued to Game Tester; no independent approval claimed.

The first combined attempt hit 19 fixture setup guards because its prior clone companyName differed. That failure is preserved separately, not relabeled as a gameplay regression or a green execution.

## Evidence

`Logs/TowerRunIntegration/front-eight-*.xml`, `.log`, `*-snapshot.json`.
`front-eight-verification.json` reconciles enumerated test cases, commands, failures and 137 source/assembly hashes. Final combined PlayMode, original PlayMode-only and combined EditMode inputs match current production source/assembly hashes. Published DLL hashes are unchanged since the initial RED run.

Pending: combined acceptance test review; original terrain/height queries and party-target policy; diagonal movement; complete monster ability selection. Existing eligibility predicates remain separate from the playable attack adapter until their real map dependencies are available.
