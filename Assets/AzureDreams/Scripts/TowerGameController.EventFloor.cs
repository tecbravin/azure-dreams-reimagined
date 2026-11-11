using System.Collections.Generic;
using UnityEngine;
using AzureDreams.Core;
using UnityEngine.InputSystem;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [SerializeField] private GameObject _floorTwoGhosh;
        [SerializeField] private GameObject _floorTwoSelfi;

        private enum VisitorEvent { Ghosh, Selfi, Argument }
        private enum VisitorStage { Waiting, Approach, Dialogue, Choice, Challenge, Depart, Finished }
        private const float VisitorMotionDuration = 0.45f;
        private readonly struct VisitorLine
        {
            public readonly bool Selfi;
            public readonly string English, Portuguese;
            public VisitorLine(bool selfi, string english, string portuguese)
            { Selfi = selfi; English = english; Portuguese = portuguese; }
        }

        // NTSC DUNGEON.BIN 0x7D152C, 0x7D2E7A and 0x7D207E.
        // Koh is inserted where the original script uses the player's name token.
        private static readonly VisitorLine[][] VisitorScripts =
        {
            new VisitorLine[]
            {
                new(false, "Huh, it’s just {name}.", "Ah, é só o {name}."),
                new(false, "You feel lonely and want my help, don’t you?", "Você está se sentindo sozinho e quer a minha ajuda, não é?"),
                new(false, "Do you wish to stay close to me?", "Quer ficar perto de mim?")
            },
            new VisitorLine[]
            {
                new(true, "Hi there, Puffy lips!", "Oi, Bocão!"),
                new(true, "You look so handsome inside the tower. Must be the darkness.", "Você fica tão bonito dentro da torre. Deve ser a escuridão."),
                new(true, "Ha ha ha ha.", "Ha ha ha ha.")
            },
            new VisitorLine[]
            {
                new(false, "What? I put an end to it, so this monster’s money is mine!", "O quê? Eu dei o golpe final, então o dinheiro desse monstro é meu!"),
                new(true, "What are you talking about? I’m the one who damaged him the most. So the money is mine!", "Do que você está falando? Eu causei mais dano. Então o dinheiro é meu!"),
                new(true, "I treat you as my big brother in town, but it’s different here in the tower.", "Na cidade eu trato você como meu irmão mais velho, mas aqui na torre é diferente."),
                new(false, "It’s my money!", "O dinheiro é meu!"),
                new(true, "It’s mine!", "É meu!"),
                new(true, "Hey! Puffy lips.", "Ei, Bocão."),
                new(false, "Selfi, How dare you talk like that?", "Selfi, como você ousa falar assim?")
            }
        };

        private TowerEventNpc _towerEventGhosh, _towerEventSelfi;
        private VisitorEvent _visitorEvent;
        private VisitorStage _visitorStage = VisitorStage.Finished;
        private int _visitorLine;
        private bool _visitorReply;
        private bool _visitorHasAnswered;
        private bool _ghoshDuelActive;
        private const string GhoshDuelWon = "tower.ghosh_duel_won";
        private float _visitorTime;
        private Vector3 _visitorFocus;
        private readonly List<Vector2Int> _visitorApproachPath = new();
        private const int VisitorTriggerSteps = 2;
        private static readonly Vector2Int FloorTwoVisitorCell = new(31, 32);
        private static readonly Vector2Int[] VisitorSteps =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly HashSet<Vector2Int> _visitorTriggerCells = new();
        public bool IsTowerEventFloor => _area == Area.Tower && _floor == 2;
        private bool IsShowingVisitorDialogue => _visitorStage is VisitorStage.Approach or VisitorStage.Dialogue or VisitorStage.Choice or VisitorStage.Challenge;

        private void SpawnTowerEventNpc()
        {
            _selfiVisitorStory = PendingSelfiEncounter();
            _visitorEvent = _selfiVisitorStory == 0 ? (VisitorEvent)RollSpawnRange(VisitorScripts.Length) : VisitorEvent.Selfi;
            var secondCell = FloorTwoVisitorCell + Vector2Int.right;
            if (_visitorEvent != VisitorEvent.Selfi)
                _towerEventGhosh = SpawnVisitor(_floorTwoGhosh, FloorTwoVisitorCell, "Ghosh");
            if (_visitorEvent != VisitorEvent.Ghosh)
                _towerEventSelfi = SpawnVisitor(_floorTwoSelfi,
                    _visitorEvent == VisitorEvent.Argument ? secondCell : FloorTwoVisitorCell, "Selfi");
            BuildVisitorTriggerCells(secondCell);
            _visitorStage = VisitorStage.Waiting;
        }

        private void BuildVisitorTriggerCells(Vector2Int secondCell)
        {
            _visitorTriggerCells.Clear();
            var pending = new Queue<(Vector2Int Cell, int Steps)>();
            _visitorTriggerCells.Add(FloorTwoVisitorCell);
            pending.Enqueue((FloorTwoVisitorCell, 0));
            if (_visitorEvent == VisitorEvent.Argument)
            {
                _visitorTriggerCells.Add(secondCell);
                pending.Enqueue((secondCell, 0));
            }

            while (pending.Count > 0)
            {
                var (cell, steps) = pending.Dequeue();
                if (steps == VisitorTriggerSteps) continue;
                foreach (var direction in VisitorSteps)
                {
                    var next = cell + direction;
                    if (CanTowerStep(cell, direction) && _visitorTriggerCells.Add(next))
                        pending.Enqueue((next, steps + 1));
                }
            }
        }

        private TowerEventNpc SpawnVisitor(GameObject prefab, Vector2Int cell, string character)
        {
            var view = Spawn(prefab, cell, "Floor 2 - " + character);
            var sprite = view.GetComponentInChildren<MonsterTurnSpriteView>();
            view.transform.position = new Vector3(cell.x, TowerGroundY(cell) + (sprite ? 0.55f : 0f), cell.y);
            return view.AddComponent<TowerEventNpc>();
        }

        private bool TryTowerEventInteraction()
        {
            if (!IsTowerEventFloor || _visitorStage != VisitorStage.Waiting || !_player ||
                _inventoryOpen || _inventoryMenu != InventoryMenu.Closed || _mapView != MapView.Sidebar ||
                !_visitorTriggerCells.Contains(_playerCell)) return false;
            BuildVisitorApproachPath();
            var playerPosition = CellPosition(_visitorApproachPath[_visitorApproachPath.Count - 1]);
            var visitor = _towerEventGhosh ? _towerEventGhosh : _towerEventSelfi;
            Vector3 actorCenter = _towerEventGhosh && _towerEventSelfi
                ? (_towerEventGhosh.transform.position + _towerEventSelfi.transform.position) * 0.5f
                : visitor.transform.position;
            _visitorFocus = Vector3.Lerp(playerPosition, actorCenter, 0.5f);
            _visitorFocus.y = 0f;
            if (_towerEventGhosh) _towerEventGhosh.SetStage(playerPosition);
            if (_towerEventSelfi) _towerEventSelfi.SetStage(playerPosition);
            _visitorLine = 0;
            _visitorReply = _visitorHasAnswered = false;
            _visitorTime = 0f;
            _visitorStage = VisitorStage.Approach;
            ShowVisitorLine();
            return true;
        }

        private void BuildVisitorApproachPath()
        {
            _visitorApproachPath.Clear();
            var preferred = FloorTwoVisitorCell + Vector2Int.up;
            var second = FloorTwoVisitorCell + Vector2Int.right;
            var previous = new Dictionary<Vector2Int, Vector2Int> { [_playerCell] = _playerCell };
            var pending = new Queue<Vector2Int>();
            pending.Enqueue(_playerCell);
            Vector2Int destination = _playerCell;
            int nearest = Distance(destination, preferred);
            while (pending.Count > 0)
            {
                var cell = pending.Dequeue();
                int distance = Distance(cell, preferred);
                if (distance < nearest)
                {
                    nearest = distance;
                    destination = cell;
                }
                if (cell == preferred) break;
                foreach (var step in VisitorSteps)
                {
                    var next = cell + step;
                    if (next == FloorTwoVisitorCell || (_visitorEvent == VisitorEvent.Argument && next == second) ||
                        next == _stairCell || (_familiar && next == _familiarCell) ||
                        _thrownEggs.Exists(ally => ally.View && ally.Cell == next) ||
                        !CanTowerStep(cell, step) || !previous.TryAdd(next, cell)) continue;
                    pending.Enqueue(next);
                }
            }
            for (var cell = destination; cell != _playerCell; cell = previous[cell])
                _visitorApproachPath.Add(cell);
            _visitorApproachPath.Add(_playerCell);
            _visitorApproachPath.Reverse();
        }

        private float VisitorApproachDuration => VisitorMotionDuration * Mathf.Max(1, _visitorApproachPath.Count - 1);

        private bool HandleVisitorEvent()
        {
            if (!IsTowerEventFloor || _turnBusy) return false;
            if (_visitorStage == VisitorStage.Waiting) return TryTowerEventInteraction();
            if (_visitorStage == VisitorStage.Finished) return false;
            if (_visitorStage == VisitorStage.Dialogue)
            {
                if (_interact != null && _interact.WasPressedThisFrame()) AdvanceVisitorDialogue();
                return true;
            }

            if (_visitorStage is VisitorStage.Choice or VisitorStage.Challenge)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.leftArrowKey.wasPressedThisFrame) _visitorReply = false;
                    if (keyboard.rightArrowKey.wasPressedThisFrame) _visitorReply = true;
                }
                if (_interact != null && _interact.WasPressedThisFrame()) AnswerVisitor(_visitorReply);
                return true;
            }

            TickVisitorMotion(Time.unscaledDeltaTime * TowerAnimationSpeed);
            return true;
        }

        private void TickVisitorMotion(float deltaTime)
        {
            float duration = _visitorStage == VisitorStage.Approach ? VisitorApproachDuration : VisitorMotionDuration;
            _visitorTime = Mathf.Min(duration, _visitorTime + deltaTime);
            float progress = _visitorTime / duration;
            if (_visitorStage == VisitorStage.Approach)
            {
                PresentVisitorApproach(progress);
                if (_towerEventGhosh) _towerEventGhosh.Approach(progress);
                if (_towerEventSelfi) _towerEventSelfi.Approach(progress);
                if (progress >= 1f)
                {
                    _visitorStage = VisitorStage.Dialogue;
                    if (_selfiVisitorStory == 4) SpawnSelfiEncounterMonster(AzureDreams.Core.MonsterSpecies.U_BOAT);
                }
            }
            else
            {
                if (_towerEventGhosh) _towerEventGhosh.Depart(progress);
                if (_towerEventSelfi) _towerEventSelfi.Depart(progress);
                if (progress >= 1f)
                {
                    if (_towerEventGhosh) RemoveGenerated(_towerEventGhosh.gameObject);
                    if (_towerEventSelfi) RemoveGenerated(_towerEventSelfi.gameObject);
                    int story = _selfiVisitorStory;
                    var storyMonster = _selfiEncounterMonster;
                    ResetVisitorEvent();
                    _selfiVisitorStory = story;
                    _selfiEncounterMonster = storyMonster;
                    CompleteSelfiVisitor();
                }
            }
        }

        private void PresentVisitorApproach(float progress)
        {
            int steps = _visitorApproachPath.Count - 1;
            if (steps > 0)
            {
                float walk = progress * steps;
                int index = Mathf.Min(steps - 1, Mathf.FloorToInt(walk));
                var from = _visitorApproachPath[index];
                var to = _visitorApproachPath[index + 1];
                var direction = to - from;
                if (progress < 1f) TryTowerFace(direction);
                _player.transform.position = Vector3.Lerp(CellPosition(from), CellPosition(to), walk - index);
                _playerLocomotion?.Present(new Vector3(direction.x, 0f, direction.y), walk - index, progress < 1f);
            }
            if (progress < 1f) return;
            _playerCell = _visitorApproachPath[steps];
            SetActorCell(_playerActor, _playerCell);
            var facing = FloorTwoVisitorCell - _playerCell;
            TryTowerFace(new Vector2Int(System.Math.Sign(facing.x), System.Math.Sign(facing.y)));
        }

        private void AdvanceVisitorDialogue()
        {
            if ((Time.unscaledTime - _dialogueStarted) * 42f < _dialogueLine.Length)
            {
                _dialogueStarted = Time.unscaledTime - (_dialogueLine.Length + 1) / 42f;
                return;
            }
            if (_visitorHasAnswered)
            {
                _visitorStage = VisitorStage.Challenge;
                _visitorReply = false;
                return;
            }
            if (++_visitorLine < CurrentVisitorScript.Length)
            {
                ShowVisitorLine();
                return;
            }
            if (_visitorEvent == VisitorEvent.Ghosh)
            {
                _visitorStage = VisitorStage.Choice;
                return;
            }
            BeginVisitorDeparture();
        }

        private void AnswerVisitor(bool accepted)
        {
            if (_visitorStage == VisitorStage.Challenge)
            {
                if (accepted) BeginGhoshDuel();
                else BeginVisitorDeparture();
                return;
            }
            if (_visitorStage != VisitorStage.Choice) return;
            bool lonely = accepted;
            if (!lonely)
            {
                BeginVisitorDeparture();
                return;
            }
            _visitorHasAnswered = true;
            _visitorStage = VisitorStage.Dialogue;
            _visitorLine = 0;
            ShowVisitorLine();
        }

        private void DrawVisitorChoices(Rect box)
        {
            if (_visitorStage is not (VisitorStage.Choice or VisitorStage.Challenge)) return;
            bool challenge = _visitorStage == VisitorStage.Challenge;
            float width = (box.width - 38f) * 0.5f;
            var left = new Rect(box.x + 13f, box.yMax - 34f, width, 24f);
            var right = new Rect(left.xMax + 12f, left.y, width, left.height);
            if (GUI.Button(left, (_visitorReply ? "" : "› ") + (challenge ? T("I decline.", "Eu recuso.") : T("No thank you.", "Não, obrigado.")))) AnswerVisitor(false);
            if (GUI.Button(right, (_visitorReply ? "› " : "") + (challenge ? T("I accept your challenge.", "Aceito o desafio.") :
                T("I feel lonelier.", "Eu me sentiria mais sozinho.")))) AnswerVisitor(true);
        }

        private void BeginGhoshDuel()
        {
            if (_towerEventGhosh) RemoveGenerated(_towerEventGhosh.gameObject);
            ResetVisitorEvent();
            SpawnManagedCreature(FloorTwoVisitorCell, MonsterSpecies.GHOSH, levelOverride: 2, allowSpawnSleep: false,
                visual: _floorTwoGhosh);
            _ghoshDuelActive = true;
            _message = T("The duel with Ghosh begins.", "O duelo com Ghosh começou.");
        }

        private string FinishGhoshDuel(CreatureState opponent, bool won)
        {
            _ghoshDuelActive = false;
            if (!won)
            {
                SpawnFx(_fxMagic, CellPosition(opponent.Cell));
                return T("Ghosh won the duel and left. Koh can still climb the tower.",
                    "Ghosh venceu o duelo e foi embora. Koh ainda pode subir a torre.");
            }

            DropTowerReward(opponent.Cell, ItemSystem.CreateFloorItem(1, ItemSystem.Kind.Herb, 20));
            if (!TownSaveVariables.GetBool(GhoshDuelWon))
            {
                DropTowerReward(opponent.Cell, ItemSystem.CreateFloorItem(3, ItemSystem.Kind.Crystal, 0));
                TownSaveVariables.SetBool(GhoshDuelWon, true);
                TownSaveVariables.Save();
            }
            PlayRetailSound(RetailSfx.MonsterDefeat);
            return T("Koh won the duel against Ghosh.", "Koh venceu o duelo contra Ghosh.");
        }

        private void BeginVisitorDeparture()
        {
            _message = string.Empty;
            _visitorStage = VisitorStage.Depart;
            _visitorTime = 0f;
        }

        private VisitorLine[] CurrentVisitorScript => _selfiVisitorStory == 0
            ? VisitorScripts[(int)_visitorEvent] : SelfiStoryScripts[_selfiVisitorStory];

        private void ShowVisitorLine()
        {
            var line = _visitorHasAnswered
                ? new VisitorLine(false, "Then draw your sword, {name}. Do you accept my challenge?",
                    "Então saque sua espada, {name}. Você aceita o meu desafio?")
                : CurrentVisitorScript[_visitorLine];
            var speaker = line.Selfi ? _towerEventSelfi : _towerEventGhosh;
            if (_towerEventGhosh && _towerEventSelfi)
            {
                bool arguing = _visitorLine < 5;
                _towerEventGhosh.Face(arguing ? _towerEventSelfi.transform.position : _player.transform.position);
                _towerEventSelfi.Face(arguing ? _towerEventGhosh.transform.position : _player.transform.position);
            }
            Texture2D portrait = null;
            if (speaker && speaker.TryGetComponent<CharacterStats>(out var stats)) portrait = stats.portraitTexture;
            if (speaker) speaker.MarkSpoken();
            ShowTownDialogue(line.Selfi ? "Selfi" : "Ghosh", T(line.English, line.Portuguese), portrait);
            _message = _dialogueLine;
        }

        private Vector3 FrameVisitorEvent(Vector3 playerTarget)
        {
            float blend = _visitorStage switch
            {
                VisitorStage.Approach => _visitorTime / VisitorApproachDuration,
                VisitorStage.Dialogue or VisitorStage.Choice or VisitorStage.Challenge => 1f,
                VisitorStage.Depart => 1f - _visitorTime / VisitorMotionDuration,
                _ => 0f
            };
            return Vector3.Lerp(playerTarget, _visitorFocus, Mathf.SmoothStep(0f, 1f, blend) * 0.7f);
        }

        private void ResetVisitorEvent()
        {
            _selfiVisitorStory = 0;
            _selfiEncounterMonster = null;
            _towerEventGhosh = _towerEventSelfi = null;
            _visitorTriggerCells.Clear();
            _visitorApproachPath.Clear();
            _visitorStage = VisitorStage.Finished;
        }
    }
}
