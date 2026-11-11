using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CoreActor = AzureDreams.Core.ActorRecord;
using CoreStatus = AzureDreams.Core.StatusId;

// Exercises the actual demo controller, not a substitute movement model.
public class TowerMovementIntegrationTests
{
    private bool _previousPotionPickup;
    private GameObject _root;
    private TowerGameController _controller;
    private Camera _camera;
    private static readonly BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private object Call(string name, params object[] args)
    {
        var method = typeof(TowerGameController).GetMethod(name, Flags);
        Assert.That(method, Is.Not.Null, "Controller must implement " + name);
        return method.Invoke(_controller, args);
    }
    private IList Creatures => Get<IList>("_creatures");
    private T FirstCreatureField<T>(string member)
    {
        Assert.That(Creatures.Count, Is.GreaterThan(0), "Floor must have a creature");
        object state = Creatures[0];
        return (T)state.GetType().GetField(member).GetValue(state);
    }
    private void SetCreatureField(string member, object value)
    {
        Assert.That(Creatures.Count, Is.GreaterThan(0));
        object state = Creatures[0];
        state.GetType().GetField(member).SetValue(state, value);
        Creatures[0] = state;
    }
    private T Get<T>(string name) => (T)typeof(TowerGameController).GetField(name, Flags).GetValue(_controller);
    private void Set(string name, object value) => typeof(TowerGameController).GetField(name, Flags).SetValue(_controller, value);
    private GameObject Player => Get<GameObject>("_player");
    private GameObject Creature => FirstCreatureField<GameObject>("View");

