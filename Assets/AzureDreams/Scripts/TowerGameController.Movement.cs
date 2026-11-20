using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;
using AzureDreamsReimagined.Rendering;
using CoreActor = AzureDreams.Core.ActorRecord;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;
using CoreAction = AzureDreams.Core.MonsterAI.ActionType;
using CoreTurns = AzureDreams.Core.TurnSystem;

namespace AzureDreamsReimagined
{
    // Demo movement adapter. Conflict priorities/damage are demo design, not a claim
    // of bit-exact PS1 turn scheduling. Simulation is synchronous; only views interpolate.
    public sealed partial class TowerGameController
    {
        [Header("Movement modes")]
        [SerializeField, Min(0.1f)] private float _townMoveSpeed = 4f;
        [SerializeField, Min(0.02f)] private float _turnDuration = 0.16f;
        private readonly CoreActor _playerActor = new CoreActor();
        private CoreActor _familiarActor = new CoreActor();
        private readonly List<CoreTurns.Intent> _turnIntents = new List<CoreTurns.Intent>(8);
        private CoreDungeon _turnGrid;
        private bool _turnBusy;
        private float _turnElapsed;
        private bool _quickTowerStep;
        private const int CombatMovementRadius = 5;
        private Vector3 _playerFrom, _familiarFrom;
        private int _pendingPlayerDamage;
        private int _pendingFamiliarDamage;
        private bool _familiarDefeated;
        private int _pendingBlueCollarDamage;
        private readonly List<(CoreActor Caster, CoreActor Target, Vector2Int Cell, sbyte Status, bool Reflected)> _pendingMonsterStatuses = new();
        private readonly TowerDamageLedger _creatureDamage = new TowerDamageLedger();
        private readonly List<(CoreActor Attacker, int TargetIndex, int Damage, byte Wand)> _creatureHits = new();
        private readonly Dictionary<CoreActor, (CoreActor Actor, byte Wand)> _finishingBlows = new();
        private readonly List<(CoreActor target, string line, bool needsActiveFamiliar, bool isAttack, CoreActor source, int element)> _turnDamageMessages = new();
        private readonly List<Vector3> _creatureFroms = new List<Vector3>();
        private PsxSpriteRenderer _playerAnimation, _familiarAnimation;
        private bool _townWasMoving;
        private bool _townWasRunningOnGround;
        private float _townWalkCycle;
        private const float TownGravity = 18f;
        private const float TownJumpHeight = 1.8f;
        private void BeginTownJump() => _townJumpVelocity=Mathf.Sqrt(2f*TownGravity*TownJumpHeight);
        private const float TownAirAccelerationRate = 3f;
        private const float TownActorRadius = 0.30f;
        private const float TownActorFootOffset = 0.55f;
        private bool _kohCollapsePlaying;

        private System.Collections.IEnumerator PlayKohCollapse()
        {
            _kohCollapsePlaying = true;
            try
            {
                _message = T("Koh collapses!", "Koh desmaiou!");
                if (_kohSprite) _kohSprite.PresentSleep(true);
                yield return new WaitForSecondsRealtime(1f);

                const float duration = 4f;
                for (float elapsed = 0f; elapsed < duration; elapsed += Time.unscaledDeltaTime)
                {
                    _fadeToBlack = Mathf.Clamp01(elapsed / duration);
                    yield return null;
                }

                _fadeToBlack = 1f;
                ReturnToTown(false);
            }
            finally { _kohCollapsePlaying = false; }
        }

        private const float TownGroundTolerance = 0.001f;
        private float _townJumpHeight, _townJumpVelocity;
        private float _townAirSpeedLimit;
        private Vector2 _townMomentum;
        private Vector2 _townFacing = Vector2.up;
        private ActorLocomotionView _playerLocomotion, _familiarLocomotion;
        private KohSpriteView _kohSprite;
        private readonly List<Rect> _townBuildings = new List<Rect>();
        public bool IsTurnAnimating => _turnBusy;
        public int TowerTurnNumber => CoreTurns.TurnNumber;

        /// <summary>Continuous, normalized, collision-constrained town movement.</summary>
        public void MoveInTown(Vector2 input, float deltaTime) => MoveInTown(input, deltaTime, false);

        private void MoveInTown(Vector2 input, float deltaTime, bool running)
        {
            if (_area != Area.Town || !_player || deltaTime <= 0f || float.IsNaN(deltaTime)) return;
            if (_townFlow && _townFlow.State.Current != TownGameState.State.TownIdle) return;
            if (MoveAuraBike(input, deltaTime)) return;
            if (input.sqrMagnitude > 0.0001f)
            {
                _townFacing = input.normalized;
                if (_kohSprite) _kohSprite.SetFacing(_townFacing);
            }
            RefreshTownNpcCells();
            // Swimming uses the same directional input but deliberately runs at
            // a slower cadence and never uses the town sprint poses.
            if (_poolKohSwimming) running = false;
            float speed = _townMoveSpeed * (running ? 1.6f : 1f) * (_poolKohSwimming ? 0.58f : 1f);
            float stepTime = Mathf.Min(deltaTime, 0.25f);
            Vector3 before = _player.transform.position;
            float startFeet = before.y - TownActorFootOffset;
            float startTerrain = TownTerrainHeight(before);
            float startGround = Mathf.Max(startTerrain, TownObstacleTop(before, TownActorRadius));
            bool grounded = _townJumpVelocity == 0f && startFeet <= startGround + TownGroundTolerance;
            if (grounded)
            {
                _townMomentum = Vector2.ClampMagnitude(input, 1f) * speed;
                _townAirSpeedLimit = speed;
                if (!_poolKohSwimming && _wait != null && _wait.WasPressedThisFrame())
                    BeginTownJump();
            }
            else if (input.sqrMagnitude > 0.0001f)
            {
                // Air input adds to takeoff velocity. Releasing the direction keeps the current drift.
                float acceleration = _townMoveSpeed * TownAirAccelerationRate;
                _townMomentum = Vector2.ClampMagnitude(
                    _townMomentum + Vector2.ClampMagnitude(input, 1f) * (acceleration * stepTime),
                    Mathf.Max(_townMoveSpeed, _townAirSpeedLimit));
            }
            bool canPush = grounded && _townJumpVelocity == 0f;
            Vector3 position = canPush ? ResolveTownPushOverlap(before, input, startFeet) : before;
            bool pushing = PushInTown(position, canPush ? input : Vector2.zero, stepTime);
            Vector2 move = (pushing ? Vector2.ClampMagnitude(_townMomentum, TownPushSpeed) : _townMomentum) * stepTime;
            bool airborne = !grounded || _townJumpVelocity != 0f;
            float feet = startFeet;
            if (airborne)
            {
                feet += _townJumpVelocity * stepTime - 0.5f * TownGravity * stepTime * stepTime;
                _townJumpVelocity -= TownGravity * stepTime;
            }
            // A descending jump can enter over the lid and land in the same frame.
            float clearance = Mathf.Max(startFeet, feet);
            // Small substeps avoid tunnelling through NPCs/walls after a long frame.
            int count = Mathf.Max(1, Mathf.CeilToInt(move.magnitude / 0.1f));
            for (int i = 0; i < count; i++)
            {
                Vector3 next = position + new Vector3(move.x / count, 0f, 0f);
                if (TownPlayerPositionOpen(next, position, clearance)) position = next;
                next = position + new Vector3(0f, 0f, move.y / count);
                if (TownPlayerPositionOpen(next, position, clearance)) position = next;
            }
            if (PoolWaterRequiresSwimsuit(position))
            {
                position = before;
                _townMomentum = Vector2.zero;
                _message = T("Use the changing room before going into the pool.",
                    "Use o vestiário antes de entrar na piscina.");
            }
            float ground = Mathf.Max(TownTerrainHeight(position), TownObstacleTop(position, TownActorRadius));
            bool steppedOffObstacle = !airborne && startGround > startTerrain + TownGroundTolerance &&
                ground < startGround - TownGroundTolerance;
            if (!airborne && !steppedOffObstacle) feet = ground;
            if (feet <= ground && _townJumpVelocity <= 0f)
            {
                feet = ground;
                _townJumpVelocity = 0f;
            }
            _townJumpHeight = Mathf.Max(0f, feet - ground);
            if (_kohSprite) _kohSprite.SetAirborne(_townJumpHeight > TownGroundTolerance);
            position.y = feet + TownActorFootOffset;
            _player.transform.position = position;
            UpdateKohPoolSwimming(position);
            _playerCell = new Vector2Int(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.z));
            var planarDelta = new Vector2(position.x - before.x, position.z - before.z);
            bool moving = planarDelta.sqrMagnitude > 0.000001f;
            _townWalkCycle += planarDelta.magnitude * 0.65f;
            bool runningOnGround = moving && running && !pushing && _townJumpVelocity == 0f &&
                _townJumpHeight <= TownGroundTolerance;
            if (runningOnGround && !_townWasRunningOnGround) SpawnFx(_fxDust, position);
            _townWasRunningOnGround = runningOnGround;
            if (_playerLocomotion) _playerLocomotion.Present(position - before, _townWalkCycle, moving, running && !_poolKohSwimming);
            if (_playerAnimation) _playerAnimation.PlaybackSpeed = moving && running && !_poolKohSwimming ? 2f : 1f;
            if (moving != _townWasMoving && _playerAnimation)
            {
                if (moving) _playerAnimation.Play(); else _playerAnimation.Stop();
            }
            _townWasMoving = moving;
            if (moving)
            {
                if (HasVisibleMessage) _messageStackActivity = Time.unscaledTime;
                CheckTownEntranceCrossing(before, position);
            }
        }

