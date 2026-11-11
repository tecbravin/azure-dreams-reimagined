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
public class CoderGeneratedLifecycleTests
{
    private const string ExpectedCompanyName = "Hermes Coder QA";
    private const string ExpectedProductName = "Tower Run Integration QA";
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
        Assert.That(Application.companyName, Is.EqualTo(ExpectedCompanyName), "Never clear production PlayerPrefs");
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
        RemoveCreature(); // Isolate item/trap effects from unrelated enemy damage.
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

    [UnityTest]
    public IEnumerator Pickup_RemovesItsVisibleMarkerAfterDeferredDestroy()
    {
        var cell = FirstItem();
        var marker = Marker("Tower Item", cell);
        Assert.That(marker, Is.Not.Null);
        StepOnto(cell);
        yield return null;
        Assert.That(marker == null || !marker.activeInHierarchy, Is.True,
            "Consumed item still has an active visible Tower Item marker at " + cell);
    }

    [UnityTest]
    public IEnumerator ConsumedTrap_DoesNotDamageAgainOnWaitOrReentry()
    {
        var cell = FirstTrap();
        StepOnto(cell);
        int health = Get<int>("_health");
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        StepOnto(cell);
        Assert.That(Get<int>("_health"), Is.EqualTo(health));
        Assert.That(Items.TriggeredTrapCount, Is.EqualTo(1));
        yield break;
    }

    [UnityTest]
    public IEnumerator TriggeredTrap_RemovesItsVisibleMarkerAfterDeferredDestroy()
    {
        var cell = FirstTrap();
        var marker = Marker("Tower Trap", cell);
        Assert.That(marker, Is.Not.Null);
        StepOnto(cell);
        yield return null;
        Assert.That(marker == null || !marker.activeInHierarchy, Is.True,
            "Consumed trap still has an active visible Tower Trap marker at " + cell);
    }

    [UnityTest]
    public IEnumerator GeneratedNonCurrencyPickup_IsRetainedInCarriedInventory()
    {
        // The initial adapter maps row-major cells to the next occupied slot.
        var map = (Dictionary<Vector2Int, int>)typeof(TowerItemTrapManager).GetField("_cellToSlot", F).GetValue(Items);
        var pair = map.First(p => Floor.Dungeon.FloorItems[p.Value] != null &&
            Floor.Dungeon.FloorItems[p.Value].Kind != AzureDreams.Core.ItemKind.Coin);
        var entry = Floor.Dungeon.FloorItems[pair.Value];
        int carried = Run.RunInventory.TotalItems;
        StepOnto(pair.Key);
        Assert.That(Run.RunInventory.TotalItems, Is.EqualTo(carried + 1),
            "Non-currency record kind=" + entry.Kind + " subtype=" + entry.Subtype + " was deleted instead of retained as an item");
        yield break;
    }

    [UnityTest]
    public IEnumerator NonCurrencyParameter_IsNotSilentlyConvertedToGold()
    {
        var map = (Dictionary<Vector2Int, int>)typeof(TowerItemTrapManager).GetField("_cellToSlot", F).GetValue(Items);
        var pair = map.First(p => Floor.Dungeon.FloorItems[p.Value] != null &&
            Floor.Dungeon.FloorItems[p.Value].Kind != AzureDreams.Core.ItemKind.Coin);
        int loot = Run.RunLoot;
        StepOnto(pair.Key);
        Assert.That(Run.RunLoot, Is.EqualTo(loot),
            "DungeonItemEntry.Param is kind-specific, not a universal currency value; no sale was requested");
        yield break;
    }

