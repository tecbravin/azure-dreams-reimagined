using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

namespace AzureDreams.Reimagined.EditorTests
{
    public class TowerItemTrapManagerTests
    {
        private TowerFloorManager _floor;
        private TowerItemTrapManager _m;

        [SetUp]
        public void SetUp()
        {
            _floor = new TowerFloorManager();
            _floor.Generate(1, 7);
            _m = new TowerItemTrapManager(_floor, 123);
        }

        [TearDown]
        public void TearDown()
        {
            _m = null;
            _floor = null;
        }

        [Test]
        public void Constructor_NullFloorThrows()
        {
            Assert.Throws<ArgumentNullException>(() => new TowerItemTrapManager(null, 123));
        }

        [Test]
        public void SpawnItems_ReturnsPlacements()
        {
            var placements = _m.SpawnItems(5);
            Assert.IsNotNull(placements);
            Assert.GreaterOrEqual(placements.Count, 1, "SpawnItems(5) should place at least one item");
        }

        [Test]
        public void SpawnItems_NegativeCountReturnsEmpty()
        {
            var placements = _m.SpawnItems(-5);
            Assert.AreEqual(0, placements.Count);
        }

        [Test]
        public void SpawnItems_ClampsToCapacity()
        {
            var placements = _m.SpawnItems(64 + 10);
            Assert.LessOrEqual(placements.Count, 64);
        }

        [Test]
        public void SpawnItems_PlacementsHaveDistinctCells()
        {
            var placements = _m.SpawnItems(8);
            Assert.GreaterOrEqual(placements.Count, 2, "Need >=2 placements to verify distinct cells");
            var cells = placements.Select(p => p.cell).ToList();
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), "Spawned items must not share a cell");
        }

        [Test]
        public void SpawnItems_EachCellMapsToMatchingItem()
        {
            var placements = _m.SpawnItems(8);
            Assert.GreaterOrEqual(placements.Count, 2, "Need >=2 placements to verify spatial association");

            foreach (var (cell, item) in placements)
            {
                var picked = _m.TryPickup(cell);
                Assert.IsNotNull(picked, "TryPickup(cell) must return the item placed on that cell");
                Assert.AreEqual(item, picked, "Picked item must match the placement for that cell");
            }

            foreach (var (cell, _) in placements)
            {
                Assert.IsNull(_m.TryPickup(cell), "Second TryPickup on same cell must return null");
            }
        }

        [Test]
        public void SpawnTraps_PlacementsHaveDistinctCells()
        {
            var cells = _m.SpawnTraps(8);
            Assert.GreaterOrEqual(cells.Count, 2, "Need >=2 traps to verify distinct cells");
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), "Spawned traps must not share a cell");
        }

        [Test]
        public void SpawnTraps_ReturnsCells()
        {
            var cells = _m.SpawnTraps(5);
            Assert.IsNotNull(cells);
            Assert.GreaterOrEqual(cells.Count, 1, "SpawnTraps(5) should place at least one trap");
        }

        [Test]
        public void SpawnTraps_NegativeCountReturnsEmpty()
        {
            var cells = _m.SpawnTraps(-1);
            Assert.AreEqual(0, cells.Count);
        }

        [Test]
        public void TryTriggerTrap_NewTrapReturnsTrue()
        {
            var cells = _m.SpawnTraps(5);
            Assert.GreaterOrEqual(cells.Count, 1, "SpawnTraps should place at least one trap");
            Assert.IsTrue(_m.TryTriggerTrap(cells[0]));
        }

        [Test]
        public void TryTriggerTrap_AlreadyTriggeredReturnsFalse()
        {
            var cells = _m.SpawnTraps(5);
            Assert.GreaterOrEqual(cells.Count, 1, "SpawnTraps should place at least one trap");
            _m.TryTriggerTrap(cells[0]);
            Assert.IsFalse(_m.TryTriggerTrap(cells[0]));
        }

        [Test]
        public void TrapStaysRevealedAndCanSpringAgainAfterRearming()
        {
            var cell = _m.SpawnTraps(1)[0];
            var trap = _m.TrapAt(cell);
            Assert.IsFalse(trap.IsRevealed);
            Assert.IsTrue(trap.IsActive);
            Assert.IsTrue(trap.FirstTime);
            Assert.IsFalse(_m.TryRearmTrap(cell));

            Assert.IsTrue(_m.TryTriggerTrap(cell));
            Assert.AreSame(trap, _m.TrapAt(cell));
            Assert.IsTrue(trap.IsRevealed);
            Assert.IsFalse(trap.IsActive);
            Assert.IsFalse(trap.FirstTime);

            Assert.IsTrue(_m.TryRearmTrap(cell));
            Assert.IsTrue(trap.IsActive);
            Assert.IsTrue(trap.IsRevealed);
            Assert.IsFalse(trap.FirstTime);
            Assert.IsTrue(_m.TryTriggerTrap(cell));
            Assert.AreEqual(1, _m.TriggeredTrapCount);
        }

        [Test]
        public void TryTriggerTrap_UnknownCellReturnsFalse()
        {
            Assert.IsFalse(_m.TryTriggerTrap(new Vector2Int(0, 0)));
        }

        [Test]
        public void IsTrapTriggered_TracksState()
        {
            var cells = _m.SpawnTraps(5);
            Assert.GreaterOrEqual(cells.Count, 1, "SpawnTraps should place at least one trap");
            Assert.IsFalse(_m.IsTrapTriggered(cells[0]));
            _m.TryTriggerTrap(cells[0]);
            Assert.IsTrue(_m.IsTrapTriggered(cells[0]));
        }

        [Test]
        public void TriggeredTrapCount_Increments()
        {
            var cells = _m.SpawnTraps(5);
            foreach (var c in cells) _m.TryTriggerTrap(c);
            Assert.AreEqual(cells.Count, _m.TriggeredTrapCount);
        }

        [Test]
        public void ResetFloor_ClearsTracking()
        {
            var cells = _m.SpawnTraps(5);
            if (cells.Count > 0) _m.TryTriggerTrap(cells[0]);
            _m.ResetFloor();
            Assert.AreEqual(0, _m.TriggeredTrapCount);
            Assert.AreEqual(0, _m.PickedCount);
        }

        [Test]
        public void TryPickup_ReverseOrder_ReturnsCorrectItems()
        {
            var placements = _m.SpawnItems(8);
            Assert.GreaterOrEqual(placements.Count, 2, "Need >=2 items to verify reverse-order pickup");
            var second = _m.TryPickup(placements[1].cell);
            var first = _m.TryPickup(placements[0].cell);
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreEqual(placements[0].item, first);
            Assert.AreEqual(placements[1].item, second);
            Assert.IsNull(_m.TryPickup(placements[0].cell));
            Assert.IsNull(_m.TryPickup(placements[1].cell));
        }

        [Test]
        public void TryPickup_UnknownCell_ReturnsNull()
        {
            Assert.IsNull(_m.TryPickup(new Vector2Int(0, 0)));
        }

        [Test]
        public void TryPickup_TwiceOnSameCell_SecondReturnsNull()
        {
            var placements = _m.SpawnItems(8);
            Assert.GreaterOrEqual(placements.Count, 1, "Need >=1 item");
            var cell = placements[0].cell;
            var first = _m.TryPickup(cell);
            Assert.IsNotNull(first);
            Assert.IsNull(_m.TryPickup(cell), "Second pickup on same cell must return null (no duplication)");
        }

        [Test]
        public void Capacity_IsMaxFloorItems()
        {
            Assert.AreEqual(64, _m.Capacity);
        }
    }
}