        private float TownTerrainHeight(Vector3 position) =>
            UsingRetailTown
                ? Mathf.Max(TownBridgeTop(position), Mathf.Max(RetailTownGround(position), TownSteppingStoneTop(position)))
                : 0f;

        private bool TownPlayerPositionOpen(Vector3 destination, Vector3 origin, float feetHeight)
        {
            if (UsingRetailTown && TownBridgeTop(destination) > Mathf.Max(feetHeight, TownBridgeTop(origin)) + 0.81f)
                return false;
            return TownPositionOpen(destination, feetHeight);
        }

        private float TownObstacleTop(Vector3 position, float radius, Transform ignored = null)
        {
            float top = Mathf.Max(FountainObstacleTop(position, radius), Mathf.Max(TownItemTop(position, radius, ignored), Mathf.Max(TownPotTop(position, radius, ignored),
                Mathf.Max(TownWallTop(position, radius), TownRoundObstacleTop(position, radius)))));
            return _interior ? Mathf.Max(top, _interior.FurnitureTop(position, radius)) : top;
        }

        private bool TownPositionOpen(Vector3 position, float feetHeight = float.NegativeInfinity,
            Transform ignored = null, float radius = TownActorRadius)
        {
            if (InMonsterShop && !Rect.MinMaxRect(9f, 9f, 22f, 18f).Contains(new Vector2(position.x, position.z))) return false;
            if (_interior && !WithinKohHome(position)) return false;
            if (UsingRetailTown)
            {
                if (!RetailTownGroundOpen(position, radius)) return false;
            }
            else if (position.x < 0.75f || position.z < 0.75f || position.x > MapSize - 1.75f || position.z > MapSize - 1.75f)
                return false;
            foreach (var cell in _walls)
                if (Mathf.Abs(position.x - cell.x) < 0.5f + radius && Mathf.Abs(position.z - cell.y) < 0.5f + radius)
                    return false;
            if (_townPeople.Count == 0)
            {
                foreach (var cell in _npcCells)
                    if (Mathf.Abs(position.x - cell.x) < 0.5f + radius && Mathf.Abs(position.z - cell.y) < 0.5f + radius)
                        return false;
            }
            else
            {
                foreach (var npc in _townPeople)
                {
                    if (!npc || !npc.isActiveAndEnabled || npc.transform == ignored) continue;
                    if (npc.BlocksTownMovement(position, radius)) return false;
                }
            }
            foreach (var door in _townFacadeDoors)
                if (door && door.BlocksFacade(position, radius)) return false;
            foreach (var rectangle in _townBuildings)
                if (position.x > rectangle.xMin - radius && position.x < rectangle.xMax + radius &&
                    position.z > rectangle.yMin - radius && position.z < rectangle.yMax + radius) return false;
            return TownObstacleTop(position, radius, ignored) <= feetHeight + TownGroundTolerance;
        }

        /// <summary>Accept exactly one action when idle; all decisions read the pre-turn snapshot.</summary>
        public bool TryTowerAction(Vector2Int step) => CommitTowerTurn(step, null, Vector2Int.zero);

        private readonly List<MonsterTurnSpriteView> _monsterSpriteActions = new();
        private readonly List<(KewneSpriteView View, Vector2Int Direction, int? TargetHeight)> _kewneActions = new();

        private void QueueMonsterSpriteAction(GameObject actor, Vector2Int direction, bool casting = false, int? targetHeight = null)
        {
            if (!actor) return;
            bool cycloneDash = !casting && actor.GetComponentInChildren<MonsterTurnSpriteView>() is { CycloneView: true };
            PlayRetailSound(casting ? RetailSfx.Spell : cycloneDash ? RetailSfx.CycloneDash : RetailSfx.MonsterAttack);
            if ((casting || targetHeight.HasValue) && actor.TryGetComponent<KewneSpriteView>(out var kewne))
            {
                _kewneActions.Add((kewne, direction, targetHeight));
                if (targetHeight.HasValue) kewne.PresentAttack(direction, 0f, targetHeight.Value);
                else kewne.PresentCast(direction, 0f);
                return;
            }
            var view = actor.GetComponentInChildren<MonsterTurnSpriteView>();
            if (!view) return;
            view.BeginAction(direction, casting);
            _monsterSpriteActions.Add(view);
        }

        private void PresentKewneActions(float progress)
        {
            foreach (var (view, direction, targetHeight) in _kewneActions)
            {
                if (!view) continue;
                if (targetHeight.HasValue) view.PresentAttack(direction, progress, targetHeight.Value);
                else view.PresentCast(direction, progress);
            }
            if (progress >= 1f) _kewneActions.Clear();
        }

