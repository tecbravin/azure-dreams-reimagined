using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AzureDreamsReimagined;

public class ArchitectTowerLifecyclePlayModeTests
{
    [UnityTest]
    public IEnumerator ReusedManagersRemainConsistentAcrossFramesAndFloors()
    {
        var floor = new TowerFloorManager();
        var manager = new TowerItemTrapManager(floor, 123);
        for (int number = 1; number <= 3; number++)
        {
            floor.Generate(number, 7);
            Assert.AreEqual(0, manager.PickedCount);
            Assert.AreEqual(0, manager.TriggeredTrapCount);
            Assert.IsTrue(floor.HasReachableExit);
            var items = manager.SpawnItems(8);
            Assert.Greater(items.Count, 0);
            var traps = manager.SpawnTraps(8);
            Assert.Greater(traps.Count, 0);
            yield return null;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                Assert.AreSame(items[i].item, manager.TryPickup(items[i].cell));
                Assert.IsNull(manager.TryPickup(items[i].cell));
            }
            foreach (var cell in traps)
            {
                Assert.IsTrue(manager.TryTriggerTrap(cell));
                Assert.IsFalse(manager.TryTriggerTrap(cell));
                Assert.IsTrue(floor.IsWalkable(cell.x, cell.y));
            }
            Assert.AreEqual(items.Count, manager.PickedCount);
            Assert.AreEqual(traps.Count, manager.TriggeredTrapCount);
            yield return null;
        }
        floor.Reset();
        Assert.AreEqual(0, manager.PickedCount);
        Assert.AreEqual(0, manager.TriggeredTrapCount);
    }
}
