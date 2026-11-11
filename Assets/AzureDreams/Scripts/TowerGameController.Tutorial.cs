using System.Collections;
using UnityEngine.InputSystem;
using AzureDreams.Core;
using UnityEngine;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private static readonly Vector2Int TutorialEntrance = new(31, 57);
        private static readonly Vector2Int TutorialElevator = new(31, 17);
        private bool _tutorialRun;
        private bool _starterJoinedThisRun;
        private int _tutorialHintsSeen;
        private int _tutorialDialogue;
        private GameObject _tutorialKewne;
        private bool InTowerTutorial => _tutorialRun && _floor == 1;

        private enum KewneMeeting { None, Arriving, Introduction, Question, Reply }
        private int _kewneIntroPage;
        private int _tutorialHintPage;

        // Adapted from TOWN.BIN 0x2D980D–0x2DA2B1.
        private static readonly (string En, string Pt)[] KewneIntroduction =
        {
            ("Aren't you a monster tamer? Hey, don't be frightened. I'm not your enemy.",
                "Você é um domador de monstros, não é? Ei, não se assuste. Não sou seu inimigo."),
            ("Surprised that I can talk? I'm clever enough to have learned the human language.",
                "Surpreso porque eu sei falar? Sou esperto o bastante para ter aprendido a língua dos humanos."),
            ("Are you going in alone? Monster tamers fight alongside monsters, and you don't have a single familiar with you.",
                "Vai entrar sozinho? Domadores lutam ao lado de monstros, e você não tem nenhum familiar com você."),
            ("You can't take on the Tower alone yet. You've got a magic collar, haven't you? I can smell it.",
                "Você ainda não pode encarar a torre sozinho. Tem um colar mágico, não tem? Consigo sentir o cheiro."),
            ("Believe it or not, I was the familiar of a great monster tamer once. My master passed away, and I've been alone ever since.",
                "Acredite se quiser, já fui o familiar de um grande domador. Meu mestre faleceu e fiquei sozinho desde então."),
            ("People keep away from me because I speak their language. And I can't go back to living by instinct like the other monsters.",
                "As pessoas me evitam porque falo a língua delas. E não consigo voltar a viver só por instinto, como os outros monstros."),
            ("I can be your familiar. Put that magic collar on me if you like.",
                "Posso ser seu familiar. Pode colocar esse colar mágico em mim, se quiser."),
            ("Great! You're my master now. What will you call me?",
                "Ótimo! Agora você é meu mestre. Como vai me chamar?"),
            ("Kewne, huh? I think my old master called me that too. And you're {name}? Yes, Master {name}!",
                "Kewne, é? Acho que meu antigo mestre também me chamava assim. E você é o {name}? Sim, mestre {name}!")
        };
        private KewneMeeting _kewneMeeting;
        private EventTrigger _townTowerGate;
        private GameObject _kewneMeetingView;
        private bool? _kewneTutorialChoice;
        private int _kewneMeetingSelection;
        private const float KewneSkipHoldSeconds = 2f;
        private float _kewneSkipHeld, _kewneSkipFade;
        private bool _kewneSkipTransition;
        private Texture2D _kewneSkipIcon;
        private GUIStyle _dialogueSkipStyle;
        private bool KewneMeetingSpeaking => _kewneMeeting is KewneMeeting.Introduction or KewneMeeting.Question or KewneMeeting.Reply;
        private bool NeedsKewneMeeting => _kewneDefinition && _kewnePrefab &&
            !TownSaveVariables.GetBool("starter_kewne_received");

        private bool TryStartKewneMeeting()
        {
            if (_area != Area.Town || _interior || !_player || !_townTowerGate || !NeedsKewneMeeting ||
                _kewneMeeting != KewneMeeting.None || _townDoorOpening || _kewneTutorialChoice.HasValue ||
                _activeTownNpc || _inventoryOpen || _inventoryMenu != InventoryMenu.Closed || _heldTownPot || _carriedItem != null ||
                _townJumpHeight > TownGroundTolerance ||
                (_townFlow && _townFlow.State.Current != TownGameState.State.TownIdle)) return false;
            var gate = _townTowerGate.transform;
            var offset = _player.transform.position - gate.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > 2.6f * 2.6f || Vector3.Dot(offset, gate.forward) > 0.1f) return false;
            if (!CanEnterWithLoadout()) return false;
            StartCoroutine(PresentKewneMeeting(gate));
            return true;
        }

        private IEnumerator PresentKewneMeeting(Transform gate)
        {
            _kewneMeeting = KewneMeeting.Arriving;
            _message = string.Empty;
            ResetPush();
            MoveInTown(Vector2.zero, 0f);
            var koh = _player;
            var side = Vector3.Dot(koh.transform.position - gate.position, gate.right) > 0f ? -gate.right : gate.right;
            var end = koh.transform.position + side * 0.95f + gate.forward * 0.5f;
            var start = end + side * 1.35f + Vector3.up * 0.9f;
            const int species = (int)MonsterSpecies.KEWNE;
            var view = SpawnActor(_kewnePrefab, _playerCell, "Kewne - Tower gate", species: species, palette: FamiliarPalette(species));
            _kewneMeetingView = view;
            var direction = -side;
            for (float elapsed = 0f; elapsed < 0.6f && koh && view && _kewneMeeting == KewneMeeting.Arriving;
                elapsed += Time.unscaledDeltaTime)
            {
                float progress = elapsed / 0.6f;
                var position = Vector3.Lerp(start, end, progress) + Vector3.up * (Mathf.Sin(progress * Mathf.PI) * 0.3f);
                PresentGateKewne(view, position, direction, elapsed, true);
                yield return null;
            }
            if (!koh || !view || _kewneMeeting != KewneMeeting.Arriving) yield break;
            PresentGateKewne(view, end, koh.transform.position - end, 0f, false);
            _townFacing = new Vector2(side.x, side.z);
            if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            _kewneMeetingSelection = 0;
            _kewneIntroPage = 0;
            _kewneSkipHeld = 0f;
            _kewneMeeting = KewneMeeting.Introduction;
            var line = KewneIntroduction[_kewneIntroPage];
            SetKewneMeetingLine(T(line.En, line.Pt));
        }

        private void PresentGateKewne(GameObject view, Vector3 position, Vector3 direction, float time, bool moving)
        {
            view.transform.position = position;
            if (view.TryGetComponent<KewneSpriteView>(out var sprite)) sprite.Face(new Vector2(direction.x, direction.z));
            if (view.TryGetComponent<ActorLocomotionView>(out var locomotion)) locomotion.Present(direction, time * 2f, moving);
        }

        private void SetKewneMeetingLine(string line)
        {
            ShowTownDialogue("Kewne", line, null);
            _message = _dialogueLine;
        }

        private bool HandleKewneMeeting()
        {
            if (_kewneSkipTransition) return true;
            if (_kewneMeeting == KewneMeeting.None) return false;
            if (KewneMeetingSpeaking && _mapAction?.IsPressed() == true)
            {
                _kewneSkipHeld += Time.unscaledDeltaTime;
                if (_kewneSkipHeld >= KewneSkipHoldSeconds)
                    StartCoroutine(SkipKewneMeeting());
                return true;
            }
            _kewneSkipHeld = 0f;
            if (_kewneMeeting == KewneMeeting.Question && Keyboard.current is { } keys)
            {
                if (keys.leftArrowKey.wasPressedThisFrame || keys.upArrowKey.wasPressedThisFrame) _kewneMeetingSelection = 0;
                if (keys.rightArrowKey.wasPressedThisFrame || keys.downArrowKey.wasPressedThisFrame) _kewneMeetingSelection = 1;
            }
            if (KewneMeetingSpeaking && _interact?.WasPressedThisFrame() == true) AdvanceKewneMeeting(_kewneMeetingSelection);
            return true;
        }

        private IEnumerator SkipKewneMeeting()
        {
            _kewneSkipTransition = true;
            _kewneMeeting = KewneMeeting.None;
            _message = string.Empty;
            const float duration = 0.32f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                _kewneSkipFade = elapsed / duration;
                yield return null;
            }

            _kewneSkipFade = 1f;
            _kewneTutorialChoice = false;
            EnterTower();
            yield return null;

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
            {
                _kewneSkipFade = 1f - elapsed / duration;
                yield return null;
            }
            _kewneSkipFade = _kewneSkipHeld = 0f;
            _kewneSkipTransition = false;
        }

        private void DrawKewneSkipPrompt(float width)
        {
            if (!KewneMeetingSpeaking && !_kewneSkipTransition) return;
            DrawDialogueSkipPrompt(width, _kewneSkipHeld / KewneSkipHoldSeconds);
        }

        private void DrawDialogueSkipPrompt(float width, float progress)
        {
            progress = Mathf.Clamp01(progress);
            if (!_kewneSkipIcon) _kewneSkipIcon = Resources.Load<Texture2D>(EntranceResources + "KewneDialogueSkip");

            string binding = _mapAction?.GetBindingDisplayString() ?? "Right Shift";
            string label = T($"Skip Dialogue [{binding}]", $"Pular diálogo [{binding}]");
            _dialogueSkipStyle ??= new GUIStyle(_messageStyle) { fontSize = 14, wordWrap = false };
            float labelWidth = _retailFont && !GameLanguage.Portuguese
                ? 8f * label.Length : _dialogueSkipStyle.CalcSize(new GUIContent(label)).x;
            float rightEdge = width - 50f * (width / Screen.width);
            var labelRect = new Rect(rightEdge - labelWidth - 1f, 31f, labelWidth, 24f);

            float opacity = Mathf.Lerp(0.75f, 1f, progress);
            var center = new Vector2(labelRect.x - 34f, 43f);
            var previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, previousColor.a * opacity);
            if (_kewneSkipIcon)
                GUI.DrawTexture(new Rect(center.x - 24f, center.y - 24f, 48f, 48f), _kewneSkipIcon, ScaleMode.ScaleToFit);
            DrawHudText(labelRect, label, _dialogueSkipStyle);
            GUI.color = previousColor;

            const int segments = 48;
            const float radius = 28f;
            var matrix = GUI.matrix;
            var gold = new Color(1f, 0.76f, 0.26f, opacity);
            for (int i = 0; i < Mathf.FloorToInt(progress * segments); i++)
            {
                GUIUtility.RotateAroundPivot(i * (360f / segments), center);
                DrawHudFill(new Rect(center.x - 2f, center.y - radius, 4f, 2.5f), gold);
                GUI.matrix = matrix;
            }
        }

        private void AdvanceKewneMeeting(int selection)
        {
            if (_kewneSkipHeld > 0f || _kewneSkipTransition) return;
            if (_kewneMeeting == KewneMeeting.Introduction)
            {
                if (++_kewneIntroPage < KewneIntroduction.Length)
                {
                    var line = KewneIntroduction[_kewneIntroPage];
                    SetKewneMeetingLine(T(line.En, line.Pt));
                    return;
                }
                _kewneMeeting = KewneMeeting.Question;
                SetKewneMeetingLine(T("You are a beginner, aren't you?", "Você é iniciante, não é?"));
                return;
            }
            if (_kewneMeeting == KewneMeeting.Question)
            {
                _kewneTutorialChoice = selection == 0;
                _kewneMeeting = KewneMeeting.Reply;
                SetKewneMeetingLine(selection == 0
                    ? T("OK, I will teach you many things about the tower. Let's go.",
                        "Certo, vou ensinar muita coisa sobre a torre. Vamos lá.")
                    : T("I can see you're a champ! Let's go.", "Já vi que você é fera! Vamos lá."));
            }
            else if (_kewneMeeting == KewneMeeting.Reply && _townTowerGate &&
                _townTowerGate.TryGetComponent<TownGateDoor>(out var door))
            {
                _kewneMeeting = KewneMeeting.None;
                StartCoroutine(PlayTownTowerEntrance(_townTowerGate, door));
            }
        }

        private void DrawKewneMeetingChoices(Rect box)
        {
            if (!KewneMeetingSpeaking) return;
            int count = _kewneMeeting == KewneMeeting.Question ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                var button = new Rect(box.xMax - 116f * (count - i), box.y - 30f, 108f, 26f);
                string label = count == 1 ? T("Continue", "Continuar") : i == 0 ? T("Yes", "Sim") : T("No", "Não");
                if (count == 2 && i == _kewneMeetingSelection) label = "▸ " + label;
                if (GUI.Button(button, label)) { AdvanceKewneMeeting(i); break; }
            }
        }

        private void PlaceTutorialContents()
        {
            // Native item records at DUNGEON.BIN 0x7C8910; flags here are Unity's inventory flags.
            void Place(int x, int y, byte kind, byte id, sbyte param)
            {
                var item = kind is ItemSystem.Kind.Sword or ItemSystem.Kind.Shield
                    ? TowerItemTrapManager.CreateTowerEquipment(kind, id, roll: 0)
                    : ItemSystem.CreateFloorItem(id, kind, param);
                _towerItemTraps.TryPlaceItem(new Vector2Int(x, y), item);
            }
            Place(6, 40, ItemSystem.Kind.Sword, 2, 0);
            Place(31, 3, ItemSystem.Kind.Shield, 1, 0);
            Place(12, 44, ItemSystem.Kind.Ball, 1, 5);
            _towerItemTraps.TryPlaceItem(new Vector2Int(56, 4),
                MonsterEgg.Encode((byte)MonsterSpecies.PULUNPA, color: 5, identified: false, heat: 80));
            Place(27, 4, ItemSystem.Kind.Herb, 1, 20);
            _towerFloorLayout.Dungeon.Grid[21, 31] = CoreDungeon.TileType.Trap;
            _towerFloorLayout.Dungeon.SetFlag(31, 21, CoreDungeon.TileFlag.HasTrap);
            var trap = _towerItemTraps.TrapAt(new Vector2Int(31, 21));
            trap.Effect = TowerTrapEffect.Sleep;
            trap.Spring();
        }

        private void BeginTutorialIntro()
        {
            var cell = FindManagedActorCell(_playerCell + Vector2Int.up, _playerCell, _stairCell);
            const int species = (int)MonsterSpecies.KEWNE;
            _tutorialKewne = SpawnActor(_kewnePrefab, cell, "Kewne - tutorial", species: species, palette: FamiliarPalette(species));
            _tutorialDialogue = 1;
            _message = string.Empty;
        }

        private void CheckTutorialSpot()
        {
            void Hint(int flag, int page, Vector2Int cell, int range)
            {
                if (_tutorialDialogue != 0 || (_tutorialHintsSeen & flag) != 0 || Distance(_playerCell, cell) > range) return;
                _tutorialHintsSeen |= flag;
                _tutorialDialogue = page;
                _tutorialHintPage = 0;
            }
            Hint(1, 2, new Vector2Int(6, 40), 3);
            Hint(2, 3, new Vector2Int(50, 44), 4);
            Hint(4, 4, new Vector2Int(56, 4), 3);
            Hint(8, 5, new Vector2Int(31, 21), 2);
            Hint(16, 6, new Vector2Int(27, 4), 3);
            Hint(32, 7, new Vector2Int(12, 44), 2);
        }

        private bool HandleTutorialInput()
        {
            if (_tutorialDialogue == 0) return false;
            if (_interact != null && _interact.WasPressedThisFrame()) FinishTutorialDialogue();
            return true;
        }

        private void FinishTutorialDialogue()
        {
            if (_tutorialDialogue == 1)
            {
                _familiarCell = FindManagedActorCell(_playerCell + Vector2Int.up, _playerCell, _stairCell);
                if (_tutorialKewne) RemoveGenerated(_tutorialKewne);
                _tutorialKewne = null;
                _familiarDefinition = _kewneDefinition;
                _familiar = SpawnFamiliar();
                _familiarHealth = FamiliarMaxHealth;
                _familiarActor.Hp = StatByte(_familiarHealth);
                _familiarLocomotion = ConfigureLocomotion(_familiar);
                _starterJoinedThisRun = true;
            }
            else if (++_tutorialHintPage < TutorialHintPageCount(_tutorialDialogue)) return;
            _tutorialDialogue = 0;
        }

        private static int TutorialHintPageCount(int topic) => topic == 6 ? 4 : topic == 7 ? 2 : 1;

        private void DrawTutorial(float width, float height)
        {
            if (_tutorialDialogue == 0 || _checkpointMenu != CheckpointMenu.Closed) return;
            string line = _tutorialDialogue switch
            {
                1 => T("I’m Kewne. I’ll come with you and show you how to explore the Tower.", "Eu sou o Kewne. Vou com você e mostro como explorar a torre."),
                2 => T("Pick up that sword. Open the inventory with Tab and equip it before you fight.", "Pegue aquela espada. Abra o inventário com Tab e equipe a espada antes de lutar."),
                // DUNGEON.BIN 0x7D45B6, with the Unity attack binding.
                3 => T("Face enemy monsters and attack with F!", "Vire-se para os monstros inimigos e ataque com F!"),
                4 => T("That’s a monster egg. You can warm it here or hatch it at home.", "É um ovo de monstro. Você pode aquecê-lo aqui ou chocá-lo em casa."),
                // DUNGEON.BIN 0x7D3BE8–0x7D4555, adapted to the Unity controls.
                6 => _tutorialHintPage switch
                {
                    0 => T("You and your familiars recover HP as turns pass. A familiar spends MP even when it walks. At zero MP, it can't move.",
                        "Você e seus familiares recuperam PV com o passar dos turnos. Um familiar gasta PM até quando anda. Com zero PM, ele não consegue se mover."),
                    1 => T("Select food in Items, then choose Give to familiar. How much MP it restores depends on the food and the monster.",
                        "Selecione um alimento em Itens e escolha Dar para o familiar. A recuperação de PM depende do alimento e do monstro."),
                    2 => T("Hold Run and Lift together to pass turns and recover HP. Watch your familiar's MP while you wait.",
                        "Segure Correr e Levantar juntos para passar turnos e recuperar PV. Fique de olho nos PM do familiar enquanto espera."),
                    _ => T("Use Change AI to choose how a familiar helps you. You can also tell it exactly what to do.",
                        "Use Mudar IA para escolher como um familiar ajuda você. Também dá para dar ordens específicas.")
                },
                7 => _tutorialHintPage == 0
                    ? T("Familiars normally follow their strategy. Use Command to choose their action and direction for the next turn.",
                        "Os familiares normalmente seguem a estratégia definida. Use Comandar para escolher a ação e a direção deles no próximo turno.")
                    : T("Your familiar follows the order after you take your own action.",
                        "Seu familiar executa a ordem depois que você fizer a sua ação."),
                _ => T("Watch your step. There are hidden traps near the elevator.", "Cuidado onde pisa. Há armadilhas escondidas perto do elevador.")
            };
            var panel = new Rect(width * 0.1f, height - 120f, width * 0.8f, 104f);
            DrawDialogueFrame(panel);
            DrawDialoguePortrait(FindDialoguePortrait("Kewne"), panel);
            DrawHudText(new Rect(panel.x + 13, panel.y + 8, panel.width - 26, 22), "Kewne", _bodyStyle);
            DrawHudText(new Rect(panel.x + 13, panel.y + 31, panel.width - 26, 49), line, _messageStyle);
            if (GUI.Button(new Rect(panel.xMax - 146, panel.yMax - 28, 136, 22), WithInteractHint("Continue", "Continuar")))
                FinishTutorialDialogue();
        }
    }
}
