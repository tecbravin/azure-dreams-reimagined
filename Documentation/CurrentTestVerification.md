# Current full test verification — 2026-11-04

Unity 6000.5.6f1, D3D11, isolated project copied from current Assets, Packages
and ProjectSettings. The user's open Editor/unsaved scene was preserved.

Final native reports:
- Logs/CurrentFullEditModeFinal.xml: 187 passed, 0 failed, 0 skipped.
- Logs/CurrentFullPlayModeFinal.xml: 177 passed, 0 failed, 0 skipped.
- Logs/CurrentTestSummary.json records counts and source/copy equivalence.

The first complete PlayMode run exposed 56 failures. Old reflection helpers
still referenced singleton creature fields after the controller moved to
List<CreatureState>. Five fixtures now inspect/position the first list member
explicitly, with value-type writeback. Isolation helpers remove all creatures
when enemy effects are outside the test's scope. Assertions about lifecycle,
health, targeting, movement and shared turn timing were preserved.

The gate test now asserts that interaction does not instantly enter the tower,
then waits up to 20 frames for the existing 16-update opening ramp. A pickup
fixture now saves, clears and restores its own persistent potion-cache flag.
The defeated-enemy assertion checks removal from the active collection instead
of attempting to read a member from an empty list.

Three explicit attack tests revealed a production defect: TryTowerAttack found
a pre-turn target but TryTowerAction(Wait) reset _pendingCreatureTarget to -1.
The explicit attack now restores the selected creature index after preparing
the shared turn, so TickTurnPresentation applies the committed damage. Existing
cardinal, diagonal, keyboard-aim and snapshot tests now pass.

The WebGL availability test no longer switches the active platform mid-suite.
It calls BuildPipeline.IsBuildTargetSupported instead. Its former switch caused
a full project reimport and changed the environment of unrelated tests. The
first isolated run was stopped after this cause was confirmed; its log remains.

No failing tests were ignored or disabled. The latest complete EditMode and
PlayMode reports supersede earlier partial or failing reports. These results
verify the existing test scope, not completion of the original-game Town port.
See AssetCompatibilityRepair.md for the Timeline/PNG/shader/CLUT repairs.
