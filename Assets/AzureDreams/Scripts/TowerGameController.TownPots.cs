using System.Collections.Generic;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private TownPotView _heldTownPot;
        private readonly List<TownPotView> _townPots = new();

        private float TownPotTop(Vector3 position, float radius, Transform ignored = null)
        {
            float top = float.NegativeInfinity;
            foreach (var pot in _townPots)
            {
                if (!pot || !pot.IsSolid || pot.transform == ignored) continue;
                var bounds = pot.ObstacleBounds;
                float dx = Mathf.Max(0f, Mathf.Max(bounds.min.x - position.x, position.x - bounds.max.x));
                float dz = Mathf.Max(0f, Mathf.Max(bounds.min.z - position.z, position.z - bounds.max.z));
                if (dx * dx + dz * dz <= radius * radius)
                    top = Mathf.Max(top, bounds.max.y);
            }
            return top;
        }

        private bool TryTownPotLift()
        {
            if (_area != Area.Town || !_player || _townDoorOpening || _activeTownNpc) return false;
            if (_heldTownPot)
            {
                Vector3 target = _player.transform.position + new Vector3(_townFacing.x, 0f, _townFacing.y) * 0.9f;
                if (!TownPositionOpen(target, radius: _heldTownPot.ObstacleBounds.extents.x)) { _message = T("There is no room to put the pot down.", "Não há espaço para colocar o vaso no chão."); return true; }
                _heldTownPot.transform.position = UsingRetailTown ? RetailTownActorPosition(target, 0f) : new Vector3(target.x, 0f, target.z);
                _heldTownPot.SetSolid(true);
                _heldTownPot = null;
                return true;
            }

            Vector3 facing = new Vector3(_townFacing.x, 0f, _townFacing.y);
            float closest = 1.3f;
            TownPotView selected = null;
            foreach (var pot in _townPots)
            {
                if (!pot || !pot.IsSolid) continue;
                Vector3 delta = pot.transform.position - _player.transform.position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance >= closest || (distance > 0.35f && Vector3.Dot(delta.normalized, facing) < 0.5f)) continue;
                selected = pot;
                closest = distance;
            }
            if (!selected) return false;
            _heldTownPot = selected;
            selected.SetSolid(false);
            PresentTownPot();
            return true;
        }

        private void ThrowTownPot()
        {
            if (!_heldTownPot) return;
            var pot = _heldTownPot;
            _heldTownPot = null;
            StartCoroutine(AnimateThrownTownPot(pot));
        }

        private System.Collections.IEnumerator AnimateThrownTownPot(TownPotView pot)
        {
            Vector3 from = pot.transform.position;
            Vector3 direction = new Vector3(_townFacing.x, 0f, _townFacing.y).normalized;
            Vector3 landing = _player.transform.position;
            Vector3 destination = landing;
            bool hitWall = false;
            // Short probes prevent a thrown pot from skipping a narrow wall.
            for (int step = 1; step <= 32; step++)
            {
                Vector3 probe = _player.transform.position + direction * (step * 0.25f);
                float terrain = TownTerrainHeight(probe);
                var cell = new Vector2Int(Mathf.RoundToInt(probe.x), Mathf.RoundToInt(probe.z));
                if (_walls.Contains(cell) || terrain > 0.65f || TownWallTop(probe, pot.ObstacleBounds.extents.x) > terrain)
                {
                    hitWall = true;
                    destination = new Vector3(probe.x, from.y, probe.z);
                    break;
                }
                if (!TownPositionOpen(probe, radius: pot.ObstacleBounds.extents.x)) break;
                landing = probe;
                destination = new Vector3(probe.x, terrain, probe.z);
            }
            float elapsed = 0f;
            float duration = ThrowFlightDuration(from, destination);
            while (pot && elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / duration);
                pot.transform.position = Vector3.Lerp(from, destination, progress)
                    + Vector3.up * Mathf.Sin(progress * Mathf.PI) * 0.7f;
                yield return null;
            }
            if (!pot) yield break;
            if (hitWall)
            {
                SpawnFx(_fxDust, destination);
                _townPots.Remove(pot);
                RemoveGenerated(pot.gameObject);
                _message = T("The pot hits the wall and shatters.", "O vaso bate na parede e se quebra.");
                yield break;
            }
            pot.transform.position = UsingRetailTown ? RetailTownActorPosition(landing, 0f)
                : new Vector3(landing.x, 0f, landing.z);
            pot.SetSolid(true);
        }

        private void PresentTownPot()
        {
            if (!_heldTownPot || !_player) return;
            PlaceHeldView(_heldTownPot.gameObject);
        }
    }
}
