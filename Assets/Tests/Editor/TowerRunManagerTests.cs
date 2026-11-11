using System;
using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

namespace AzureDreams.Reimagined.EditorTests
{
    public class TowerRunManagerTests
    {
        private TowerRunManager _m;
        private Inventory _town;

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteAll();
            _town = new Inventory();
            _m = new TowerRunManager();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteAll();
            _m = null;
            _town = null;
        }

        [Test]
        public void InitialState_NotInRun()
        {
            Assert.AreEqual(TowerRunManager.State.NotInRun, _m.CurrentState);
        }

        [Test]
        public void BeginRun_SetsInTower()
        {
            _m.BeginRun(42, 1, _town, 20, 50, 2, "Kewne", 3);
            Assert.AreEqual(TowerRunManager.State.InTower, _m.CurrentState);
            Assert.IsTrue(_m.IsInTower);
        }

        [Test]
        public void BeginRun_TransfersInventory()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            _town.AddItem(item, 2);
            _m.BeginRun(42, 1, _town, 20, 50, 2, "", 0);
            Assert.AreEqual(0, _town.TotalItems);
            Assert.AreEqual(2, _m.RunInventory.TotalItems);
        }

        [Test]
        public void BeginRun_SetsFields()
        {
            _m.BeginRun(99, 3, _town, 15, 100, 4, "Dragon", 5);
            Assert.AreEqual(3, _m.Floor);
            Assert.AreEqual(99, _m.Seed);
            Assert.AreEqual(15, _m.Health);
            Assert.AreEqual(100, _m.Gold);
            Assert.AreEqual(4, _m.TownLevel);
            Assert.AreEqual("Dragon", _m.FamiliarName);
            Assert.AreEqual(5, _m.Potions);
            Assert.AreEqual(0, _m.RunLoot);
        }

        [Test]
        public void BeginRun_ClampsStartFloor()
        {
            _m.BeginRun(1, 0, _town, 20, 0, 1, "", 0);
            Assert.AreEqual(1, _m.Floor);
        }

        [Test]
        public void BeginRun_NullTownInventoryOk()
        {
            Assert.DoesNotThrow(() => _m.BeginRun(1, 1, null, 20, 0, 1, "", 0));
            Assert.IsNotNull(_m.RunInventory);
        }

        [Test]
        public void Rng_NotNullAfterBegin()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            Assert.IsNotNull(_m.Rng);
        }

        [Test]
        public void AdvanceFloor_EntersFloorTransition()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AdvanceFloor();
            Assert.AreEqual(TowerRunManager.State.FloorTransition, _m.CurrentState);
            Assert.AreEqual(2, _m.Floor);
        }

        [Test]
        public void CompleteFloorTransition_BackToInTower()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AdvanceFloor();
            _m.CompleteFloorTransition();
            Assert.AreEqual(TowerRunManager.State.InTower, _m.CurrentState);
        }

        [Test]
        public void AdvanceFloor_WhenNotInTowerNoOp()
        {
            _m.AdvanceFloor();
            Assert.AreEqual(TowerRunManager.State.NotInRun, _m.CurrentState);
            Assert.AreEqual(0, _m.Floor);
        }

        [Test]
        public void Defeat_SetsDefeat()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.Defeat();
            Assert.AreEqual(TowerRunManager.State.Defeat, _m.CurrentState);
        }

        [Test]
        public void Victory_SetsVictory()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.Victory();
            Assert.AreEqual(TowerRunManager.State.Victory, _m.CurrentState);
        }

        [Test]
        public void ApplyDamage_DefeatAtZero()
        {
            _m.BeginRun(1, 1, _town, 5, 0, 1, "", 0);
            _m.ApplyDamage(5);
            Assert.AreEqual(TowerRunManager.State.Defeat, _m.CurrentState);
            Assert.AreEqual(0, _m.Health);
        }

        [Test]
        public void AddLoot_Increments()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AddLoot(10);
            _m.AddLoot(5);
            Assert.AreEqual(15, _m.RunLoot);
        }

        [Test]
        public void AddLoot_NonPositiveIgnored()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AddLoot(0);
            _m.AddLoot(-3);
            Assert.AreEqual(0, _m.RunLoot);
        }

        [Test]
        public void ReturnToTown_Survived_FullLoot()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AddLoot(30);
            int reward = _m.ReturnToTown(true);
            Assert.AreEqual(30, reward);
            Assert.AreEqual(30, _m.Gold);
            Assert.AreEqual(TowerRunManager.State.Returning, _m.CurrentState);
        }

        [Test]
        public void ReturnToTown_Defeat_HalfLoot()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AddLoot(30);
            int reward = _m.ReturnToTown(false);
            Assert.AreEqual(15, reward);
            Assert.AreEqual(15, _m.Gold);
        }

        [Test]
        public void ReturnToTown_TransfersInventoryBack()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            _town.AddItem(item, 1);
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            Assert.AreEqual(0, _town.TotalItems);
            _m.ReturnToTown(true);
            Assert.AreEqual(1, _town.TotalItems);
        }

        [Test]
        public void ReturnToTown_UpdatesTownLevel()
        {
            _m.BeginRun(1, 1, _town, 20, 90, 1, "", 0);
            _m.AddLoot(20);
            _m.ReturnToTown(true);
            // gold 110 -> town level 1+110/50 = 3
            Assert.AreEqual(3, _m.TownLevel);
        }

        [Test]
        public void ReturnToTown_Idempotent()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.AddLoot(30);
            int r1 = _m.ReturnToTown(true);
            int r2 = _m.ReturnToTown(true);
            Assert.AreEqual(30, r1);
            Assert.AreEqual(0, r2);
            Assert.AreEqual(30, _m.Gold);
        }

        [Test]
        public void FinishReturn_SetsNotInRun()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.ReturnToTown(true);
            _m.FinishReturn();
            Assert.AreEqual(TowerRunManager.State.NotInRun, _m.CurrentState);
        }
        [Test]
        public void ReturnToTown_ItemUsedBeforeReturn_PotionsStaleOk()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.Potions = 2;
            _m.AddLoot(10);
            int reward = _m.ReturnToTown(true);
            Assert.AreEqual(10, reward);
            // Potions persists whatever caller set (caller recomputes from inventory).
            Assert.AreEqual(2, _m.Potions);
        }

        [Test]
        public void SaveRun_LoadRun_Disabled()
        {
            _m.BeginRun(7, 2, _town, 12, 40, 3, "X", 1);
            _m.SaveRun();
            var m2 = new TowerRunManager();
            Assert.IsFalse(m2.LoadRun());
        }

        [Test]
        public void LoadRun_NoSaveReturnsFalse()
        {
            var m2 = new TowerRunManager();
            Assert.IsFalse(m2.LoadRun());
        }

        [Test]
        public void ReturnToTown_SecondCallDoesNotTransferInventoryTwice()
        {
            var item = ScriptableObject.CreateInstance<ItemDefinition>();
            _town.AddItem(item, 1);
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            Assert.AreEqual(0, _town.TotalItems);
            int r1 = _m.ReturnToTown(true);
            Assert.AreEqual(1, _town.TotalItems);
            int r2 = _m.ReturnToTown(true);
            Assert.AreEqual(0, r2);
            Assert.AreEqual(1, _town.TotalItems, "Second ReturnToTown must not transfer inventory again");
            Assert.IsNull(_m.RunInventory, "RunInventory must be null after first return");
        }

        [Test]
        public void ClearRun_RemovesState()
        {
            _m.BeginRun(1, 1, _town, 20, 0, 1, "", 0);
            _m.SaveRun();
            _m.ClearRun();
            var m2 = new TowerRunManager();
            Assert.IsFalse(m2.LoadRun());
        }
    }
}