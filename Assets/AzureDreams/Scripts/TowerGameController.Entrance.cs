using System.Collections;
using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string EntranceResources = "Tower/Entrance/";
        private static readonly byte[] EntranceIconKinds =
        {
            ItemSystem.Kind.Crystal, ItemSystem.Kind.Ball, ItemSystem.Kind.Bell,
            ItemSystem.Kind.Seed, ItemSystem.Kind.Loupe, ItemSystem.Kind.Glasses
        };

        private bool _townDoorOpening, _towerArrivalPlaying, _towerEntranceCameraActive;
        private float _entranceFade, _entranceTitleAlpha;
        private Vector2Int _towerEntranceEdge, _towerEntranceInward;
        private int _towerEntranceWidth;
        private TownGateDoor _innerTowerDoor;
        private Material _entranceDoorMaterial, _entranceStoneMaterial, _entrancePortalMaterial;
        private Texture2D _entranceTitle;
        private Sprite _entranceGlow;
        private AudioSource _entranceAudio;
        private AudioClip _entranceDoorSound;

        private Material EntranceMaterial(ref Material cached, string resource, Material fallback)
        {
            if (cached) return cached;
            var texture = Resources.Load<Texture2D>(EntranceResources + resource);
            var template = Resources.Load<Material>(FacadeDoorResources + "FacadeDoor");
            if (!texture || !template) return fallback;
            cached = new Material(template) { mainTexture = texture };
            return cached;
        }

        private void ConfigureTownEntranceDoor(GameObject gate)
        {
            if (!gate.TryGetComponent<TownGateDoor>(out var door)) door = gate.AddComponent<TownGateDoor>();
            gate.transform.rotation = Quaternion.Euler(0f, UsingRetailTown ? 180f : 90f, 0f);
            door.ConfigureDoubleDoor(UsingRetailTown ? 4f : 1.06f, UsingRetailTown ? 3f : 1.45f,
                EntranceMaterial(ref _entranceDoorMaterial, "Door", _wallMaterial));
            LoadEntranceDoorSound();
        }

        private IEnumerator PlayTownTowerEntrance(EventTrigger trigger, TownGateDoor door)
        {
            _townDoorOpening = true;
            _message = string.Empty;
            ResetPush();
            var actor = _player;
            var start = actor.transform.position;
            var companion = _kewneMeetingView;
            var companionStart = companion ? companion.transform.position : Vector3.zero;
            var gate = trigger.transform;
            var forward = gate.forward;
            var center = gate.position + Vector3.up * (UsingRetailTown ? 1.5f : 0.8f);
            float outsideDistance = Mathf.Clamp(Vector3.Dot(gate.position - start, forward), 0.1f, 0.52f);
            var threshold = gate.position - forward * outsideDistance + Vector3.up * TownActorFootOffset;
            var finish = gate.position + forward * 0.88f + Vector3.up * TownActorFootOffset;
            var icons = CreateEntranceIcons(gate);
            var portal = TownGateDoor.AddPanel(gate, "Tower entrance light",
                new Vector3(0f, UsingRetailTown ? 1.5f : 0.72f, 1.04f),
                new Vector3(UsingRetailTown ? 3.95f : 1.06f, UsingRetailTown ? 2.96f : 1.44f, 0.03f),
                EntranceMaterial(ref _entrancePortalMaterial, "Portal", _wallMaterial));
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            const float duration = 2.25f;
            bool playedDoor = false;
            Animate(_playerAnimation, true);
            for (float time = 0f; time < duration && _townDoorOpening && actor && trigger; time += Time.unscaledDeltaTime)
            {
                float opening = Mathf.Clamp01((time - 0.53f) / 0.68f);
                if (!playedDoor && time >= 0.53f)
                {
                    PlayEntranceDoorSound();
                    playedDoor = true;
                }
                door.SetOpen(Mathf.SmoothStep(0f, 1f, opening));
                float approach = Mathf.InverseLerp(0.06f, 0.58f, time);
                float crossing = Mathf.InverseLerp(0.96f, 1.94f, time);
                var position = time < 0.96f ? Vector3.Lerp(start, threshold, approach) :
                    Vector3.Lerp(threshold, finish, crossing);
                bool walking = (approach > 0f && approach < 1f && (start - threshold).sqrMagnitude > 0.001f) ||
                    (crossing > 0f && crossing < 1f);
                PresentEntranceWalk(actor, position, forward, time, walking);
                if (companion)
                {
                    var companionOffset = gate.right * 0.72f - forward * 0.22f;
                    var companionPosition = time < 0.96f
                        ? Vector3.Lerp(companionStart, threshold + companionOffset, approach)
                        : Vector3.Lerp(threshold, finish, crossing) + companionOffset;
                    PresentGateKewne(companion, companionPosition, forward, time, walking);
                }
                SetEntranceCamera(Vector3.Lerp(start - Vector3.up * TownActorFootOffset, center, 0.65f),
                    yaw, 18f, UsingRetailTown ? 8.9f : 5.6f);
                AnimateEntranceIcons(icons, center, gate.right, forward, time);
                _entranceFade = Mathf.InverseLerp(1.93f, duration, time);
                yield return null;
            }

            if (_townDoorOpening && actor && trigger)
            {
                PresentEntranceWalk(actor, finish, forward, duration, false);
                Animate(_playerAnimation, false);
                _townDoorOpening = false;
                ResetEntranceCamera();
                if (trigger.TryActivate(actor) && _area == Area.Tower) yield break;
            }

            if (actor && actor == _player) actor.transform.position = start;
            if (companion)
            {
                RemoveGenerated(companion);
                _kewneMeetingView = null;
                _kewneTutorialChoice = null;
            }
            if (door) door.SetOpen(0f);
            foreach (var icon in icons) if (icon) RemoveGenerated(icon.gameObject);
            if (portal) Destroy(portal);
            _townDoorOpening = false;
            ResetEntranceCamera();
            UpdateCameraFollow(snap: true);
        }

        private void PrepareManagedTowerEntrance()
        {
            if (InTowerTutorial)
            {
                // The retail tutorial's three entrance tiles end at row 62.
                _towerEntranceEdge = new Vector2Int(TutorialEntrance.x, 62);
                _towerEntranceInward = Vector2Int.down;
                _towerEntranceWidth = 3;
                return;
            }

            bool foundWall = _towerFloorLayout.TryMoveEntranceToWall(out _towerEntranceInward);
            _towerEntranceEdge = _towerFloorLayout.StairsDownCell;
            _towerEntranceWidth = foundWall ? 1 : 0;
            if (!foundWall) _towerEntranceInward = Vector2Int.up;
        }

        private bool IsTowerEntranceBoundary(Vector2Int cell, Vector2Int outward)
        {
            if (_floor != 1 || _towerEntranceWidth == 0 || outward != -_towerEntranceInward) return false;
            return IsTowerDoorEdge(cell, _towerEntranceWidth);
        }

        private bool IsTowerDoorEdge(Vector2Int cell, int width)
        {
            var outward = -_towerEntranceInward;
            var delta = cell - _towerEntranceEdge;
            return delta.x * outward.x + delta.y * outward.y == 0 &&
                Mathf.Abs(delta.x * outward.y - delta.y * outward.x) <= width / 2;
        }

        private bool CrossesClosedTowerDoor(Vector2Int from, Vector2Int step)
        {
            if (!_innerTowerDoor || !_innerTowerDoor.gameObject.activeInHierarchy) return false;
            int width = Mathf.Max(1, _towerEntranceWidth);
            return (step == -_towerEntranceInward && IsTowerDoorEdge(from, width)) ||
                (step == _towerEntranceInward && IsTowerDoorEdge(from + step, width));
        }

        private void CreateInnerTowerDoor(Vector2Int edge, Vector2Int inward, int width, bool addThreshold)
        {
            _towerEntranceEdge = edge;
            _towerEntranceInward = inward;
            var forward = new Vector3(inward.x, 0f, inward.y);
            var gate = new GameObject("Tower inner double doors");
            gate.transform.SetParent(transform, false);
            gate.transform.SetPositionAndRotation(new Vector3(edge.x, 0f, edge.y) - forward * 0.5f,
                Quaternion.LookRotation(forward));
            _generated.Add(gate);
            _innerTowerDoor = gate.AddComponent<TownGateDoor>();
            float span = Mathf.Max(1, width);
            float height = width == 3 ? 2.4f : 1.85f;
            _innerTowerDoor.ConfigureDoubleDoor(span, height,
                EntranceMaterial(ref _entranceDoorMaterial, "Door", _wallMaterial));
            var stone = EntranceMaterial(ref _entranceStoneMaterial, "Stone", _wallMaterial);
            foreach (float side in new[] { -1f, 1f })
                TownGateDoor.AddPanel(gate.transform, "Stone jamb",
                    new Vector3(side * (span * 0.5f + 0.085f), height * 0.5f, 0f),
                    new Vector3(0.17f, height, 0.4f), stone);
            if (!addThreshold || width == 0) return;

            var v = new List<Vector3>();
            var indices = new List<int>();
            var uv = new List<Vector2>();
            var origin = gate.transform.position - Vector3.up * 0.002f;
            for (int i = 0; i < width; i++)
            {
                var left = origin + gate.transform.right * (i - span * 0.5f);
                var right = left + gate.transform.right;
                AppendFloorQuad(v, indices, uv, left - forward * 1.2f, left, right, right - forward * 1.2f);
                if (_earlyTowerTiles) SetTowerTileUvs(uv, 68, 324, 24, 384);
            }
            CreateManagedMesh("Tower entrance threshold", v, indices, uv,
                _earlyTowerTiles ? _earlyTowerTiles : _groundMaterial, false);
        }

        private IEnumerator PlayTowerArrival()
        {
            _message = string.Empty;
            _towerArrivalPlaying = true;
            _entranceFade = 1f;
            LoadEntranceDoorSound();
            var actor = _player;
            var door = _innerTowerDoor;
            if (actor && door)
            {
                var forward = door.transform.forward;
                var start = door.transform.position - forward * 0.92f + Vector3.up * TownActorFootOffset;
                var end = CellPosition(_playerCell);
                float walkDistance = Vector3.Distance(start, end);
                float walkSeconds = Mathf.Max(1.12f, walkDistance / 3.7f);
                const float duration = 2.8f;
                float yaw = CameraYaw();
                float approachYaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
                float orbit = Mathf.DeltaAngle(approachYaw, yaw) - 360f;
                float closeStart = 0.38f + walkSeconds * 1.26f / walkDistance;
                bool closing = false;
                door.SetOpen(1f);
                Animate(_playerAnimation, true);
                for (float time = 0f; time < duration && _towerArrivalPlaying && actor && door; time += Time.unscaledDeltaTime * TowerAnimationSpeed)
                {
                    float walk = Mathf.Clamp01((time - 0.38f) / walkSeconds);
                    var position = Vector3.Lerp(start, end, walk);
                    PresentEntranceWalk(actor, position, forward, time, walk > 0f && walk < 1f);
                    if (_starterJoinedThisRun && _familiar)
                    {
                        var companionEnd = CellPosition(_familiarCell);
                        var companionStart = start + door.transform.right * 0.72f - forward * 0.22f;
                        PresentGateKewne(_familiar, Vector3.Lerp(companionStart, companionEnd, walk),
                            forward, time, walk > 0f && walk < 1f);
                    }
                    if (!closing && time >= closeStart)
                    {
                        closing = true;
                        PlayEntranceDoorSound();
                    }
                    if (closing)
                        door.SetOpen(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(closeStart, closeStart + 0.66f, time)));
                    float sweep = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 2.55f, time));
                    var focus = Vector3.Lerp(door.transform.position + forward * 0.65f,
                        position + Vector3.up * (0.35f - TownActorFootOffset) +
                            Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 0.45f, sweep);
                    SetEntranceCamera(focus, yaw - orbit * (1f - sweep),
                        Mathf.Lerp(17f, 46.25f, sweep), Mathf.Lerp(11f, _towerCameraDistance * _cameraZoom, sweep));
                    _entranceFade = 1f - Mathf.InverseLerp(0f, 0.40f, time);
                    _entranceTitleAlpha = Mathf.InverseLerp(0.12f, 0.45f, time) * (1f - Mathf.InverseLerp(1.65f, 2.4f, time));
                    yield return null;
                }
                if (!_towerArrivalPlaying || !actor || !door) yield break;
                door.SetOpen(0f);
                PresentEntranceWalk(actor, end, forward, duration, false);
                Animate(_playerAnimation, false);
                if (_useGeneratedTowerFloors && _towerEntranceWidth == 0) RemoveGenerated(door.gameObject);
            }

            ResetEntranceCamera();
            UpdateCameraFollow(snap: true);
            if (!InTowerTutorial && actor && _familiarDefinition)
                yield return SummonTowerEntryFamiliars(actor);
            if (!_towerArrivalPlaying || _player != actor) yield break;

            _towerArrivalPlaying = false;
            if (InTowerTutorial && !_starterJoinedThisRun) BeginTutorialIntro();
        }

        private void PresentEntranceWalk(GameObject actor, Vector3 position, Vector3 forward, float time, bool walking)
        {
            actor.transform.position = position;
            var direction = new Vector2(forward.x, forward.z);
            if (_area == Area.Town) _townFacing = direction;
            else _towerFacing = new Vector2Int(Mathf.RoundToInt(direction.x), Mathf.RoundToInt(direction.y));
            if (_kohSprite) _kohSprite.SetFacing(direction);
            if (_playerLocomotion) _playerLocomotion.Present(forward, time * 2f, walking);
        }

        private void SetEntranceCamera(Vector3 focus, float yaw, float pitch, float distance)
        {
            _towerEntranceCameraActive = true;
            if (!_gameCamera) return;
            var rotation = Quaternion.Euler(pitch, yaw, 0f);
            _gameCamera.orthographic = false;
            _gameCamera.fieldOfView = 40f;
            _gameCamera.transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }

        private Transform[] CreateEntranceIcons(Transform parent)
        {
            if (!_entranceGlow)
            {
                var texture = Resources.Load<Texture2D>(EntranceResources + "IconGlow");
                if (texture) _entranceGlow = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    Vector2.one * 0.5f, 64f);
            }
            var icons = new Transform[EntranceIconKinds.Length];
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = new GameObject("Tower seal icon");
                icon.transform.SetParent(parent, false);
                _generated.Add(icon);
                var glow = icon.AddComponent<SpriteRenderer>();
                glow.sprite = _entranceGlow;
                glow.color = Color.HSVToRGB(i / (float)icons.Length, 0.37f, 1f);
                var glyph = new GameObject("Seal glyph");
                glyph.transform.SetParent(icon.transform, false);
                glyph.transform.localPosition = Vector3.back * 0.015f;
                glyph.transform.localScale = Vector3.one * 0.42f;
                var sprite = glyph.AddComponent<SpriteRenderer>();
                sprite.sprite = ItemDisplaySprite(EntranceIconKinds[i], 1);
                sprite.sortingOrder = 1;
                icons[i] = icon.transform;
            }
            return icons;
        }

        private void AnimateEntranceIcons(Transform[] icons, Vector3 center, Vector3 right, Vector3 forward, float time)
        {
            float gather = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 1.24f, time));
            for (int i = 0; i < icons.Length; i++)
            {
                var icon = icons[i];
                float angle = i * (Mathf.PI * 2f / icons.Length) - time * 3.5f;
                float orbit = Mathf.Lerp(2.9f, 0.16f, gather);
                icon.position = center - forward * 0.48f +
                    (right * Mathf.Cos(angle) + Vector3.up * (Mathf.Sin(angle) * 0.72f)) * orbit;
                if (_gameCamera) icon.rotation = _gameCamera.transform.rotation;
                icon.localScale = Vector3.one * (Mathf.Lerp(0.55f, 1.6f, gather) *
                    (1f - Mathf.InverseLerp(1.28f, 1.63f, time)));
            }
        }

        private void PlayEntranceDoorSound()
        {
            LoadEntranceDoorSound();
            if (!_entranceDoorSound) return;
            if (!_entranceAudio)
            {
                _entranceAudio = gameObject.AddComponent<AudioSource>();
                _entranceAudio.playOnAwake = false;
                _entranceAudio.volume = _sfxVolume;
            }
            _entranceAudio.PlayOneShot(_entranceDoorSound);
        }

        private void LoadEntranceDoorSound()
        {
            if (!_entranceDoorSound) _entranceDoorSound = Resources.Load<AudioClip>(EntranceResources + "TowerDoor");
        }

        private void DrawTowerEntranceOverlay(float width, float height)
        {
            var previous = GUI.color;
            if (_entranceTitleAlpha > 0f)
            {
                if (!_entranceTitle) _entranceTitle = Resources.Load<Texture2D>(EntranceResources + "FloorTitle");
                if (_entranceTitle)
                {
                    GUI.color = new Color(1f, 1f, 1f, _entranceTitleAlpha);
                    GUI.DrawTexture(new Rect(width * 0.5f - 132f, height * 0.38f, 264f, 108f), _entranceTitle);
                }
            }
            GUI.color = Color.white;
            if (_entranceFade > 0f)
                DrawHudFill(new Rect(0, 0, width, height), new Color(0f, 0f, 0f, _entranceFade));
            GUI.color = previous;
        }

        private void ResetEntranceCamera()
        {
            _towerEntranceCameraActive = false;
            _entranceFade = _entranceTitleAlpha = 0f;
        }

        private void StopTowerEntrance()
        {
            if (_towerArrivalPlaying && _player)
            {
                _player.transform.position = CellPosition(_playerCell);
                if (_innerTowerDoor) _innerTowerDoor.SetOpen(0f);
                Animate(_playerAnimation, false);
            }
            _townDoorOpening = _towerArrivalPlaying = false;
            _townTowerGate = null;
            _kewneMeetingView = null;
            _kewneMeeting = KewneMeeting.None;
            _kewneTutorialChoice = null;
            _innerTowerDoor = null;
            _towerEntranceWidth = 0;
            ResetEntranceCamera();
        }

        private void DisposeTowerEntranceArt()
        {
            if (_entranceDoorMaterial) Destroy(_entranceDoorMaterial);
            if (_entranceStoneMaterial) Destroy(_entranceStoneMaterial);
            if (_entrancePortalMaterial) Destroy(_entrancePortalMaterial);
            if (_entranceGlow) Destroy(_entranceGlow);
        }
    }
}
