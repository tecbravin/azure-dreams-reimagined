using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Dungeon = AzureDreams.Core.DungeonGenerator;
using Entry = AzureDreams.Core.DungeonItemEntry;

// Independent acceptance harness. No production runtime changes or mocked pickup path.
public class CoderMonsterLifecycleTests
{
    private GameObject _root;
    private Camera _camera;
    private GameObject _npcTemplate;
    private TowerGameController _controller;
    private const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private static readonly Vector2Int[] Directions = { Vector2Int.up, Vector2Int.right, Vector2Int.down, Vector2Int.left };
    private T Get<T>(string name) => (T)typeof(TowerGameController).GetField(name, F).GetValue(_controller);
    private void Set(string name, object value) => typeof(TowerGameController).GetField(name, F).SetValue(_controller, value);
    private IList Creatures => Get<IList>("_creatures");
    private T FirstCreatureField<T>(string member)
    {
        Assert.That(Creatures.Count, Is.GreaterThan(0), "Floor must have a creature");
        object state = Creatures[0];
        return (T)state.GetType().GetField(member).GetValue(state);
    }
    private void SetFirstCreatureCell(Vector2Int cell)
    {
        Assert.That(Creatures.Count, Is.GreaterThan(0));
        object state = Creatures[0];
        state.GetType().GetField("Cell").SetValue(state, cell);
        Creatures[0] = state; // CreatureState is a value type.
    }
    private object Call(string name, params object[] args) => typeof(TowerGameController).GetMethod(name, F).Invoke(_controller, args);
    private TowerFloorManager Floor => Get<TowerFloorManager>("_towerFloorLayout");
    private TowerItemTrapManager Items => Get<TowerItemTrapManager>("_towerItemTraps");
    private TowerRunManager Run => Get<TowerRunManager>("_towerRun");
    private List<GameObject> Generated => Get<List<GameObject>>("_generated");
    private List<Vector2Int> ItemCells => Floor.OpenCells.Where(c => HasItem(c)).ToList();
    private List<Vector2Int> TrapCells => Floor.OpenCells.Where(c => Floor.Dungeon.Grid[c.y, c.x] == Dungeon.TileType.Trap).ToList();
    private bool HasItem(Vector2Int c) => (Floor.Dungeon.GetFlags(c.x, c.y) & (ushort)Dungeon.TileFlag.HasItem) != 0;
    private GameObject Marker(string name, Vector2Int c) => Generated.Find(g => g && g.name == name &&
        Mathf.RoundToInt(g.transform.position.x) == c.x && Mathf.RoundToInt(g.transform.position.z) == c.y);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        Assert.That(Application.isPlaying, Is.True, "Must execute in actual Unity PlayMode");
        Assert.That(Application.companyName, Is.EqualTo("Hermes Coder QA"), "Never clear production PlayerPrefs");
        Assert.That(Application.productName, Is.EqualTo("Tower Run Integration QA"), "Unexpected product name");
        SaveSystem.Clear();
        _root = new GameObject("Independent Generated Items Acceptance");
        _controller = _root.AddComponent<TowerGameController>();
        _camera = new GameObject("Independent Generated Items Camera").AddComponent<Camera>();
        _camera.enabled = false;
        // Fixture supplies the town NPC's explicit dependencies. Baseline tests
        // retain and separately report the unconfigured-fallback startup failure.
        _npcTemplate = new GameObject("QA NPC template with explicit dependencies");
        _npcTemplate.transform.position = new Vector3(-100f, 0f, -100f);
        _npcTemplate.AddComponent<CharacterStats>();
        _npcTemplate.AddComponent<TownMovementController>();
        _npcTemplate.AddComponent<NPCController>();
        _controller.Configure(null, null, _npcTemplate, null, null, null, null, null, null, _camera);
        yield return null; // Real OnEnable + Start, not a replacement controller.
        Set("_familiarDefinition", null);
        Call("EnterTown");
        _controller.ConfigureGeneratedTowerFloors(true);
        Call("EnterTower");

