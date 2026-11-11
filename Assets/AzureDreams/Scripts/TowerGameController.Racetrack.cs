using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string RaceLobby = "Interior_0a_racetrack_entrance";
        private const string RaceCourse = "Interior_37_racetrack_course";
        private const string RaceCourseMusicCue = "MonsterRaceCourse.mp3";
        private const string RaceTicketPair = "racetrack.ticket_pair";
        private const string RaceTicketCount = "racetrack.ticket_count";
        private const string RaceTicketOdds = "racetrack.ticket_odds";
        private const int RaceTicketPrice = 100;
        private const int RaceTicketLimit = 10;
        private enum RaceScreen { Closed, Tickets, Silver, Instructions, Loading, Countdown, Racing, Results }

        private static readonly string[] RaceNames = { "Running Boss", "Okuchi Chuck", "Nenkojo Relina", "Holy Night" };
        private static readonly (int A, int B)[] RacePairs = { (0, 1), (0, 2), (0, 3), (1, 2), (1, 3), (2, 3) };
        // Centerline reconstructed from the three loops of the captured course.
        private static readonly Vector2[] RaceRoute =
        {
            new(45, 26), new(71, 26), new(74, 24), new(76, 21), new(76, 18),
            new(73, 16), new(55, 16), new(52, 18), new(50, 22), new(50, 32),
            new(49, 35), new(46, 37), new(29, 37), new(26, 35), new(25, 32),
            new(25, 23), new(25, 19), new(23, 17), new(21, 16), new(18, 17),
            new(16, 20), new(16, 23), new(18, 26), new(45, 26)
        };
        private static readonly float[] RaceHurdles = { 20f, 62f, 110f, 145f };

        private sealed class NyuelRacer
        {
            public GameObject Mount;
            public MonsterTurnSpriteView Sprite;
            public float Distance, Lane, Speed, Pace, Jump, Sprint;
            public int Hurdle;
        }

        private readonly NyuelRacer[] _nyuelRacers = new NyuelRacer[4];
        private readonly int[] _raceOdds = new int[6];
        private readonly int[] _racePlaces = new int[4];
        private RaceScreen _raceScreen;
        private int _racePair, _raceTickets = 1, _raceChoice, _raceFinished, _racePrize;
        private float _raceStartTime, _raceClock, _raceThirdFinished, _raceLength;
        private float[] _raceDistances;
        private bool _raceSpectating;

        private bool InRaceLobby => _interior && _interior.interiorId == RaceLobby;
        private bool InRaceCourse => _interior && _interior.interiorId == RaceCourse;
        private int BoughtRaceTickets => TownSaveVariables.GetInt(RaceTicketCount);

        private void SetupRacetrack()
        {
            if (!InRaceLobby) return;
            _raceScreen = RaceScreen.Closed;
            _racePair = 0;
            _raceTickets = 1;
            for (int i = 0; i < _raceOdds.Length; i++) _raceOdds[i] = Random.Range(3, 10);

            foreach (var npc in _townPeople)
            {
                if (!npc) continue;
                bool silver = npc.npcId == "dar";
                bool clerk = npc.npcId == "fur";
                npc.npcId = silver ? "race_silver" : clerk ? "race_clerk" : "race_gambler";
                if (npc.TryGetComponent<CharacterStats>(out var stats))
                {
                    stats.displayName = silver ? "Silver" : clerk ? T("Ticket Clerk", "Bilheteira") : T("Gambler", "Apostador");
                    stats.portraitTexture = null;
                }
                npc.dialogueLines = new[]
                {
                    T("The problem with that last race was not with the monsters! It was the jockey that was shameful!",
                        "O problema da última corrida não foram os monstros! A culpa foi do jóquei!"),
                    T("Are you going to be in today's race? If you are, I'm gonna bet on you.",
                        "Vai correr hoje? Se for, vou apostar em você.")
                };
            }
        }

        private bool TryRacetrackInteraction()
        {
            if (!_player) return false;
            if (InRaceCourse && Vector2.Distance(new Vector2(_player.transform.position.x, _player.transform.position.z),
                    new Vector2(_interior.entryPosition.x, _interior.entryPosition.z)) < 1.15f)
            {
                StartCoroutine(LoadInterior(RaceLobby));
                return true;
            }
            if (!InRaceLobby) return false;

            NPCController nearest = null;
            float distance = 1.65f;
            foreach (var npc in _townPeople)
            {
                if (!npc || npc.npcId is not ("race_silver" or "race_clerk")) continue;
                Vector3 delta = npc.transform.position - _player.transform.position;
                delta.y = 0f;
                if (delta.magnitude >= distance) continue;
                nearest = npc;
                distance = delta.magnitude;
            }
            if (!nearest) return false;
            _raceChoice = 0;
            _message = string.Empty;
            _raceScreen = nearest.npcId == "race_silver" ? RaceScreen.Silver : RaceScreen.Tickets;
            PlayRetailSound(RetailSfx.Confirm);
            return true;
        }

        private bool HandleRacetrackInput()
        {
            if (_raceScreen == RaceScreen.Closed) return false;
            if (_raceScreen == RaceScreen.Loading) return true;
            if (_raceScreen == RaceScreen.Countdown)
            {
                if (Time.unscaledTime >= _raceStartTime)
                {
                    _raceScreen = RaceScreen.Racing;
                    PlayAreaMusic("MonsterRacing.mp3");
                    PlayRetailSound(RetailSfx.Confirm);
                }
                return true;
            }
            if (_raceScreen == RaceScreen.Racing)
            {
                TickNyuelRace(Mathf.Min(0.05f, Time.unscaledDeltaTime));
                return true;
            }
            if (_cancel?.WasPressedThisFrame() == true)
            {
                if (_raceScreen == RaceScreen.Results) ReturnFromRace();
                else if (_raceScreen == RaceScreen.Instructions) _raceScreen = RaceScreen.Silver;
                else _raceScreen = RaceScreen.Closed;
                return true;
            }

            var keys = Keyboard.current;
            if (keys != null)
            {
                int row = (keys.downArrowKey.wasPressedThisFrame ? 1 : 0) - (keys.upArrowKey.wasPressedThisFrame ? 1 : 0);
                if (_raceScreen == RaceScreen.Tickets && BoughtRaceTickets == 0)
                {
                    _racePair = (_racePair + row + RacePairs.Length) % RacePairs.Length;
                    int quantity = (keys.rightArrowKey.wasPressedThisFrame ? 1 : 0) - (keys.leftArrowKey.wasPressedThisFrame ? 1 : 0);
                    _raceTickets = Mathf.Clamp(_raceTickets + quantity, 1, RaceTicketLimit);
                }
                if (_raceScreen == RaceScreen.Silver) _raceChoice = (_raceChoice + row + 4) % 4;
            }
            if (_interact?.WasPressedThisFrame() == true) ConfirmRaceMenu();
            return true;
        }

        private void ConfirmRaceMenu()
        {
            switch (_raceScreen)
            {
                case RaceScreen.Tickets:
                    if (BoughtRaceTickets != 0) { _raceScreen = RaceScreen.Closed; return; }
                    int price = _raceTickets * RaceTicketPrice;
                    if (_gold < price) return;
                    _gold -= price;
                    TownSaveVariables.SetInt(RaceTicketPair, _racePair);
                    TownSaveVariables.SetInt(RaceTicketCount, _raceTickets);
                    TownSaveVariables.SetInt(RaceTicketOdds, _raceOdds[_racePair]);
                    PersistTownProgress();
                    PlayRetailSound(RetailSfx.Confirm);
                    break;
                case RaceScreen.Silver:
                    if (_raceChoice < 2) BeginNyuelRace(_raceChoice == 1);
                    else _raceScreen = _raceChoice == 2 ? RaceScreen.Instructions : RaceScreen.Closed;
                    break;
                case RaceScreen.Instructions: _raceScreen = RaceScreen.Silver; break;
                case RaceScreen.Results: ReturnFromRace(); break;
            }
        }

        private void BeginNyuelRace(bool spectating)
        {
            _raceSpectating = spectating;
            _raceScreen = RaceScreen.Loading;
            StartCoroutine(EnterNyuelRace());
        }

        private System.Collections.IEnumerator EnterNyuelRace()
        {
            yield return LoadInterior(RaceCourse);
            if (!InRaceCourse) { _raceScreen = RaceScreen.Silver; yield break; }

            _raceDistances = new float[RaceRoute.Length];
            for (int i = 1; i < RaceRoute.Length; i++)
                _raceDistances[i] = _raceDistances[i - 1] + Vector2.Distance(RaceRoute[i - 1], RaceRoute[i]);
            _raceLength = _raceDistances[_raceDistances.Length - 1];
            _raceFinished = 0;
            _racePrize = 0;
            _raceClock = 0f;
            _raceThirdFinished = -1f;

            int species = (int)MonsterSpecies.NYUEL;
            var nyuel = HatchedFamiliarPrefab(species);
            var jockeyPrefab = FindStoryResidentPrefab("monster_shop_hunter") ?? _npcPrefab;
            for (int i = 0; i < _nyuelRacers.Length; i++)
            {
                var mount = SpawnActor(nyuel, new Vector2Int(45, 26), RaceNames[i], species: species, groundedIdle: true);
                foreach (var collider in mount.GetComponentsInChildren<Collider>()) collider.enabled = false;
                _nyuelRacers[i] = new NyuelRacer
                {
                    Mount = mount,
                    Sprite = mount.GetComponentInChildren<MonsterTurnSpriteView>(),
                    Lane = (i - 1.5f) * 0.67f,
                    Pace = Random.Range(5.65f, 7.3f)
                };
                if ((i != 0 || _raceSpectating) && jockeyPrefab)
                {
                    var rider = Instantiate(jockeyPrefab, mount.transform);
                    rider.transform.localPosition = Vector3.up * 0.78f;
                    rider.transform.localScale *= 0.72f;
                    foreach (var collider in rider.GetComponentsInChildren<Collider>()) collider.enabled = false;
                }
                PlaceNyuelRacer(i, false);
            }

            foreach (float distance in RaceHurdles)
            {
                Vector2 position = RacePoint(distance, out Vector2 direction);
                Vector3 center = new(position.x, 0f, position.y);
                center.y = TownTerrainHeight(center) + 0.23f;
                var hurdle = CreatePrimitive(PrimitiveType.Cube, "Nyuel race hurdle", center,
                    new Vector3(2.75f, 0.24f, 0.13f), _accentMaterial, false);
                hurdle.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.y));
            }
            _player.SetActive(!_raceSpectating);
            UpdateCameraFollow(snap: true);
            _raceStartTime = Time.unscaledTime + 3f;
            _raceScreen = RaceScreen.Countdown;
        }

        private Vector2 RacePoint(float distance, out Vector2 direction)
        {
            int segment = 1;
            while (segment < _raceDistances.Length - 1 && distance > _raceDistances[segment]) segment++;
            Vector2 a = RaceRoute[segment - 1], b = RaceRoute[segment];
            direction = (b - a).normalized;
            return Vector2.Lerp(a, b, Mathf.InverseLerp(_raceDistances[segment - 1], _raceDistances[segment], distance));
        }

        private void PlaceNyuelRacer(int index, bool moving)
        {
            var racer = _nyuelRacers[index];
            Vector2 center = RacePoint(racer.Distance, out Vector2 direction) + new Vector2(-direction.y, direction.x) * racer.Lane;
            Vector3 position = new(center.x, 0f, center.y);
            position.y = TownTerrainHeight(position) + Mathf.Sin(Mathf.Clamp01(racer.Jump / 0.7f) * Mathf.PI) * 0.85f;
            racer.Mount.transform.position = position;
            if (racer.Sprite)
                racer.Sprite.PresentMovement(new Vector3(direction.x, 0f, direction.y), racer.Distance * 0.37f % 1f, moving);
            if (index != 0) return;
            _player.transform.position = position + Vector3.up * (0.75f + TownActorFootOffset);
            if (_kohSprite) _kohSprite.SetFacing(direction);
        }

        private void TickNyuelRace(float dt)
        {
            _raceClock += dt;
            Vector2 input = _move?.ReadValue<Vector2>() ?? Vector2.zero;
            bool accelerate = _runAction?.IsPressed() == true || input.y > 0.5f;
            bool pulse = _runAction?.WasPressedThisFrame() == true;
            bool jump = _wait?.WasPressedThisFrame() == true;
            for (int i = 0; i < _nyuelRacers.Length; i++)
            {
                var racer = _nyuelRacers[i];
                if (racer.Distance >= _raceLength) continue;
                racer.Jump = Mathf.Max(0f, racer.Jump - dt);
                if (i == 0 && !_raceSpectating)
                {
                    racer.Lane = Mathf.Clamp(racer.Lane + input.x * dt * 2.3f, -1.15f, 1.15f);
                    racer.Sprint = Mathf.Max(0f, racer.Sprint - dt * 3f);
                    if (pulse) racer.Sprint = Mathf.Min(3f, racer.Sprint + 1.15f);
                    float target = input.y < -0.5f ? -2.2f : accelerate || racer.Sprint > 0f ? 6.5f + racer.Sprint : 0f;
                    racer.Speed = Mathf.MoveTowards(racer.Speed, target, dt * 4.5f);
                    if (jump && racer.Jump == 0f) racer.Jump = 0.7f;
                }
                else
                {
                    racer.Speed = racer.Pace + Mathf.Sin(_raceClock * 0.8f + i * 3f) * 0.8f;
                    if (racer.Hurdle < RaceHurdles.Length && RaceHurdles[racer.Hurdle] - racer.Distance < 2f)
                        racer.Jump = 0.55f;
                }

                racer.Distance = Mathf.Clamp(racer.Distance + racer.Speed * dt, 0f, _raceLength);
                if (racer.Hurdle < RaceHurdles.Length && racer.Distance >= RaceHurdles[racer.Hurdle])
                {
                    racer.Hurdle++;
                    if (racer.Jump <= 0f) racer.Speed *= 0.28f;
                }
                PlaceNyuelRacer(i, racer.Distance < _raceLength && Mathf.Abs(racer.Speed) > 0.1f);
                if (racer.Distance >= _raceLength) _racePlaces[_raceFinished++] = i;
            }
            if (_raceFinished == 3 && _raceThirdFinished < 0f) _raceThirdFinished = _raceClock;
            if (_raceFinished == 4 || (_raceThirdFinished >= 0f && _raceClock - _raceThirdFinished > 10f))
                FinishNyuelRace();
        }

        private void FinishNyuelRace()
        {
            int tickets = BoughtRaceTickets;
            int pairIndex = TownSaveVariables.GetInt(RaceTicketPair, -1);
            if (tickets > 0 && pairIndex >= 0 && pairIndex < RacePairs.Length)
            {
                var pair = RacePairs[pairIndex];
                int a = _racePlaces[0], b = _racePlaces[1];
                if ((a == pair.A && b == pair.B) || (a == pair.B && b == pair.A))
                {
                    int prize = tickets * RaceTicketPrice * TownSaveVariables.GetInt(RaceTicketOdds);
                    _racePrize = Mathf.Min(prize, int.MaxValue - _gold);
                    _gold += _racePrize;
                }
            }
            if (tickets > 0) TownSaveVariables.SetInt(RaceTicketCount, 0);
            PersistTownProgress();
            _raceScreen = RaceScreen.Results;
            PlayAreaMusic(RaceCourseMusicCue);
        }

        private void ReturnFromRace()
        {
            _raceScreen = RaceScreen.Loading;
            StartCoroutine(ReturnToRaceLobby());
        }

        private System.Collections.IEnumerator ReturnToRaceLobby()
        {
            yield return LoadInterior(RaceLobby);
            _raceScreen = RaceScreen.Closed;
        }

        private bool DrawRacetrack(float width, float height)
        {
            if (_raceScreen == RaceScreen.Closed) return false;
            if (_raceScreen == RaceScreen.Loading) return true;
            if (_raceScreen is RaceScreen.Countdown or RaceScreen.Racing)
            {
                var hud = new Rect(18f, 18f, 320f, 70f);
                DrawDialogueFrame(hud);
                int position = 1;
                if (_nyuelRacers[0].Distance >= _raceLength)
                    position = System.Array.IndexOf(_racePlaces, 0, 0, _raceFinished) + 1;
                else
                    for (int i = 1; i < _nyuelRacers.Length; i++)
                        if (_nyuelRacers[i].Distance > _nyuelRacers[0].Distance) position++;
                string phase = _raceScreen == RaceScreen.Countdown
                    ? T($"Ready… {Mathf.Max(1, Mathf.CeilToInt(_raceStartTime - Time.unscaledTime))}", $"Preparar… {Mathf.Max(1, Mathf.CeilToInt(_raceStartTime - Time.unscaledTime))}")
                    : _raceSpectating ? T("Watching the race", "Assistindo à corrida")
                    : T($"Jockey 1 · Place {position}/4", $"Jóquei 1 · Posição {position}/4");
                DrawHudText(new Rect(hud.x + 10f, hud.y + 5f, hud.width - 20f, 25f), phase, _bodyStyle);
                DrawHudText(new Rect(hud.x + 10f, hud.y + 32f, hud.width - 20f, 30f),
                    $"{_raceClock:0.0}s   ·   {_nyuelRacers[0].Distance / _raceLength:P0}", _messageStyle);
                return true;
            }

            float w = Mathf.Min(width - 24f, 480f);
            var box = new Rect((width - w) * 0.5f, (height - 330f) * 0.5f, w, 330f);
            DrawDialogueFrame(box);
            GUILayout.BeginArea(new Rect(box.x + 15f, box.y + 12f, box.width - 30f, box.height - 24f));
            switch (_raceScreen)
            {
                case RaceScreen.Tickets: DrawRaceTickets(); break;
                case RaceScreen.Silver:
                    GUILayout.Label("Silver", _bodyStyle);
                    GUILayout.Label(T($"Hi, {HeroName}. Isn't it a beautiful Monster racetrack? Thank you so much. Do you want to ride today?",
                        $"Olá, {HeroName}. A pista de monstros ficou linda, não acha? Muito obrigado. Quer montar hoje?"), _messageStyle);
                    DrawRaceOption(0, T("I'm going to race.", "Vou correr."));
                    DrawRaceOption(1, T("I'll just watch.", "Só vou assistir."));
                    DrawRaceOption(2, T("Show me how to ride.", "Ensine-me a montar."));
                    DrawRaceOption(3, T("Not today.", "Hoje não."));
                    break;
                case RaceScreen.Instructions:
                    GUILayout.Label(T("Riding a Nyuel", "Como montar um Nyuel"), _bodyStyle);
                    string run = _runAction?.GetBindingDisplayString() ?? "Shift";
                    string hop = _wait?.GetBindingDisplayString() ?? "Space";
                    GUILayout.Label(T($"Hold {run} to trot and tap it repeatedly to speed up. Move left or right to change lanes, down to back up, and press {hop} to jump the hurdles.",
                        $"Segure {run} para trotar e aperte várias vezes para acelerar. Use esquerda ou direita para mudar de faixa, para baixo para recuar e {hop} para pular os obstáculos."), _messageStyle);
                    if (GUILayout.Button(WithInteractHint("Back", "Voltar"))) ConfirmRaceMenu();
                    break;
                case RaceScreen.Results:
                    GUILayout.Label(T("Race results", "Resultado da corrida"), _bodyStyle);
                    for (int place = 0; place < _raceFinished; place++)
                    {
                        int rider = _racePlaces[place];
                        GUILayout.Label($"{place + 1}.  #{rider + 1}  {RaceNames[rider]}", _messageStyle);
                    }
                    GUILayout.Label(_racePrize > 0 ? T($"Winning ticket: +{_racePrize} G", $"Bilhete vencedor: +{_racePrize} G") :
                        T("No prize.", "Sem prêmio."), _messageStyle);
                    if (GUILayout.Button(WithInteractHint("Return to the lobby", "Voltar à bilheteria"))) ReturnFromRace();
                    break;
            }
            GUILayout.EndArea();
            return true;
        }

        private void DrawRaceOption(int index, string text)
        {
            if (!GUILayout.Button((_raceChoice == index ? "▸ " : "") + text)) return;
            _raceChoice = index;
            ConfirmRaceMenu();
        }

        private void DrawRaceTickets()
        {
            GUILayout.Label(T("Betting tickets", "Bilhetes de aposta"), _bodyStyle);
            GUILayout.Label(T($"{HeroName}, you're jockey number one. A ticket costs 100 G.", $"{HeroName}, você é o jóquei número um. Cada bilhete custa 100 G."), _messageStyle);
            if (BoughtRaceTickets > 0)
            {
                var bought = RacePairs[Mathf.Clamp(TownSaveVariables.GetInt(RaceTicketPair), 0, RacePairs.Length - 1)];
                GUILayout.Label(T($"{BoughtRaceTickets} ticket(s) on {bought.A + 1}–{bought.B + 1}. Good luck with the race itself too.",
                    $"{BoughtRaceTickets} bilhete(s) na dupla {bought.A + 1}–{bought.B + 1}. Boa sorte na corrida também."), _messageStyle);
                if (GUILayout.Button(WithInteractHint("Back", "Voltar"))) ConfirmRaceMenu();
                return;
            }
            for (int i = 0; i < RacePairs.Length; i++)
            {
                var pair = RacePairs[i];
                if (GUILayout.Button($"{(_racePair == i ? "▸ " : "")}{pair.A + 1}–{pair.B + 1}    ×{_raceOdds[i]}"))
                    _racePair = i;
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("−")) _raceTickets = Mathf.Max(1, _raceTickets - 1);
            GUILayout.Label(T($"{_raceTickets} tickets · {_raceTickets * RaceTicketPrice} G", $"{_raceTickets} bilhetes · {_raceTickets * RaceTicketPrice} G"), _messageStyle);
            if (GUILayout.Button("+")) _raceTickets = Mathf.Min(RaceTicketLimit, _raceTickets + 1);
            GUILayout.EndHorizontal();
            if (_gold >= _raceTickets * RaceTicketPrice)
            {
                if (GUILayout.Button(WithInteractHint("Buy tickets", "Comprar bilhetes"))) ConfirmRaceMenu();
            }
            else GUILayout.Label(T("Not enough gold.", "Ouro insuficiente."), _messageStyle);
            if (GUILayout.Button(T("Leave", "Sair"))) _raceScreen = RaceScreen.Closed;
        }
    }
}