        private bool CommitTowerTurn(Vector2Int step, CoreActor pushedActor, Vector2Int pushStep,
            bool familiarCasting = false, Vector2Int? summonCell = null)
        {
            if (_area != Area.Tower || _turnBusy || LevelUpCelebrationBlocksTurn ||
                _towerArrivalPlaying || _elevatorAscending || MonsterDenPlaying ||
                !_player || Mathf.Abs(step.x) + Mathf.Abs(step.y) > 1)
                return false;
            bool executeFamiliarCommands = !KohSkipsAction;
            if (!executeFamiliarCommands)
            {
                step = Vector2Int.zero;
                pushedActor = null;
            }
            else if (step != Vector2Int.zero && pushedActor == null && KohIsConfused)
            {
                var direction = RollTowerDirection(allowDiagonals: false);
                TryTowerFace(direction);
                step = CanTowerStep(_playerCell, direction) ? direction : Vector2Int.zero;
            }
            Vector2Int destination = _playerCell + step;
            bool wait = step == Vector2Int.zero;
            bool pushing = pushedActor != null;
            if (!wait && (!CanTowerStep(_playerCell, step) || (!pushing && CreatureAt(destination) >= 0))) return false;
            if (!wait && _carriedView && destination == _stairCell)
            {
                _message = T("Put down or throw what you are holding before using the elevator.", "Coloque no chÃƒÂ£o ou arremesse o que estÃƒÂ¡ segurando antes de usar o elevador.");
                return false;
            }
            Vector2Int? yieldingDestination = null;
            if (!wait && !pushing)
            {
                var familiar = _familiar && destination == _familiarCell ? _familiarActor :
                    _thrownEggs.Find(ally => ally.View && ally.Turns > 0 && ally.Cell == destination)?.Actor;
                if (familiar != null)
                {
                    if (MonsterExhausted(familiar) || ItemImmobilized(familiar) ||
                        (familiar == _familiarActor && _landingFamiliar)) return false;
                    pushStep = FindFamiliarYieldStep(destination, step);
                    if (pushStep == Vector2Int.zero) return false;
                    pushedActor = familiar;
                    yieldingDestination = destination + pushStep;
                }
            }
            _inspectItemOnArrival = !wait && _interact?.IsPressed() == true;
            if (HasVisibleMessage) _messageStackActivity = Time.unscaledTime;
            PrepareTurnGrid();
            _turnIntents.Clear();
            _kohMixingThisTurn = false;
            _monsterSpriteActions.Clear();
            PresentKewneActions(1f);
            _pendingPlayerDamage = 0;
            _pendingFamiliarDamage = _pendingBlueCollarDamage = 0;
            _pendingMonsterStatuses.Clear();
            _creatureDamage.Clear();
            _creatureHits.Clear();
            _finishingBlows.Clear();
            _turnDamageMessages.Clear();
            _speciesActionMessages = null;
            Vector2Int? reservedPushCell = yieldingDestination ??
                (pushedActor == null ? (Vector2Int?)null : _playerCell + pushStep * 2);
            _pushedEggAlly = pushedActor == null ? null : _thrownEggs.Find(ally => ally.Actor == pushedActor);
            PrepareThrownEggTurn(summonCell ?? reservedPushCell, destination, _pushedEggAlly);
            if (_pushedEggAlly != null)
            {
                SetActorCell(pushedActor, _pushedEggAlly.Cell);
                _pushedEggFrom = _pushedEggAlly.View.transform.position;
                AddIntent(pushedActor, pushStep, CoreTurns.PriorityPlayer);
            }
            foreach (var ally in _thrownEggs)
            {
                if (ally == _pushedEggAlly || !ally.Salamander || !ally.View) continue;
                SetActorCell(ally.Actor, ally.Cell);
                AddIntent(ally.Actor, Vector2Int.zero, CoreTurns.PriorityFamiliar);
            }
            SetActorCell(_playerActor, _playerCell);
            if (_familiar) SetActorCell(_familiarActor, _familiarCell);
            AddIntent(_playerActor, step, CoreTurns.PriorityPlayer);

            // All creatures get their turn
            for (int ci = 0; ci < _creatures.Count; ci++)
            {
                var cs = _creatures[ci];
                SetActorCell(cs.Actor, cs.Cell);
                if (cs.Actor == pushedActor)
                {
                    AddIntent(cs.Actor, IsSacredTile(cs.Cell + pushStep) ? Vector2Int.zero : pushStep, CoreTurns.PriorityPlayer);
                    continue;
                }
                if (cs.Actor == _landingMonster || cs.Actor.Species == RockSpecies)
                {
                    AddIntent(cs.Actor, Vector2Int.zero, CoreTurns.PriorityMonster);
                    continue;
                }
                bool beldo = cs.Actor.Species == (byte)BeldoBossSpecies;
                bool skipsAction = (beldo && !_beldoIntroduced) || MonsterExhausted(cs.Actor) ||
                    ItemImmobilized(cs.Actor) || SlowBlocksAction(cs.Actor);
                bool soloOpponent = beldo || (_ghoshDuelActive && cs.Actor.Species == (byte)MonsterSpecies.GHOSH);
                // Monstros decidem com base em onde Koh vai ficar neste turno (sua acao ja
                // "aconteceu" do ponto de vista da IA), nao em onde ele estava antes de agir -
                // assim eles reagem a posicao final de Koh, igual a um turno sequencial.
                var kohActedCell = destination;
                bool targetFamiliar = !soloOpponent && _familiar && !IsSubmerged(_familiarActor) &&
                    !_manoevaDisguised.ContainsKey(_familiarActor) && !IsSacredTile(_familiarCell) &&
                    (IsSacredTile(kohActedCell) || Distance(cs.Cell, _familiarCell) < Distance(cs.Cell, kohActedCell));
                var targetCell = targetFamiliar ? _familiarCell : kohActedCell;
                bool targetBlue = !soloOpponent && _blueCollarAlly != null && _blueCollarAlly.Actor.Hp > 0 &&
                    !IsSubmerged(_blueCollarAlly.Actor) && !_manoevaDisguised.ContainsKey(_blueCollarAlly.Actor) &&
                    !IsSacredTile(_blueCollarAlly.Cell) &&
                    (IsSacredTile(targetCell) || Distance(cs.Cell, _blueCollarAlly.Cell) < Distance(cs.Cell, targetCell));
                if (targetBlue) targetCell = _blueCollarAlly.Cell;
                bool confused = Combat.HasStatus(cs.Actor, StatusId.CONFUSION);
                bool blind = Combat.HasStatus(cs.Actor, StatusId.BLIND);
                bool fleeing = _holyBellTurns > 0;
                bool canAct = !skipsAction && !IsFrog(cs.Actor) && !fleeing && !confused && !blind;
                if ((HasSpeciesCommand(cs.Actor) || HasInvoluntarySpeciesAction(cs.Actor)) && TryEnemySpeciesAction(cs, canAct,
                    canAct && FamiliarHasSight(cs.Cell, kohActedCell)))
                {
                    AddIntent(cs.Actor, Vector2Int.zero, CoreTurns.PriorityMonster);
                    continue;
                }
                // Enemies lose track of Koh/familiars if too far away
                bool tooFarFromTarget = Distance(cs.Cell, targetCell) > 8;
                bool canAttack = !skipsAction && !IsFrog(cs.Actor) && !IsSubmerged(cs.Actor) &&
                    !fleeing && !IsSacredTile(targetCell) && !tooFarFromTarget;
                // Troll ataca a distancia arremessando uma arma quando alinhado (linha, coluna
                // ou diagonal) e com linha de visao livre - ver select_creature_action_and_target.c,
                // case MONSTER_TROLL, no decomp (ele usa a mesma resolucao de ataque normal, so
                // que a distancia maior, por isso entra aqui e nao no sistema generico de magias).
                bool trollRangedAttack = canAttack && !confused && !blind &&
                    cs.Actor.Species == (byte)MonsterSpecies.TROLL &&
                    Distance(cs.Cell, targetCell) is > 1 and <= 10 &&
                    (targetCell.x == cs.Cell.x || targetCell.y == cs.Cell.y ||
                     Mathf.Abs(targetCell.x - cs.Cell.x) == Mathf.Abs(targetCell.y - cs.Cell.y)) &&
                    FamiliarHasSight(cs.Cell, targetCell, clearShot: true) && RollSpawnRange(4) == 0;
                bool attack = canAttack && (Distance(cs.Cell, targetCell) == 1 || trollRangedAttack);
                var (spellPower, spellStatus, spellId) = canAttack && !confused && !blind && !attack
                    ? PrepareMonsterSpell(cs.Actor, cs.Cell, targetCell) : default;
                if (attack && !confused && !blind)
                {
                    // Mandara (confusao), Zu/Cyclone (paralisia) e Blume (lavagem cerebral)
                    // aplicam seu efeito no ataque corpo a corpo EM VEZ de dano normal, igual ao
                    // decomp (checks de STATUS_* antes de usar a habilidade). Troll nao entra
                    // aqui - o ataque a distancia dele e so dano fisico normal. Viper (veneno)
                    // tambem substitui o dano. Weadog e diferente: ele causa dano normal E
                    // drena MP ao mesmo tempo (ver bloco 'else if (attack)' abaixo), por isso
                    // nao entra em TryStatusOnAttack.
                    if (TryMandaraSpin(cs.Actor)) spellStatus = StatusId.CONFUSION;
                    else if (TryStatusOnAttack(cs.Actor, out var onAttackStatus)) spellStatus = onAttackStatus;
                }
                bool cast = spellPower > 0 || spellStatus != 0;
                var monsterStep = skipsAction || attack || cast || _manoevaDisguised.ContainsKey(cs.Actor) ? Vector2Int.zero : fleeing ? HolyBellStep(cs.Cell) : confused || blind || tooFarFromTarget
                    ? RandomTowerStep(cs.Cell, avoidSacredTiles: true)
                    : ChaseStep(cs.Cell, targetCell, avoidSacredTiles: true);
                if (cs.Cell + monsterStep == summonCell) monsterStep = Vector2Int.zero;
                AddIntent(cs.Actor, monsterStep, CoreTurns.PriorityMonster);
                if (attack || cast) RevertManoevaDisguise(cs.Actor);
                if (cast)
                {
                    var target = targetBlue ? _blueCollarAlly.Actor : targetFamiliar ? _familiarActor : _playerActor;
                    QueueMonsterSpriteAction(cs.View, targetCell - cs.Cell, casting: true);
                    bool spinning = spellId == 0;
                    SpendMonsterMp(cs.Actor, spinning ? 256 : NativeSpellCost(cs.Actor, spellId));
                    bool reflected = !spinning && ReflectMonsterSpell(target);
                    QueueSpellCue(spellId, UnitElement(cs.Actor), cs.Cell, reflected ? cs.Cell : targetCell,
                        reflected ? targetCell : null);
                    if (spellStatus != 0)
                    {
                        _pendingMonsterStatuses.Add((cs.Actor, reflected ? cs.Actor : target, targetCell, spellStatus, reflected));
                        continue;
                    }
                    int damage = SpellOrImpactDamage(reflected ? cs.Actor : target, spellPower,
                        EffectiveSpellLevel(cs.Actor, cs.Actor.Level), UnitElement(cs.Actor), cs.Actor);
                    if (reflected) QueueCreatureDamage(target, ci, damage, element: UnitElement(cs.Actor));
                    else
                    {
                        if (ActiveCombatShield(target)?.Subtype == LiveShield) ApplyLiveShield(target, cs.Actor, damage);
                        if (targetBlue) _pendingBlueCollarDamage += damage;
                        else if (targetFamiliar) _pendingFamiliarDamage += damage;
                        else _pendingPlayerDamage += damage;
                        RecordTowerHit(cs.Actor, target, damage, needsActiveFamiliar: targetFamiliar && !targetBlue,
                            element: UnitElement(cs.Actor));
                    }
                }
                else if (attack)
                {
                    var direction = confused ? RollTowerDirection(allowDiagonals: true) : targetCell - cs.Cell;
                    QueueMonsterSpriteAction(cs.View, direction);
                    SpendMonsterMp(cs.Actor, 256);
                    if (confused)
                    {
                        targetCell = cs.Cell + direction;
                        targetBlue = _blueCollarAlly != null && _blueCollarAlly.Actor.Hp > 0 && targetCell == _blueCollarAlly.Cell;
                        targetFamiliar = _familiar && targetCell == _familiarCell;
                        if ((!targetBlue && !targetFamiliar && targetCell != _playerCell) ||
                            !HasTowerPassage(cs.Cell, direction) || IsSacredTile(targetCell)) continue;
                    }
                    int enemyAttack = cs.Actor.CurrentAttack > 0 ? cs.Actor.CurrentAttack : (_creatureDefinition ? _creatureDefinition.AttackDamage : 2);
                    int damage = Mathf.Max(1, enemyAttack);
                    var target = targetBlue ? _blueCollarAlly.Actor : targetFamiliar ? _familiarActor : _playerActor;
                    damage = WithItemCombatEffects(cs.Actor, damage, target, cs.Cell, targetCell);
                    if (targetBlue) _pendingBlueCollarDamage += damage;
                    else if (targetFamiliar) _pendingFamiliarDamage += damage;
                    else _pendingPlayerDamage += damage;
                    RecordTowerHit(cs.Actor, target, damage, needsActiveFamiliar: targetFamiliar && !targetBlue);
                    // Weadog drena MP no mesmo golpe que causa dano normal (nao substitui o dano,
                    // diferente de Zu/Blume/Viper - ver TryStatusOnAttack).
                    if (cs.Actor.Species == (byte)MonsterSpecies.WEADOG && RollSpawnRange(4) == 0)
                        _pendingMonsterStatuses.Add((cs.Actor, target, targetCell, StatusId.MP_DRAIN, false));
                }
            }
            int familiarTargetIndex = -1;
            bool familiarHasManualOrder = executeFamiliarCommands && _familiar &&
                _pendingFamiliarCommands.ContainsKey(_familiarActor);
            if (_familiar && pushedActor == _familiarActor)
                AddIntent(_familiarActor, pushStep, CoreTurns.PriorityPlayer);
            else if (_familiar && familiarCasting)
            {
                AddIntent(_familiarActor, Vector2Int.zero, CoreTurns.PriorityFamiliar);
                QueueMonsterSpriteAction(_familiar, _towerFacing, casting: true);
            }
            else if (_familiar && !familiarHasManualOrder && _familiarActor.Species == (byte)MonsterSpecies.MANOEVA &&
                !_manoevaDisguised.ContainsKey(_familiarActor) && ManoevaCanDisguise &&
                RollSpeciesAiOnce(_familiarActor, 16) && TryManoevaDisguise(_familiarActor, _familiarCell))
            {
                AddIntent(_familiarActor, Vector2Int.zero, CoreTurns.PriorityFamiliar);
            }
            else if (_familiar && !familiarHasManualOrder &&
                _manoevaDisguised.TryGetValue(_familiarActor, out var familiarItemForm) && familiarItemForm)
            {
                AddIntent(_familiarActor, Vector2Int.zero, CoreTurns.PriorityFamiliar);
            }
            else if (_familiar)
            {
                Vector2Int? blueOrderDestination = executeFamiliarCommands && _blueCollarAlly?.View &&
                    _pendingFamiliarCommands.TryGetValue(_blueCollarAlly.Actor, out var manual) &&
                    manual.Order == ManualOrder.Move ? _blueCollarAlly.Cell + manual.Direction : null;
                var (familiarStep, target) = PlanFamiliarTurn(_familiarActor, _familiar, _familiarCell,
                    destination, summonCell ?? reservedPushCell, blueOrderDestination, executeFamiliarCommands);
                AddIntent(_familiarActor, familiarStep, CoreTurns.PriorityFamiliar);
                familiarTargetIndex = target;
                if (target >= 0)
                {
                    var targetCell = _creatures[target].Cell;
                    var direction = targetCell - _familiarCell;
                    QueueMonsterSpriteAction(_familiar, direction,
                        targetHeight: TowerTileHeight(targetCell) - TowerTileHeight(_familiarCell));
                    SpendMonsterMp(_familiarActor, 256);
                    QueueCreatureDamage(_familiarActor, target, WithItemCombatEffects(_familiarActor, FamiliarWeaponDamage(),
                        _creatures[target].Actor, _familiarCell, targetCell));
                    RevertManoevaDisguise(_familiarActor);
                }
            }

            if (_blueCollarAlly?.View)
            {
                var blue = _blueCollarAlly;
                _blueFamiliarFrom = blue.View.transform.position;
                if (blue != _pushedEggAlly)
                {
                    SetActorCell(blue.Actor, blue.Cell);
                    bool bluePendingCommand = executeFamiliarCommands && _pendingFamiliarCommands.ContainsKey(blue.Actor);
                    if (!bluePendingCommand && blue.Actor.Species == (byte)MonsterSpecies.MANOEVA &&
                        !_manoevaDisguised.ContainsKey(blue.Actor) && ManoevaCanDisguise &&
                        RollSpeciesAiOnce(blue.Actor, 16) && TryManoevaDisguise(blue.Actor, blue.Cell))
                    {
                        AddIntent(blue.Actor, Vector2Int.zero, CoreTurns.PriorityFamiliar);
                    }
                    else if (!bluePendingCommand &&
                        _manoevaDisguised.TryGetValue(blue.Actor, out var blueItemForm) && blueItemForm)
                    {
                        AddIntent(blue.Actor, Vector2Int.zero, CoreTurns.PriorityFamiliar);
                    }
                    else
                    {
                        Vector2Int? redDestination = null;
                        if (_familiar)
                        {
                            var redIntent = _turnIntents.Find(intent => intent.Actor == _familiarActor);
                            redDestination = _familiarCell + new Vector2Int(redIntent.DeltaX, redIntent.DeltaY);
                        }
                        var (blueStep, target) = PlanFamiliarTurn(blue.Actor, blue.View, blue.Cell, destination,
                            summonCell ?? reservedPushCell, redDestination, executeFamiliarCommands);
                        AddIntent(blue.Actor, blueStep, CoreTurns.PriorityFamiliar);
                        if (target >= 0)
                        {
                            var targetCell = _creatures[target].Cell;
                            QueueMonsterSpriteAction(blue.View, targetCell - blue.Cell,
                                targetHeight: TowerTileHeight(targetCell) - TowerTileHeight(blue.Cell));
                            SpendMonsterMp(blue.Actor, 256);
                            QueueCreatureDamage(blue.Actor, target, WithItemCombatEffects(blue.Actor, blue.Actor.CurrentAttack,
                                _creatures[target].Actor, blue.Cell, targetCell));
                            RevertManoevaDisguise(blue.Actor);
                        }
                    }
                }
            }

            _playerFrom = _player.transform.position;
            _familiarFrom = _familiar ? _familiar.transform.position : Vector3.zero;
            _creatureFroms.Clear();
            foreach (var cs in _creatures)
                _creatureFroms.Add(cs.View ? cs.View.transform.position : Vector3.zero);
            bool quietStep = (!executeFamiliarCommands || _pendingFamiliarCommands.Count == 0) &&
                !wait && !pushing && !_carriedView && !InTowerTutorial && _floor != 2 &&
                !_creatures.Exists(creature => Distance(creature.Cell, _playerCell) <= CombatMovementRadius ||
                    Distance(creature.Cell, destination) <= CombatMovementRadius);
            CoreTurns.ResolveTurn(_turnIntents, _turnGrid);
            ApplyFastMovement(pushedActor, summonCell, pushing, executeFamiliarCommands);
            if (executeFamiliarCommands) _pendingFamiliarCommands.Clear();
            foreach (var intent in _turnIntents)
                if (intent.Actor != _playerActor && intent.Actor != pushedActor &&
                    intent.Action == CoreAction.Move && _doubleMpCost.ContainsKey(intent.Actor))
                    SpendMonsterMp(intent.Actor, 256);
            _playerCell = new Vector2Int(_playerActor.TileX, _playerActor.TileY);
            _quickTowerStep = quietStep && _playerCell == destination;
            if (_pushedEggAlly != null)
                _pushedEggAlly.Cell = new Vector2Int(pushedActor.TileX, pushedActor.TileY);
            if (_blueCollarAlly?.View)
                _blueCollarAlly.Cell = new Vector2Int(_blueCollarAlly.Actor.TileX, _blueCollarAlly.Actor.TileY);
            for (int ci = 0; ci < _creatures.Count; ci++)
            {
                var cs = _creatures[ci];
                cs.Cell = new Vector2Int(cs.Actor.TileX, cs.Actor.TileY);
                _creatures[ci] = cs;
            }
            if (_familiar) _familiarCell = new Vector2Int(_familiarActor.TileX, _familiarActor.TileY);
            _turnElapsed = 0f;
            _turnBusy = true;
            Animate(_playerAnimation, _playerFrom != CellPosition(_playerCell));
            for (int ci = 0; ci < _creatures.Count; ci++)
                Animate(_creatures[ci].Animation, _creatureFroms[ci] != CellPosition(_creatures[ci].Cell));
            Animate(_familiarAnimation, _familiar && _familiarFrom != CellPosition(_familiarCell));
            if (_quickTowerStep) PresentTurnLocomotion(0f);
            return true;
        }

