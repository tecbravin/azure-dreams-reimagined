using UnityEngine;
using UnityEngine.InputSystem;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private const string CasinoSlotsRoom = "Interior_06_casino_2";
        private const int CasinoCoin = 100;
        private enum CasinoRound { Closed, Bet, Spinning, Result }
        private CasinoRound _casinoRound;
        private int _casinoCoins;
        private int _casinoStopped;
        private int _casinoPocket;
        private int _casinoWagerPocket;
        private float _casinoSpinStarted;
        private readonly int[] _casinoReels = new int[3];
        private const int CasinoPocketCount = 6;
        private static readonly string[] CasinoSymbols = { "7", "G", "◆", "●", "★", "✦" };
        private static readonly int[] CasinoMultipliers = { 100, 20, 12, 8, 5, 3 };
        private static readonly Vector2[] SlotMachines = { new(10.5f, 11.5f), new(15.5f, 11.5f), new(21.5f, 11.5f) };
        private static readonly Vector2 RouletteSwitch = new(16.5f, 13.5f);
        private static readonly Vector2 CasinoPassage = new(21f, 10f);

        private bool AtCasinoSlots => _interior && _interior.interiorId == CasinoSlotsRoom;
        private bool AtCasinoRoulette => _interior && _interior.interiorId == CasinoRoom;
        private string CasinoPrizeKey => AtCasinoSlots ? "casino.slots.prize" : "casino.roulette.prize";
        private int CasinoPrize => TownSaveVariables.GetInt(CasinoPrizeKey);
        private Vector2 CasinoPlayerPoint => new(_player.transform.position.x, _player.transform.position.z);

        private void SetupCasino()
        {
            _casinoRound = CasinoRound.Closed;
            if (!AtCasinoRoulette && !AtCasinoSlots) return;
            if (AtCasinoSlots)
            {
                foreach (var point in SlotMachines)
                {
                    // Leave the carpet and surrounding retail mesh intact.
                    var basePoint = new Vector3(point.x, 0f, point.y);
                    CreatePrimitive(PrimitiveType.Cube, "Slot cabinet", basePoint + Vector3.up * 0.62f,
                        new Vector3(1.45f, 1.24f, 0.66f), _wallMaterial, true);
                    CreatePrimitive(PrimitiveType.Cube, "Slot control panel", basePoint + new Vector3(0f, 0.69f, 0.35f),
                        new Vector3(1.45f, 0.17f, 0.37f), _accentMaterial, false);
                    for (int reel = 0; reel < 3; reel++)
                        CreatePrimitive(PrimitiveType.Cube, "Slot reel window", basePoint + new Vector3((reel - 1) * 0.36f, 0.95f, 0.34f),
                            new Vector3(0.31f, 0.30f, 0.04f), _groundMaterial, false);
                    _townBuildings.Add(new Rect(point.x - 0.72f, point.y - 0.33f, 1.44f, 0.66f));
                }
            }
            else
            {
                // The roulette wheel is part of the captured room geometry.
                CreatePrimitive(PrimitiveType.Cylinder, "Roulette switch", new Vector3(RouletteSwitch.x, 0.045f, RouletteSwitch.y),
                    new Vector3(0.28f, 0.045f, 0.28f), _accentMaterial, false);
                CreatePrimitive(PrimitiveType.Cylinder, "Casino passage", new Vector3(CasinoPassage.x, 0.045f, CasinoPassage.y),
                    new Vector3(0.58f, 0.045f, 0.58f), _accentMaterial, false);
            }

            foreach (var person in _townPeople)
            {
                if (!person || person.npcId == "vivian") continue;
                bool attendant = person.npcId == "fur";
                if (person.TryGetComponent<CharacterStats>(out var stats))
                    stats.displayName = attendant ? T("Attendant", "Atendente") : T("Gambler", "Apostador");
                person.npcId = attendant ? "casino_attendant" : "casino_gambler";
                person.dialogueLines = attendant
                    ? new[]
                    {
                        AtCasinoSlots
                            ? T("Welcome, Mr. Owner. Won't you play a little now you're here?", "Bem-vindo, senhor proprietário. Que tal jogar um pouco?")
                            : T("Welcome to the casino.", "Bem-vindo ao cassino."),
                        AtCasinoSlots
                            ? T("Adjust your bet with up and down. Stop each reel when you see the symbol you want.", "Ajuste a aposta com cima e baixo. Pare cada rolo quando vir o símbolo que deseja.")
                            : T("Choose a switch, adjust your bet, then start the roulette. Remember to collect your winnings.", "Escolha um número, ajuste a aposta e acione a roleta. Lembre-se de recolher o prêmio.")
                    }
                    : new[]
                    {
                        T("Come on, come on… Ah, almost!", "Vai, vai… Ah, quase!"),
                        T("I couldn't quit at the casino in the city of Haruka, but it's even more fun here.", "Eu não conseguia parar de jogar no cassino de Haruka, mas aqui é ainda mais divertido.")
                    };
            }
        }

        private bool TryCasinoInteraction()
        {
            if (!AtCasinoRoulette && !AtCasinoSlots) return false;
            Vector2 point = CasinoPlayerPoint;
            if (AtCasinoSlots && Vector2.Distance(point, new Vector2(_interior.entryPosition.x, _interior.entryPosition.z)) <= 0.85f)
            {
                StartCoroutine(LoadInterior(CasinoRoom));
                return true;
            }
            if (AtCasinoRoulette && Vector2.Distance(point, CasinoPassage) <= 1.05f)
            {
                StartCoroutine(LoadInterior(CasinoSlotsRoom));
                return true;
            }
            if (!NearCasinoMachine(point)) return false;
            _casinoCoins = 1;
            _casinoWagerPocket = 0;
            _casinoRound = CasinoPrize > 0 ? CasinoRound.Result : CasinoRound.Bet;
            _message = string.Empty;
            PlayRetailSound(RetailSfx.Confirm);
            return true;
        }

        private bool NearCasinoMachine(Vector2 point)
        {
            if (AtCasinoRoulette) return Vector2.Distance(point, RouletteSwitch) <= 1.35f;
            foreach (var machine in SlotMachines)
                if (Vector2.Distance(point, machine) <= 1.45f) return true;
            return false;
        }

        private void DrawCasinoHint(float width, float height)
        {
            if (!_player || (!AtCasinoSlots && !AtCasinoRoulette) || _activeTownNpc) return;
            Vector2 point = CasinoPlayerPoint;
            string hint = null;
            if (AtCasinoRoulette && Vector2.Distance(point, CasinoPassage) <= 1.05f)
                hint = WithInteractHint("Slot machine room", "Salão dos caça-níqueis");
            else if (AtCasinoSlots && Vector2.Distance(point, new Vector2(_interior.entryPosition.x, _interior.entryPosition.z)) <= 0.85f)
                hint = WithInteractHint("Return to the roulette room", "Voltar ao salão da roleta");
            else if (NearCasinoMachine(point))
                hint = AtCasinoSlots ? WithInteractHint("Slot machine", "Caça-níqueis") : WithInteractHint("Roulette", "Roleta");
            if (hint == null) return;
            var box = new Rect((width - 300f) * 0.5f, height * 0.66f, 300f, 32f);
            DrawDialogueFrame(box);
            DrawHudText(new Rect(box.x + 9f, box.y + 4f, box.width - 18f, 24f), hint, _messageStyle);
        }

        private bool HandleCasinoInput()
        {
            if (_casinoRound == CasinoRound.Closed) return false;
            if (!AtCasinoSlots && !AtCasinoRoulette)
            {
                _casinoRound = CasinoRound.Closed;
                return false;
            }
            if (_casinoRound == CasinoRound.Spinning && !AtCasinoSlots && Time.unscaledTime - _casinoSpinStarted >= 2.8f)
                ResolveCasinoRound(_casinoPocket == _casinoWagerPocket ? _casinoCoins * CasinoCoin * CasinoPocketCount : 0);
            if (_cancel?.WasPressedThisFrame() == true && _casinoRound != CasinoRound.Spinning)
            {
                _casinoRound = CasinoRound.Closed;
                PlayRetailSound(RetailSfx.Cancel);
                return true;
            }

            var keys = Keyboard.current;
            if (_casinoRound == CasinoRound.Bet && keys != null)
            {
                if (keys.upArrowKey.wasPressedThisFrame) ChangeCasinoBet(1);
                if (keys.downArrowKey.wasPressedThisFrame) ChangeCasinoBet(-1);
                if (AtCasinoRoulette && keys.leftArrowKey.wasPressedThisFrame)
                    _casinoWagerPocket = (_casinoWagerPocket + CasinoPocketCount - 1) % CasinoPocketCount;
                if (AtCasinoRoulette && keys.rightArrowKey.wasPressedThisFrame)
                    _casinoWagerPocket = (_casinoWagerPocket + 1) % CasinoPocketCount;
            }
            if (_interact?.WasPressedThisFrame() == true) AdvanceCasinoRound();
            return true;
        }

        private void ChangeCasinoBet(int delta)
        {
            _casinoCoins = Mathf.Clamp(_casinoCoins + delta, 1, 3);
        }

        private void AdvanceCasinoRound()
        {
            if (_casinoRound == CasinoRound.Bet)
            {
                int wager = _casinoCoins * CasinoCoin;
                if (_gold < wager) return;
                _gold -= wager;
                _casinoSpinStarted = Time.unscaledTime;
                _casinoStopped = 0;
                for (int i = 0; i < _casinoReels.Length; i++) _casinoReels[i] = Random.Range(0, 12);
                _casinoPocket = Random.Range(0, CasinoPocketCount);
                _casinoRound = CasinoRound.Spinning;
                PersistTownProgress();
                PlayRetailSound(RetailSfx.Confirm);
            }
            else if (_casinoRound == CasinoRound.Spinning && AtCasinoSlots && Time.unscaledTime - _casinoSpinStarted >= 0.25f)
            {
                _casinoReels[_casinoStopped] = CasinoReelPosition(_casinoStopped);
                _casinoStopped++;
                PlayRetailSound(RetailSfx.Confirm);
                if (_casinoStopped == _casinoReels.Length) ResolveCasinoRound(CasinoSlotPayout());
            }
            else if (_casinoRound == CasinoRound.Result)
            {
                if (CasinoPrize > 0)
                {
                    int collected = Mathf.Min(CasinoPrize, int.MaxValue - _gold);
                    _gold += collected;
                    TownSaveVariables.SetInt(CasinoPrizeKey, CasinoPrize - collected);
                    PersistTownProgress();
                    PlayRetailSound(RetailSfx.Confirm);
                    _casinoRound = CasinoRound.Closed;
                    _message = T($"Collected {collected} G.", $"Você recolheu {collected} G.");
                }
                else _casinoRound = CasinoRound.Bet;
            }
        }

        private int CasinoReelPosition(int reel)
        {
            if (reel < _casinoStopped || _casinoRound != CasinoRound.Spinning) return _casinoReels[reel];
            int elapsed = Mathf.FloorToInt((Time.unscaledTime - _casinoSpinStarted) * (11f + reel * 2f));
            return (_casinoReels[reel] + elapsed) % 12;
        }

        private int CasinoSymbol(int reel, int row)
        {
            return ((CasinoReelPosition(reel) + row + 12) % 12 + reel * 3) % CasinoSymbols.Length;
        }

        private int CasinoLinePayout(int left, int middle, int right)
        {
            int symbol = CasinoSymbol(0, left);
            return CasinoSymbol(1, middle) == symbol && CasinoSymbol(2, right) == symbol
                ? CasinoMultipliers[symbol] * CasinoCoin * _casinoCoins : 0;
        }

        private int CasinoSlotPayout()
        {
            int payout = CasinoLinePayout(0, 0, 0);
            if (_casinoCoins >= 2) payout += CasinoLinePayout(-1, -1, -1) + CasinoLinePayout(1, 1, 1);
            if (_casinoCoins == 3) payout += CasinoLinePayout(-1, 0, 1) + CasinoLinePayout(1, 0, -1);
            return payout;
        }

        private void ResolveCasinoRound(int prize)
        {
            TownSaveVariables.SetInt(CasinoPrizeKey, prize);
            PersistTownProgress();
            _casinoRound = CasinoRound.Result;
        }

        private bool DrawCasino(float width, float height)
        {
            if (_casinoRound == CasinoRound.Closed)
            {
                DrawCasinoHint(width, height);
                return false;
            }
            float panelWidth = Mathf.Min(460f, width - 24f);
            var panel = new Rect((width - panelWidth) * 0.5f, (height - 270f) * 0.5f, panelWidth, 270f);
            DrawDialogueFrame(panel);
            GUILayout.BeginArea(new Rect(panel.x + 16f, panel.y + 12f, panel.width - 32f, panel.height - 20f));
            GUILayout.Label(AtCasinoSlots ? T("Slot machine", "Caça-níqueis") : T("Roulette", "Roleta"), _bodyStyle);
            GUILayout.Label(T($"Gold: {_gold} G", $"Ouro: {_gold} G"), _messageStyle);
            if (AtCasinoSlots)
            {
                for (int row = -1; row <= 1; row++)
                {
                    GUILayout.BeginHorizontal();
                    for (int reel = 0; reel < 3; reel++)
                        GUILayout.Box(CasinoSymbols[CasinoSymbol(reel, row)], GUILayout.ExpandWidth(true), GUILayout.Height(24));
                    GUILayout.EndHorizontal();
                }
                GUILayout.Label(T("1 coin: center • 2: all rows • 3: diagonals too",
                    "1 moeda: linha central • 2: três linhas • 3: inclui diagonais"), _messageStyle);
            }
            else
            {
                int current = _casinoRound == CasinoRound.Spinning
                    ? (_casinoPocket + Mathf.CeilToInt((2.8f - (Time.unscaledTime - _casinoSpinStarted)) * 10f)) % CasinoPocketCount
                    : _casinoPocket;
                var wheel = GUILayoutUtility.GetRect(160f, 96f);
                Vector2 hub = wheel.center;
                for (int i = 0; i < CasinoPocketCount; i++)
                {
                    float angle = i * 2f * Mathf.PI / CasinoPocketCount;
                    var center = hub + new Vector2(66f * Mathf.Sin(angle), -34f * Mathf.Cos(angle));
                    var pocket = new Rect(center.x - 20f, center.y - 12f, 40f, 24f);
                    bool selected = _casinoRound == CasinoRound.Bet ? i == _casinoWagerPocket : i == current;
                    string label = (selected ? "▸ " : "") + (i + 1);
                    if (selected) DrawHudFill(pocket, new Color(0.63f, 0.45f, 0.13f, 0.8f));
                    if (_casinoRound == CasinoRound.Bet)
                    {
                        if (GUI.Button(pocket, label)) _casinoWagerPocket = i;
                    }
                    else GUI.Box(pocket, label);
                }
                GUILayout.Label(T($"Selected pocket: {_casinoWagerPocket + 1}", $"Número escolhido: {_casinoWagerPocket + 1}"), _messageStyle);
            }
            if (_casinoRound == CasinoRound.Bet)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("−", GUILayout.Width(48f))) ChangeCasinoBet(-1);
                GUILayout.Label(T($"Bet: {_casinoCoins * CasinoCoin} G", $"Aposta: {_casinoCoins * CasinoCoin} G"), _messageStyle);
                if (GUILayout.Button("+", GUILayout.Width(48f))) ChangeCasinoBet(1);
                GUILayout.EndHorizontal();
                if (_gold < _casinoCoins * CasinoCoin)
                    GUILayout.Label(T("Not enough gold.", "Ouro insuficiente."), _messageStyle);
                else if (GUILayout.Button(WithInteractHint("Play", "Jogar"))) AdvanceCasinoRound();
            }
            else if (_casinoRound == CasinoRound.Spinning)
            {
                if (AtCasinoSlots && GUILayout.Button(WithInteractHint("Stop reel", "Parar rolo"))) AdvanceCasinoRound();
                if (AtCasinoRoulette) GUILayout.Label(T("The wheel is spinning…", "A roleta está girando…"), _messageStyle);
            }
            else
            {
                if (CasinoPrize > 0)
                {
                    if (GUILayout.Button(WithInteractHint($"Collect {CasinoPrize} G", $"Recolher {CasinoPrize} G"))) AdvanceCasinoRound();
                }
                else
                {
                    GUILayout.Label(T("No prize this round.", "Sem prêmio nesta rodada."), _messageStyle);
                    if (GUILayout.Button(WithInteractHint("Play again", "Jogar de novo"))) AdvanceCasinoRound();
                }
            }
            if (_casinoRound != CasinoRound.Spinning && GUILayout.Button(T("Leave", "Sair"))) _casinoRound = CasinoRound.Closed;
            GUILayout.EndArea();
            return true;
        }
    }
}