        yield return null; // Drain town objects scheduled for destruction.
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_root) UnityEngine.Object.Destroy(_root);
        if (_camera) UnityEngine.Object.Destroy(_camera.gameObject);
        if (_npcTemplate) UnityEngine.Object.Destroy(_npcTemplate);
        yield return null;
        SaveSystem.Clear();
    }

    private void RemoveCreature()
    {
        // Remove all floor creatures to isolate item/trap lifecycle effects.
        foreach (var state in Creatures)
        {
            var creature = (GameObject)state.GetType().GetField("View").GetValue(state);
            if (creature) { Generated.Remove(creature); UnityEngine.Object.DestroyImmediate(creature); }
        }
        Creatures.Clear();
    }

    // Position the fixture beside a real target; exercise the actual accepted move,
    // resolver and shared presentation completion for the pickup/trigger itself.
    private Vector2Int Approach(Vector2Int target)
    {
        foreach (var direction in Directions)
        {
            var from = target - direction;
            if (from.x <= 0 || from.y <= 0 || from.x >= Dungeon.GridSize - 1 || from.y >= Dungeon.GridSize - 1 ||
                !Floor.IsWalkable(from.x, from.y) || from == Floor.StairsUpCell) continue;
            Set("_playerCell", from);
            Get<GameObject>("_player").transform.position = new Vector3(from.x, .55f, from.y);
            return direction;
        }
        Assert.Fail("Generated target has no walkable cardinal approach: " + target);
        return Vector2Int.zero;
    }

    private void StepOnto(Vector2Int cell)
    {
        var direction = Approach(cell);
        Assert.That(_controller.TryTowerAction(direction), Is.True, "Real move onto generated target must be accepted");
        _controller.TickTurnPresentation(1f);
    }

    private Vector2Int FirstItem()
    {
        var cells = ItemCells;
        Assert.That(cells.Count, Is.GreaterThan(0), "Fixture requires generated items");
        return cells[0];
    }

    private Vector2Int FirstTrap()
    {
        var cells = TrapCells;
        Assert.That(cells.Count, Is.GreaterThan(0), "Fixture requires generated traps");
        return cells[0];
    }

    private static void AssertFreshSleep(AzureDreams.Core.ActorRecord actor)
    {
        for (int i = 0; i < 4; i++)
            if (actor.StatusId(i) == AzureDreams.Core.StatusId.SLEEP)
                Assert.That(actor.StatusValue(i), Is.InRange(8, 40));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NewFloor_UsesFreshEnemyRecordWithoutResettingParty(bool generated)
    {
        if (!generated) { Call("ReturnToTown", true); _controller.ConfigureGeneratedTowerFloors(false); Call("EnterTower"); }
        var previous = FirstCreatureField<AzureDreams.Core.ActorRecord>("Actor");
        previous.SetStatus(0, AzureDreams.Core.StatusId.SLEEP, 60);
        var player = Get<AzureDreams.Core.ActorRecord>("_playerActor");
        var familiar = Get<AzureDreams.Core.ActorRecord>("_familiarActor");
        player.SetStatus(0, AzureDreams.Core.StatusId.SLEEP, 12);
        familiar.SetStatus(0, AzureDreams.Core.StatusId.SLEEP, 13);
        Set("_floor", 3); // Floor 2 is now an NPC event floor, not an enemy lifecycle fixture.
        Call("BuildTowerFloor");
        var fresh = FirstCreatureField<AzureDreams.Core.ActorRecord>("Actor");
        Assert.That(fresh, Is.Not.SameAs(previous), "A new monster must not reuse its predecessor's record");
        AssertFreshSleep(fresh);
        Assert.That(Get<AzureDreams.Core.ActorRecord>("_playerActor"), Is.SameAs(player));
        Assert.That(Get<AzureDreams.Core.ActorRecord>("_familiarActor"), Is.SameAs(familiar));
        Assert.That(AzureDreams.Core.Combat.HasStatus(player, AzureDreams.Core.StatusId.SLEEP), Is.True);
        Assert.That(AzureDreams.Core.Combat.HasStatus(familiar, AzureDreams.Core.StatusId.SLEEP), Is.True);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NewRun_DoesNotInheritDefeatedEnemySleep(bool generated)
    {
        if (!generated) { Call("ReturnToTown", true); _controller.ConfigureGeneratedTowerFloors(false); Call("EnterTower"); }
        var defeated = FirstCreatureField<AzureDreams.Core.ActorRecord>("Actor");
        defeated.SetStatus(0, AzureDreams.Core.StatusId.SLEEP, 60);
        RemoveCreature();
        Call("ReturnToTown", true);
        Call("EnterTower");
        var fresh = FirstCreatureField<AzureDreams.Core.ActorRecord>("Actor");
        Assert.That(fresh, Is.Not.SameAs(defeated));
        AssertFreshSleep(fresh);
        // The adjacent attack below needs an awake monster regardless of the spawn roll.
        for (int i = 0; i < 4; i++)
            if (fresh.StatusId(i) == AzureDreams.Core.StatusId.SLEEP) fresh.SetStatus(i, 0, 0);
        // Put both actors on a controlled open pair; generated placement is not
        // the behavior under test and the authored map is smaller than Floor.
        var from = new Vector2Int(2, 1);
        var target = new Vector2Int(3, 1);
        Get<HashSet<Vector2Int>>("_walls").Remove(from);
        Get<HashSet<Vector2Int>>("_walls").Remove(target);
        if (generated) { Floor.Dungeon.Grid[from.y, from.x] = Dungeon.TileType.Floor; Floor.Dungeon.Grid[target.y, target.x] = Dungeon.TileType.Floor; }
        Set("_playerCell", from); SetFirstCreatureCell(target);
        Get<GameObject>("_player").transform.position = new Vector3(from.x, .55f, from.y);
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(target.x, .55f, target.y);
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        int health = Get<int>("_health");
        _controller.TickTurnPresentation(1f);
        Assert.That(Get<int>("_health"), Is.LessThan(health), "New adjacent enemy must act, not sleep from the previous run");
    }
}