    private void KeepTestCreatureAwake()
    {
        var actor = FirstCreatureField<CoreActor>("Actor");
        for (int i = 0; i < 4; i++)
            if (actor.StatusId(i) == CoreStatus.SLEEP) actor.SetStatus(i, 0, 0);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _previousPotionPickup = TownSaveVariables.GetBool("pickup_potion_cache");
        TownSaveVariables.SetBool("pickup_potion_cache", false);
        _root = new GameObject("MovementIntegrationController");
        _controller = _root.AddComponent<TowerGameController>();
        _camera = new GameObject("MovementIntegrationCamera").AddComponent<Camera>();
        _camera.enabled = false;
        _controller.Configure(null, null, null, null, null, null, null, null, null, _camera);
        yield return null; // Real Start/OnEnable lifecycle.
        Set("_familiarDefinition", null);
        Call("EnterTown");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        TownSaveVariables.SetBool("pickup_potion_cache", _previousPotionPickup);
        if (_root) UnityEngine.Object.Destroy(_root);
        if (_camera) UnityEngine.Object.Destroy(_camera.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Town_WalkingFacesDirection_AndResetsVisualPoseWhenIdle()
    {
        Call("MoveInTown", Vector2.right, 0.05f);
        Assert.That(Vector3.Dot(Player.transform.forward, Vector3.right), Is.GreaterThan(0.99f));
        var visual = Player.transform.Find("Locomotion Visual");
        Assert.That(visual, Is.Not.Null, "Static demo actors need a visual locomotion pivot");
        Assert.That(visual.localPosition.y, Is.GreaterThan(0f));
        Call("MoveInTown", Vector2.zero, 0.05f);
        Assert.That(visual.localPosition, Is.EqualTo(Vector3.zero));
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_NearbyPickup_EntersControllerInventoryOnlyOnce()
    {
        var pickupObject = Get<List<GameObject>>("_generated").Find(go => go && go.name == "Potion Pickup");
        Assert.That(pickupObject, Is.Not.Null);
        var pickup = pickupObject.GetComponent<ItemPickup>();
        var bank = Get<Inventory>("_townInventory");
        int before = bank.GetCount(pickup.item);
        Player.transform.position = pickupObject.transform.position + Vector3.right * 0.5f;
        Call("Interact");
        Assert.That(bank.GetCount(pickup.item), Is.EqualTo(before + 1));
        Assert.That(Get<ItemDefinition>("_potionItem"), Is.SameAs(pickup.item));
        Call("Interact");
        Assert.That(bank.GetCount(pickup.item), Is.EqualTo(before + 1), "Deferred Destroy must not permit duplicate collection");
        yield return null;
        Assert.That(pickupObject == null, Is.True);
    }

    [UnityTest]
    public IEnumerator Tower_ReturnDoesNotDuplicateCarriedTownItems()
    {
        var bank = Get<Inventory>("_townInventory");
        var potion = Get<ItemDefinition>("_potionItem");
        int original = bank.GetCount(potion);
        bank.AddItem(potion, 2);
        Call("EnterTower");
        Assert.That(Get<Inventory>("_runInventory").GetCount(potion), Is.EqualTo(original + 2));
        Call("ReturnToTown", true);
        Assert.That(bank.GetCount(potion), Is.EqualTo(original + 2), "Carried items must transfer, not be copied then merged twice");
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_MovementIsContinuous_NotOneTilePerInput()
    {
        var before = Player.transform.position;
        Call("MoveInTown", Vector2.right, 0.05f);
        var delta = Player.transform.position - before;
        Assert.That(delta.x, Is.GreaterThan(0f).And.LessThan(1f));
        Assert.That(delta.z, Is.EqualTo(0f).Within(0.0001f));
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_AirInputAddsVelocityAndReleasingDirectionKeepsMomentum()
    {
        Get<HashSet<Vector2Int>>("_walls").Clear();
        Get<List<Vector2Int>>("_npcCells").Clear();
        Get<List<NPCController>>("_townPeople").Clear();
        Get<List<Rect>>("_townBuildings").Clear();
        Player.transform.position = new Vector3(5f, 0.55f, 5f);
        _controller.MoveInTown(Vector2.zero, 0.05f);
        Set("_townJumpVelocity", Mathf.Sqrt(2f * 18f * 1.8f));
        _controller.MoveInTown(Vector2.zero, 0.05f);
        Vector3 takeoff = Player.transform.position;
        Assert.That(takeoff.x, Is.EqualTo(5f));
        Assert.That(takeoff.y, Is.GreaterThan(0.55f));

        _controller.MoveInTown(Vector2.right, 0.1f);
        Vector3 afterSteering = Player.transform.position;
        float steeredDistance = afterSteering.x - takeoff.x;
        Assert.That(steeredDistance, Is.GreaterThan(0f));
        Assert.That(afterSteering.y, Is.GreaterThan(takeoff.y));

        _controller.MoveInTown(Vector2.zero, 0.1f);
        float coastDistance = Player.transform.position.x - afterSteering.x;
        Assert.That(coastDistance, Is.EqualTo(steeredDistance).Within(0.0001f));

        float beforeBrake = Player.transform.position.x;
        _controller.MoveInTown(Vector2.left, 0.1f);
        Assert.That(Player.transform.position.x - beforeBrake, Is.LessThan(coastDistance));
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_PushSeparatesOverlappingActorsAndMovesTheNpcContinuously()
    {
        Get<HashSet<Vector2Int>>("_walls").Clear();
        Get<List<Vector2Int>>("_npcCells").Clear();
        Get<List<Rect>>("_townBuildings").Clear();
        var people = Get<List<NPCController>>("_townPeople");
        people.Clear();
        var npc = new GameObject("Push target").AddComponent<NPCController>();
        npc.transform.SetParent(_root.transform);
        npc.transform.position = Player.transform.position = new Vector3(5f, 0.55f, 5f);
        people.Add(npc);

        _controller.MoveInTown(Vector2.right, 0.05f);
        Assert.That(npc.transform.position.x - Player.transform.position.x, Is.GreaterThanOrEqualTo(0.72f));
        Assert.That(npc.transform.position.x, Is.EqualTo(5f));
        for (int i = 0; i < 6; i++) _controller.MoveInTown(Vector2.right, 0.05f);
        float pushed = npc.transform.position.x;
        Assert.That(pushed, Is.GreaterThan(5f));
        _controller.MoveInTown(Vector2.right, 0.05f);
        Assert.That(npc.transform.position.x, Is.GreaterThan(pushed));
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_StopsAtNpcWithoutTunneling()
    {
        Player.transform.position = new Vector3(5f, 0.55f, 5f);
        for (int i = 0; i < 20; i++) Call("MoveInTown", Vector2.left, 0.05f);
        Assert.That(Player.transform.position.x, Is.GreaterThan(4.4f));
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_IdleDoesNotAdvanceAnyActor()
    {
        Call("EnterTower");
        var player = Player.transform.position;
        var monster = Creature.transform.position;
        yield return null;
        yield return null;
        Assert.That(Player.transform.position, Is.EqualTo(player));
        Assert.That(Creature.transform.position, Is.EqualTo(monster));
    }

    [UnityTest]
    public IEnumerator Tower_AllMoversUseSameProgress_AndRejectReentry()
    {
        Call("EnterTower");
        KeepTestCreatureAwake();
        Get<HashSet<Vector2Int>>("_walls").Clear();
        SetCreatureField("Cell", new Vector2Int(8, 7));
        Creature.transform.position = new Vector3(8, 0.55f, 7);
        var p0 = Player.transform.position;
        var c0 = Creature.transform.position;
        Assert.That((bool)Call("TryTowerAction", Vector2Int.right), Is.True);
        Assert.That((bool)Call("TryTowerAction", Vector2Int.right), Is.False, "One command per shared animation phase");
        Assert.That(Player.transform.position, Is.EqualTo(p0));
        Assert.That(Creature.transform.position, Is.EqualTo(c0));
        Call("TickTurnPresentation", 0.04f);
        float playerProgress = Vector3.Distance(p0, Player.transform.position);
        float monsterProgress = Vector3.Distance(c0, Creature.transform.position);
        Assert.That(playerProgress, Is.GreaterThan(0).And.LessThan(1));
        Assert.That(monsterProgress, Is.EqualTo(playerProgress).Within(0.0001f));
        Call("TickTurnPresentation", 1f);
        Assert.That(Player.transform.position, Is.EqualTo(p0 + Vector3.right));
        Assert.That(Creature.transform.position, Is.EqualTo(c0 + Vector3.left));
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_BlockedPlayerMoveDoesNotConsumeTurn()
    {
        Call("EnterTower");
        var before = Creature.transform.position;
        Assert.That((bool)Call("TryTowerAction", Vector2Int.left), Is.False);
        Call("TickTurnPresentation", 1f);
        Assert.That(Creature.transform.position, Is.EqualTo(before));
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_WaitConsumesOneSharedTurn()
    {
        Call("EnterTower");
        KeepTestCreatureAwake();
        Get<HashSet<Vector2Int>>("_walls").Clear();
        var before = Player.transform.position;
        var monster = Creature.transform.position;
        Assert.That((bool)Call("TryTowerAction", Vector2Int.zero), Is.True);
        Call("TickTurnPresentation", 1f);
        Assert.That(Player.transform.position, Is.EqualTo(before));
        Assert.That(Vector3.Distance(monster, Creature.transform.position), Is.EqualTo(1f).Within(0.001f));
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_AttacksAreCommittedFromSameSnapshot()
    {
        Call("EnterTower");
        KeepTestCreatureAwake();
        SetCreatureField("Cell", new Vector2Int(2, 1));
        Creature.transform.position = new Vector3(2, 0.55f, 1);
        SetCreatureField("Health", 1);
        Set("_health", 20);
        Assert.That((bool)Call("TryTowerAction", Vector2Int.right), Is.True);
        Call("TickTurnPresentation", 1f);
        Assert.That(Get<int>("_health"), Is.EqualTo(18), "Enemy's already committed attack still occurs in the shared phase");
        Assert.That(Creatures.Count, Is.Zero, "Defeated creature leaves the active floor collection");
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_FamiliarSharesTheSameAnimationClock()
    {
        Call("EnterTower");
        KeepTestCreatureAwake();
        Get<HashSet<Vector2Int>>("_walls").Clear();
        var familiar = new GameObject("FamiliarTest"); familiar.transform.SetParent(_root.transform);
        familiar.transform.position = new Vector3(3, 0.55f, 1);
        Set("_familiar", familiar); Set("_familiarCell", new Vector2Int(3, 1));
        var p0 = Player.transform.position; var c0 = Creature.transform.position; var f0 = familiar.transform.position;
        Assert.That((bool)Call("TryTowerAction", Vector2Int.up), Is.True);
        Call("TickTurnPresentation", 0.04f);
        float progress = Vector3.Distance(p0, Player.transform.position);
        Assert.That(Vector3.Distance(c0, Creature.transform.position), Is.EqualTo(progress).Within(0.0001f));
        Assert.That(Vector3.Distance(f0, familiar.transform.position), Is.EqualTo(progress).Within(0.0001f));
        Call("TickTurnPresentation", 1f);
        Assert.That(familiar.transform.position, Is.EqualTo(new Vector3(2, 0.55f, 1)));
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_CarriedFamiliarExpiresAndLeavesWithoutVacatingKohsTile()
    {
        Call("EnterTower");
        KeepTestCreatureAwake();
        Get<HashSet<Vector2Int>>("_walls").Clear();
        var cell = Get<Vector2Int>("_playerCell");
        Set("_towerFacing", Vector2Int.up);
        SetCreatureField("Cell", cell + Vector2Int.right);
        Creature.transform.position = Player.transform.position + Vector3.right;
        FirstCreatureField<CoreActor>("Actor").HitBaseline = byte.MaxValue;

        var familiar = new GameObject("Carried Familiar Test");
        familiar.transform.SetParent(_root.transform);
        familiar.transform.position = Player.transform.position;
        var actor = new CoreActor { Species = (byte)AzureDreams.Core.MonsterSpecies.KEWNE };
        actor.SetStatus(0, CoreStatus.FROG, 1);
        Set("_familiar", familiar);
        Set("_familiarCell", cell);
        Set("_familiarActor", actor);
        Set("_familiarHealth", 12);
        Set("_health", 20);

        Assert.That(_controller.TryTowerLift(), Is.True);
        Assert.That((actor.TileX, actor.TileY), Is.EqualTo(((sbyte)-1, (sbyte)-1)));
        Assert.That(Get<int>("_pendingPlayerDamage"), Is.Positive);
        Call("TickTurnPresentation", float.MaxValue);
        Assert.That(actor.StatusId(0), Is.Zero, "Frog form expires while the familiar is held.");
        Assert.That(Get<bool>("_carriedFamiliar"), Is.True);

        Call("PutFamiliarInBag");
        Assert.That(Get<bool>("_carriedFamiliar"), Is.False);
        Assert.That(Get<GameObject>("_carriedView"), Is.Null);
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(cell));
        Assert.That(Get<int>("_pendingPlayerDamage"), Is.Positive,
            "Removing the carried familiar must leave Koh targetable on the same cell.");
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_DiagonalSpeedDoesNotExceedCardinalSpeed()
    {
        var before = Player.transform.position;
        Call("MoveInTown", Vector2.one, 0.05f);
        Assert.That(Vector3.Distance(before, Player.transform.position), Is.EqualTo(0.2f).Within(0.0001f));
        yield break;
    }

    [UnityTest]
    public IEnumerator Town_GateEntersTurnBasedTower()
    {
        for (int i = 0; i < 4; i++) Call("MoveInTown", Vector2.right, 0.25f);
        Call("Interact");
        Assert.That(Get<int>("_floor"), Is.Zero, "Gate interaction must preserve its opening ramp");
        for (int frame = 0; frame < 20 && Get<int>("_floor") == 0; frame++) yield return null;
        Assert.That(Get<int>("_floor"), Is.EqualTo(1));
        Assert.That(Player.transform.position, Is.EqualTo(new Vector3(1, 0.55f, 1)));
        Assert.That((bool)Call("TryTowerAction", Vector2Int.zero), Is.True);
        Call("TickTurnPresentation", 1f);
        yield break;
    }

    [UnityTest]
    public IEnumerator Tower_StairsTransitionAfterSharedAnimationCompletes()
    {
        Call("EnterTower"); Get<HashSet<Vector2Int>>("_walls").Clear();
        Set("_playerCell", new Vector2Int(9, 10)); Player.transform.position = new Vector3(9, 0.55f, 10);
        Assert.That((bool)Call("TryTowerAction", Vector2Int.right), Is.True);
        Assert.That(Get<int>("_floor"), Is.EqualTo(1));
        Call("TickTurnPresentation", 1f);
        Assert.That(Get<int>("_floor"), Is.EqualTo(1));
        Assert.That(Get<object>("_checkpointMenu").ToString(), Is.EqualTo("Ascend"));
        Call("AscendFromElevator");
        var rider = Get<GameObject>("_player");
        var platform = Get<Transform>("_elevatorPlatform");
        float riderHeight = rider.transform.position.y;
        float platformHeight = platform.position.y;
        Call("TickElevatorAscent", 0.5f);
        Assert.That(Get<int>("_floor"), Is.EqualTo(1));
        Assert.That(rider.transform.position.y, Is.GreaterThan(riderHeight));
        Assert.That(rider.transform.position.y - riderHeight, Is.EqualTo(platform.position.y - platformHeight).Within(0.001f));
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.False);
        Call("TickElevatorAscent", 1f);
        Assert.That(Get<int>("_floor"), Is.EqualTo(2));
        Assert.That(_controller.IsTurnAnimating, Is.False);
        Assert.That((bool)Call("TryTowerAction", Vector2Int.zero), Is.True);
        Call("TickTurnPresentation", 1f);
        yield break;
    }

    [UnityTest]
    public IEnumerator Transition_CancelsOldTurn_AndTownMovesFreely()
    {
        Call("EnterTower");
        Assert.That((bool)Call("TryTowerAction", Vector2Int.right), Is.True);
        Call("EnterTown");
        var before = Player.transform.position;
        Call("TickTurnPresentation", 1f);
        Assert.That(Player.transform.position, Is.EqualTo(before));
        Call("MoveInTown", Vector2.right, 0.05f);
        Assert.That(Player.transform.position.x, Is.GreaterThan(before.x));
        yield break;
    }
}
