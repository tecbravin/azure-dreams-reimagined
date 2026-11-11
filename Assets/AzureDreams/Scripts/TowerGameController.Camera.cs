using System.Collections.Generic;
using UnityEngine;
using AzureDreams.Core;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [Header("Camera framing")]
        [SerializeField, Min(1f)] private float _towerCameraDistance = 8f;
        [SerializeField, Min(1f)] private float _townCameraDistance = 9.5f;

        private float _cameraZoom = 1f;
        private float _cameraYawOffset;
        private GameObject _cameraFollowPlayer;
        private Vector3 _cameraFocus;
        private float _cameraFollowYaw;
        private float _cameraFollowDistance;
        private const float InteriorCameraDistance = 7f;
        private const float CameraFollowRate = 16f;
        private const float CameraOrbitRate = 13f;
        private float GameplayCameraPitch => _interior ? 55f : _area == Area.Tower ? 46.25f : 35f;

        private void ReadCameraControls()
        {
            var mouse = Mouse.current;
            float scroll = mouse == null ? 0f : mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
                _cameraZoom = Mathf.Clamp(_cameraZoom * Mathf.Pow(0.88f, Mathf.Sign(scroll)), 0.55f, 1.8f);
            if (_rotateLeftAction?.WasPressedThisFrame() == true) _cameraYawOffset -= 45f;
            if (_rotateRightAction?.WasPressedThisFrame() == true) _cameraYawOffset += 45f;
            _cameraYawOffset = Mathf.Repeat(_cameraYawOffset, 360f);
        }

        private Vector2 CameraRelativeMovement(Vector2 input)
        {
            float yaw = _cameraFollowPlayer == _player && _cameraFollowPlayer ? _cameraFollowYaw : CameraYaw();
            var world = Quaternion.Euler(0, yaw, 0) * new Vector3(input.x, 0, input.y);
            return new Vector2(world.x, world.z);
        }

        private float CameraYaw() => (_area == Area.Town && UsingRetailTown ? 180f : 0f) + _cameraYawOffset;

        private const string BlindKohLayerName = "Blind Koh";
        private readonly List<Renderer> _blindKohRenderers = new();
        private readonly List<(GameObject View, int Layer)> _blindKohLayers = new();
        private Camera _blindCamera;
        private PostProcessingController _blindCameraEffects;
        private int _blindKohLayer, _normalCameraMask;
        private CameraClearFlags _normalClearFlags;
        private Color _normalClearColor;
        private bool KohIsBlind => _area == Area.Tower && Combat.HasStatus(_playerActor, StatusId.BLIND);

        private bool _towerBackdropActive;
        private CameraClearFlags _townClearFlags;
        private Color _townClearColor;
        private int _backdropFloor;

        private void UpdateTowerCameraBackdrop()
        {
            if (_area == Area.Tower)
            {
                if (!_towerBackdropActive)
                {
                    _townClearFlags = _gameCamera.clearFlags;
                    _townClearColor = _gameCamera.backgroundColor;
                    _towerBackdropActive = true;
                }
                if (_backdropFloor == _floor) return;
                _backdropFloor = _floor;
                _gameCamera.clearFlags = CameraClearFlags.SolidColor;
                Color baseColor = _floor <= 3 ? new Color(0.015f, 0.09f, 0.075f) :
                    _floor <= 5 ? Color.black :
                    _floor <= 7 ? new Color(0.025f, 0.065f, 0.015f) :
                    _floor <= 9 ? new Color(0.055f, 0.035f, 0.02f) :
                    _floor <= 11 ? new Color(0.09f, 0.015f, 0.005f) :
                    TowerFloorThemeResolver.GetThemeFogColor(TowerFloorThemeResolver.GetThemeForFloor(_floor)) * 0.18f;
                baseColor.a = 1f;
                _gameCamera.backgroundColor = baseColor;
            }
            else if (_towerBackdropActive)
            {
                _gameCamera.clearFlags = _townClearFlags;
                _gameCamera.backgroundColor = _townClearColor;
                _towerBackdropActive = false;
                _backdropFloor = 0;
            }
        }

        private void LateUpdate()
        {
            AnimateTowerElevator();
            // Update the camera first so the lift uses this frame's screen axes.
            UpdateCameraFollow();
            UpdateMinimapTerrain();
            DiscoverMinimapItems();
            OrientVisibleTrapMarkers();
            UpdateTownKewne();
            if (_kohSprite) _kohSprite.PresentLift(_heldTownPot || _carriedView || _pickupView || HoldingBowlingBall);
            PresentGroundPickup();
            if (_area == Area.Tower && _carriedView)
                PlaceHeldView(_carriedView, _carriedFamiliar || _carriedMonster.Actor != null);
            PresentTownPot();
            PresentTownInventoryItem();
            PresentMonsterExhaustion();
            if (_kohSprite)
            {
                bool greenFlash = _area == Area.Tower &&
                    (KohIsConfused || Combat.HasStatus(_playerActor, StatusId.POISON));
                _kohSprite.PresentGreenStatusFlash(greenFlash);
                _kohSprite.PresentParalysis(_area == Area.Tower && KohIsParalyzed);
                _kohSprite.PresentSleep(_wakingInBed || (_area == Area.Tower && KohIsAsleep));
                var visibleEquipment = _area == Area.Tower && !IsFrog(_playerActor) ? _runInventory : null;
                var weapon = visibleEquipment?.EquippedItem(PlayerEquipped, false);
                var shield = visibleEquipment?.EquippedItem(PlayerEquipped, true);
                // Barry's Rusty Sword has no separate equipped sprite in the retail bank.
                const byte copperSwordSubtype = 2;
                byte weaponVisualSubtype = weapon is { Kind: ItemKind.Sword, Subtype: RustySwordSubtype }
                    ? copperSwordSubtype : weapon?.Subtype ?? 0;
                _kohSprite.SetEquipment(weapon?.Kind ?? 0, weaponVisualSubtype, weapon == null ? null : ItemDisplaySprite(weapon),
                    shield?.Subtype ?? 0, shield == null ? null : ItemDisplaySprite(shield));
            }
            UpdateBlindnessView();
        }

        private void UpdateBlindnessView()
        {
            if (!KohIsBlind || !_gameCamera || !_player)
            {
                RestoreBlindnessView();
                return;
            }

            if (_blindCamera != _gameCamera)
            {
                RestoreBlindnessView();
                _blindKohLayer = LayerMask.NameToLayer(BlindKohLayerName);
                if (_blindKohLayer < 0) return;
                _blindCamera = _gameCamera;
                _normalCameraMask = _blindCamera.cullingMask;
                _normalClearFlags = _blindCamera.clearFlags;
                _normalClearColor = _blindCamera.backgroundColor;
                _blindCamera.cullingMask = 1 << _blindKohLayer;
                _blindCamera.clearFlags = CameraClearFlags.SolidColor;
                _blindCamera.backgroundColor = Color.black;
                if (_blindCamera.TryGetComponent(out _blindCameraEffects))
                    _blindCameraEffects.PreserveBlackBackground = true;
            }

            // A temporary frog model can be added to Koh during the effect.
            _player.GetComponentsInChildren(true, _blindKohRenderers);
            foreach (var renderer in _blindKohRenderers)
            {
                var visual = renderer.gameObject;
                if (visual.layer == _blindKohLayer) continue;
                _blindKohLayers.Add((visual, visual.layer));
                visual.layer = _blindKohLayer;
            }
            _blindKohRenderers.Clear();
        }

        private void RestoreBlindnessView()
        {
            if (_blindCamera)
            {
                _blindCamera.cullingMask = _normalCameraMask;
                _blindCamera.clearFlags = _normalClearFlags;
                _blindCamera.backgroundColor = _normalClearColor;
            }
            _blindCamera = null;
            if (_blindCameraEffects) _blindCameraEffects.PreserveBlackBackground = false;
            _blindCameraEffects = null;
            foreach (var (visual, layer) in _blindKohLayers)
                if (visual) visual.layer = layer;
            _blindKohLayers.Clear();
        }

        private KewneSpriteView _townKewne;
        private float _townKewneBearing;

        private void UpdateTownKewne()
        {
            bool hasKewne = _area == Area.Town && !InRaceCourse && _player && _kewnePrefab && _familiarDefinition &&
                ((_kewneDefinition && _familiarDefinition == _kewneDefinition) ||
                 _hatchedSpecies == (int)AzureDreams.Core.MonsterSpecies.KEWNE);
            if (!hasKewne)
            {
                if (_townKewne) RemoveGenerated(_townKewne.gameObject);
                _townKewne = null;
                return;
            }

            float behind = Mathf.Atan2(-_townFacing.x, -_townFacing.y) * Mathf.Rad2Deg;
            if (!_townKewne)
            {
                const int species = (int)AzureDreams.Core.MonsterSpecies.KEWNE;
                var view = SpawnActor(_kewnePrefab, _playerCell, "Kewne - town follower", species: species,
                    palette: FamiliarPalette(species, _familiarActor));
                _townKewne = view.GetComponent<KewneSpriteView>();
                _townKewne.SetTownFlight();
                _townKewneBearing = behind;
            }
            else
            {
                _townKewneBearing = Mathf.LerpAngle(_townKewneBearing, behind, 1f - Mathf.Exp(-9f * Time.deltaTime));
            }

            var offset = Quaternion.Euler(0f, _townKewneBearing, 0f) * Vector3.forward;
            _townKewne.transform.position = _player.transform.position + offset * 2f + Vector3.up * 0.30f;
            _townKewne.Face(_townFacing);
        }

        private void UpdateCameraFollow(bool snap = false)
        {
            if (!_gameCamera || !_player) return;

            var target = _player.transform.position;
            target.y -= TownActorFootOffset + (_area == Area.Town ? _townJumpHeight : 0f);
            target = FrameTheaterCamera(FrameBowlingCamera(FrameVisitorEvent(target)));

            UpdateTowerCameraBackdrop();
            if (_quakeAscending)
                _gameCamera.transform.position = _quakeCameraStart + TowerQuakeShake();
            if (_towerEntranceCameraActive || _elevatorAscending) return;
            bool tower = _area == Area.Tower;
            float pitch = GameplayCameraPitch;
            float distance = (_interior ? InteriorCameraDistance : tower ? _towerCameraDistance : _townCameraDistance) * _cameraZoom;
            float yaw = CameraYaw();

            // A new room or floor replaces Koh's view. Frame the new map immediately.
            if (snap || _cameraFollowPlayer != _player)
            {
                _cameraFollowPlayer = _player;
                _cameraFocus = target;
                _cameraFollowYaw = yaw;
                _cameraFollowDistance = distance;
            }
            else
            {
                float followBlend = 1f - Mathf.Exp(-CameraFollowRate * Time.deltaTime);
                float orbitBlend = 1f - Mathf.Exp(-CameraOrbitRate * Time.deltaTime);
                _cameraFocus = Vector3.Lerp(_cameraFocus, target, followBlend);
                _cameraFollowYaw = Mathf.LerpAngle(_cameraFollowYaw, yaw, orbitBlend);
                _cameraFollowDistance = Mathf.Lerp(_cameraFollowDistance, distance, orbitBlend);
            }

            var rotation = Quaternion.Euler(pitch, _cameraFollowYaw, _trapReversalUntil > _completedTowerTurns ? 180f : 0f);
            var focus = _cameraFocus + Vector3.up * 0.35f +
                Quaternion.Euler(0, _cameraFollowYaw, 0) * Vector3.forward * 0.45f;
            _gameCamera.orthographic = false;
            _gameCamera.fieldOfView = 40f;
            _gameCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * _cameraFollowDistance + TowerQuakeShake(), rotation);
            if (tower && UsesManagedTowerLayout) UpdateTowerWallCutaway();
        }
    }
}
