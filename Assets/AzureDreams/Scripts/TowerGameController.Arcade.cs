using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string ArcadeMoleRoom = "Interior_09_arcade_1";
        private const string ArcadeBattingRoom = "Interior_2a_arcade_2";
        private enum ArcadePhase { Closed, Countdown, Playing, Results }
        private ArcadePhase _arcadePhase;
        private readonly List<GameObject> _arcadeViews = new();
        private readonly ArcadeMole[] _arcadeMoles = new ArcadeMole[4];
        private readonly ArcadeInk[] _arcadeInk = new ArcadeInk[30];
        private readonly ArcadeCreatureView[] _arcadeTakopoos = new ArcadeCreatureView[3];
        private readonly int[] _arcadeReturns = new int[3];
        private static readonly Vector2[] ArcadeHoles = { new(10.5f, 9.5f), new(9.5f, 12.5f), new(15.5f, 12.5f), new(14.5f, 9.5f) };
        private Vector3 _arcadeReturnPosition;
        private Vector2 _arcadeReturnFacing;
        private float _arcadeClock, _arcadeSpawnTimer, _arcadeSwing = 1f, _arcadeLane;
        private int _arcadeSpawned, _arcadeScore, _arcadeMisses, _arcadeMenuChoice, _arcadePattern;
        private bool _arcadeNewRecord;
        private bool _arcadeSwingHit;
        private Material _arcadeInkMaterial;
        private bool InArcade => _interior && _interior.interiorId is ArcadeMoleRoom or ArcadeBattingRoom;
        private bool ArcadeBatting => _interior && _interior.interiorId == ArcadeBattingRoom;
        private string ArcadeScoreKey => ArcadeBatting ? "arcade.batting" : "arcade.moles";
        private const float ArcadeBatX = 26f;
        private const float ArcadeTakoX = 16.5f;

        private sealed class ArcadeMole
        {
            public ArcadeCreatureView View;
            public float Age, Lifetime;
            public bool Hit, Active;
        }

        private sealed class ArcadeInk
        {
            public GameObject View;
            public float X, Z, Speed, Delay;
            public int Source;
            public bool Active, Returning;
        }

        private void SetupArcade()
        {
            if (!InArcade) return;
            _message = string.Empty;
            ApplyNativeMinigameResident("arcade_boy", "ArcadeBoy");
            ApplyNativeMinigameResident("arcade_girl", "ArcadeGirl");
            foreach (var npc in _townPeople)
                if (npc && npc.npcId.StartsWith("arcade_", StringComparison.Ordinal))
                {
                    npc.dialogueLines = Array.Empty<string>();
                    if (npc.TryGetComponent<CharacterStats>(out var stats))
                        stats.displayName = npc.npcId switch
                        {
                            "arcade_attendant" => T("Attendant", "Atendente"),
                            "arcade_visitor" => T("Visitor", "Visitante"),
                            "arcade_boy" => T("Boy", "Menino"),
                            "arcade_girl" => T("Girl", "Menina"),
                            _ => "Bet"
                        };
                }
        }

        private void BeginArcadeGame()
        {
            if (!InArcade || _changingInterior || _arcadePhase != ArcadePhase.Closed || !_player || _heldTownPot || _carriedItem != null) return;
            _arcadeReturnPosition = _player.transform.position;
            _arcadeReturnFacing = _townFacing;
            _arcadeClock = _arcadeSpawnTimer = 0;
            _arcadeSwing = 1f;
            _arcadeSpawned = _arcadeScore = _arcadeMisses = _arcadeMenuChoice = 0;
            _arcadePattern = UnityEngine.Random.Range(0, 4);
            _arcadeNewRecord = false;
            _arcadeSwingHit = false;
            Array.Clear(_arcadeReturns, 0, _arcadeReturns.Length);
            _townJumpHeight = _townJumpVelocity = 0f;
            _townMomentum = Vector2.zero;
            if (ArcadeBatting)
            {
                _arcadeLane = 16.5f;
                _player.transform.position = new Vector3(ArcadeBatX + .6f, TownActorFootOffset, _arcadeLane);
                _townFacing = Vector2.left;
                for (int i = 0; i < 3; i++)
                {
                    var actor = new GameObject("Arcade Takopoo " + (i + 1));
                    actor.transform.SetParent(transform, false);
                    actor.transform.position = new Vector3(ArcadeTakoX, -.20f, 15.3f + i * 1.2f);
                    actor.transform.rotation = Quaternion.LookRotation(Vector3.right);
                    _arcadeViews.Add(actor);
                    _arcadeTakopoos[i] = actor.AddComponent<ArcadeCreatureView>();
                    _arcadeTakopoos[i].Configure(true);
                }
            }
            else
            {
                _player.transform.position = new Vector3(12.5f, 1f + TownActorFootOffset, 12.5f);
                for (int i = 0; i < _arcadeMoles.Length; i++)
                {
                    var actor = new GameObject("Arcade Soilclaw " + (i + 1));
                    actor.transform.SetParent(transform, false);
                    _arcadeViews.Add(actor);
                    var view = actor.AddComponent<ArcadeCreatureView>();
                    view.Configure(false);
                    actor.SetActive(false);
                    _arcadeMoles[i] = new ArcadeMole { View = view };
                }
            }
            _playerLocomotion?.Present(Vector3.zero, 0, false);
            _kohSprite?.SetFacing(_townFacing);
            _arcadePhase = ArcadePhase.Countdown;
            UpdateCameraFollow(true);
        }

        private bool HandleArcadeInput()
        {
            if (_arcadePhase == ArcadePhase.Closed) return TryArcadePassage();
            // Town dialogue owns input while confirming cancellation.
            if (_storyChoices != null) return false;
            if (_cancel?.WasPressedThisFrame() == true)
            {
                if (_arcadePhase == ArcadePhase.Results) EndArcadeGame();
                else StoryAsk(FindStoryActor("arcade_attendant"), "Stop playing?", "Parar de jogar?",
                    (T("Keep playing", "Continuar jogando"), () => { }),
                    (T("Stop", "Parar"), EndArcadeGame));
                return true;
            }
            if (_arcadePhase == ArcadePhase.Results)
            {
                if (MenuDirectionPressed(1) || MenuDirectionPressed(2)) _arcadeMenuChoice ^= 1;
                if (_interact?.WasPressedThisFrame() == true) SelectArcadeResult();
                return true;
            }
            TickArcadeGame(Mathf.Min(.05f, Time.unscaledDeltaTime));
            return true;
        }

        private void TickArcadeGame(float dt)
        {
            if (!InArcade || !_player || _arcadePhase is ArcadePhase.Closed or ArcadePhase.Results) return;
            _arcadeClock += dt;
            if (_arcadePhase == ArcadePhase.Countdown)
            {
                if (_arcadeClock >= 3f)
                {
                    _arcadePhase = ArcadePhase.Playing;
                    _arcadeClock = 0;
                    PlayRetailSound(RetailSfx.Confirm);
                }
                return;
            }
            _arcadeSwing += dt;
            if (_attackAction?.WasPressedThisFrame() == true && _arcadeSwing >= .36f)
            {
                _arcadeSwing = 0;
                _arcadeSwingHit = false;
                PlayRetailSound(RetailSfx.WeaponSwing);
            }
            if (ArcadeBatting) TickArcadeBatting(dt);
            else TickArcadeMoles(dt);
            _kohSprite?.PresentAttack(_townFacing, Mathf.Clamp01(_arcadeSwing / .36f));
        }

        private void TickArcadeMoles(float dt)
        {
            MoveArcadeMolePlayer(dt);
            var position = _player.transform.position;
            _arcadeSpawnTimer -= dt;
            // Original advance_object_spawning_displays_high: 30 spawns, a
            // four-entry rotating pattern, and max(16, 42 - presented / 6) ticks.
            if (_arcadeSpawned < 30 && _arcadeSpawnTimer <= 0)
            {
                int hole = (_arcadePattern + _arcadeSpawned) & 3;
                var mole = _arcadeMoles[hole];
                if (!mole.Active)
                {
                    mole.Active = true;
                    mole.Hit = false;
                    mole.Age = 0;
                    mole.Lifetime = (2 * (14 + UnityEngine.Random.Range(0, 16)) + 20) / 30f;
                    mole.View.gameObject.SetActive(true);
                    _arcadeSpawned++;
                    _arcadeSpawnTimer = Mathf.Max(16, 42 - _arcadeSpawned / 6) / 30f;
                }
            }
            bool any = false;
            for (int i = 0; i < _arcadeMoles.Length; i++)
            {
                var mole = _arcadeMoles[i];
                if (!mole.Active) continue;
                mole.Age += dt;
                if (mole.Age >= mole.Lifetime)
                {
                    if (!mole.Hit) _arcadeMisses++;
                    mole.Active = false;
                    mole.View.gameObject.SetActive(false);
                    continue;
                }
                any = true;
                Vector2 hole = ArcadeHoles[i];
                float emerge = Mathf.Min(Mathf.Clamp01(mole.Age / .18f), Mathf.Clamp01((mole.Lifetime - mole.Age) / .18f));
                var point = new Vector3(hole.x, 0, hole.y);
                point.y = 2f - .95f * (1 - emerge);
                mole.View.transform.position = point;
                Vector3 face = _player.transform.position - point;
                face.y = 0;
                if (face.sqrMagnitude > .01f) mole.View.transform.rotation = Quaternion.LookRotation(face);
                Vector2 delta = hole - new Vector2(position.x, position.z);
                if (!mole.Hit && !_arcadeSwingHit && mole.Age > .18f && _arcadeSwing is >= .07f and <= .24f &&
                    delta.sqrMagnitude <= 2.8f && (delta.sqrMagnitude < .3f || Vector2.Dot(delta.normalized, _townFacing) > .3f))
                {
                    mole.Hit = true;
                    _arcadeSwingHit = true;
                    _arcadeScore++;
                    mole.Lifetime = mole.Age + .2f;
                    PlayRetailSound(RetailSfx.Hit);
                }
            }
            if (_arcadeSpawned == 30 && !any) CompleteArcadeGame();
        }

        private static float ArcadeBoardFloor(Vector3 point) =>
            point.x < 10f || point.x > 15f || (point.z < 10f && (point.x < 11f || point.x > 14f)) ? 2f : 1f;

        private void MoveArcadeMolePlayer(float dt)
        {
            // The captured navigation mask deliberately excludes the game board.
            // During the activity, use its captured one/two-unit platforms.
            var input = Vector2.ClampMagnitude(_move?.ReadValue<Vector2>() ?? Vector2.zero, 1);
            Vector3 direction = new(-input.x, 0, -input.y);
            bool run = _runAction?.IsPressed() == true;
            float speed = _townMoveSpeed * (run ? 1.6f : 1f);
            var position = _player.transform.position;
            float feet = position.y - TownActorFootOffset;
            float ground = ArcadeBoardFloor(position);
            if (feet <= ground + .01f && _wait?.WasPressedThisFrame() == true) _townJumpVelocity = Mathf.Sqrt(2 * TownGravity * 1.8f);
            feet += _townJumpVelocity * dt - .5f * TownGravity * dt * dt;
            _townJumpVelocity -= TownGravity * dt;
            var target = position + direction * speed * dt;
            target.x = Mathf.Clamp(target.x, 9.4f, 15.6f);
            target.z = Mathf.Clamp(target.z, 9.4f, 13.0f);
            if (ArcadeBoardFloor(target) > feet + .08f) target = position;
            ground = ArcadeBoardFloor(target);
            if (feet <= ground && _townJumpVelocity <= 0) { feet = ground; _townJumpVelocity = 0; }
            target.y = feet + TownActorFootOffset;
            _player.transform.position = target;
            _townJumpHeight = Mathf.Max(0, feet - ground);
            _kohSprite?.SetAirborne(_townJumpHeight > .01f);
            if (direction.sqrMagnitude > .001f) _townFacing = new Vector2(direction.x, direction.z);
            _playerLocomotion?.Present(direction, _arcadeClock * (run ? 2.5f : 1.7f) % 1, direction.sqrMagnitude > .001f, run);
        }

        private void TickArcadeBatting(float dt)
        {
            Vector2 input = _move?.ReadValue<Vector2>() ?? Vector2.zero;
            _arcadeLane = Mathf.Clamp(_arcadeLane - input.y * dt * 3.3f, 14.8f, 18.2f);
            _player.transform.position = new Vector3(ArcadeBatX + .6f, TownActorFootOffset, _arcadeLane);
            _townFacing = Vector2.left;
            _playerLocomotion?.Present(new Vector3(0, 0, -input.y), _arcadeClock * 1.8f % 1, Mathf.Abs(input.y) > .1f);
            _arcadeSpawnTimer -= dt;
            if (_arcadeSpawned < 30 && _arcadeSpawnTimer <= 0)
            {
                // initialize_tako_minigame/update_tako_minigame: three targets,
                // ten table-selected volleys. Exact native timing table unresolved.
                int volley = _arcadeSpawned / 3;
                for (int i = 0; i < 3; i++)
                {
                    var ball = CreatePrimitive(PrimitiveType.Sphere, "Arcade ink ball", Vector3.zero,
                        Vector3.one * .27f, _groundMaterial, false);
                    if (!_arcadeInkMaterial) _arcadeInkMaterial = new Material(AssetMode.Load<Shader>("Town/Arcade/Creature")) { color = new Color(.04f, .02f, .06f) };
                    ball.GetComponent<Renderer>().sharedMaterial = _arcadeInkMaterial;
                    _arcadeViews.Add(ball);
                    _arcadeInk[_arcadeSpawned++] = new ArcadeInk
                    {
                        View = ball, X = ArcadeTakoX + .5f, Z = 15.3f + i * 1.2f,
                        Source = i, Speed = 4.6f + volley * .12f, Delay = i * .25f,
                        Active = true
                    };
                }
                _arcadeSpawnTimer = 2.7f;
            }
            bool any = false;
            foreach (var ink in _arcadeInk)
            {
                if (ink == null || !ink.Active) continue;
                any = true;
                ink.Delay -= dt;
                ink.View.SetActive(ink.Delay <= 0);
                if (ink.Delay > 0) continue;
                ink.X += (ink.Returning ? -8f : ink.Speed) * dt;
                ink.View.transform.position = new Vector3(ink.X, .85f, ink.Z);
                if (ink.Returning)
                {
                    if (ink.X > ArcadeTakoX) continue;
                    ink.Active = false;
                    ink.View.SetActive(false);
                    int hits = ++_arcadeReturns[ink.Source];
                    _arcadeTakopoos[ink.Source].SetInk(hits);
                    if (hits == 10) _arcadeScore += 10;
                    PlayArcadeSound(0x50C, RetailSfx.Hit);
                }
                else if (Mathf.Abs(ink.X - ArcadeBatX) <= .38f && Mathf.Abs(ink.Z - _arcadeLane) <= .55f &&
                    _arcadeSwing is >= .10f and <= .33f)
                {
                    bool perfect = _arcadeSwing <= .20f;
                    _arcadeScore += perfect ? 2 : 1;
                    PlayArcadeSound(perfect ? 0x513 : 0x510, perfect ? RetailSfx.Confirm : RetailSfx.Hit);
                    if (perfect) ink.Returning = true;
                    else { ink.Active = false; ink.View.SetActive(false); }
                }
                else if (ink.X > ArcadeBatX + .5f)
                {
                    _arcadeMisses++;
                    ink.Active = false;
                    ink.View.SetActive(false);
                    PlayRetailSound((RetailSfx)0x506);
                }
            }
            if (_arcadeSpawned == 30 && !any) CompleteArcadeGame();
        }

        private void CompleteArcadeGame()
        {
            if (_arcadePhase != ArcadePhase.Playing) return;
            string key = ArcadeScoreKey;
            int best = TownSaveVariables.GetInt(key + ".best");
            _arcadeNewRecord = _arcadeScore > best;
            TownSaveVariables.SetInt(key + ".last", _arcadeScore);
            TownSaveVariables.SetBool(key + ".played", true);
            TownSaveVariables.SetBool(key + ".new_record", _arcadeNewRecord);
            if (_arcadeNewRecord) TownSaveVariables.SetInt(key + ".best", _arcadeScore);
            PersistTownProgress();
            _arcadePhase = ArcadePhase.Results;
            _arcadeMenuChoice = 0;
            _playerLocomotion?.Present(Vector3.zero, 0, false);
            _kohSprite?.PresentAttack(_townFacing, 1);
            PlayRetailSound(_arcadeNewRecord ? RetailSfx.Confirm : RetailSfx.Cancel);
        }

        private void PlayArcadeSound(int selector, RetailSfx fallback)
        {
            var sound = (RetailSfx)selector;
            if (!_retailSounds.TryGetValue(sound, out var clip))
                _retailSounds[sound] = clip = AssetMode.Load<AudioClip>($"Sfx/Retail/{selector:X3}");
            PlayRetailSound(clip ? sound : fallback);
        }

        private void SelectArcadeResult()
        {
            bool replay = _arcadeMenuChoice == 0;
            EndArcadeGame();
            if (replay) BeginArcadeGame();
        }

        private void EndArcadeGame() => ResetArcade(true);

        private void ResetArcade(bool restorePlayer = false)
        {
            if (_arcadePhase != ArcadePhase.Closed && restorePlayer && _player)
            {
                _player.transform.position = _arcadeReturnPosition;
                _playerCell = new Vector2Int(Mathf.RoundToInt(_arcadeReturnPosition.x), Mathf.RoundToInt(_arcadeReturnPosition.z));
                _townFacing = _arcadeReturnFacing;
            }
            _kohSprite?.PresentAttack(_townFacing, 1);
            _kohSprite?.SetAirborne(false);
            _townJumpHeight = _townJumpVelocity = 0;
            _townMomentum = Vector2.zero;
            foreach (var view in _arcadeViews)
            {
                if (!view) continue;
                _generated.Remove(view);
                Destroy(view);
            }
            _arcadeViews.Clear();
            if (_arcadeInkMaterial) Destroy(_arcadeInkMaterial);
            _arcadeInkMaterial = null;
            Array.Clear(_arcadeMoles, 0, _arcadeMoles.Length);
            Array.Clear(_arcadeInk, 0, _arcadeInk.Length);
            Array.Clear(_arcadeTakopoos, 0, _arcadeTakopoos.Length);
            _arcadePhase = ArcadePhase.Closed;
            if (restorePlayer)
            {
                _kohSprite?.SetFacing(_townFacing);
                _playerLocomotion?.Present(Vector3.zero, 0, false);
                UpdateCameraFollow(true);
            }
        }

        private bool FrameArcadeCamera()
        {
            if (_arcadePhase == ArcadePhase.Closed || !_gameCamera) return false;
            var target = ArcadeBatting ? new Vector3(21.5f, 0, 16.5f) : new Vector3(12.5f, 0, 10.8f);
            _gameCamera.transform.position = target + (ArcadeBatting ? new Vector3(0, 10, 11) : new Vector3(0, 11, 7));
            _gameCamera.transform.LookAt(target);
            _gameCamera.orthographic = false;
            _gameCamera.fieldOfView = 44f;
            return true;
        }

        private bool DrawArcade(float width, float height)
        {
            if (_arcadePhase == ArcadePhase.Closed || _storyChoices != null) return false;
            string title = ArcadeBatting ? T("Takopoo batting center", "Rebatidas dos Takopoos") : T("Hit the Mole", "Acerte a Toupeira");
            var hud = new Rect(18, 18, Mathf.Min(450, width - 36), 108);
            DrawDialogueFrame(hud);
            DrawHudText(new Rect(hud.x + 12, hud.y + 7, hud.width - 24, 26), title, _bodyStyle);
            DrawHudText(new Rect(hud.x + 12, hud.y + 33, hud.width - 24, 25),
                T($"Score {_arcadeScore}  ·  Best {TownSaveVariables.GetInt(ArcadeScoreKey + ".best")}  ·  {_arcadeSpawned}/30", $"Pontos {_arcadeScore}  ·  Recorde {TownSaveVariables.GetInt(ArcadeScoreKey + ".best")}  ·  {_arcadeSpawned}/30"), _messageStyle);
            string attack = _attackAction?.GetBindingDisplayString() ?? "Attack";
            string cancel = _cancel?.GetBindingDisplayString() ?? "Q";
            string controls = ArcadeBatting ? T($"Up/down: move · {attack}: hit · {cancel}: stop", $"Cima/baixo: mover · {attack}: rebater · {cancel}: parar") :
                T($"Move · {attack}: attack · {_wait?.GetBindingDisplayString()}: jump", $"Mover · {attack}: atacar · {_wait?.GetBindingDisplayString()}: pular");
            DrawHudText(new Rect(hud.x + 12, hud.y + 63, hud.width - 24, 35), controls, _messageStyle);
            if (_arcadePhase == ArcadePhase.Countdown)
            {
                var countdown = new Rect((width - 160) / 2, height * .35f, 160, 56);
                DrawDialogueFrame(countdown);
                DrawHudText(new Rect(countdown.x + 10, countdown.y + 12, 140, 35), T("Ready, set, ", "Preparar, ") + Mathf.Max(1, Mathf.CeilToInt(3 - _arcadeClock)), _bodyStyle);
            }
            if (_arcadePhase == ArcadePhase.Results)
            {
                var box = new Rect((width - 380) / 2, (height - 230) / 2, 380, 230);
                DrawDialogueFrame(box);
                DrawHudText(new Rect(box.x + 16, box.y + 16, 348, 28), _arcadeNewRecord ? T("New record!", "Novo recorde!") : T("Results", "Resultado"), _bodyStyle);
                DrawHudText(new Rect(box.x + 16, box.y + 52, 348, 65), T($"Score: {_arcadeScore}\nMissed: {_arcadeMisses}", $"Pontos: {_arcadeScore}\nErros: {_arcadeMisses}"), _messageStyle);
                if (GUI.Button(new Rect(box.x + 16, box.y + 128, 348, 32), (_arcadeMenuChoice == 0 ? "› " : "") + T("Play again", "Jogar novamente"))) { _arcadeMenuChoice = 0; SelectArcadeResult(); }
                if (GUI.Button(new Rect(box.x + 16, box.y + 168, 348, 32), (_arcadeMenuChoice == 1 ? "› " : "") + T("Return", "Voltar"))) EndArcadeGame();
            }
            return true;
        }
    }

}