        private const sbyte ChaosTurns = 16;

        private bool TryMandaraSpin(CoreActor actor) =>
            actor.Species == (byte)MonsterSpecies.MANDARA && CanCastMagic(actor) && RollSpawnRange(4) == 0;

        // Zu/Cyclone (paralisia), Blume (lavagem cerebral) e Viper (veneno) trocam o dano
        // normal do ataque corpo a corpo por um efeito de status, igual ao decomp (cada um
        // checa o status antes de usar a habilidade em select_creature_action_and_target.c).
        // Weadog NAO entra aqui - ele causa dano normal E drena MP ao mesmo tempo, ver o
        // bloco 'else if (attack)' em CommitMonsterTurn.
        private bool TryStatusOnAttack(CoreActor actor, out sbyte status)
        {
            status = 0;
            switch ((MonsterSpecies)actor.Species)
            {
                case MonsterSpecies.ZU:
                case MonsterSpecies.CYCLONE:
                    status = StatusId.PARALYSIS;
                    break;
                case MonsterSpecies.BLUME:
                    status = StatusId.BRAINWASH;
                    break;
                case MonsterSpecies.VIPER:
                    status = StatusId.POISON;
                    break;
                default:
                    return false;
            }
            return RollSpawnRange(4) == 0;
        }

