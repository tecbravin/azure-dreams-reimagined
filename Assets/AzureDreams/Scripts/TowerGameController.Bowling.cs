using UnityEngine;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string BowlingInterior = "Interior_0c_alley";
        private const int BowlingPinCount = 8;
        private const float BowlingStartZ = 17.6f;
        private const float BowlingLaneEdge = 1.58f;
        private const float BowlingPhysicsStep = 1f / 60f;
        private static readonly float[] BowlingLaneCenters = { 16f, 23f };
        // Eight challenges, each with one roll and a different eight-pin arrangement.
        private static readonly string[][] BowlingPatterns =
        {
            new[] { "..X..", ".XXX.", ".XXXX" },
            new[] { "..X..", ".XXX.", "XXXX." },
            new[] { ".XX..", "..XX.", "...XX", "..XX." },
            new[] { "..XX.", "..XX.", ".XX..", ".XX.." },
            new[] { "..XX.", ".XX..", ".XX..", "..XX." },
            new[] { "..X..", ".XXX.", "..XX.", "..XX." },
            new[] { "..X..", "..XXX", "..XX.", ".XX.." },
            new[] { ".XX..", ".XXX.", "..XXX" },
        };

        private enum BowlingPhase { Closed, Menu, Rules, Pickup, Position, Aim, Spin, Rolling, Score, Results }
        private sealed class BowlingPin
        {
            public GameObject View;
            public Vector2 Position, Velocity;
            public float Fall;
            public bool Down;
        }

        private sealed class BowlingLane
        {
            public GameObject Ball;
            public BowlingPin[] Pins;
        }

        private readonly BowlingLane[] _bowlingLanes = new BowlingLane[2];
        private BowlingPhase _bowlingPhase;
        private int _bowlingLane, _bowlingMenuChoice, _bowlingFrame, _bowlingScore, _bowlingKnocked;
        private Vector2 _bowlingBallPosition, _bowlingBallVelocity, _bowlingFacing;
        private Vector3 _bowlingReturnPosition;
        private float _bowlingPosition, _bowlingAim, _bowlingSpin, _bowlingPhaseStarted, _bowlingSettleRemaining, _bowlingAccumulator;
        private bool _bowlingPositioned, _bowlingBallStopped;
        private GameObject _bowlingGuide;
        private MaterialPropertyBlock _bowlingTint;

        private bool InBowlingAlley => _interior && _interior.interiorId == BowlingInterior;
        private BowlingLane ActiveBowlingLane => _bowlingLanes[_bowlingLane];
        private float BowlingCenter => BowlingLaneCenters[_bowlingLane];
        private bool HoldingBowlingBall => _bowlingPhase is BowlingPhase.Position or BowlingPhase.Aim or BowlingPhase.Spin;

        private void ResetBowling()
        {
            _bowlingPhase = BowlingPhase.Closed;
            _bowlingPositioned = false;
            _bowlingGuide = null;
            for (int i = 0; i < _bowlingLanes.Length; i++) _bowlingLanes[i] = null;
        }

        private void SetupBowling()
        {
            if (!InBowlingAlley) return;
            foreach (var npc in _townPeople)
            {
                if (!npc) continue;
                bool attendant = npc.npcId == "fur";
                bool gambler = npc.npcId == "monster_shop_hunter";
                if (attendant || gambler)
                {
                    npc.npcId = attendant ? "bowling_attendant" : "bowling_gambler";
                    if (npc.TryGetComponent<CharacterStats>(out var stats))
                    {
                        stats.displayName = attendant ? T("Attendant", "Atendente") : T("Gambler", "Apostador");
                        stats.portraitTexture = null;
                    }
                }
                npc.dialogueLines = attendant ? new[]
                {
                    T("Welcome to the bowling center. Do you want to try Bowling?",
                        "Bem-vindo ao salão de boliche. Quer jogar?")
                } : gambler ? new[]
                {
                    T("I've done this game enough to know it inside out.", "Já joguei tanto que conheço o jogo de trás para frente."),
                    T("I'm pretty good when it comes to placing bets.", "Sou muito bom quando o assunto é apostar.")
                } : new[]
                {
                    T($"{HeroName}, this bowling center is really fun, isn't it?", $"{HeroName}, este boliche é bem divertido, não acha?"),
                    npc.npcId == "hush"
                        ? T($"{HeroName}, I've got no idea that Bet is secretly training, get the hint?",
                            $"{HeroName}, eu não sei de nada sobre a Bet estar treinando escondida, entendeu?")
                        : T("Those two seem to really enjoy this bowling center.", "Aqueles dois parecem gostar muito deste boliche.")
                };
            }

            for (int i = 0; i < _bowlingLanes.Length; i++)
            {
                float center = BowlingLaneCenters[i];
                var lane = new BowlingLane { Pins = new BowlingPin[BowlingPinCount] };
                var launch = new Vector2(center, BowlingStartZ);
                Vector3 ballOrigin = BowlingWorld(launch, 0.25f);
                lane.Ball = new GameObject("Living bowling ball");
                lane.Ball.transform.position = ballOrigin;
                var body = BowlingPart(PrimitiveType.Sphere, "Ball body", ballOrigin, Vector3.one * 0.48f, new Color(0.23f, 0.42f, 0.3f));
                body.transform.SetParent(lane.Ball.transform, true);
                for (int eye = -1; eye <= 1; eye += 2)
                {
                    var view = BowlingPart(PrimitiveType.Sphere, "Ball eye",
                        ballOrigin + new Vector3(eye * 0.08f, 0.04f, 0.20f),
                        new Vector3(0.065f, 0.085f, 0.045f), new Color(0.94f, 0.86f, 0.55f));
                    view.transform.SetParent(lane.Ball.transform, true);
                }
                lane.Ball.transform.SetParent(_interior.transform, true);

                for (int pin = 0; pin < lane.Pins.Length; pin++)
                {
                    Vector3 origin = BowlingWorld(new Vector2(center, 10.5f), 0.02f);
                    var pinRoot = new GameObject("Mushrom pin " + (pin + 1));
                    pinRoot.transform.SetParent(_interior.transform, false);
                    pinRoot.transform.position = origin;
                    var stem = BowlingPart(PrimitiveType.Capsule, "Mushrom stem", origin + Vector3.up * 0.20f,
                        new Vector3(0.23f, 0.22f, 0.23f), new Color(0.83f, 0.75f, 0.52f));
                    stem.transform.SetParent(pinRoot.transform, true);
                    var cap = BowlingPart(PrimitiveType.Sphere, "Mushrom cap", origin + Vector3.up * 0.43f,
                        new Vector3(0.36f, 0.23f, 0.36f), new Color(0.60f, 0.37f, 0.22f));
                    cap.transform.SetParent(pinRoot.transform, true);
                    for (int eye = -1; eye <= 1; eye += 2)
                    {
                        var glare = BowlingPart(PrimitiveType.Sphere, "Mushrom eye",
                            origin + new Vector3(eye * 0.06f, 0.28f, 0.108f),
                            new Vector3(0.048f, 0.063f, 0.04f), new Color(0.25f, 0.17f, 0.10f));
                        glare.transform.SetParent(pinRoot.transform, true);
                    }
                    lane.Pins[pin] = new BowlingPin { View = pinRoot };
                }
                _bowlingLanes[i] = lane;
                PlaceBowlingPins(i, 0);
            }

            _bowlingGuide = BowlingPart(PrimitiveType.Cube, "Bowling aim line", Vector3.zero, Vector3.one,
                new Color(0.07f, 0.045f, 0.025f));
            _bowlingGuide.SetActive(false);
        }

        private GameObject BowlingPart(PrimitiveType type, string label, Vector3 point, Vector3 size, Color color)
        {
            var obj = CreatePrimitive(type, label, point, size, null, false);
            _bowlingTint ??= new MaterialPropertyBlock();
            _bowlingTint.SetColor("_BaseColor", color);
            _bowlingTint.SetColor("_Color", color);
            obj.GetComponent<Renderer>().SetPropertyBlock(_bowlingTint);
            return obj;
        }

        private Vector3 BowlingWorld(Vector2 point, float lift)
        {
            Vector3 world = new(point.x, 0f, point.y);
            world.y = TownTerrainHeight(world) + lift;
            return world;
        }

        private void PlaceBowlingPins(int laneIndex, int frame)
        {
            var lane = _bowlingLanes[laneIndex];
            var pattern = BowlingPatterns[frame];
            int index = 0;
            for (int row = 0; row < pattern.Length; row++)
                for (int x = 0; x < pattern[row].Length; x++)
                {
                    if (pattern[row][x] != 'X') continue;
                    var pin = lane.Pins[index++];
                    pin.Position = new Vector2(BowlingLaneCenters[laneIndex] + (x - 2) * 0.46f, 11.55f - row * 0.46f);
                    pin.Velocity = Vector2.zero;
                    pin.Fall = 0f;
                    pin.Down = false;
                    pin.View.transform.SetPositionAndRotation(BowlingWorld(pin.Position, 0.02f), Quaternion.identity);
                }
        }

        private bool TryBowlingInteraction()
        {
            if (!InBowlingAlley || !_player) return false;
            var p = _player.transform.position;
            int lane = Mathf.Abs(p.x - BowlingLaneCenters[0]) <= Mathf.Abs(p.x - BowlingLaneCenters[1]) ? 0 : 1;
            bool atLane = Mathf.Abs(p.x - BowlingLaneCenters[lane]) < 1.25f && Mathf.Abs(p.z - BowlingStartZ) < 1.35f;
            var attendant = _townPeople.Find(person => person && person.npcId == "bowling_attendant");
            Vector3 toClerk = attendant ? attendant.transform.position - p : Vector3.positiveInfinity;
            bool atCounter = toClerk.x * toClerk.x + toClerk.z * toClerk.z < 2.4f;
            if (!atLane && !atCounter) return false;
            _bowlingLane = lane;
            _bowlingMenuChoice = 0;
            SetBowlingPhase(BowlingPhase.Menu);
            _message = string.Empty;
            PlayRetailSound(RetailSfx.Confirm);
            return true;
        }

        private void SetBowlingPhase(BowlingPhase phase)
        {
            _bowlingPhase = phase;
            _bowlingPhaseStarted = Time.unscaledTime;
        }

        private void BeginBowling()
        {
            _bowlingReturnPosition = _player.transform.position;
            _bowlingFacing = _townFacing;
            _bowlingPositioned = true;
            _bowlingFrame = 0;
            _bowlingScore = 0;
            SetupBowlingFrame();
        }

        private void SetupBowlingFrame()
        {
            _bowlingPosition = 0f;
            _bowlingAim = 0f;
            _bowlingSpin = 0f;
            _bowlingKnocked = 0;
            PlaceBowlingPins(_bowlingLane, _bowlingFrame);
            SetBowlingPhase(BowlingPhase.Pickup);
            PresentBowlingPosition();
            UpdateCameraFollow(snap: true);
        }

        private void EndBowling()
        {
            if (_bowlingPositioned)
            {
                if (_bowlingGuide) _bowlingGuide.SetActive(false);
                PlaceBowlingPins(_bowlingLane, 0);
                ActiveBowlingLane.Ball.transform.SetPositionAndRotation(
                    BowlingWorld(new Vector2(BowlingCenter, BowlingStartZ), 0.25f), Quaternion.identity);
                _player.transform.position = _bowlingReturnPosition;
                _playerCell = new Vector2Int(Mathf.RoundToInt(_bowlingReturnPosition.x), Mathf.RoundToInt(_bowlingReturnPosition.z));
                _townFacing = _bowlingFacing;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
                _bowlingPositioned = false;
            }
            SetBowlingPhase(BowlingPhase.Closed);
            UpdateCameraFollow(snap: true);
        }

        private bool HandleBowlingInput()
        {
            if (_bowlingPhase == BowlingPhase.Closed) return false;
            if (!InBowlingAlley) { ResetBowling(); return false; }
            if (_bowlingPhase == BowlingPhase.Rolling)
            {
                _bowlingAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.10f);
                while (_bowlingAccumulator >= BowlingPhysicsStep && _bowlingPhase == BowlingPhase.Rolling)
                {
                    _bowlingAccumulator -= BowlingPhysicsStep;
                    TickBowlingBall(BowlingPhysicsStep);
                }
                return true;
            }
            if (_cancel?.WasPressedThisFrame() == true)
            {
                if (_bowlingPhase == BowlingPhase.Rules) SetBowlingPhase(BowlingPhase.Menu);
                else EndBowling();
                PlayRetailSound(RetailSfx.Cancel);
                return true;
            }

            if (_bowlingPhase == BowlingPhase.Menu && Keyboard.current is { } keys)
            {
                if (keys.downArrowKey.wasPressedThisFrame) _bowlingMenuChoice = (_bowlingMenuChoice + 1) % 3;
                if (keys.upArrowKey.wasPressedThisFrame) _bowlingMenuChoice = (_bowlingMenuChoice + 2) % 3;
            }
            if (_bowlingPhase == BowlingPhase.Position)
            {
                float direction = _move?.ReadValue<Vector2>().x ?? 0f;
                float screenRight = _gameCamera && _gameCamera.transform.right.x < 0f ? -1f : 1f;
                _bowlingPosition = Mathf.Clamp(_bowlingPosition + direction * screenRight * Time.unscaledDeltaTime * 1.9f, -1.32f, 1.32f);
                PresentBowlingPosition();
            }
            else if (_bowlingPhase == BowlingPhase.Aim)
            {
                _bowlingAim = Mathf.Sin((Time.unscaledTime - _bowlingPhaseStarted) * 2.2f) * 0.32f;
                PresentBowlingAim();
            }
            else if (_bowlingPhase == BowlingPhase.Spin)
                _bowlingSpin = Mathf.Sin((Time.unscaledTime - _bowlingPhaseStarted) * 2.65f);

            if (_interact?.WasPressedThisFrame() == true) AdvanceBowling();
            return true;
        }

        private void AdvanceBowling()
        {
            PlayRetailSound(RetailSfx.Confirm);
            switch (_bowlingPhase)
            {
                case BowlingPhase.Menu:
                    if (_bowlingMenuChoice == 0) BeginBowling();
                    else if (_bowlingMenuChoice == 1) SetBowlingPhase(BowlingPhase.Rules);
                    else EndBowling();
                    break;
                case BowlingPhase.Rules: SetBowlingPhase(BowlingPhase.Menu); break;
                case BowlingPhase.Pickup: SetBowlingPhase(BowlingPhase.Position); PresentBowlingPosition(); break;
                case BowlingPhase.Position: SetBowlingPhase(BowlingPhase.Aim); PresentBowlingAim(); break;
                case BowlingPhase.Aim: SetBowlingPhase(BowlingPhase.Spin); break;
                case BowlingPhase.Spin: LaunchBowlingBall(); break;
                case BowlingPhase.Score:
                    if (++_bowlingFrame < BowlingPatterns.Length) SetupBowlingFrame();
                    else ShowBowlingResults();
                    break;
                case BowlingPhase.Results: EndBowling(); break;
            }
        }

        private void ShowBowlingResults()
        {
            var gambler = _townPeople.Find(person => person && person.npcId == "bowling_gambler");
            if (gambler)
            {
                string reaction = _bowlingScore == BowlingPinCount * BowlingPatterns.Length
                    ? T("Wow, you're really something. You're not some famous gamer by chance, are you?",
                        "Uau, você joga muito. Por acaso você é algum jogador famoso?")
                    : _bowlingScore >= 32
                        ? T($"{_bowlingScore} pins! Not bad at all.", $"{_bowlingScore} pinos! Nada mal.")
                        : T("Well, good try. But you've got promise.", "Boa tentativa. Você tem potencial.");
                gambler.dialogueLines = new[] { reaction };
            }
            SetBowlingPhase(BowlingPhase.Results);
        }

        private void PresentBowlingPosition()
        {
            var point = new Vector2(BowlingCenter + _bowlingPosition, BowlingStartZ);
            _player.transform.position = BowlingWorld(point + Vector2.up * 0.58f, TownActorFootOffset);
            _townFacing = Vector2.down;
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            ActiveBowlingLane.Ball.transform.position = BowlingWorld(point, _bowlingPhase == BowlingPhase.Pickup ? 0.25f : 0.66f);
        }

        private void PresentBowlingAim()
        {
            Vector3 direction = new(Mathf.Sin(_bowlingAim), 0f, -Mathf.Cos(_bowlingAim));
            var origin = BowlingWorld(new Vector2(BowlingCenter + _bowlingPosition, BowlingStartZ), 0.055f);
            _bowlingGuide.transform.SetPositionAndRotation(origin + direction * 1.55f, Quaternion.LookRotation(direction));
            _bowlingGuide.transform.localScale = new Vector3(0.043f, 0.022f, 3.1f);
            _bowlingGuide.SetActive(true);
        }

        private void LaunchBowlingBall()
        {
            _bowlingBallPosition = new Vector2(BowlingCenter + _bowlingPosition, BowlingStartZ);
            _bowlingBallVelocity = new Vector2(Mathf.Sin(_bowlingAim), -Mathf.Cos(_bowlingAim)) * 6.75f;
            _bowlingBallStopped = false;
            _bowlingAccumulator = 0f;
            _bowlingGuide.SetActive(false);
            SetBowlingPhase(BowlingPhase.Rolling);
        }

        private void TickBowlingBall(float dt)
        {
            var lane = ActiveBowlingLane;
            if (!_bowlingBallStopped)
            {
                _bowlingBallVelocity.x += _bowlingSpin * 1.65f * dt;
                _bowlingBallPosition += _bowlingBallVelocity * dt;
                if (Mathf.Abs(_bowlingBallPosition.x - BowlingCenter) <= BowlingLaneEdge)
                    foreach (var pin in lane.Pins)
                    {
                        Vector2 offset = pin.Position - _bowlingBallPosition;
                        if (pin.Down || offset.sqrMagnitude > 0.37f * 0.37f) continue;
                        KnockBowlingPin(pin, _bowlingBallVelocity * 0.50f + offset.normalized * 1.4f);
                        _bowlingBallVelocity *= 0.91f;
                    }

                var ball = lane.Ball.transform;
                bool gutter = Mathf.Abs(_bowlingBallPosition.x - BowlingCenter) > BowlingLaneEdge;
                ball.position = BowlingWorld(_bowlingBallPosition, gutter ? 0.11f : 0.25f);
                ball.Rotate(Vector3.right, _bowlingBallVelocity.magnitude * dt * 230f, Space.World);
                if (_bowlingBallPosition.y < 8.7f || gutter)
                {
                    _bowlingBallStopped = true;
                    _bowlingSettleRemaining = 0.95f;
                }
            }

            foreach (var pin in lane.Pins)
            {
                if (!pin.Down) continue;
                pin.Fall = Mathf.Min(1f, pin.Fall + dt * 4.8f);
                pin.Position += pin.Velocity * dt;
                if (pin.Velocity.sqrMagnitude > 0.30f)
                {
                    foreach (var other in lane.Pins)
                    {
                        Vector2 offset = other.Position - pin.Position;
                        if (other.Down || offset.sqrMagnitude >= 0.42f * 0.42f) continue;
                        KnockBowlingPin(other, pin.Velocity * 0.64f + offset.normalized * 1.15f);
                        pin.Velocity *= 0.78f;
                    }
                }

                var axis = new Vector3(pin.Velocity.y, 0f, -pin.Velocity.x);
                pin.View.transform.position = BowlingWorld(pin.Position, 0.025f);
                if (axis.sqrMagnitude > 0.0001f)
                    pin.View.transform.rotation = Quaternion.AngleAxis(pin.Fall * 89f, axis.normalized);
                pin.Velocity *= Mathf.Exp(-2.1f * dt);
            }

            if (_bowlingBallStopped && (_bowlingSettleRemaining -= dt) <= 0f)
            {
                _bowlingScore += _bowlingKnocked;
                if (_bowlingKnocked == BowlingPinCount) PlayRetailSound(RetailSfx.Confirm);
                SetBowlingPhase(BowlingPhase.Score);
            }
        }

        private void KnockBowlingPin(BowlingPin pin, Vector2 velocity)
        {
            if (_bowlingKnocked++ == 0) PlayRetailSound(RetailSfx.Hit);
            pin.Down = true;
            pin.Velocity = velocity;
        }

        private Vector3 FrameBowlingCamera(Vector3 original)
        {
            if (_bowlingPhase is not (BowlingPhase.Rolling or BowlingPhase.Score or BowlingPhase.Results) ||
                ActiveBowlingLane?.Ball == null) return original;
            return ActiveBowlingLane.Ball.transform.position - Vector3.forward * 0.5f;
        }

        private bool DrawBowling(float width, float height)
        {
            if (_bowlingPhase == BowlingPhase.Closed) return false;
            if (_bowlingPhase is BowlingPhase.Menu or BowlingPhase.Rules)
            {
                var panel = new Rect(width * 0.5f - 205f, height * 0.5f - 93f, 410f, 186f);
                DrawDialogueFrame(panel);
                GUILayout.BeginArea(new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, panel.height - 20f));
                GUILayout.Label(T("Bowling Center", "Salão de boliche"), _bodyStyle);
                if (_bowlingPhase == BowlingPhase.Menu)
                {
                    GUILayout.Label(T("Welcome to the bowling center. Do you want to try Bowling?",
                        "Bem-vindo ao salão de boliche. Quer jogar?"), _messageStyle);
                    DrawBowlingChoice(0, T("Yes, let me try.", "Sim, quero jogar."));
                    DrawBowlingChoice(1, T("Explain the game.", "Explique as regras."));
                    DrawBowlingChoice(2, T("No, not today.", "Não, hoje não."));
                }
                else
                {
                    GUILayout.Label(T("Pick up the living ball. Move left or right to choose where to bowl. Confirm when the black line points where you want the ball to go, then confirm again to set the spin. Each of the eight layouts gives you one roll.",
                        "Pegue a bola viva. Mova-se para a esquerda ou direita para escolher de onde jogar. Confirme quando a linha preta apontar para onde quer lançar. Confirme de novo para definir o efeito. Você tem um lançamento em cada uma das oito formações."), _messageStyle);
                    if (GUILayout.Button(WithInteractHint("Back", "Voltar"))) AdvanceBowling();
                }
                GUILayout.EndArea();
                return true;
            }

            var hud = new Rect(16f, 18f, 350f, 120f);
            DrawDialogueFrame(hud);
            DrawHudText(new Rect(hud.x + 11f, hud.y + 5f, hud.width - 22f, 23f),
                T($"Frame {Mathf.Min(_bowlingFrame + 1, 8)}/8   ·   Pins: {_bowlingScore}/64",
                    $"Rodada {Mathf.Min(_bowlingFrame + 1, 8)}/8   ·   Pinos: {_bowlingScore}/64"), _bodyStyle);
            string prompt = _bowlingPhase switch
            {
                BowlingPhase.Pickup => T("Pick up the ball", "Pegar a bola"),
                BowlingPhase.Position => T("Move left or right and confirm your position", "Mova-se para os lados e confirme a posição"),
                BowlingPhase.Aim => T("Lock the aim", "Travar a direção"),
                BowlingPhase.Spin => T("Set the spin and bowl", "Definir o efeito e lançar"),
                BowlingPhase.Rolling => T("Ball in play…", "Bola em jogo…"),
                BowlingPhase.Score when _bowlingKnocked == BowlingPinCount => T("Strike! Next frame", "Strike! Próxima rodada"),
                BowlingPhase.Score => T($"{_bowlingKnocked} pins. Continue", $"{_bowlingKnocked} pinos. Continuar"),
                _ => T("Finish game", "Encerrar partida")
            };
            if (_bowlingPhase == BowlingPhase.Spin)
            {
                var bar = new Rect(hud.x + 13f, hud.y + 34f, hud.width - 26f, 10f);
                DrawHudFill(bar, new Color(0.17f, 0.24f, 0.35f));
                DrawHudFill(new Rect(bar.center.x + _bowlingSpin * (bar.width * 0.5f - 5f) - 3f, bar.y - 2f, 6f, 14f),
                    new Color(0.98f, 0.76f, 0.31f));
            }
            if (_bowlingPhase == BowlingPhase.Rolling)
                DrawHudText(new Rect(hud.x + 11f, hud.y + 54f, hud.width - 22f, 26f), prompt, _messageStyle);
            else if (GUI.Button(new Rect(hud.x + 11f, hud.y + 55f, hud.width - 22f, 27f), $"{prompt} [{InteractKey}]"))
                AdvanceBowling();
            return true;
        }

        private void DrawBowlingChoice(int index, string label)
        {
            if (!GUILayout.Button((_bowlingMenuChoice == index ? "▸ " : "") + label)) return;
            _bowlingMenuChoice = index;
            AdvanceBowling();
        }
    }
}
