using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class ArchitectTowerInvariantTests
{
    [Test]
    public void Run_TerminalStateFreezesHealthAndLoot()
    {
        var run = new TowerRunManager();
        run.BeginRun(7, 1, null, 20, 0, 1, "", 0);
        run.AddLoot(12);
        run.Defeat();
        run.ApplyDamage(5);
        run.SetHealth(100);
        run.AddLoot(10);
        Assert.AreEqual(20, run.Health);
        Assert.AreEqual(12, run.RunLoot);
    }

    [Test]
    public void Run_NegativeDamageCannotHeal()
    {
        var run = new TowerRunManager();
        run.BeginRun(7, 1, null, 20, 0, 1, "", 0);
        run.ApplyDamage(-10);
        Assert.AreEqual(20, run.Health);
    }

    [Test]
    public void Floor_ResetClearsSeedAndStairs()
    {
        var floor = new TowerFloorManager();
        floor.Generate(2, 77);
        floor.Reset();
        Assert.AreEqual(0, floor.Seed);
        Assert.AreEqual(Vector2Int.zero, floor.StairsDownCell);
        Assert.AreEqual(Vector2Int.zero, floor.StairsUpCell);
    }

    [Test]
    public void Floor_FindOpenCellRechecksMutableGrid()
    {
        var floor = new TowerFloorManager();
        floor.Generate(1, 7);
        var stale = floor.OpenCells[0];
        floor.Dungeon.Grid[stale.y, stale.x] = AzureDreams.Core.DungeonGenerator.TileType.Wall;
        var found = floor.FindOpenCell(new Vector2Int(-1, -1));
        Assert.IsTrue(floor.IsWalkable(found.x, found.y));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Placement_UsesOnlyUnoccupiedUnblockedTerrain(bool traps)
    {
        var floor = new TowerFloorManager();
        floor.Generate(1, 7);
        var dungeon = floor.Dungeon;
        System.Array.Clear(dungeon.FloorItems, 0, dungeon.FloorItems.Length);
        foreach (var c in floor.OpenCells)
            dungeon.Flags[c.y, c.x] = (ushort)(AzureDreams.Core.DungeonGenerator.TileFlag.Blocked |
                AzureDreams.Core.DungeonGenerator.TileFlag.HasActor);
        var safe = floor.OpenCells[floor.OpenCells.Count / 2];
        dungeon.Grid[safe.y, safe.x] = AzureDreams.Core.DungeonGenerator.TileType.Corridor;
        dungeon.Flags[safe.y, safe.x] = 0;
        var manager = new TowerItemTrapManager(floor, 123);
        if (traps)
        {
            var placed = manager.SpawnTraps(100);
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(safe, placed[0]);
            Assert.IsTrue(manager.TryTriggerTrap(safe));
            Assert.AreEqual(AzureDreams.Core.DungeonGenerator.TileType.Corridor, dungeon.Grid[safe.y, safe.x]);
        }
        else
        {
            var placed = manager.SpawnItems(10);
            Assert.AreEqual(1, placed.Count);
            Assert.AreEqual(safe, placed[0].cell);
            Assert.AreSame(placed[0].item, manager.TryPickup(safe));
        }
    }

    [Test]
    public void Placement_RegenerationAutomaticallyClearsCounters()
    {
        var floor = new TowerFloorManager();
        floor.Generate(1, 7);
        var manager = new TowerItemTrapManager(floor, 123);
        var items = manager.SpawnItems(8);
        manager.TryPickup(items[0].cell);
        var traps = manager.SpawnTraps(8);
        manager.TryTriggerTrap(traps[0]);
        floor.Generate(1, 7); // Same floor/seed, different dungeon identity.
        Assert.AreEqual(0, manager.PickedCount);
        Assert.AreEqual(0, manager.TriggeredTrapCount);
        Assert.IsFalse(manager.IsTrapTriggered(traps[0]));
        var next = manager.SpawnItems(8);
        foreach (var entry in next) Assert.AreSame(entry.item, manager.TryPickup(entry.cell));
    }

    [Test]
    public void Placement_ResetTrackingPreservesLiveSlotAndTerrainOwnership()
    {
        var floor = new TowerFloorManager();
        floor.Generate(1, 7);
        var manager = new TowerItemTrapManager(floor, 123);
        var items = manager.SpawnItems(8);
        manager.ResetFloor();
        for (int i = items.Count - 1; i >= 0; i--)
            Assert.AreSame(items[i].item, manager.TryPickup(items[i].cell));
    }

    [Test]
    public void Floor_ExitReachabilityReflectsLiveTerrain()
    {
        var floor = new TowerFloorManager();
        floor.Generate(1, 7);
        var property = typeof(TowerFloorManager).GetProperty("HasReachableExit");
        Assert.IsNotNull(property, "Explicit reachability query is required");
        Assert.IsTrue((bool)property.GetValue(floor));
        var entry = floor.StairsDownCell;
        foreach (var step in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
        {
            var c = entry + step;
            if (c.x >= 0 && c.y >= 0 && c.x < 64 && c.y < 64)
                floor.Dungeon.Grid[c.y, c.x] = AzureDreams.Core.DungeonGenerator.TileType.Wall;
        }
        Assert.IsFalse((bool)property.GetValue(floor));
        floor.Reset();
        Assert.IsFalse((bool)property.GetValue(floor));
    }

    [Test]
    public void FloorAndRun_AdvanceCannotOverflow()
    {
        var floor = new TowerFloorManager();
        typeof(TowerFloorManager).GetField("<CurrentFloor>k__BackingField", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).SetValue(floor, int.MaxValue);
        floor.AdvanceFloor();
        Assert.AreEqual(int.MaxValue, floor.CurrentFloor);
        var run = new TowerRunManager();
        run.BeginRun(7, int.MaxValue, null, 20, 0, 1, "", 0);
        run.AdvanceFloor();
        Assert.AreEqual(int.MaxValue, run.Floor);
        Assert.AreEqual(TowerRunManager.State.InTower, run.CurrentState);
    }

    [Test]
    public void Run_ReentryCannotDiscardActiveInventoryOrReseed()
    {
        var run = new TowerRunManager();
        run.BeginRun(7, 1, new Inventory(), 20, 0, 1, "", 0);
        var inventory = run.RunInventory;
        run.AddLoot(12);
        run.BeginRun(99, 3, new Inventory(), 4, 50, 1, "", 0);
        Assert.AreSame(inventory, run.RunInventory);
        Assert.AreEqual(7, run.Seed);
        Assert.AreEqual(12, run.RunLoot);
    }
}
