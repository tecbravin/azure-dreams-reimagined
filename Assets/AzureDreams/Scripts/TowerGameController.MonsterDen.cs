using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const float MonsterDenDropDuration = 0.55f;
        private const float MonsterDenDropInterval = 0.045f;
        private readonly List<(GameObject View, Vector3 Ground)> _monsterDenDrops = new();
        private Vector2Int? _monsterDenTrigger;
        private int _monsterDenRoom = -1;
        private int _monsterDenAttempts;
        private float _monsterDenElapsed;
        private bool MonsterDenPlaying => _monsterDenDrops.Count != 0;

        private static int MonsterDenAttempts(int roomArea, int bonus) => Mathf.Min((roomArea >> 2) + bonus, 28) + 1;

        private void PlanMonsterDen()
        {
            if (InTowerTutorial || !TowerFloorRules.AllowsRandomMonsters(_floor)) return;

            // Give the rare room feature its own stream so it doesn't shift ordinary loot or encounters.
            var random = new System.Random(_towerFloorLayout.Seed ^ 0xD3A13);
            var dungeon = _towerFloorLayout.Dungeon;
            for (int i = 0; i < dungeon.RoomCount; i++)
            {
                if (random.Next(256) != 1) continue;
                var candidates = MonsterDenCells(i);
                if (candidates.Count == 0) continue;
                var room = dungeon.Rooms[i];
                _monsterDenRoom = i;
                _monsterDenTrigger = candidates[random.Next(candidates.Count)];
                _monsterDenAttempts = MonsterDenAttempts(room.Width * room.Height, random.Next(8));
                return;
            }
        }

        private List<Vector2Int> MonsterDenCells(int roomIndex)
        {
            var room = _towerFloorLayout.Dungeon.Rooms[roomIndex];
            var cells = new List<Vector2Int>();
            for (int y = room.Y; y < room.Y + room.Height; y++)
            for (int x = room.X; x < room.X + room.Width; x++)
            {
                var cell = new Vector2Int(x, y);
                if (!InsideMap(cell) || _walls.Contains(cell) || _npcCells.Contains(cell) ||
                    cell == _stairCell || cell == _towerFloorLayout.StairsDownCell ||
                    BellCellOccupied(cell) || _itemMarkers.ContainsKey(cell) || TrapAt(cell) != null ||
                    IsSacredTile(cell)) continue;
                cells.Add(cell);
            }
            return cells;
        }

        private void TryStartMonsterDen()
        {
            if (_monsterDenTrigger != _playerCell || !EnteredTowerCell(_playerFrom, _playerCell)) return;
            _monsterDenTrigger = null;

            var candidates = MonsterDenCells(_monsterDenRoom);
            int count = Mathf.Min(_monsterDenAttempts, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                int index = RollSpawnRange(candidates.Count);
                var cell = candidates[index];
                candidates.RemoveAt(index);
                var species = TowerEncounterTable.Roll(_floor, _towerRun.Rng);
                var view = SpawnManagedCreature(cell, species, allowSpawnSleep: false);
                var ground = view.transform.position;
                view.transform.position = ground + Vector3.up * 3.5f;
                _monsterDenDrops.Add((view, ground));
            }
            if (!MonsterDenPlaying) return;
            _monsterDenElapsed = 0f;
            _towerShakeStartedAt = Time.time;
            int combatLines = _combatMessageLines;
            _message = JoinTowerMessages(T("It's a Monster Den!", "É um covil de monstros!"),
                combatLines > 0 ? _message : null);
            if (combatLines > 0) _combatMessageLines = combatLines + 1;
        }

        private void TickMonsterDen(float deltaTime)
        {
            float previous = _monsterDenElapsed;
            _monsterDenElapsed += deltaTime;
            for (int i = 0; i < _monsterDenDrops.Count; i++)
            {
                var (view, ground) = _monsterDenDrops[i];
                if (!view) continue;
                float delay = i * MonsterDenDropInterval;
                float progress = Mathf.Clamp01((_monsterDenElapsed - delay) / MonsterDenDropDuration);
                view.transform.position = ground + Vector3.up * (3.5f * (1f - progress * progress));
                float landing = delay + MonsterDenDropDuration;
                if (previous < landing && _monsterDenElapsed >= landing) SpawnFx(_fxDust, ground);
            }

            if (_monsterDenElapsed >= MonsterDenDropDuration + (_monsterDenDrops.Count - 1) * MonsterDenDropInterval)
                _monsterDenDrops.Clear();
        }

        private void ResetMonsterDen()
        {
            _monsterDenTrigger = null;
            _monsterDenRoom = -1;
            _monsterDenAttempts = 0;
            _monsterDenElapsed = 0f;
            _monsterDenDrops.Clear();
        }
    }
}