        private (int Power, sbyte Status, int Spell) PrepareMonsterSpell(CoreActor actor, Vector2Int from, Vector2Int target)
        {
            if (IsTroll(actor) || !CanCastMagic(actor)) return default;
            var unit = UnitProfile(actor.Species);
            int native = unit?.nativeSpellId ?? 0;
            int spell = native != 0 ? native : unit?.hiddenSpellId ?? 0;
            int cost = NativeSpellCost(actor, spell);
            if (cost == 0 || actor.MpRaw < cost && !_mpProtection.ContainsKey(actor)) return default;
            int power = DirectSpellPower(spell);
            sbyte status = DirectSpellStatus(spell);
            if (power == 0 && status == 0) return default;
            var delta = target - from;
            int distance = Distance(from, target);
            if (distance is < 2 or > 4 || (delta.x != 0 && delta.y != 0)) return default;
            var step = new Vector2Int(System.Math.Sign(delta.x), System.Math.Sign(delta.y));
            for (int i = 0; i < distance; i++)
            {
                if (!HasTowerPassage(from, step)) return default;
                from += step;
                if (from != target && BellCellOccupied(from)) return default;
            }
            return RollSpawnRange(4) == 0 ? (power, status, spell) : default;
        }

        private int FindFamiliarBowGunTarget(CoreActor actor, Vector2Int origin)
        {
            var weapon = CombatGear(actor, false);
            if (!IsTroll(actor) || !IsTrollWeapon(weapon) || weapon.Subtype != 15) return -1;
            const int range = 10;
            int closest = range + 1;
            int target = -1;
            foreach (var direction in BellSteps)
            {
                var from = origin;
                for (int distance = 1; distance < closest; distance++)
                {
                    if (!HasTowerPassage(from, direction)) break;
                    from += direction;
                    if (from == _playerCell || (_familiar && actor != _familiarActor && from == _familiarCell) ||
                        _thrownEggs.Exists(ally => ally.View && ally.Cell == from)) break;
                    int enemy = CreatureAt(from);
                    if (enemy < 0) continue;
                    closest = distance;
                    target = enemy;
                    break;
                }
            }
            return target;
        }

        private static int MovementSpeedTurns(CoreActor actor)
        {
            if (actor == null) return 0;
            for (int i = 0; i < 4; i++)
                if (actor.StatusId(i) == StatusId.SPEED) return actor.StatusValue(i);
            // Picket's innate 'Quick' trait (Haste - takes two actions per turn): always hasted
            // when no explicit Speed status (e.g. a Slow trap) overrides it above.
            if (actor.Species == (byte)MonsterSpecies.PICKET) return 2;
            return 0;
        }

        private static bool SlowBlocksAction(CoreActor actor)
        {
            int turns = MovementSpeedTurns(actor);
            return turns < 0 && (turns & 1) == 0;
        }

        private void ApplyFastMovement(CoreActor pushedActor, Vector2Int? summonCell, bool pushing, bool executeFamiliarCommands)
        {
            // Extend a successful move by one tile without starting a second combat turn.
            foreach (var intent in _turnIntents)
            {
                var actor = intent.Actor;
                if (intent.Action != CoreAction.Move || actor == pushedActor || MovementSpeedTurns(actor) <= 0 ||
                    (executeFamiliarCommands && _pendingFamiliarCommands.ContainsKey(actor)) ||
                    (pushing && actor == _playerActor) ||
                    (_holyBellTurns > 0 && intent.Priority == CoreTurns.PriorityMonster)) continue;
                var from = new Vector2Int(actor.TileX, actor.TileY);
                var original = actor == _playerActor ? _playerCell : actor == _familiarActor ? _familiarCell :
                    actor == _blueCollarAlly?.Actor ? _blueCollarAlly.Cell :
                    _creatures.Find(creature => creature.Actor == actor).Cell;
                var step = new Vector2Int(intent.DeltaX, intent.DeltaY);
                if (from != original + step || from == _stairCell || TrapAt(from) is { IsActive: true }) continue;
                if (actor == _playerActor && (_itemMarkers.ContainsKey(from) || (_hasLoot && from == _lootCell) ||
                    InTowerTutorial || (_visitorStage == VisitorStage.Waiting && _visitorTriggerCells.Contains(from)))) continue;
                var next = from + step;
                if (!CanTowerStep(from, step) || !CanClimbTowerHeight(original, next) || next == summonCell ||
                    (actor != _playerActor && (next == _stairCell ||
                    (intent.Priority == CoreTurns.PriorityMonster && IsSacredTile(next))))) continue;
                if (_turnIntents.Exists(other => other.Actor.TileX == next.x && other.Actor.TileY == next.y) ||
                    _thrownEggs.Exists(ally => ally.View && ally.Cell == next)) continue;
                SetActorCell(actor, next);
            }
        }

        private bool CanTowerStep(Vector2Int from, Vector2Int step) =>
            HasTowerPassage(from, step) && CanClimbTowerHeight(from, from + step);

        private bool CanClimbTowerHeight(Vector2Int from, Vector2Int destination) =>
            TowerTileHeight(destination) - TowerTileHeight(from) <= TowerFloorManager.TerrainBlockHeight;

        private bool HasTowerPassage(Vector2Int from, Vector2Int step)
        {
            Vector2Int destination = from + step;
            return InsideMap(destination) && !_walls.Contains(destination) && !_npcCells.Contains(destination) &&
                !CrossesClosedTowerDoor(from, step);
        }

        private Vector2Int FindFamiliarYieldStep(Vector2Int from, Vector2Int approach)
        {
            bool IsOpen(Vector2Int step)
            {
                var cell = from + step;
                return CanTowerStep(from, step) && cell != _stairCell && CreatureAt(cell) < 0 &&
                    (!_familiar || cell != _familiarCell) &&
                    !_thrownEggs.Exists(ally => ally.View && ally.Turns > 0 && ally.Cell == cell) &&
                    !IsRevealedActiveTrap(cell);
            }

            var side = new Vector2Int(-approach.y, approach.x);
            if (IsOpen(side)) return side;
            if (IsOpen(-side)) return -side;
            return IsOpen(approach) ? approach : Vector2Int.zero;
        }

