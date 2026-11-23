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
        private enum VisitorSpeaker { Koh, Ghosh, Selfi, Narrator }
        private enum VisitorStage { Waiting, Approach, Dialogue, Choice, Challenge, DuelIntro, DuelResult, Depart, Finished }
        private const float VisitorMotionDuration = 0.45f;
        private readonly struct VisitorLine
        {
            public readonly VisitorSpeaker Speaker;
            public readonly string English, Portuguese;
            public readonly int PortraitVariant;
            public VisitorLine(bool selfi, string english, string portuguese, int portraitVariant = 0)
                : this(selfi ? VisitorSpeaker.Selfi : VisitorSpeaker.Ghosh, english, portuguese, portraitVariant)
            {
            }

            public VisitorLine(VisitorSpeaker speaker, string english, string portuguese, int portraitVariant = 0)
            {
                Speaker = speaker;
                English = english;
                Portuguese = portuguese;
                PortraitVariant = portraitVariant;
            }
        }

        // NTSC DUNGEON.BIN 0x7D152C.
        // Koh is inserted where the original script uses the player's name token.
        private static readonly VisitorLine[] GhoshOpeningScript =
        {
            new(false, "Huh, it’s just {name}. You feel lonely and want my help, don’t you?",
                "Ah, é só o {name}. Está se sentindo sozinho e quer a minha ajuda, não é?"),
            new(false, "Do you wish to stay close to me?", "Quer ficar perto de mim?")
        };

        // DUNGEON.BIN 0x7D152C: Ghosh has three distinct replies after his
        // first question. Keeping them as separate pages preserves the retail
        // pacing before the duel (or his departure).
        private static readonly VisitorLine[] GhoshModestyScript =
        {
            new(false, "Stupid {name} finally learned about being modest toward noble people.",
                "O tolo do {name} finalmente aprendeu a ser modesto diante de pessoas nobres.")
        };

        private static readonly VisitorLine[] GhoshChallengeScript =
        {
            new(false, "Hummmm. How rude! Your speech and behavior are not very courteous.",
                "Hummmm. Que rude! Sua fala e seu comportamento não são nada corteses.", 1),
            new(false, "I cannot allow it any more. We shall battle right now, right here!",
                "Não posso mais permitir isso. Nós vamos lutar agora mesmo, aqui mesmo!", 1)
        };

        private static readonly VisitorLine[] GhoshChallengeRefusalScript =
        {
            new(false, "You’re afraid of me, aren’t you? Well, that’s fine. Be careful in the future.",
                "Você está com medo de mim, não está? Pois bem. Tome cuidado no futuro.")
        };

        private static readonly VisitorLine[] GhoshDuelVictoryScript =
        {
            new(false, "W w wait.. oh no, wait. I’ll give you this herb.", "E-espera... Ah, espera. Vou te dar esta erva.", 1),
            new(false, "Ummmmm... I guess I let my guard down a little.", "Hmmmmm... Acho que baixei um pouco a guarda.", 1),
            new(false, "I’ll be back.", "Eu voltarei.")
        };

        private static readonly VisitorLine[] GhoshDuelLossScript =
        {
            new(false, "Huh, you are worse than rust on a sword.", "Hã, você é pior do que ferrugem numa espada.")
        };

        private TowerEventNpc _towerEventGhosh, _towerEventSelfi;
        private VisitorEvent _visitorEvent;
        private VisitorStage _visitorStage = VisitorStage.Finished;
        private int _visitorLine;
        private bool _visitorReply;
        private bool _selfiPulunpaAccepted;
        private bool _selfiPulunpaSecondPlea;
        private bool _selfiPulunpaDeclined;
        private bool _ghoshDuelActive;
        private VisitorLine[] _visitorScriptOverride;
        private const string GhoshDuelWon = "tower.ghosh_duel_won";
        private const string GhoshDuelWinsKey = "tower.ghosh_duel_wins";
        private const string GhoshDuelLossesKey = "tower.ghosh_duel_losses";
        private float _visitorTime;
        private Vector3 _visitorFocus;
        private readonly List<Vector2Int> _visitorApproachPath = new();
        private const int VisitorTriggerSteps = 2;
        private static readonly Vector2Int FloorTwoVisitorCell = new(31, 32);
        private static readonly Vector2Int[] VisitorSteps =
            { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
        private readonly HashSet<Vector2Int> _visitorTriggerCells = new();
        private string _visitorPortraitPath;
        private float _visitorPortraitStarted;
        public bool IsTowerEventFloor => _area == Area.Tower && _floor == 2;
        private bool IsShowingVisitorDialogue => _visitorStage is VisitorStage.Approach or VisitorStage.Dialogue or VisitorStage.Choice or
            VisitorStage.Challenge or VisitorStage.DuelIntro or VisitorStage.DuelResult;

        private void SpawnTowerEventNpc()
        {
            _selfiVisitorStory = PendingSelfiEncounter();
            _selfiPulunpaAccepted = false;
            _selfiPulunpaSecondPlea = false;
            _selfiPulunpaDeclined = false;
            _visitorScriptOverride = null;
            _visitorEvent = _selfiVisitorStory == SelfiGhoshEncounter ? VisitorEvent.Argument :
                _selfiVisitorStory == 0 ? (VisitorEvent)RollSpawnRange((int)VisitorEvent.Argument + 1) : VisitorEvent.Selfi;
            var secondCell = FloorTwoVisitorCell + Vector2Int.right;
            if (_visitorEvent != VisitorEvent.Selfi)
                _towerEventGhosh = SpawnVisitor(_floorTwoGhosh, FloorTwoVisitorCell, "Ghosh");
            if (_visitorEvent != VisitorEvent.Ghosh)
                _towerEventSelfi = SpawnVisitor(_floorTwoSelfi,
                    _visitorEvent == VisitorEvent.Argument ? secondCell : FloorTwoVisitorCell, "Selfi");
            if (_towerEventGhosh) StartCoroutine(MatchGhoshHeightToKoh(_towerEventGhosh, _player));
            BuildVisitorTriggerCells(secondCell);
            _visitorStage = VisitorStage.Waiting;
        }

        private System.Collections.IEnumerator MatchGhoshHeightToKoh(TowerEventNpc visitor, GameObject koh)
        {
            // Both sprite views need a frame assigned before measuring their height.
            yield return null;
            if (!visitor || !koh || visitor != _towerEventGhosh || koh != _player) yield break;

            var kohSprite = koh.GetComponentInChildren<SpriteRenderer>();
            var ghoshSprite = visitor.GetComponentInChildren<SpriteRenderer>();
            if (!kohSprite || !kohSprite.sprite || !ghoshSprite || !ghoshSprite.sprite) yield break;

            float kohHeight = kohSprite.sprite.bounds.size.y * Mathf.Abs(kohSprite.transform.lossyScale.y);
            float ghoshHeight = ghoshSprite.sprite.bounds.size.y * Mathf.Abs(ghoshSprite.transform.lossyScale.y);
            if (kohHeight <= 0f || ghoshHeight <= 0f) yield break;

            // Keep the sprite's foot point fixed when scaling its parent.
            var feet = new Vector3(0f, ghoshSprite.sprite.bounds.min.y, 0f);
            float footHeight = ghoshSprite.transform.TransformPoint(feet).y;
            visitor.transform.localScale *= kohHeight / ghoshHeight;
            ghoshSprite.transform.position += Vector3.up *
                (footHeight - ghoshSprite.transform.TransformPoint(feet).y);
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
            bool retail=character=="Ghosh" && RetailResidentBanks.Apply(view,"ghosh");
            var sprite = retail ? null : view.GetComponentInChildren<MonsterTurnSpriteView>();
            view.transform.position = new Vector3(cell.x, TowerGroundY(cell) + (sprite ? 0.55f : 0f), cell.y);
            return view.AddComponent<TowerEventNpc>();
        }

        private bool TryTowerEventInteraction()
        {
            if (!IsTowerEventFloor || _visitorStage != VisitorStage.Waiting || !_player ||
                _inventoryOpen || _inventoryMenu != InventoryMenu.Closed ||
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
            _visitorReply = false;
            _visitorTime = 0f;
            _visitorStage = VisitorStage.Approach;
            if (_towerEventSelfi) PlayAreaMusic(SelfiMusicCue);
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
            if (_visitorStage is VisitorStage.Dialogue or VisitorStage.DuelIntro or VisitorStage.DuelResult)
            {
                if (_interact != null && _interact.WasPressedThisFrame()) AdvanceVisitorDialogue();
                return true;
            }

            if (_visitorStage is VisitorStage.Choice or VisitorStage.Challenge)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    if (MenuDirectionPressed(3)) _visitorReply = false;
                    if (MenuDirectionPressed(4)) _visitorReply = true;
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
                    if (_selfiVisitorStory == SelfiUBoatRescue)
                        SpawnSelfiEncounterMonster(AzureDreams.Core.MonsterSpecies.U_BOAT);
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
                    if (_playingMusicCue == SelfiMusicCue) PlayAreaMusic(TowerMusicCue(_floor));
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
            if (++_visitorLine < CurrentVisitorScript.Length)
            {
                ShowVisitorLine();
                return;
            }
            if (_visitorStage == VisitorStage.DuelIntro)
            {
                BeginGhoshDuel();
                return;
            }
            if (_visitorStage == VisitorStage.DuelResult)
            {
                ResetVisitorEvent();
                _message = string.Empty;
                return;
            }
            if (_visitorEvent == VisitorEvent.Ghosh && _visitorScriptOverride == GhoshChallengeScript)
            {
                _visitorScriptOverride = null;
                _visitorStage = VisitorStage.Challenge;
                _visitorReply = false;
                return;
            }
            if (_visitorScriptOverride != null)
            {
                _visitorScriptOverride = null;
                BeginVisitorDeparture();
                return;
            }
            if (SelfiVisitorChoices() != default)
            {
                _visitorStage = VisitorStage.Choice;
                _visitorReply = false;
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
                if (accepted) ShowGhoshDuelRecord();
                else
                {
                    _visitorScriptOverride = GhoshChallengeRefusalScript;
                    _visitorStage = VisitorStage.Dialogue;
                    _visitorLine = 0;
                    ShowVisitorLine();
                }
                return;
            }
            if (_visitorStage != VisitorStage.Choice) return;
            if (_selfiVisitorStory == SelfiPulunpaRescue)
            {
                if (!accepted)
                {
                    _selfiPulunpaAccepted = true;
                    BeginVisitorDeparture();
                    return;
                }

                if (!_selfiPulunpaSecondPlea)
                {
                    _selfiPulunpaSecondPlea = true;
                    _visitorStage = VisitorStage.Dialogue;
                    _visitorLine = 0;
                    ShowVisitorLine();
                    return;
                }
                _selfiPulunpaDeclined = true;
                _visitorStage = VisitorStage.Dialogue;
                _visitorLine = 0;
                ShowVisitorLine();
                return;
            }
            var selfiReply = SelfiVisitorReply(right: accepted);
            if (selfiReply != null)
            {
                _visitorScriptOverride = selfiReply;
                _visitorStage = VisitorStage.Dialogue;
                _visitorLine = 0;
                ShowVisitorLine();
                return;
            }
            if (_visitorEvent == VisitorEvent.Ghosh)
            {
                _visitorScriptOverride = accepted ? GhoshChallengeScript : GhoshModestyScript;
                _visitorStage = VisitorStage.Dialogue;
                _visitorLine = 0;
                ShowVisitorLine();
                return;
            }
            BeginVisitorDeparture();
        }

        private void DrawVisitorChoices(Rect choices)
        {
            if (_visitorStage is not (VisitorStage.Choice or VisitorStage.Challenge)) return;
            bool challenge = _visitorStage == VisitorStage.Challenge;
            var selfiChoices = SelfiVisitorChoices();
            string leftLabel = challenge ? T("I don't have time for that.", "Não tenho tempo para isso.") :
                selfiChoices.Left ?? T("No thank you.", "Não, obrigado.");
            string rightLabel = challenge ? T("I accept your challenge.", "Aceito o desafio.") :
                selfiChoices.Right ?? T("I feel lonelier.", "Eu me sentiria mais sozinho.");
            int reply = DrawDialogueChoices(choices, _visitorReply ? 1 : 0, leftLabel, rightLabel);
            if (reply >= 0) AnswerVisitor(reply == 1);
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

        private static int GhoshDuelWins => TownSaveVariables.GetInt(GhoshDuelWinsKey,
            TownSaveVariables.GetBool(GhoshDuelWon) ? 1 : 0);

        // The original shows prior results after accepting the challenge, before combat.
        private void ShowGhoshDuelRecord()
        {
            int wins = GhoshDuelWins;
            int losses = TownSaveVariables.GetInt(GhoshDuelLossesKey);
            BeginGhoshDuelDialogue(VisitorStage.DuelIntro, new[]
            {
                new VisitorLine(VisitorSpeaker.Narrator, $"So, {wins} wins and {losses} losses for you.",
                    $"Então, você tem {wins} vitórias e {losses} derrotas.")
            });
        }

        private void BeginGhoshDuelDialogue(VisitorStage stage, VisitorLine[] script)
        {
            _visitorScriptOverride = script;
            _visitorStage = stage;
            _visitorLine = 0;
            ShowVisitorLine();
        }

        private void FinishGhoshDuel(CreatureState opponent, bool won)
        {
            _ghoshDuelActive = false;
            if (!won)
            {
                SpawnFx(_fxMagic, CellPosition(opponent.Cell));
                TownSaveVariables.SetInt(GhoshDuelLossesKey, TownSaveVariables.GetInt(GhoshDuelLossesKey) + 1);
                TownSaveVariables.Save();
                BeginGhoshDuelDialogue(VisitorStage.DuelResult, GhoshDuelLossScript);
                return;
            }

            TownSaveVariables.SetInt(GhoshDuelWinsKey, GhoshDuelWins + 1);
            DropTowerReward(opponent.Cell, ItemSystem.CreateFloorItem(1, ItemSystem.Kind.Herb, 20));
            if (!TownSaveVariables.GetBool(GhoshDuelWon))
            {
                DropTowerReward(opponent.Cell, ItemSystem.CreateFloorItem(3, ItemSystem.Kind.Crystal, 0));
                TownSaveVariables.SetBool(GhoshDuelWon, true);
            }
            TownSaveVariables.Save();
            PlayRetailSound(RetailSfx.MonsterDefeat);
            BeginGhoshDuelDialogue(VisitorStage.DuelResult, GhoshDuelVictoryScript);
        }

        private void BeginVisitorDeparture()
        {
            _message = string.Empty;
            _visitorStage = VisitorStage.Depart;
            _visitorTime = 0f;
        }

        private VisitorLine[] CurrentVisitorScript => _visitorScriptOverride ??
            (_selfiVisitorStory == 0
                ? _visitorEvent switch
                {
                    VisitorEvent.Argument => SelfiArgumentScript(),
                    VisitorEvent.Selfi => SelfiStoryScripts[SelfiTowerIntroductions],
                    _ => GhoshOpeningScript
                }
                : _selfiVisitorStory == SelfiPulunpaRescue && _selfiPulunpaDeclined
                    ? SelfiPulunpaDeclinedScript : _selfiVisitorStory == SelfiPulunpaRescue && _selfiPulunpaSecondPlea
                        ? SelfiPulunpaSecondPleaScript : _selfiVisitorStory == SelfiGhoshEncounter
                            ? SelfiArgumentScript() : SelfiStoryScripts[_selfiVisitorStory]);

        private void ShowVisitorLine()
        {
            // Floor 2 visitor conversations use the same single-line dialogue
            // presentation as town NPCs, not the tower's combat message history.
            _message = string.Empty;
            var script = CurrentVisitorScript;
            var line = script[_visitorLine];
            var speaker = line.Speaker switch
            {
                VisitorSpeaker.Selfi => _towerEventSelfi,
                VisitorSpeaker.Ghosh => _towerEventGhosh,
                _ => null
            };
            if (_towerEventGhosh && _towerEventSelfi)
            {
                // Both actors turn to Koh for the lines that acknowledge him.
                bool arguing = _visitorLine < (script == SelfiRefusedRescueArgument ? 2 : 5);
                _towerEventGhosh.Face(arguing ? _towerEventSelfi.transform.position : _player.transform.position);
                _towerEventSelfi.Face(arguing ? _towerEventGhosh.transform.position : _player.transform.position);
            }
            _visitorPortraitPath = line.Speaker switch
            {
                VisitorSpeaker.Selfi => SelfiPortrait(line.PortraitVariant),
                VisitorSpeaker.Ghosh => GhoshPortraitPath(line.PortraitVariant),
                _ => null
            };
            _visitorPortraitStarted = Time.unscaledTime;
            Texture2D portrait = StreamPortrait(_visitorPortraitPath);
            if (!portrait && speaker && speaker.TryGetComponent<CharacterStats>(out var stats))
                portrait = stats.portraitTexture;
            if (speaker) speaker.MarkSpoken();
            ShowTownDialogue(line.Speaker switch
            {
                VisitorSpeaker.Selfi => "Selfi",
                VisitorSpeaker.Ghosh => "Ghosh",
                VisitorSpeaker.Narrator => string.Empty,
                _ => "Koh"
            }, T(line.English, line.Portuguese), portrait);
        }

        private Vector3 FrameVisitorEvent(Vector3 playerTarget)
        {
            float blend = _visitorStage switch
            {
                VisitorStage.Approach => _visitorTime / VisitorApproachDuration,
                VisitorStage.Dialogue or VisitorStage.Choice or VisitorStage.Challenge or VisitorStage.DuelIntro => 1f,
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
            _visitorPortraitPath = null;
            _visitorScriptOverride = null;
            _visitorStage = VisitorStage.Finished;
        }
    }
}
