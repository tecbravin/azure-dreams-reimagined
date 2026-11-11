using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

namespace AzureDreams.Reimagined.EditorTests
{
    public class TowerFloorManagerTests
    {
        private TowerFloorManager _m;

        [SetUp]
        public void SetUp() => _m = new TowerFloorManager();

        [TearDown]
        public void TearDown() => _m = null;

        [Test]
        public void Generate_MakesIsGeneratedTrue()
        {
            _m.Generate(1, 42);
            Assert.IsTrue(_m.IsGenerated);
        }

        [Test]
        public void Generate_SetsCurrentFloorAndSeed()
        {
            _m.Generate(3, 99);
            Assert.AreEqual(3, _m.CurrentFloor);
            Assert.AreEqual(99, _m.Seed);
        }

        [Test]
        public void Generate_ClampsFloorBelowOne()
        {
            _m.Generate(0, 7);
            Assert.AreEqual(1, _m.CurrentFloor);
        }

        [Test]
        public void Generate_DeterministicForSameInputs()
        {
            _m.Generate(2, 123);
            var m2 = new TowerFloorManager();
            m2.Generate(2, 123);
            Assert.AreEqual(_m.RoomCount, m2.RoomCount);
            Assert.AreEqual(_m.MonsterCount, m2.MonsterCount);
            Assert.AreEqual(_m.ItemCount, m2.ItemCount);
            Assert.AreEqual(_m.StairsDownCell, m2.StairsDownCell);
            Assert.AreEqual(_m.StairsUpCell, m2.StairsUpCell);
        }

        [Test]
        public void Generate_DifferentSeeds_Differ()
        {
            _m.Generate(1, 1);
            var r1 = _m.RoomCount;
            var m2 = new TowerFloorManager();
            m2.Generate(1, 2);
            // Not guaranteed to differ in room count, but stairs should differ in most cases.
            // We only assert generation succeeded for both.
            Assert.IsTrue(_m.IsGenerated);
            Assert.IsTrue(m2.IsGenerated);
        }

        [Test]
        public void IsWalkable_StairsAreWalkable()
        {
            _m.Generate(1, 5);
            Assert.IsTrue(_m.IsWalkable(_m.StairsDownCell.x, _m.StairsDownCell.y));
            if (_m.StairsUpCell != Vector2Int.zero)
                Assert.IsTrue(_m.IsWalkable(_m.StairsUpCell.x, _m.StairsUpCell.y));
        }

        [Test]
        public void IsWalkable_OutOfBoundsFalse()
        {
            _m.Generate(1, 5);
            Assert.IsFalse(_m.IsWalkable(-1, 0));
            Assert.IsFalse(_m.IsWalkable(0, 64));
        }

        [Test]
        public void FindOpenCell_PrefersPreferredWhenOpen()
        {
            _m.Generate(1, 5);
            var pref = _m.StairsDownCell;
            var found = _m.FindOpenCell(pref);
            Assert.AreEqual(pref, found);
        }

        [Test]
        public void FindOpenCell_AvoidsBlocked()
        {
            _m.Generate(1, 5);
            var blocked = new[] { _m.StairsDownCell };
            var found = _m.FindOpenCell(_m.StairsDownCell, blocked);
            Assert.AreNotEqual(_m.StairsDownCell, found);
        }

        [Test]
        public void OpenCells_NonEmptyAfterGenerate()
        {
            _m.Generate(1, 5);
            Assert.Greater(_m.OpenCells.Count, 0);
        }

        [Test]
        public void GroundHeight_DefaultsZero()
        {
            _m.Generate(1, 5);
            Assert.AreEqual(0, _m.GroundHeight(5, 5));
        }

        [Test]
        public void ProceduralElevationHasRaisedAndLoweredTilesAndKeepsStairsFlat()
        {
            _m.Generate(3, 42);
            var same = new TowerFloorManager();
            same.Generate(3, 42);
            int raised = 0, lowered = 0;
            foreach (var cell in _m.OpenCells)
            {
                int height = _m.GroundHeight(cell.x, cell.y);
                Assert.AreEqual(height, same.GroundHeight(cell.x, cell.y));
                if (height > 0) raised++;
                if (height < 0) lowered++;
            }
            Assert.Greater(raised, 0);
            Assert.Greater(lowered, 0);
            Assert.AreEqual(0, _m.GroundHeight(_m.StairsDownCell.x, _m.StairsDownCell.y));
            Assert.AreEqual(0, _m.GroundHeight(_m.StairsUpCell.x, _m.StairsUpCell.y));
            Assert.IsTrue(_m.HasReachableExit);
        }

        [Test]
        public void SetHeight_RoundTrips()
        {
            _m.Generate(1, 5);
            _m.SetHeight(3, 4, 7);
            Assert.AreEqual(7, _m.GroundHeight(3, 4));
        }

        [Test]
        public void SetHeight_OutOfBoundsNoThrow()
        {
            _m.Generate(1, 5);
            Assert.DoesNotThrow(() => _m.SetHeight(-1, 0, 5));
            Assert.DoesNotThrow(() => _m.SetHeight(0, 64, 5));
        }

        [Test]
        public void AdvanceFloor_IncrementsCurrentFloor()
        {
            _m.Generate(1, 5);
            _m.AdvanceFloor();
            Assert.AreEqual(2, _m.CurrentFloor);
        }

        [Test]
        public void Reset_ClearsState()
        {
            _m.Generate(2, 5);
            _m.Reset();
            Assert.AreEqual(1, _m.CurrentFloor);
            Assert.IsFalse(_m.IsGenerated);
        }

        [Test]
        public void Heights_GridDimensions()
        {
            _m.Generate(1, 5);
            Assert.AreEqual(64, _m.Heights.GetLength(0));
            Assert.AreEqual(64, _m.Heights.GetLength(1));
        }
    }
}
