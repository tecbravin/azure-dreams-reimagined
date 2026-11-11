# Tower In-Tower Manager Systems — Integration Contract

Architect-owned managers (new files, separate from TowerGameController*.cs):

## 1. TowerFloorManager (`Assets/AzureDreams/Scripts/TowerFloorManager.cs`)
Wraps the pure core `AzureDreams.Core.DungeonGenerator`. Deterministic for (floorNumber, seed).

API:
- `void Generate(int floorNumber, int seed)` — generate/regenerate a floor.
- `void AdvanceFloor()` — increment CurrentFloor (no regen).
- `void Reset()` — reset to floor 1.
- `bool IsWalkable(int x, int y)` / `bool IsBlocked(...)`
- `int GroundHeight(int x, int y)` / `void SetHeight(int x, int y, int h)`
- `Vector2Int FindOpenCell(Vector2Int preferred, params Vector2Int[] blocked)`
- Props: `CurrentFloor`, `Seed`, `Dungeon` (core), `StairsDownCell`, `StairsUpCell`, `Heights`, `IsGenerated`, `OpenCells`, `RoomCount`, `MonsterCount`, `ItemCount`.

Integration points (Coder):
- `TowerGameController.BuildTowerFloor()` → call `Generate(_floor, seed)` then apply layout (walls/stairs) from Dungeon grid.
- `CompleteTowerCell()` stairs branch → `AdvanceFloor()` then `Generate(_floor, seed)`.

## 2. TowerItemTrapManager (`Assets/AzureDreams/Scripts/TowerItemTrapManager.cs`)
Owns item/trap spawn, pickup, trigger tracking. Uses TowerFloorManager + RetailRng.

API:
- ctor `(TowerFloorManager floor, int seed)` or `(floor, RetailRng rng)` (shared run stream).
- `void UseRunRng(RetailRng rng)` — replace RNG with run stream.
- `List<(Vector2Int cell, DungeonItemEntry item)> SpawnItems(int count)`
- `List<Vector2Int> SpawnTraps(int count)`
- `DungeonItemEntry TryPickup(Vector2Int cell)` — returns item & clears slot/flag.
- `bool TryTriggerTrap(Vector2Int cell)` / `bool IsTrapTriggered(cell)`
- `void ResetFloor()` / `int PickedCount` / `int TriggeredTrapCount` / `int Capacity`.

Integration points (Coder):
- Floor build → `SpawnItems(n)` / `SpawnTraps(n)` then render markers.
- Player steps on item cell → `TryPickup(cell)` → add to run inventory.
- Player steps on trap tile → `TryTriggerTrap(cell)` → apply trap effect.

## 3. TowerRunManager (`Assets/AzureDreams/Scripts/TowerRunManager.cs`)
Run lifecycle state machine + persistence + per-run RNG. Reuses Inventory, SaveSystem, RetailRng, TurnSystem (no duplication).

State enum: `NotInRun, InTower, FloorTransition, Victory, Defeat, Returning`.

API:
- `void BeginRun(seed, startFloor, townInventory, health, gold, townLevel, familiarName, potions)`
- `void AdvanceFloor()` / `void CompleteFloorTransition()` / `void Defeat()` / `void Victory()`
- `int ReturnToTown(bool survived)` — computes reward, transfers inventory back, persists town progress, returns reward.
- `void FinishReturn()`
- `void ApplyDamage(int)` / `void AddLoot(int)` / `void SetHealth(int)`
- `void SaveRun()` / `bool LoadRun()` / `void ClearRun()`
- Props: `CurrentState`, `Floor`, `Seed`, `Health`, `Gold`, `RunLoot`, `TownLevel`, `FamiliarName`, `Potions`, `RunInventory`, `TownInventory`, `Rng`, `IsInTower`.

Integration points (Coder):
- `EnterTower()` → `BeginRun(...)`; set `Potions` from town inventory before `ReturnToTown`.
- Stairs → `AdvanceFloor()` (then floor manager regenerates).
- Death → `Defeat()` then `ReturnToTown(false)`.
- R key → `ReturnToTown(true)`.
- Use run RNG stream for item/trap managers via `UseRunRng(run.Rng)`.

## Ownership / constraints
- No edits to `TowerGameController*.cs` or shared DLL made by Architect; Coder integrates.
- If controller changes are needed, send contract/diff first.
- Core files not modified (reuse only).
- Coder clone `azure-movement-qa-fdedbe62` not overwritten.

## Non-retail fallbacks (documented, not claimed as retail)
- TowerFloorManager: ground heights are flat (0). Retail applies room-patch height deltas (generate.c room feature rolls). Tables not yet ported.
- TowerItemTrapManager: item selection uses a 4-way modulo roll. Retail uses rarity-weighted selection (spawn.c build_cumulative_item_weights / select_weighted_floor_item) with definition flags and floor-specific overrides. Tables not yet ported.
- TowerRunManager: mid-run persistence (SaveRun/LoadRun/ClearRun) is disabled (not supported). Only town progress persists via ReturnToTown (SaveSystem).

## Tests
- EditMode: `TowerFloorManagerTests`, `TowerItemTrapManagerTests`, `TowerRunManagerTests`.
- Decomp reference: `docs/guide/mechanics/dungeon-generation.md`, `items.md`.