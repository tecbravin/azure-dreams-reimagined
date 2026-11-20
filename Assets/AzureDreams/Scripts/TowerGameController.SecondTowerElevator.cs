using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// The second elevator on floor 2.  Layout 2 has four identical ring
    /// structures; the regular lift stands in the centre of the north-east ring
    /// (55, 9).  This one takes the matching centre of the north-west ring
    /// (8, 9).  It stays sealed (grey core inside the Seal trap glyph) until
    /// Beldo has been defeated; afterwards it carries Koh to the second tower,
    /// which the port runs as floors 41-140 (shown as 1F-100F of the second
    /// tower; rules in TowerGameController.SecondTower.cs).
    /// </summary>
    public sealed partial class TowerGameController
    {
        internal static readonly Vector2Int SecondTowerElevatorCell = new(8, 9);
        internal const int SecondTowerFirstFloor = TowerFloorRules.BeldoFloor + 1;

        private Transform _secondTowerElevator;
        private SpriteRenderer _secondTowerOrb, _secondTowerGlow, _secondTowerSeal;
        private bool _ascendToSecondTower;

        private bool SecondTowerElevatorUnsealed => TownSaveVariables.GetBool(BeldoClearedKey);
        private bool HasSecondTowerElevator => _area == Area.Tower && _floor == 2 && _secondTowerElevator;
        private bool OnSecondTowerElevator => HasSecondTowerElevator && _playerCell == SecondTowerElevatorCell;

        private void CreateSecondTowerElevator()
        {
            _secondTowerElevator = null;
            if (_floor != 2 || _towerFloorLayout == null ||
                !_towerFloorLayout.IsWalkable(SecondTowerElevatorCell.x, SecondTowerElevatorCell.y)) return;
            if (!HasRetailElevatorArt) _elevatorSheet = AssetMode.Load<Texture2D>("Tower/ElevatorSheet");

            var cell = SecondTowerElevatorCell;
            var root = new GameObject("Second tower elevator");
            root.transform.SetParent(transform, false);
            root.transform.position = new Vector3(cell.x, TowerGroundY(cell), cell.y);
            _generated.Add(root);
            _secondTowerElevator = root.transform;
            if (!HasRetailElevatorArt || _elevatorFrames == null)
            {
                CreateMarker(cell, "Second tower elevator (sealed)", _accentMaterial, PrimitiveType.Cylinder, 0.7f);
                return;
            }
            AddElevatorBlockSides(root.transform);
            _secondTowerOrb = CreateElevatorLayer(root.transform, "Second elevator orb", 0.018f, -2);
            _secondTowerGlow = CreateElevatorLayer(root.transform, "Second elevator glow", 0.021f, -1);

            // The Seal trap glyph wraps the dead core while the elevator is sealed.
            var seal = new GameObject("Second elevator seal");
            seal.transform.SetParent(root.transform, false);
            seal.transform.localPosition = Vector3.up * 0.024f;
            seal.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _secondTowerSeal = seal.AddComponent<SpriteRenderer>();
            _secondTowerSeal.sprite = TrapSprite(TowerTrapEffect.Seal);
            _secondTowerSeal.sortingOrder = 0;
            if (_secondTowerSeal.sprite)
                seal.transform.localScale = Vector3.one * (2f * ElevatorBlockHalfWidth * 1.08f / _secondTowerSeal.sprite.bounds.size.x);
            AnimateSecondTowerElevator();
        }

        private void AnimateSecondTowerElevator()
        {
            if (!_secondTowerElevator || !_secondTowerOrb || _elevatorFrames == null || _elevatorFrames.Length == 0) return;
            if (!SecondTowerElevatorUnsealed)
            {
                // Grey, lifeless core; the seal glyph slowly pulses.
                _secondTowerOrb.sprite = _elevatorFrames[0];
                _secondTowerOrb.color = new Color(0.42f, 0.42f, 0.45f, 1f);
                _secondTowerOrb.transform.localScale = Vector3.one * (2f * ElevatorBlockHalfWidth * 0.62f);
                _secondTowerGlow.enabled = false;
                if (_secondTowerSeal)
                {
                    _secondTowerSeal.enabled = true;
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.time * 2.2f);
                    _secondTowerSeal.color = new Color(1f, 1f, 1f, pulse);
                }
                return;
            }
            if (_secondTowerSeal) _secondTowerSeal.enabled = false;
            _secondTowerGlow.enabled = true;
            _secondTowerOrb.color = Color.white;
            _secondTowerOrb.transform.localScale = Vector3.one * (2f * ElevatorBlockHalfWidth);
            float speed = _trapGoUp ? _trapAnimationSpeed : TowerAnimationSpeed;
            int last = _elevatorFrames.Length - 1;
            float cycle = last > 0 ? Mathf.PingPong(Time.time * speed * 3.5f + 1.3f, last) : 0f;
            int first = Mathf.Clamp(Mathf.FloorToInt(cycle), 0, last);
            _secondTowerOrb.sprite = _elevatorFrames[first];
            _secondTowerGlow.sprite = _elevatorFrames[Mathf.Min(first + 1, last)];
            _secondTowerGlow.color = new Color(1f, 1f, 1f, Mathf.SmoothStep(0f, 1f, cycle - first));
        }

        /// <summary>Called when Koh steps onto a tile (and on the confirm button).</summary>
        private bool TrySecondTowerElevator(bool fromInteract)
        {
            if (!OnSecondTowerElevator || _carriedView) return false;
            if (!SecondTowerElevatorUnsealed)
            {
                _message = T("Something seems to be keeping the elevator from working.",
                    "Algo parece estar impedindo que o elevador funcione.");
                return true;
            }
            if (!fromInteract && _elevatorDeclined) return false;
            _ascendToSecondTower = true;
            _checkpointMenu = CheckpointMenu.Ascend;
            return true;
        }

        /// <summary>Elevator ride target: the second tower from the sealed lift, otherwise the next floor.</summary>
        private void AdvanceTowerFloorForElevator()
        {
            if (_ascendToSecondTower && _floor == 2)
            {
                _ascendToSecondTower = false;
                _towerRun.JumpToFloor(SecondTowerFirstFloor);
                return;
            }
            _ascendToSecondTower = false;
            _towerRun.AdvanceFloor();
        }
    }
}