    [UnityTest]
    public IEnumerator BusyMoveRejectsReentry_AndCollectsOnlyOnce()
    {
        var cell = FirstItem();
        Assert.That(_controller.TryTowerAction(Approach(cell)), Is.True);
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.False);
        _controller.TickTurnPresentation(1f);
        _controller.TickTurnPresentation(1f);
        Assert.That(Items.PickedCount, Is.EqualTo(1));
        yield break;
    }

    [UnityTest]
    public IEnumerator Trap_UsesAuthoredFiveDamage_AndClearsTrapFlag()
    {
        var cell = FirstTrap();
        int health = Get<int>("_health");
        StepOnto(cell);
        Assert.That(Get<int>("_health"), Is.EqualTo(health - 5));
        Assert.That(Run.Health, Is.EqualTo(health - 5));
        Assert.That(Items.TriggeredTrapCount, Is.EqualTo(1));
        Assert.That(Items.IsTrapTriggered(cell), Is.True);
        Assert.That(Floor.Dungeon.GetFlags(cell.x, cell.y) & (ushort)Dungeon.TileFlag.HasTrap, Is.EqualTo(0));
        Assert.That(Floor.Dungeon.Grid[cell.y, cell.x], Is.Not.EqualTo(Dungeon.TileType.Trap));
        yield break;
    }

    [UnityTest]
    public IEnumerator TrapDamage_WaitsForSharedTurnPresentationCompletion()
    {
        var cell = FirstTrap();
        int health = Get<int>("_health");
        Assert.That(_controller.TryTowerAction(Approach(cell)), Is.True);
        _controller.TickTurnPresentation(.01f);
        Assert.That(Get<int>("_health"), Is.EqualTo(health));
        _controller.TickTurnPresentation(1f);
        Assert.That(Get<int>("_health"), Is.EqualTo(health - 5));
        yield break;
    }

    [UnityTest]
    public IEnumerator LethalTrap_ReturnsToTown_AndReleasesFloorManagerBridge()
    {
        var cell = FirstTrap();
        Set("_health", 5);
        Run.SetHealth(5);
        var previous = Run;
        StepOnto(cell);
        Assert.That(Get<object>("_area").ToString(), Is.EqualTo("Town"));
        Assert.That(previous.CurrentState, Is.EqualTo(TowerRunManager.State.NotInRun));
        Assert.That(Get<TowerItemTrapManager>("_towerItemTraps"), Is.Null);
        Assert.That(_controller.IsTurnAnimating, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator FloorTransition_ReplacesManager_AndStartsFreshTracking()
    {
        StepOnto(FirstItem());
        var previous = Items;
        Assert.That(previous.PickedCount, Is.EqualTo(1));
        var previousDungeon = Floor.Dungeon;
        StepOnto(Floor.StairsUpCell);
        Assert.That(Run.Floor, Is.EqualTo(2));
        Assert.That(Floor.CurrentFloor, Is.EqualTo(2));
        Assert.That(Run.CurrentState, Is.EqualTo(TowerRunManager.State.InTower));
        Assert.That(Items, Is.Not.SameAs(previous));
        Assert.That(Floor.Dungeon, Is.Not.SameAs(previousDungeon));
        Assert.That(Items.PickedCount, Is.EqualTo(0));
        Assert.That(Items.TriggeredTrapCount, Is.EqualTo(0));
        Assert.That(ItemCells.Count, Is.GreaterThan(0));
        yield break;
    }

    [UnityTest]
    public IEnumerator ReturnToTown_RemovesAllOldItemAndTrapMarkers()
    {
        var markers = Generated.Where(g => g && (g.name == "Tower Item" || g.name == "Tower Trap")).ToArray();
        Assert.That(markers.Length, Is.GreaterThan(0));
        Call("ReturnToTown", true);
        yield return null;
        Assert.That(markers.All(g => !g), Is.True);
        Assert.That(Get<TowerItemTrapManager>("_towerItemTraps"), Is.Null);
    }

    private string LayoutSignature() => string.Join(";", ItemCells.Select(c => "I" + c)) + "|" +
        string.Join(";", TrapCells.Select(c => "T" + c)) + "|" +
        string.Join(";", Floor.Dungeon.FloorItems.Where(e => e != null).Select(e => e.Kind + ":" + e.Subtype + ":" + e.Param));

    [UnityTest]
    public IEnumerator EveryGeneratedItem_CanBeCollectedWithoutStrandedSlots()
    {
        var cells = ItemCells;
        int total = Floor.Dungeon.FloorItems.Count(e => e != null);
        Assert.That(cells.Count, Is.EqualTo(total));
        Set("_health", 1000);
        Run.SetHealth(1000);
        foreach (var cell in cells) StepOnto(cell);
        Assert.That(ItemCells.Count, Is.EqualTo(0));
        Assert.That(Floor.Dungeon.FloorItems.Count(e => e != null), Is.EqualTo(0));
        Assert.That(Items.PickedCount, Is.EqualTo(total));
        yield break;
    }

    [UnityTest]
    public IEnumerator MovingOntoEmptyFloor_DoesNotAwardLootOrTrapDamage()
    {
        var cell = Floor.OpenCells.First(c => c != Floor.StairsDownCell && c != Floor.StairsUpCell &&
            !HasItem(c) && Floor.Dungeon.Grid[c.y, c.x] != Dungeon.TileType.Trap &&
            Directions.Any(d => Floor.IsWalkable(c.x - d.x, c.y - d.y)));
        int health = Get<int>("_health"), loot = Run.RunLoot;
        StepOnto(cell);
        Assert.That(Run.RunLoot, Is.EqualTo(loot));
        Assert.That(Get<int>("_health"), Is.EqualTo(health));
        yield break;
    }
    [UnityTest]
    public IEnumerator GeneratedItems_AndTraps_HaveOneMarkerPerOccupiedCell()
    {
        var cells = ItemCells;
        var traps = TrapCells;
        Assert.That(cells.Count, Is.GreaterThan(0));
        Assert.That(traps.Count, Is.GreaterThan(0));
        Assert.That(Generated.Count(g => g && g.name == "Tower Item"), Is.EqualTo(cells.Count));
        Assert.That(Generated.Count(g => g && g.name == "Tower Trap"), Is.EqualTo(traps.Count));
        foreach (var c in cells) { Assert.That(Marker("Tower Item", c), Is.Not.Null); Assert.That(Floor.IsWalkable(c.x, c.y), Is.True); }
        foreach (var c in traps) { Assert.That(Marker("Tower Trap", c), Is.Not.Null); Assert.That(c, Is.Not.EqualTo(Floor.StairsDownCell).And.Not.EqualTo(Floor.StairsUpCell)); }
        yield break;
    }

    [UnityTest]
    public IEnumerator RawPickup_WaitsForTurnCompletionAndSurvivesReturnWithoutDuplication()
    {
        var map = (Dictionary<Vector2Int, int>)typeof(TowerItemTrapManager).GetField("_cellToSlot", F).GetValue(Items);
        var pair = map.First();
        var expected = Floor.Dungeon.FloorItems[pair.Value];
        var bytes = new[] { (int)expected.Subtype, expected.Kind, expected.Param, expected.Flags };
        int count = Run.RunInventory.TotalItems, loot = Run.RunLoot;
        var town = Get<Inventory>("_townInventory");
        int banked = town.TotalItems;
        Assert.That(_controller.TryTowerAction(Approach(pair.Key)), Is.True);
        _controller.TickTurnPresentation(.01f);
        Assert.That(Run.RunInventory.TotalItems, Is.EqualTo(count));
        _controller.TickTurnPresentation(1f);
        Assert.That(Run.RunInventory.TotalItems, Is.EqualTo(count + 1));
        Assert.That(Run.RunLoot, Is.EqualTo(loot), "Raw Param is not money, including unknown coin denominations");
        var actual = Run.RunInventory.DungeonItems.Last();
        CollectionAssert.AreEqual(bytes, new[] { (int)actual.Subtype, actual.Kind, actual.Param, actual.Flags });
        Assert.That(actual, Is.Not.SameAs(expected));
        Call("ReturnToTown", true);
        Assert.That(town.TotalItems, Is.EqualTo(banked + count + 1));
        Call("ReturnToTown", true);
        Assert.That(town.TotalItems, Is.EqualTo(banked + count + 1));
        Call("EnterTower"); RemoveCreature();
        Assert.That(Run.RunInventory.TotalItems, Is.EqualTo(banked + count + 1));
        Assert.That(town.TotalItems, Is.Zero);
        yield break;
    }
}
