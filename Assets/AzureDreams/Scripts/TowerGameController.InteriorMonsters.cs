using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private void SetupInteriorDisplayMonsters()
        {
            if (!_interior) return;
            switch (_interior.interiorId)
            {
                case SilverHouse:
                    SpawnInteriorDisplayMonster("silver_nyuel_1", MonsterSpecies.NYUEL,
                        new Vector3(12f, 0f, 11.75f), new Vector2(0.12f, 0.15f), 0.2f);
                    SpawnInteriorDisplayMonster("silver_nyuel_2", MonsterSpecies.NYUEL,
                        new Vector3(11.75f, 0f, 14.25f), new Vector2(0.12f, 0.15f), 1.1f);
                    break;
                case MonsterShopScene:
                    // Captured cage floors: X 19..21 / Z 10..12 and X 10..12 / Z 15..17.
                    // Their collision height is the roof, not the floor on which the monster stands.
                    SpawnInteriorDisplayMonster("shop_pulunpa_1", MonsterSpecies.PULUNPA,
                        new Vector3(20f, 0f, 11f), new Vector2(0.25f, 0.3f), 0.3f);
                    SpawnInteriorDisplayMonster("shop_pulunpa_2", MonsterSpecies.PULUNPA,
                        new Vector3(11f, 0f, 16f), new Vector2(0.25f, 0.3f), 1.2f);
                    break;
            }
        }

        private void SpawnSilverTownNyuels()
        {
            if (_interior) return;
            Vector3 origin = CellPosition(new Vector2Int(21, 45));
            for (int i = 0; i < 2; i++)
            {
                // Keep the former outdoor gathering spot clear of scenery.
                for (int candidate = 0; candidate < 8; candidate++)
                {
                    float angle = (candidate * 45f + (i == 0 ? 180f : 0f)) * Mathf.Deg2Rad;
                    Vector3 position = origin + new Vector3(Mathf.Cos(angle) * 2f, 0f, Mathf.Sin(angle) * 2f);
                    position = RetailTownActorPosition(position, 0f);
                    if (!TownPositionOpen(position, feetHeight: position.y, radius: 0.9f)) continue;
                    bool occupied = false;
                    foreach (var generated in _generated)
                        if (generated && generated.GetComponent<TownDisplayMonster>() &&
                            (generated.transform.position - position).sqrMagnitude < 2.25f)
                        { occupied = true; break; }
                    if (occupied) continue;
                    SpawnInteriorDisplayMonster("town_silver_nyuel_" + i, MonsterSpecies.NYUEL,
                        position, new Vector2(0.12f, 0.15f), i == 0 ? 0.2f : 1.1f);
                    break;
                }
            }
        }

        private void SpawnInteriorDisplayMonster(string placementId, MonsterSpecies species,
            Vector3 floorPosition, Vector2 halfExtents, float delay)
        {
            foreach (var generated in _generated)
                if (generated && generated.TryGetComponent<TownDisplayMonster>(out var existing) &&
                    existing.PlacementId == placementId) return;
            var prefab = HatchedFamiliarPrefab((int)species);
            if (!prefab) return;
            var cell = new Vector2Int(Mathf.FloorToInt(floorPosition.x), Mathf.FloorToInt(floorPosition.z));
            var monster = SpawnActor(prefab, cell, placementId, species: (int)species, groundedIdle: true);
            if (species == MonsterSpecies.NYUEL) ConfigureRacetrackNyuel(monster);
            foreach (var collider in monster.GetComponentsInChildren<Collider>()) collider.enabled = false;
            // SpawnActor and the sprite view share this foot offset. Do not sample the cage roof.
            monster.AddComponent<TownDisplayMonster>().Configure(placementId,
                floorPosition + Vector3.up * TownActorFootOffset, halfExtents, delay);
        }
    }
}
