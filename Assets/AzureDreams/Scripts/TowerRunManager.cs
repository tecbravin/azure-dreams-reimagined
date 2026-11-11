using System;
using UnityEngine;
using CoreRng = AzureDreams.Core.RetailRng;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Tower run lifecycle / state machine — owns entry, turn/resolution gating,
    /// floor transitions, victory/defeat/return, persistence and the per-run RNG.
    /// Reuses the existing core/Unity systems (TurnSystem, Inventory, SaveSystem,
    /// RetailRng) without duplicating them.
    ///
    /// INTEGRATION POINT (Coder): TowerGameController.EnterTower() should call
    /// BeginRun(...); ReturnToTown() should call ReturnToTown(survived) and use
    /// the returned reward; stair handling should call AdvanceFloor().
    /// Do not mutate TowerGameController*.cs without a contract/diff first.
    /// </summary>
    public sealed class TowerRunManager
    {
        public enum State
        {
            NotInRun = 0,
            InTower = 1,
            FloorTransition = 2,
            Victory = 3,
            Defeat = 4,
            Returning = 5,
        }

        private const string RunFloorKey = "AzureDreams.Run.Floor";
        private const string RunSeedKey = "AzureDreams.Run.Seed";
        private const string RunHealthKey = "AzureDreams.Run.Health";
        private const string RunGoldKey = "AzureDreams.Run.Gold";
        private const string RunLootKey = "AzureDreams.Run.Loot";

        private CoreRng _rng;
        private bool _returned;

        public State CurrentState { get; private set; } = State.NotInRun;

        public int Floor { get; private set; }
        public int Seed { get; private set; }
        public int Health { get; private set; }
        public int Gold { get; private set; }
        public int RunLoot { get; private set; }
        public int TownLevel { get; private set; } = 1;
        public string FamiliarName { get; set; } = string.Empty;
        public int Potions { get; set; }

        public Inventory RunInventory { get; private set; }
        public Inventory TownInventory { get; private set; }

        public CoreRng Rng => _rng;

        public bool IsInTower => CurrentState == State.InTower || CurrentState == State.FloorTransition;

        /// <summary>
        /// Begin a new tower run. Transfers the town inventory into a fresh run
        /// inventory and seeds the per-run RNG.
        /// </summary>
        public void BeginRun(int seed, int startFloor, Inventory townInventory,
            int health, int gold, int townLevel, string familiarName, int potions)
        {
            // An active/terminal run must finish returning before a new run can own its inventory.
            if (CurrentState != State.NotInRun) return;
            if (startFloor < 1) startFloor = 1;
            Seed = seed;
            Floor = startFloor;
            Health = health;
            Gold = gold;
            RunLoot = 0;
            TownLevel = townLevel < 1 ? 1 : townLevel;
            FamiliarName = familiarName ?? string.Empty;
            Potions = potions < 0 ? 0 : potions;
            TownInventory = townInventory;
            RunInventory = new Inventory();
            if (townInventory != null) townInventory.TransferTo(RunInventory);
            _rng = new CoreRng((uint)seed);
            _returned = false;
            CurrentState = State.InTower;
        }

        /// <summary>Advance to the next floor (enters FloorTransition state).</summary>
        public void AdvanceFloor()
        {
            if (CurrentState != State.InTower || Floor == int.MaxValue) return;
            Floor++;
            CurrentState = State.FloorTransition;
        }

        /// <summary>Complete a floor transition (back to InTower).</summary>
        public void CompleteFloorTransition()
        {
            if (CurrentState == State.FloorTransition) CurrentState = State.InTower;
        }

        /// <summary>Mark the run as defeated.</summary>
        public void Defeat() { if (IsInTower) CurrentState = State.Defeat; }

        /// <summary>Mark the run as victorious.</summary>
        public void Victory() { if (IsInTower) CurrentState = State.Victory; }

        /// <summary>
        /// Return to town. Computes reward (full loot if survived, half if not),
        /// persists town progress, and transfers run inventory back to town.
        /// Returns the computed reward amount.
        /// </summary>
        public int ReturnToTown(bool survived)
        {
            if (_returned) return 0;
            if (CurrentState != State.InTower && CurrentState != State.Defeat && CurrentState != State.Victory) return 0;
            _returned = true;
            int reward;
            if (survived)
            {
                reward = RunLoot;
                Gold += RunLoot;
            }
            else
            {
                reward = RunLoot / 2;
                Gold += reward;
            }
            TownLevel = 1 + Gold / 50;
            if (RunInventory != null && TownInventory != null)
                RunInventory.TransferTo(TownInventory);
            RunInventory = null;

            int potions = Potions;

            SaveSystem.Save(Gold, TownLevel, FamiliarName, potions);
            CurrentState = State.Returning;
            return reward;
        }

        /// <summary>Finish returning (back to NotInRun).</summary>
        public void FinishReturn() { if (CurrentState == State.Returning) CurrentState = State.NotInRun; }

        /// <summary>Apply damage to the player's health.</summary>
        public void ApplyDamage(int amount)
        {
            if (CurrentState != State.InTower || amount <= 0) return;
            Health = amount >= Health ? 0 : Health - amount;
            if (Health <= 0) Defeat();
        }

        /// <summary>Add salvage to the run loot.</summary>
        public void AddLoot(int amount) { if (CurrentState == State.InTower && amount > 0) RunLoot += amount; }

        /// <summary>Set health directly (e.g. after heal).</summary>
        public void SetHealth(int value) { if (CurrentState == State.InTower) Health = value; }

        /// <summary>
        /// Mid-run persistence is NOT supported: only town progress (via ReturnToTown)
        /// is persisted. These APIs are disabled to avoid pretending a full run load.
        /// </summary>
        public void SaveRun()
        {
            // Not supported: mid-run state is not persisted.
        }

        /// <summary>
        /// Mid-run persistence is NOT supported. Always returns false.
        /// </summary>
        public bool LoadRun()
        {
            // Not supported: mid-run state is not restored.
            return false;
        }

        /// <summary>
        /// Mid-run persistence is NOT supported. No-op.
        /// </summary>
        public void ClearRun()
        {
            // Not supported.
        }
    }
}