        private void PrepareTurnGrid()
        {
            if (UsesManagedTowerLayout && _towerFloorLayout != null && _towerFloorLayout.IsGenerated)
            {
                _turnGrid = _towerFloorLayout.Dungeon;
                return;
            }
            if (_turnGrid == null) _turnGrid = new CoreDungeon(seed: 0, floorNumber: 1);
            for (int y = 0; y < MapSize; y++)
                for (int x = 0; x < MapSize; x++)
                {
                    var cell = new Vector2Int(x, y);
                    _turnGrid.Grid[y, x] = InsideMap(cell) && !_walls.Contains(cell) && !_npcCells.Contains(cell)
                        ? CoreDungeon.TileType.Floor : CoreDungeon.TileType.Wall;
                }
        }
        private void AddIntent(CoreActor actor, Vector2Int step, int priority)
        {
            _turnIntents.Add(new CoreTurns.Intent { Actor = actor, Action = step == Vector2Int.zero ? CoreAction.Wait : CoreAction.Move,
                DeltaX = step.x, DeltaY = step.y, Priority = priority });
        }
        private static void SetActorCell(CoreActor actor, Vector2Int cell)
        {
            actor.TileX = (sbyte)cell.x; actor.TileY = (sbyte)cell.y;
        }
        private static int Distance(Vector2Int a, Vector2Int b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
        private Vector2Int RollTowerDirection(bool allowDiagonals)
        {
            int count = allowDiagonals ? 8 : 4;
            int direction = _towerRun?.Rng != null ? RollSpawnRange(count) : Random.Range(0, count);
            var (x, y) = DirStep.Step(allowDiagonals ? direction : direction * 2);
            return new Vector2Int(x, y);
        }

        private Vector2Int RandomTowerStep(Vector2Int from, bool avoidSacredTiles = false)
        {
            var step = RollTowerDirection(allowDiagonals: false);
            return CanTowerStep(from, step) && (!avoidSacredTiles || !IsSacredTile(from + step)) ? step : Vector2Int.zero;
        }

        private Vector2Int ChaseStep(Vector2Int from, Vector2Int target, bool avoidSacredTiles = false)
        {
            Vector2Int delta = target - from;
            Vector2Int first = Mathf.Abs(delta.x) >= Mathf.Abs(delta.y)
                ? new Vector2Int(System.Math.Sign(delta.x), 0) : new Vector2Int(0, System.Math.Sign(delta.y));
            Vector2Int second = first.x != 0 ? new Vector2Int(0, System.Math.Sign(delta.y)) : new Vector2Int(System.Math.Sign(delta.x), 0);
            foreach (var step in new[] { first, second })
                if (step != Vector2Int.zero && CanTowerStep(from, step) && from + step != _stairCell &&
                    (!avoidSacredTiles || !IsSacredTile(from + step)))
                    return step;
            return Vector2Int.zero;
        }

        /// <summary>All views share this one clock; no actor runs its own turn coroutine.</summary>
        private bool HopsBetweenTowerBlocks(Vector3 from, Vector2Int cell) =>
            EnteredTowerCell(from, cell) && Mathf.Abs(CellPosition(cell).y - from.y) > 0.01f;

        private Vector3 TowerStepPosition(Vector3 from, Vector2Int cell, float progress)
        {
            var position = Vector3.Lerp(from, CellPosition(cell), progress);
            if (HopsBetweenTowerBlocks(from, cell))
                position.y += 0.28f * (4f * progress * (1f - progress));
            return position;
        }

        public void TickTurnPresentation(float deltaTime)
        {
            if (!_turnBusy || _area != Area.Tower || deltaTime < 0f || float.IsNaN(deltaTime)) return;
            if (_spellPresentationStarted)
            {
                if (TickSpellPresentation(deltaTime)) ResolveTowerTurnEffects();
                return;
            }
            _turnElapsed += deltaTime;
            float duration = _quickTowerStep ? _turnDuration * 0.65f : _turnDuration;
            bool hopsBetweenBlocks = HopsBetweenTowerBlocks(_playerFrom, _playerCell);
            for (int ci = 0; ci < _creatures.Count; ci++)
                if (_creatures[ci].View)
                    hopsBetweenBlocks |= HopsBetweenTowerBlocks(_creatureFroms[ci], _creatures[ci].Cell);
            if (_familiar) hopsBetweenBlocks |= HopsBetweenTowerBlocks(_familiarFrom, _familiarCell);
            if (_blueCollarAlly?.View)
                hopsBetweenBlocks |= HopsBetweenTowerBlocks(_blueFamiliarFrom, _blueCollarAlly.Cell);
            if (_pushedEggAlly?.View && _pushedEggAlly != _blueCollarAlly)
                hopsBetweenBlocks |= HopsBetweenTowerBlocks(_pushedEggFrom, _pushedEggAlly.Cell);
            if (hopsBetweenBlocks) duration = Mathf.Max(duration, 0.22f);
            float t = Mathf.Clamp01(_turnElapsed / Mathf.Max(0.02f, duration));
            if (_player)
            {
                _player.transform.position = TowerStepPosition(_playerFrom, _playerCell, t);
            }
            for (int ci = 0; ci < _creatures.Count; ci++)
                if (_creatures[ci].View) _creatures[ci].View.transform.position = TowerStepPosition(_creatureFroms[ci], _creatures[ci].Cell, t);
            if (_familiar) _familiar.transform.position = TowerStepPosition(_familiarFrom, _familiarCell, t);
            if (_blueCollarAlly?.View)
                _blueCollarAlly.View.transform.position = TowerStepPosition(_blueFamiliarFrom, _blueCollarAlly.Cell, t);
            if (_pushedEggAlly?.View && _pushedEggAlly != _blueCollarAlly)
                _pushedEggAlly.View.transform.position = TowerStepPosition(_pushedEggFrom, _pushedEggAlly.Cell, t);
            PresentTurnLocomotion(t);
            foreach (var view in _monsterSpriteActions)
                if (view) view.PresentAction(t);
            PresentKewneActions(t);
            if (_kohAttacking && _kohSprite) _kohSprite.PresentAttack(_towerFacing, t);
            if (_towerPushing && _kohSprite)
                _kohSprite.PresentPush(_towerPushDirection, t < 1f ? HeldPushPose : 0f);
            if (t >= 1f)
            {
                _towerPushing = false;
            }
            if (t >= 1f) _kohAttacking = false;
            float releaseProgress = ThrowInFlight
                ? Mathf.Clamp01(_turnElapsed / ThrowFlightDuration(_releaseFrom, _releaseTo))
                : t;
            PresentCarry(releaseProgress);
            if (t < 1f || releaseProgress < 1f) return;
            _messageBeforeSpellResolution = _message;
            if (_doubleMpCost.ContainsKey(_playerActor)) SpendMonsterMp(_playerActor, 256);
            TickItemStatuses(_playerActor);
            if (_familiar) TickItemStatuses(_familiarActor);
            else if (_carriedFamiliar) TickItemStatuses(_familiarActor, carried: true);
            for (int i = 0; i < _creatures.Count; i++) TickItemStatuses(_creatures[i].Actor);
            if (_carriedMonster.Actor != null) TickItemStatuses(_carriedMonster.Actor, carried: true);
            if (!_familiar) TickMovementSpeed(_familiarActor);
            if (_carriedMonster.Actor != null) TickMovementSpeed(_carriedMonster.Actor);
            foreach (var reserve in _bagFamiliars) TickMovementSpeed(reserve.Actor);
            foreach (var ally in _thrownEggs) TickItemStatuses(ally.Actor);
            if (_reflectionTurns > 0) _reflectionTurns--;
            if (_holyBellTurns > 0) _holyBellTurns--;
            TickIceBlocks();
            TickSacredTiles();
            ResolveItemCommand();
            Animate(_playerAnimation, false); Animate(_familiarAnimation, false);
            foreach (var cs in _creatures) Animate(cs.Animation, false);
            if (_spellCues.Count > 0)
            {
                _spellPresentationStarted = true;
                PlayRetailSound(RetailSfx.Spell);
                if (!TickSpellPresentation(deltaTime == float.MaxValue ? deltaTime : 0f)) return;
            }
            ResolveTowerTurnEffects();
        }

        private void ResolveTowerTurnEffects()
        {
            _turnBusy = false;
            string messageBeforeResolution = _messageBeforeSpellResolution;
            _messageBeforeSpellResolution = null;
            // Apply already committed effects together, including attacks by a defeated actor.
            if (_pendingUseItem && _runInventory != null && _runInventory.RemoveItem(_pendingUseItem))
            {
                if (_pendingUseItem == _potionItem)
                {
                    PlayRetailSound(RetailSfx.Eat);
                    ApplyMedicinalHerb(_playerActor);
                }
                else AdjustItemHealth(_playerActor, _pendingUseItem.Value);
            }
            _pendingUseItem = null;
            if (!_testInvulnerable && _pendingPlayerDamage > 0)
            {
                _health -= _pendingPlayerDamage;
                CloseMinimap();
            }
            if (_familiar && _pendingFamiliarDamage > 0)
            {
                if (_area == Area.Tower)
                    _familiarHealth -= _pendingFamiliarDamage;
                else
                    _familiarHealth = FamiliarMaxHealth;
            }
            _pendingFamiliarDamage = 0;
            if (_blueCollarAlly != null)
                _blueCollarAlly.Actor.Hp = StatByte(_blueCollarAlly.Actor.Hp - _pendingBlueCollarDamage);
            _pendingBlueCollarDamage = 0;
            if (_familiar) _familiarActor.Hp = StatByte(_familiarHealth);
            // Apply every committed hit before removing dead entries, so indices
            // still refer to the same pre-turn creature snapshot.
            for (int i = 0; i < _creatures.Count; i++)
            {
                var cs = _creatures[i];
                int remaining = cs.Health;
                if (remaining > 0 && _creatureDamage.ForTarget(i) >= remaining)
                {
                    foreach (var hit in _creatureHits)
                    {
                        if (hit.TargetIndex != i) continue;
                        remaining -= hit.Damage;
                        if (remaining > 0) continue;
                        _finishingBlows[cs.Actor] = (hit.Attacker, hit.Wand);
                        break;
                    }
                }
                cs.Health -= _creatureDamage.ForTarget(i);
                cs.Actor.Hp = StatByte(cs.Health);
                _creatures[i] = cs;
            }
            foreach (var hit in _creatureHits)
                if (hit.Wand != 0) ResolveWandHit(hit.Wand, _creatures[hit.TargetIndex].Actor);
            bool lostGhoshDuel = _ghoshDuelActive && _health <= 0;
            bool kewneRescue = CanKewneRescueKoh();
            if (kewneRescue || lostGhoshDuel) _health = 1;
            _playerActor.Hp = StatByte(_health);
            _towerRun?.SetHealth(_health);
            ResolveMonsterWakeUps();
            foreach (var (caster, target, cell, status, reflected) in _pendingMonsterStatuses)
                ApplyDirectSpellStatus(caster, target, cell, status, spread: !reflected);
            _pendingMonsterStatuses.Clear();
            if (_health > 0 && EnteredTowerCell(_playerFrom, _playerCell))
                TriggerTrapFor(_playerCell, _playerActor);
            if (_familiar && _familiarHealth > 0 && EnteredTowerCell(_familiarFrom, _familiarCell))
                TriggerTrapFor(_familiarCell, _familiarActor);
            if (_blueCollarAlly?.View && _blueCollarAlly.Actor.Hp > 0 && EnteredTowerCell(_blueFamiliarFrom, _blueCollarAlly.Cell))
                TriggerTrapFor(_blueCollarAlly.Cell, _blueCollarAlly.Actor);
            for (int i = 0; i < _creatures.Count; i++)
            {
                var creature = _creatures[i];
                if (creature.Health <= 0 || !EnteredTowerCell(_creatureFroms[i], creature.Cell)) continue;
                TriggerTrapFor(creature.Cell, creature.Actor, enemy: true);
                creature.Actor.Hp = StatByte(_creatures[i].Health);
            }
            if (_pushedEggAlly != null && _pushedEggAlly != _blueCollarAlly && EnteredTowerCell(_pushedEggFrom, _pushedEggAlly.Cell))
            {
                TriggerTrapFor(_pushedEggAlly.Cell, _pushedEggAlly.Actor);
                if (_pushedEggAlly.Actor.Hp == 0) _pushedEggAlly.Turns = 0;
            }
            _pushedEggAlly = null;
            _pendingPlayerDamage = 0;
            _creatureDamage.Clear();
            ResolvePoisonDamage();
            string damage = TakeTowerDamageMessages();
            string defeats = null;
            bool defeatedBeldo = false;
            for (int ci = _creatures.Count - 1; ci >= 0; ci--)
            {
                var creature = _creatures[ci];
                bool duelOpponent = _ghoshDuelActive && creature.Actor.Species == (byte)MonsterSpecies.GHOSH;
                if (creature.Health > 0 && !(duelOpponent && lostGhoshDuel)) continue;
                if (duelOpponent)
                    FinishGhoshDuel(creature, !lostGhoshDuel);
                else if (!_revertedActors.Remove(creature.Actor))
                {
                    // update_actor_defeat_removal: 0x806 for Ifrit/Manoeva, 0x805 otherwise;
                    // Baloon's own defeat callback plays its pop (0x807).
                    PlayRetailSound(MonsterDefeatPresenter.IsBaloon(creature.Actor.Species) ? RetailSfx.BaloonPop :
                        MonsterDefeatPresenter.UsesAlternateCue(creature.Actor.Species) ? RetailSfx.MonsterDefeatAlt :
                        RetailSfx.MonsterDefeat);
                    if (creature.CarriesWaterMedal) DropWaterMedal(creature.Cell);
                    // Viper carregando um ovo (TryViperGrabEgg): cai de volta no chao ao ser derrotada.
                    if (_viperCarriedEgg.TryGetValue(creature.Actor, out var carriedEgg))
                    {
                        DropTowerReward(creature.Cell, carriedEgg);
                        _viperCarriedEgg.Remove(creature.Actor);
                    }
                    _finishingBlows.TryGetValue(creature.Actor, out var finisher);
                    string defeat = DescribeDefeatAndGrantExperience(creature.Actor, finisher.Actor);
                    defeats = JoinTowerMessages(defeats, defeat);
                    if (finisher.Wand == MoneyWand)
                        defeats = JoinTowerMessages(defeats, DropMoneyWandCoins(creature));
                    if (_floor == TowerFloorRules.BeldoFloor && creature.Actor.Species == (byte)BeldoBossSpecies)
                        defeatedBeldo = true;
                }
                RetireTowerHitView(creature.View);
                _creatures.RemoveAt(ci);
            }
            if (_familiar && _familiarHealth <= 0)
            {
                RetireTowerHitView(_familiar);
                _familiar = null;
                _familiarDefeated = true;
                _message = T("Your familiar cannot fight and returns to the bag.", "Seu familiar nÃƒÂ£o pode lutar e volta para a bolsa.");
            }
            ResolveSelfiRescue();
            ResolveSpeciesObstacles();
            damage = JoinTowerMessages(damage, _speciesActionMessages);
            TickTowerVitals();
            RestoreExpiredTrapTerrain();
            if (_health > 0 && (kewneRescue || defeatedBeldo))
            {
                StartBeldoEnding(kewneRescue);
                CoreTurns.BeginTurn();
                return;
            }
            if (_health <= 0 || _returnWithCrystal)
            {
                if (_health <= 0) StartCoroutine(PlayKohCollapse());
                else
                {
                    _returnWithCrystal = false;
                    ReturnToTown(true);
                }
                PublishTowerTurnReport(damage, defeats, messageBeforeResolution);
                return;
            }
            var familiarAction = _finishFamiliarAction;
            _finishFamiliarAction = null;
            familiarAction?.Invoke();
            CompleteEggWarming();
            CompleteThrownEggTurn();
            if (_fusionPlaying)
            {
                TickTowerReinforcements();
                PublishTowerTurnReport(damage, defeats, messageBeforeResolution);
                ShowTowerQuakeWarning();
                CoreTurns.BeginTurn();
                return;
            }
            if (_trapGoUp)
            {
                StartCoroutine(PlayGoUpTransition());
                CoreTurns.BeginTurn();
                return;
            }
            if (_trapGoDown)
            {
                StartCoroutine(PlayGoDownTransition());
                CoreTurns.BeginTurn();
                return;
            }
            if (TryStartFloorCollapse())
            {
                CoreTurns.BeginTurn();
                return;
            }
            int completedFloor = _floor;
            CompleteTowerCell();
            if (_area == Area.Tower)
            {
                if (_floor == completedFloor)
                {
                    TickTowerReinforcements();
                    PublishTowerTurnReport(damage, defeats, messageBeforeResolution);
                    ShowTowerQuakeWarning();
                    TryStartMonsterDen();
                }
                CoreTurns.BeginTurn();
            }
        }

        private void QueuePlayerWeaponHit(int targetIndex, Vector2Int targetCell)
        {
            var weapon = CombatGear(_playerActor, false);
            byte wand = weapon?.Kind == ItemSystem.Kind.Wand ? weapon.Subtype : (byte)0;
            var target = _creatures[targetIndex].Actor;
            int damage = WithItemCombatEffects(_playerActor, PlayerWeaponDamage(), target, _playerCell, targetCell);
            QueueCreatureDamage(_playerActor, targetIndex, damage, wand);
        }

        private void QueueCreatureDamage(CoreActor attacker, int targetIndex, int damage, byte wand = 0, int? element = null)
        {
            _creatureDamage.Add(targetIndex, damage);
            if (damage > 0) _creatureHits.Add((attacker, targetIndex, damage, wand));
            RecordTowerHit(attacker, _creatures[targetIndex].Actor, damage, element: element);
        }

        private string TowerCombatName(CoreActor actor)
        {
            if (actor == _playerActor) return HeroName;
            if (actor.Species == (byte)BeldoBossSpecies) return "Beldo";
            if (actor.Species == RockSpecies) return T("Rock", "Pedra");
            if (actor == _familiarActor && _familiarDefinition) return FamiliarDisplayName;
            if (_blueCollarFamiliar?.Actor == actor) return _blueCollarFamiliar.Name;
            return FindEggSpecies(actor.Species)?.name ?? ((AzureDreams.Core.MonsterSpecies)actor.Species).ToString();
        }

        // Red damage number for the tower message log/dialogue box (white by default,
        // see TowerGameController.cs's lazy GUIStyle init). Styles there have richText
        // enabled, so this renders instead of showing the raw tag.
        private static string DamageLogSpan(int damage) => $"<color=#FF5454>{damage}</color>";

        private void RecordTowerHit(CoreActor attacker, CoreActor target, int damage, bool needsActiveFamiliar = false, int? element = null)
        {
            if (damage <= 0) return;
            string source = TowerCombatName(attacker);
            string name = TowerCombatName(target);
            _turnDamageMessages.Add((target, T($"{source} hit {name} for {DamageLogSpan(damage)} damage.",
                $"{source} atingiu {name} e causou {DamageLogSpan(damage)} de dano."), needsActiveFamiliar, true, attacker,
                element ?? (GearElement(CombatGear(attacker, false)) is var weaponElement && weaponElement != 0
                    ? weaponElement : UnitElement(attacker))));
        }

        private void RecordTowerHazard(CoreActor target, int damage, string sourceEn, string sourcePt)
        {
            if (damage <= 0) return;
            string name = TowerCombatName(target);
            _turnDamageMessages.Add((target, T($"{name} took {DamageLogSpan(damage)} damage from {sourceEn}.",
                $"{name} sofreu {DamageLogSpan(damage)} de dano por {sourcePt}."), false, false, null, 0));
        }

        private void ResolveMonsterWakeUps()
        {
            if (_towerRun?.Rng is not { } rng) return;
            // Resolve after the committed actions, before any new sleep from a trap.
            foreach (var hit in _turnDamageMessages)
                if (hit.isAttack && (hit.target != _playerActor || !_testInvulnerable) && (!hit.needsActiveFamiliar || _familiar))
                    TryWakeMonsterFromAttack(hit.target, rng);
        }

        private string TakeTowerDamageMessages()
        {
            string report = null;
            foreach (var hit in _turnDamageMessages)
            {
                if (hit.target == _playerActor && _testInvulnerable) continue;
                if (hit.needsActiveFamiliar && !_familiar) continue;
                if (hit.isAttack) PlayRetailSound(RetailSfx.Hit);
                PresentTowerHit(hit.target, hit.source, hit.element);
                report = JoinTowerMessages(report, hit.line);
            }
            _turnDamageMessages.Clear();
            return report;
        }

        private void PresentTowerHit(CoreActor target, CoreActor source, int element)
        {
            GameObject view;
            if (target == _playerActor) view = _player;
            else if (target == _familiarActor) view = _familiar;
            else
            {
                int enemy = _creatures.FindIndex(creature => creature.Actor == target);
                view = enemy >= 0 ? _creatures[enemy].View : _thrownEggs.Find(ally => ally.Actor == target)?.View;
            }
            if (!view || IsSubmerged(target)) return;

            var away = source == null ? Vector3.zero :
                new Vector3(target.TileX - source.TileX, 0f, target.TileY - source.TileY);
            if (!view.TryGetComponent<ActorLocomotionView>(out var locomotion))
                locomotion = ConfigureLocomotion(view);
            locomotion.PresentHit(away);
            PresentImpactBurst(view.transform.position + Vector3.up * 0.3f, element);
        }

        private void RetireTowerHitView(GameObject view)
        {
            var monster = view ? view.GetComponentInChildren<MonsterTurnSpriteView>() : null;
            float defeatDuration = monster ? MonsterDefeatPresenter.Begin(monster, monster.DefeatSpecies, monster.Facing) : 0f;
            if (defeatDuration > 0f)
            {
                foreach (var collider in view.GetComponentsInChildren<Collider>()) collider.enabled = false;
                StartCoroutine(RemoveAfterDefeat(view, defeatDuration));
                return;
            }
            if (!view || !view.TryGetComponent<ActorLocomotionView>(out var locomotion) || locomotion.HitProgress >= 1f)
            {
                RemoveGenerated(view);
                return;
            }
            foreach (var collider in view.GetComponentsInChildren<Collider>()) collider.enabled = false;
            StartCoroutine(RemoveAfterFlinch(view));
        }

        private System.Collections.IEnumerator RemoveAfterFlinch(GameObject view)
        {
            yield return new WaitForSeconds(ActorLocomotionView.HitDuration);
            if (view) RemoveGenerated(view);
        }

        private System.Collections.IEnumerator RemoveAfterDefeat(GameObject view, float duration)
        {
            yield return new WaitForSeconds(duration);
            if (view) RemoveGenerated(view);
        }

        private void PublishTowerTurnReport(string damage, string defeats, string previousMessage)
        {
            string report = JoinTowerMessages(damage, defeats);
            if (report == null) return;
            _message = JoinTowerMessages(report, _message != previousMessage ? _message : null);
            if (damage != null)
            {
                _combatMessageLines = 1;
                foreach (char character in _message)
                    if (character == '\n') _combatMessageLines++;
            }
        }

        private static string JoinTowerMessages(string first, string second) =>
            string.IsNullOrEmpty(first) ? second : string.IsNullOrEmpty(second) ? first : first + "\n" + second;

        private void CompleteTowerCell()
        {
            if (_hasLoot && _playerCell == _lootCell)
            {
                _towerRun.AddLoot(10 + _floor * 2);
                _runLoot = _towerRun.RunLoot;
                _hasLoot = false;
                if (_salvageMarker) RemoveGenerated(_salvageMarker);
                _salvageMarker = null;
                if (_runInventory != null && _runInventory.HasSpace() && _random.NextDouble() < 0.5)
                {
                    if (_potionItem == null) _potionItem = CreatePotionDefinition();
                    if (_runInventory == null) _runInventory = new Inventory();
                    _runInventory.AddItem(_potionItem, 1);
                }
                _message = T("You found tower salvage. Return safely to town to keep it.", "VocÃƒÂª achou tesouros da torre. Volte em seguranÃƒÂ§a para guardÃƒÂ¡-los.");
            }
            if (_demoItemsConvertedToGold && _towerItemTraps == null && _itemMarkers.ContainsKey(_playerCell))
            {
                CreditCoinPickup(ItemSystem.ConvertToGold(null));
                ConsumeFloorMarker(_itemMarkers, _playerCell);
            }
            CheckGroundItemOnArrival();
            if (InTowerTutorial) CheckTutorialSpot();
            if (_playerCell == _stairCell && CanAscendFromFloor && !_elevatorDeclined && !_carriedView && !ItemImmobilized(_playerActor))
                _checkpointMenu = CheckpointMenu.Ascend;
            else if (_playerCell == _stairCell && InSecondTower && AtSecondTowerTop)
                ShowNoMoreFloors();
            else if (OnSecondTowerElevator && !ItemImmobilized(_playerActor))
                TrySecondTowerElevator(fromInteract: false);
            if (_playerCell != _stairCell && _playerCell != SecondTowerElevatorCell) _elevatorDeclined = false;
            if (_playerCell != SecondTowerElevatorCell) _ascendToSecondTower = false;
        }
        private static void Animate(PsxSpriteRenderer renderer, bool moving)
        {
            if (!renderer) return;
            if (moving) renderer.Play(); else renderer.Stop();
        }
        private void CancelTurn()
        {
            if (_playerLocomotion) _playerLocomotion.Present(Vector3.zero, 0f, false);
            foreach (var cs in _creatures) if (cs.Locomotion) cs.Locomotion.Present(Vector3.zero, 0f, false);
            if (_familiarLocomotion) _familiarLocomotion.Present(Vector3.zero, 0f, false);
            _kohAttacking = false;
            _turnBusy = false; _turnElapsed = 0f;
            _quickTowerStep = false;
            ClearSpellPresentation();
            _messageBeforeSpellResolution = null;
            _pendingUseItem = null;
            _pendingMonsterStatuses.Clear();
            _pendingPoison.Clear();
            _towerFacing = Vector2Int.up;
            _pendingPlayerDamage = _pendingFamiliarDamage = 0;
            _creatureDamage.Clear();
            _creatureHits.Clear();
            _finishingBlows.Clear();
            _turnDamageMessages.Clear();
            _turnIntents.Clear();
            _kohMixingThisTurn = false; _townWasMoving = false;
            _monsterSpriteActions.Clear();
            PresentKewneActions(1f);
            _townWasRunningOnGround = false;
            _playerAnimation = _familiarAnimation = null;
            _playerLocomotion = _familiarLocomotion = null;
            _kohSprite = null;
            _townWalkCycle = 0f;
        }
        private void ConfigurePlayerView()
        {
            _playerLocomotion = ConfigureLocomotion(_player);
            _kohSprite = _player ? _player.GetComponent<KohSpriteView>() : null;
            if (_kohSprite)
            {
                bool inTown = _area == Area.Town;
                // Interiors use a closer camera for tighter room framing, which makes every
                // sprite read larger on screen via perspective - this used to be compensated
                // away here by shrinking Koh's own scale (InteriorCameraDistance/_townCameraDistance,
                // about 0.74x), but no other town resident (TownCharacterSpriteView and friends)
                // applies that compensation, so Koh ended up looking noticeably smaller than
                // every NPC standing next to him indoors. The retail game never corrected for
                // camera-distance perspective changes either (same finding as the tower pitch
                // stretch fix above), so Koh now stays at his authored size everywhere, same as
                // every other character.
                _kohSprite.ConfigurePresentation(_gameCamera, inTown, 1f, GameplayCameraPitch);
                _kohSprite.SetFacing(_area == Area.Tower ? (Vector2)_towerFacing : _townFacing);
            }
            for (int ci = 0; ci < _creatures.Count; ci++)
            {
                var cs = _creatures[ci];
                cs.Locomotion = ConfigureLocomotion(cs.View);
                if (cs.View) cs.Animation = cs.View.GetComponentInChildren<PsxSpriteRenderer>();
                _creatures[ci] = cs;
            }
            _familiarLocomotion = ConfigureLocomotion(_familiar);
            if (_player) _playerAnimation = _player.GetComponentInChildren<PsxSpriteRenderer>();
            if (_familiar) _familiarAnimation = _familiar.GetComponentInChildren<PsxSpriteRenderer>();
            Animate(_playerAnimation, false); Animate(_familiarAnimation, false);
            foreach (var cs in _creatures) Animate(cs.Animation, false);
        }

        private static ActorLocomotionView ConfigureLocomotion(GameObject actor)
        {
            if (!actor) return null;
            if (!actor.TryGetComponent<ActorLocomotionView>(out var view))
                view = actor.AddComponent<ActorLocomotionView>();
            view.Present(Vector3.zero, 0f, false);
            return view;
        }

        private void PresentTurnLocomotion(float progress)
        {
            Vector3 playerDelta = CellPosition(_playerCell) - _playerFrom;
            Vector3 familiarDelta = CellPosition(_familiarCell) - _familiarFrom;
            bool playerMoving = progress < 1f && playerDelta.sqrMagnitude > 0.000001f;
            if (_playerLocomotion) _playerLocomotion.Present(playerDelta, progress, playerMoving);
            if (_player && _frogViews.TryGetValue(_player, out var form) && form.Sprite)
                form.Sprite.PresentMovement(playerDelta, progress, playerMoving);
            for (int ci = 0; ci < _creatures.Count; ci++)
            {
                Vector3 creatureDelta = CellPosition(_creatures[ci].Cell) - _creatureFroms[ci];
                if (_creatures[ci].Locomotion) _creatures[ci].Locomotion.Present(creatureDelta, progress, progress < 1f && creatureDelta.sqrMagnitude > 0.000001f);
            }
            if (_familiarLocomotion) _familiarLocomotion.Present(familiarDelta, progress, progress < 1f && familiarDelta.sqrMagnitude > 0.000001f);
            if (_blueCollarAlly?.View)
            {
                Vector3 delta = CellPosition(_blueCollarAlly.Cell) - _blueFamiliarFrom;
                var step = Vector2Int.RoundToInt(new Vector2(delta.x, delta.z));
                bool moving = progress < 1f && step != Vector2Int.zero;
                var view = _blueCollarAlly.View;
                view.GetComponent<ActorLocomotionView>()?.Present(delta, progress, moving);
                if (moving && view.TryGetComponent<KewneSpriteView>(out var kewne)) kewne.Face(step);
            }
        }
    }
}
