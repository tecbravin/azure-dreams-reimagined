using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using CoreActor = AzureDreams.Core.ActorRecord;
using CoreStatus = AzureDreams.Core.StatusId;

public class TowerActorActionsIntegrationTests
{
    private GameObject _root;
    private TowerGameController _controller;
    private Camera _camera;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
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
    private object Call(string name, params object[] args)
    {
        var method = typeof(TowerGameController).GetMethod(name, Flags);
        Assert.That(method, Is.Not.Null, "Missing actor action: " + name);
        return method.Invoke(_controller, args);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _root = new GameObject("TowerActorActionsTest");
        _controller = _root.AddComponent<TowerGameController>();
        _camera = new GameObject("TowerActorActionsCamera").AddComponent<Camera>();
        _camera.enabled = false;
        _controller.Configure(null, null, null, null, null, null, null, null, null, _camera);
        yield return null;
        Set("_familiarDefinition", null);
        Call("EnterTown");
        Call("EnterTower");
        var actor = FirstCreatureField<CoreActor>("Actor");
        for (int i = 0; i < 4; i++)
            if (actor.StatusId(i) == CoreStatus.SLEEP) actor.SetStatus(i, 0, 0);
        Get<HashSet<Vector2Int>>("_walls").Clear();
        SetCreatureField("Cell", new Vector2Int(8, 8));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(8, 0.55f, 8);
    }

    [Test]
    public void FamiliarEvolvesInPlaceWhenOneAwardCrossesLevelTwenty()
    {
        int flame = (int)AzureDreams.Core.MonsterSpecies.FLAME;
        byte ifrit = (byte)AzureDreams.Core.MonsterSpecies.IFRIT;
        Assert.That(Call("EquipHatchedFamiliar", flame, 19), Is.EqualTo(true));
        var familiar = Get<CoreActor>("_familiarActor");
        familiar.SetRaw(8, 1);
        familiar.SetRaw(9, 4);
        familiar.SetRaw(10, 4);
        var thresholds = AzureDreams.Core.Leveling.BuildExperienceTable();
        object[] gain = { familiar, thresholds[20], (int)familiar.Hp };
        Call("AddExperience", gain);
        Assert.That(Get<CoreActor>("_familiarActor"), Is.SameAs(familiar));
        Assert.That(Get<int>("_hatchedSpecies"), Is.EqualTo(ifrit));
        Assert.That(familiar.Species, Is.EqualTo(ifrit));
        Assert.That(familiar.Level, Is.EqualTo(21));
        Assert.That(familiar.Experience, Is.EqualTo(thresholds[20]));
        Assert.That(familiar.U8(8), Is.EqualTo(1));
    }

    [TestCase(CoreStatus.SLEEP, 2)]
    [TestCase(CoreStatus.PARALYSIS, 2)]
    [TestCase(CoreStatus.SPEED, -2)]
    public void StatusPassesOnlyTheTurnsThatBlockKoh(sbyte status, sbyte duration)
    {
        Creatures.Clear();
        var actor = Get<CoreActor>("_playerActor");
        var originalCell = Get<Vector2Int>("_playerCell");
        int turns = Get<int>("_completedTowerTurns");
        actor.SetStatus(0, status, duration);

        Assert.That(Call("TryPassStatusBlockedTurn"), Is.True);
        _controller.TickTurnPresentation(1f);
        if (status != CoreStatus.SPEED)
        {
            Assert.That(Call("TryPassStatusBlockedTurn"), Is.True);
            _controller.TickTurnPresentation(1f);
        }

        Assert.That(Call("TryPassStatusBlockedTurn"), Is.False);
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(originalCell));
        Assert.That(Get<int>("_completedTowerTurns") - turns, Is.EqualTo(status == CoreStatus.SPEED ? 1 : 2));
    }

    [Test]
    public void LevelUpCelebrationBlocksMovementWithoutSpendingATurn()
    {
        int turns = Get<int>("_completedTowerTurns");
        var cell = Get<Vector2Int>("_playerCell");
        Call("ChangeActorLevel", Get<CoreActor>("_playerActor"), 1);

        Assert.That(Call("TickLevelUpCelebration", 0f), Is.True);
        Assert.That(_controller.TryTowerAction(Vector2Int.right), Is.False);
        Assert.That(Call("TryPassStatusBlockedTurn"), Is.False);
        Assert.That(Call("TickLevelUpCelebration", 3f), Is.True);
        Assert.That(Call("TickLevelUpCelebration", 0f), Is.False);
        Assert.That(Get<int>("_completedTowerTurns"), Is.EqualTo(turns));
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(cell));
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_root) UnityEngine.Object.Destroy(_root);
        if (_camera) UnityEngine.Object.Destroy(_camera.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Controller_DirectTownTransitionFinishesTheManagedRun()
    {
        var run = Get<TowerRunManager>("_towerRun");
        Call("EnterTown");
        Assert.That(run.CurrentState, Is.EqualTo(TowerRunManager.State.NotInRun));
        Assert.That(run.RunInventory, Is.Null);
        Assert.That(Get<Inventory>("_runInventory"), Is.Null);
        yield break;
    }

    [UnityTest]
    public IEnumerator FusionPreviewKeepsTheBodyAndInheritsTheStrongestDuplicateWithoutChangingEitherParent()
    {
        var fire = new CoreActor { Species = (byte)AzureDreams.Core.MonsterSpecies.TROLL,
            Level = 7, Experience = 127, Hp = 20, MaxHp = 24, MaxMp = 100, MpRaw = 20 << 8,
            Flags14 = AzureDreams.Core.Element.Fire | AzureDreams.Core.FamiliarSystem.LevaFusionProtection };
        var water = new CoreActor { Species = (byte)AzureDreams.Core.MonsterSpecies.PULUNPA,
            Level = 3, Hp = 12, MaxHp = 12, MaxMp = 100, MpRaw = 40 << 8, Flags14 = AzureDreams.Core.Element.Water };
        fire.SetRaw(8, 7);
        fire.SetRaw(9, 3);
        water.SetRaw(8, 8);
        water.SetRaw(9, 12);
        var result = (CoreActor)Call("ComputeFusionResult", fire, water);

        Assert.That(result.Species, Is.EqualTo(fire.Species));
        Assert.That(result.Level, Is.EqualTo(7));
        Assert.That(result.Experience, Is.EqualTo(127));
        Assert.That(result.Flags14 & 7, Is.EqualTo(AzureDreams.Core.Element.Water));
        Assert.That(result.Flags14 & AzureDreams.Core.FamiliarSystem.LevaFusionProtection, Is.Zero);
        Assert.That(result.U8(8), Is.EqualTo(8));
        Assert.That(result.U8(9), Is.EqualTo(12));
        Assert.That(result.U8(11), Is.Zero);
        Assert.That(result.MpRaw, Is.EqualTo((30 << 8) | 0xff));
        Assert.That(fire.U8(9), Is.EqualTo(3));
        Assert.That(fire.Flags14 & AzureDreams.Core.FamiliarSystem.LevaFusionProtection, Is.Not.Zero);
        Assert.That(water.MpRaw, Is.EqualTo(40 << 8));
        yield break;
    }

    [UnityTest]
    public IEnumerator Controller_PermanentFamiliarKeepsExperienceWhenReturningWithATemporaryFamiliar()
    {
        var townPrefs = typeof(TownSaveVariables);
        const BindingFlags staticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        var savedTown = townPrefs.GetMethod("Capture", staticPrivate).Invoke(null, null);
        bool hadSave = SaveSystem.HasSave;
        SaveSystem.Load(out int gold, out int townLevel, out string familiar, out int potions);
        var definition = ScriptableObject.CreateInstance<ActorDefinition>();
        definition.Configure("Test familiar", ActorKind.Familiar, 20, 5, 0.16f, Color.white, null);
        try
        {
            var permanent = new CoreActor { Level = 5, Experience = 117, MaxHp = 20 };
            var reserveType = typeof(TowerGameController).GetNestedType("BagFamiliar", BindingFlags.NonPublic);
            var reserve = Activator.CreateInstance(reserveType, nonPublic: true);
            reserveType.GetField("Definition", Flags).SetValue(reserve, definition);
            reserveType.GetField("Actor", Flags).SetValue(reserve, permanent);
            var bag = Get<IList>("_bagFamiliars");
            bag.Clear();
            bag.Add(reserve);
            Set("_familiarDefinition", definition);
            Set("_familiarActor", new CoreActor { Experience = 400 });
            // An empty parked record marks a temporary active familiar when the permanent one is in the bag.
            Set("_parkedFamiliarActor", new CoreActor());
            Set("_parkedFamiliarDefinition", null);

            Call("ReturnToTown", true);

            Assert.That(Get<CoreActor>("_familiarActor"), Is.SameAs(permanent));
            Assert.That(TownSaveVariables.GetInt("home.active_familiar_exp"), Is.EqualTo(117));
            var restored = new CoreActor();
            var restoreActor = typeof(TowerGameController).GetMethod("RestoreStoredFamiliarActor", staticPrivate);
            Assert.That(restoreActor.Invoke(null, new object[] { TownSaveVariables.GetString("traveling_familiar_record"), restored }), Is.True);
            Assert.That(restored.Experience, Is.EqualTo(117));
            Assert.That(restored.Level, Is.EqualTo(5));

            Get<CoreActor>("_playerActor").Experience = 200;
            Call("EnterTower");
            Assert.That(Get<CoreActor>("_familiarActor").Experience, Is.EqualTo(117));
            Assert.That(Get<CoreActor>("_familiarActor").Level, Is.EqualTo(5));
            Assert.That(Get<CoreActor>("_playerActor").Experience, Is.Zero);
        }
        finally
        {
            townPrefs.GetMethod("Restore", staticPrivate).Invoke(null, new[] { savedTown });
            TownSaveVariables.Save();
            if (hadSave) SaveSystem.Save(gold, townLevel, familiar, potions);
            else SaveSystem.Clear();
            UnityEngine.Object.Destroy(definition);
        }
        yield break;
    }

    [UnityTest]
    public IEnumerator Controller_StairsAdvanceTheRunManagerAfterPresentation()
    {
        var run = Get<TowerRunManager>("_towerRun");
        var stairs = Get<Vector2Int>("_stairCell");
        Set("_playerCell", stairs + Vector2Int.left);
        Get<GameObject>("_player").transform.position = new Vector3(stairs.x - 1, 0.55f, stairs.y);
        Assert.That(_controller.TryTowerAction(Vector2Int.right), Is.True);
        Assert.That(run.Floor, Is.EqualTo(1));
        _controller.TickTurnPresentation(1f);
        Assert.That(run.Floor, Is.EqualTo(1));
        Assert.That(Get<object>("_checkpointMenu").ToString(), Is.EqualTo("Ascend"));
        Call("AscendFromElevator");
        Call("TickElevatorAscent", 2f);
        Assert.That(run.Floor, Is.EqualTo(2));
        Assert.That(run.Floor, Is.EqualTo(Get<int>("_floor")));
        Assert.That(run.CurrentState, Is.EqualTo(TowerRunManager.State.InTower));
        yield break;
    }

    [UnityTest]
    public IEnumerator Controller_CollectedSalvageBelongsToRunManager()
    {
        var run = Get<TowerRunManager>("_towerRun");
        var loot = Get<Vector2Int>("_lootCell");
        Set("_playerCell", loot);
        Get<GameObject>("_player").transform.position = new Vector3(loot.x, 0.55f, loot.y);
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(run.RunLoot, Is.GreaterThan(0));
        Assert.That(run.RunLoot, Is.EqualTo(Get<int>("_runLoot")));
        yield break;
    }

    [UnityTest]
    public IEnumerator Controller_CompletedTurnSynchronizesHealthWithRunManager()
    {
        var run = Get<TowerRunManager>("_towerRun");
        Set("_health", 20);
        SetCreatureField("Cell", new Vector2Int(2, 1));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(2, 0.55f, 1);
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(run.Health, Is.EqualTo(Get<int>("_health")));
        Assert.That(run.Health, Is.EqualTo(18));
        yield break;
    }

    [UnityTest]
    public IEnumerator Controller_ReturnUsesManagerRewardAndPersistsRemainingPotionsOnlyOnce()
    {
        var run = Get<TowerRunManager>("_towerRun");
        var inventory = Get<Inventory>("_runInventory");
        var potion = Get<ItemDefinition>("_potionItem");
        inventory.AddItem(potion, 2);
        Set("_health", 6);
        Assert.That((bool)Call("TryTowerUseItem", potion), Is.True);
        _controller.TickTurnPresentation(1f);
        int remaining = inventory.GetCount(potion);
        int initialGold = run.Gold;
        run.AddLoot(30);
        Call("ReturnToTown", true);
        Assert.That(run.CurrentState, Is.EqualTo(TowerRunManager.State.NotInRun));
        Assert.That(Get<int>("_gold"), Is.EqualTo(initialGold + 30));
        Assert.That(Get<Inventory>("_townInventory").GetCount(potion), Is.EqualTo(remaining));
        SaveSystem.Load(out int savedGold, out int savedLevel, out string savedFamiliar, out int savedPotions);
        Assert.That(savedGold, Is.EqualTo(initialGold + 30));
        Assert.That(savedPotions, Is.EqualTo(remaining));
        Call("ReturnToTown", true);
        Assert.That(Get<int>("_gold"), Is.EqualTo(initialGold + 30));
        Assert.That(Get<Inventory>("_townInventory").GetCount(potion), Is.EqualTo(remaining));
        yield break;
    }

    [UnityTest]
    public IEnumerator Controller_EnterTowerSharesTheRunManagersInventory()
    {
        var field = typeof(TowerGameController).GetField("_towerRun", Flags);
        Assert.That(field, Is.Not.Null, "Controller must delegate run ownership to TowerRunManager");
        var run = (TowerRunManager)field.GetValue(_controller);
        Assert.That(run, Is.Not.Null);
        Assert.That(run.CurrentState, Is.EqualTo(TowerRunManager.State.InTower));
        Assert.That(run.Floor, Is.EqualTo(Get<int>("_floor")));
        Assert.That(run.RunInventory, Is.SameAs(Get<Inventory>("_runInventory")));
        Assert.That(run.TownInventory, Is.SameAs(Get<Inventory>("_townInventory")));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_NewRunResetsFacingToTheNewActorOrientation()
    {
        Assert.That((bool)Call("TryTowerFace", Vector2Int.right), Is.True);
        Call("EnterTown");
        Call("EnterTower");
        var property = typeof(TowerGameController).GetProperty("TowerFacing", Flags);
        Assert.That((Vector2Int)property.GetValue(_controller), Is.EqualTo(Vector2Int.up));
        Assert.That(Vector3.Dot(Get<GameObject>("_player").transform.forward, Vector3.forward), Is.GreaterThan(0.99f));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_ExplicitAttackFacesTheRequestedDirection()
    {
        Assert.That((bool)Call("TryTowerAttack", Vector2Int.right), Is.True);
        Assert.That(Vector3.Dot(Get<GameObject>("_player").transform.forward, Vector3.right), Is.GreaterThan(0.99f));
        _controller.TickTurnPresentation(1f);
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_ExplicitAttackHitsOnlyTheSelectedPreTurnAdjacentCell()
    {
        SetCreatureField("Cell", new Vector2Int(2, 1));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(2, 0.55f, 1);
        Assert.That((bool)Call("TryTowerAttack", Vector2Int.right), Is.True);
        Assert.That((bool)Call("TryTowerAttack", Vector2Int.right), Is.False);
        Assert.That((bool)Call("TryTowerFace", Vector2Int.up), Is.False);
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(7));
        Assert.That(Get<int>("_health"), Is.EqualTo(18));
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(new Vector2Int(1, 1)));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_AttackDoesNotHitAMonsterThatMovesIntoTheFrontCellLater()
    {
        SetCreatureField("Cell", new Vector2Int(3, 1));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(3, 0.55f, 1);
        Assert.That((bool)Call("TryTowerAttack", Vector2Int.right), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<Vector2Int>("Cell"), Is.EqualTo(new Vector2Int(2, 1)));
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(10));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_RejectsMalformedAttackDirectionsWithoutSpendingATurn()
    {
        int turn = _controller.TowerTurnNumber;
        // Diagonal (1,1) is now a valid front-tile direction, not malformed input.
        foreach (var direction in new[] { Vector2Int.zero, new Vector2Int(2, 1), new Vector2Int(int.MinValue, 0), new Vector2Int(int.MaxValue, 1) })
        {
            Assert.That((bool)Call("TryTowerAttack", direction), Is.False);
            Assert.That((bool)Call("TryTowerFace", direction), Is.False);
        }
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(turn));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_HealingClampsToMaximumAndRejectsUnsupportedOrMissingItems()
    {
        var herb = ScriptableObject.CreateInstance<ItemDefinition>();
        var sword = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            herb.Configure("Strong herb", ItemDefinition.ItemType.Heal, int.MaxValue, 0, "Clamp safely");
            sword.Configure("Sword", ItemDefinition.ItemType.Attack, 3, 0, "Not a healing action");
            var inventory = Get<Inventory>("_runInventory");
            inventory.AddItem(sword, 1);
            int turn = _controller.TowerTurnNumber;
            Assert.That((bool)Call("TryTowerUseItem", herb), Is.False);
            Assert.That((bool)Call("TryTowerUseItem", sword), Is.False);
            Assert.That(_controller.TowerTurnNumber, Is.EqualTo(turn));
            inventory.AddItem(herb, 1);
            Set("_health", 19);
            Assert.That((bool)Call("TryTowerUseItem", herb), Is.True);
            _controller.TickTurnPresentation(1f);
            Assert.That(Get<int>("_health"), Is.EqualTo(20));
            Assert.That(inventory.GetCount(herb), Is.Zero);
        }
        finally { UnityEngine.Object.Destroy(herb); UnityEngine.Object.Destroy(sword); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Monster_SleepBlocksChasingAndClearedSleepAllowsItAgain()
    {
        var actor = FirstCreatureField<object>("Actor");
        var setter = actor.GetType().GetMethod("SetStatus");
        setter.Invoke(actor, new object[] { 0, (sbyte)1, (sbyte)8 });
        var before = FirstCreatureField<Vector2Int>("Cell");
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<Vector2Int>("Cell"), Is.EqualTo(before));
        setter.Invoke(actor, new object[] { 0, (sbyte)0, (sbyte)0 });
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<Vector2Int>("Cell"), Is.Not.EqualTo(before));
        yield break;
    }

    [UnityTest]
    public IEnumerator Familiar_CommittedAttackEmitsExactlyOneEffect()
    {
        yield return VerifyFamiliarAttackEffect(3, 1, 8);
    }

    [UnityTest]
    public IEnumerator Familiar_EnemyApproachingAfterPlanningDoesNotEmitAnAttackEffect()
    {
        yield return VerifyFamiliarAttackEffect(4, 0, 10);
    }

    private IEnumerator VerifyFamiliarAttackEffect(int enemyX, int expectedEffects, int expectedEnemyHealth)
    {
        var effect = new GameObject("FamiliarAttackEffectProbe");
        try
        {
            effect.SetActive(false);
            Set("_fxMagic", effect);
            var familiar = new GameObject("Familiar attack FX probe");
            familiar.transform.SetParent(_root.transform);
            familiar.transform.position = new Vector3(2, 0.55f, 1);
            Set("_familiar", familiar);
            Set("_familiarCell", new Vector2Int(2, 1));
            SetCreatureField("Cell", new Vector2Int(enemyX, 1));
            FirstCreatureField<GameObject>("View").transform.position = new Vector3(enemyX, 0.55f, 1);
            Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
            int effects = 0;
            foreach (Transform child in _root.transform)
                if (child.name == "FamiliarAttackEffectProbe(Clone)") effects++;
            Assert.That(effects, Is.EqualTo(expectedEffects));
            _controller.TickTurnPresentation(1f);
            Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(expectedEnemyHealth));
        }
        finally { UnityEngine.Object.Destroy(effect); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Familiar_SleepDoesNotEmitAnAttackEffect()
    {
        var effect = new GameObject("SleepAttackEffectProbe");
        try
        {
            effect.SetActive(false);
            Set("_fxMagic", effect);
            var familiar = new GameObject("Sleeping familiar FX probe");
            familiar.transform.SetParent(_root.transform);
            familiar.transform.position = new Vector3(2, 0.55f, 1);
            Set("_familiar", familiar);
            Set("_familiarCell", new Vector2Int(2, 1));
            SetCreatureField("Cell", new Vector2Int(3, 1));
            FirstCreatureField<GameObject>("View").transform.position = new Vector3(3, 0.55f, 1);
            var actor = Get<object>("_familiarActor");
            actor.GetType().GetMethod("SetStatus").Invoke(actor, new object[] { 0, (sbyte)1, (sbyte)8 });
            Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
            int effects = 0;
            foreach (Transform child in _root.transform)
                if (child.name == "SleepAttackEffectProbe(Clone)") effects++;
            Assert.That(effects, Is.Zero, "FX must follow the committed attack, not post-movement adjacency");
            _controller.TickTurnPresentation(1f);
        }
        finally { UnityEngine.Object.Destroy(effect); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Familiar_SleepingCompanionDoesNotAttack()
    {
        var familiar = new GameObject("Sleeping familiar");
        familiar.transform.SetParent(_root.transform);
        familiar.transform.position = new Vector3(2, 0.55f, 1);
        Set("_familiar", familiar);
        Set("_familiarCell", new Vector2Int(2, 1));
        SetCreatureField("Cell", new Vector2Int(3, 1));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(3, 0.55f, 1);
        var actor = Get<object>("_familiarActor");
        actor.GetType().GetMethod("SetStatus").Invoke(actor, new object[] { 0, (sbyte)1, (sbyte)8 });
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(10));
        yield break;
    }

    [UnityTest]
    public IEnumerator Monster_SleepingActorDoesNotAttackTheAdjacentPlayer()
    {
        var actor = FirstCreatureField<object>("Actor");
        actor.GetType().GetMethod("SetStatus").Invoke(actor, new object[] { 0, (sbyte)1, (sbyte)8 });
        SetCreatureField("Cell", new Vector2Int(2, 1));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(2, 0.55f, 1);
        Set("_health", 20);
        Assert.That(_controller.TryTowerAction(Vector2Int.zero), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(Get<int>("_health"), Is.EqualTo(20));
        Assert.That(FirstCreatureField<Vector2Int>("Cell"), Is.EqualTo(new Vector2Int(2, 1)));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_RunAndLiftPassesTurnsBeforeDirectionalInteraction()
    {
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        bool oldRun = Application.runInBackground;
        Keyboard keyboard = null;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            Creatures.Clear();
            var cell = Get<Vector2Int>("_playerCell");
            Assert.That(_controller.TryTowerFace(Vector2Int.up), Is.True);
            int turns = Get<int>("_completedTowerTurns");

            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.L, Key.E, Key.D));
            for (int frame = 0; frame < 8 && Get<int>("_completedTowerTurns") < turns + 2; frame++)
            {
                yield return null;
                _controller.TickTurnPresentation(1f);
            }

            Assert.That(Get<int>("_completedTowerTurns"), Is.GreaterThanOrEqualTo(turns + 2));
            Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(cell));
            Assert.That(_controller.TowerFacing, Is.EqualTo(Vector2Int.up));

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            _controller.TickTurnPresentation(1f);
            int afterRelease = Get<int>("_completedTowerTurns");
            yield return null;
            _controller.TickTurnPresentation(1f);
            Assert.That(Get<int>("_completedTowerTurns"), Is.EqualTo(afterRelease));
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Application.runInBackground = oldRun;
        }
    }

    [UnityTest]
    public IEnumerator Player_ShiftDirectionFacesWithoutMovingOrSpendingATurn()
    {
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        bool oldRun = Application.runInBackground;
        Keyboard keyboard = null;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            int turn = _controller.TowerTurnNumber;
            var player = Get<GameObject>("_player");
            Vector3 before = player.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.D));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(player.transform.position, Is.EqualTo(before));
            Assert.That(_controller.TowerTurnNumber, Is.EqualTo(turn));
            Assert.That(Vector3.Dot(player.transform.forward, Vector3.right), Is.GreaterThan(0.99f));
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Application.runInBackground = oldRun;
        }
    }

    [UnityTest]
    public IEnumerator Player_KeyboardDiagonalAimThenAttackUsesTheSelectedDiagonal()
    {
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        bool oldRun = Application.runInBackground;
        Keyboard keyboard = null;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            var diagonal = new Vector2Int(1, 1);
            var cell = Get<Vector2Int>("_playerCell");
            SetCreatureField("Cell", cell + diagonal);
            FirstCreatureField<GameObject>("View").transform.position = new Vector3(cell.x + 1, 0.55f, cell.y + 1);
            int before = _controller.TowerTurnNumber;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftShift, Key.W, Key.D));
            yield return null;
            yield return null;
            Assert.That(_controller.TowerFacing, Is.EqualTo(diagonal));
            Assert.That(_controller.TowerTurnNumber, Is.EqualTo(before));
            Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(cell));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            yield return null;
            _controller.TickTurnPresentation(1f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(7));
            Assert.That(_controller.TowerTurnNumber, Is.EqualTo(before + 1));
            Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(cell));
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Application.runInBackground = oldRun;
        }
    }

    [UnityTest]
    public IEnumerator Player_KeyboardAttackIsWiredToTheRealUpdatePath()
    {
        var oldEditor = InputSystem.settings.editorInputBehaviorInPlayMode;
        var oldBackground = InputSystem.settings.backgroundBehavior;
        bool oldRun = Application.runInBackground;
        Keyboard keyboard = null;
        try
        {
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            int turn = _controller.TowerTurnNumber;
            Vector3 before = Get<GameObject>("_player").transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.F));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(_controller.TowerTurnNumber, Is.EqualTo(turn + 1));
            Assert.That(Get<GameObject>("_player").transform.position, Is.EqualTo(before));
        }
        finally
        {
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = oldEditor;
            InputSystem.settings.backgroundBehavior = oldBackground;
            Application.runInBackground = oldRun;
        }
    }

    [UnityTest]
    public IEnumerator Player_HealingItemUsesConfiguredValueAndConsumesOneStack()
    {
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            item.Configure("Test herb", ItemDefinition.ItemType.Heal, 5, 0, "Configured healing");
            var inventory = Get<Inventory>("_runInventory");
            inventory.AddItem(item, 2);
            Set("_health", 6);
            int before = _controller.TowerTurnNumber;
            Assert.That((bool)Call("TryTowerUseItem", item), Is.True);
            Assert.That((bool)Call("TryTowerUseItem", item), Is.False, "Busy actions must not consume a second stack");
            Call("TickTurnPresentation", 1f);
            Assert.That(Get<int>("_health"), Is.EqualTo(11));
            Assert.That(inventory.GetCount(item), Is.EqualTo(1));
            Assert.That(_controller.TowerTurnNumber, Is.EqualTo(before + 1));
        }
        finally { UnityEngine.Object.Destroy(item); }
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_FacingChangesWithoutAdvancingTheSharedTurn()
    {
        var player = Get<GameObject>("_player");
        Vector3 before = player.transform.position;
        Vector3 enemyBefore = FirstCreatureField<GameObject>("View").transform.position;
        int turn = _controller.TowerTurnNumber;
        Assert.That((bool)Call("TryTowerFace", Vector2Int.right), Is.True);
        Assert.That(Vector3.Dot(player.transform.forward, Vector3.right), Is.GreaterThan(0.99f));
        Assert.That(player.transform.position, Is.EqualTo(before));
        Assert.That(FirstCreatureField<GameObject>("View").transform.position, Is.EqualTo(enemyBefore));
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(turn));
        Assert.That(_controller.IsTurnAnimating, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_AllEightFrontDirectionsAlsoAllowEmptySwings()
    {
        int before = _controller.TowerTurnNumber;
        var cell = Get<Vector2Int>("_playerCell");
        int accepted = 0;
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
            {
                if (x == 0 && y == 0) continue;
                var direction = new Vector2Int(x, y);
                Assert.That(_controller.TryTowerFace(direction), Is.True);
                Assert.That(_controller.TryTowerAttack(direction), Is.True);
                _controller.TickTurnPresentation(1f);
                accepted++;
                Assert.That(_controller.TowerFacing, Is.EqualTo(direction));
                Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(cell));
            }
        Assert.That(accepted, Is.EqualTo(8));
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(before + accepted));
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(10));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_InvalidFrontDirectionsDoNotChangeFacingOrSpendTurns()
    {
        var beforeFacing = _controller.TowerFacing;
        int beforeTurn = _controller.TowerTurnNumber;
        var invalid = new[] { Vector2Int.zero, new Vector2Int(2, 0), new Vector2Int(-2, -2),
            new Vector2Int(int.MinValue, 0), new Vector2Int(int.MaxValue, int.MinValue) };
        foreach (var direction in invalid)
        {
            Assert.That(_controller.TryTowerFace(direction), Is.False);
            Assert.That(_controller.TryTowerAttack(direction), Is.False);
        }
        Assert.That(_controller.TowerFacing, Is.EqualTo(beforeFacing));
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(beforeTurn));
        Assert.That(_controller.IsTurnAnimating, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_DiagonalAttackDoesNotHitAnActorApproachingDuringThatTurn()
    {
        var cell = Get<Vector2Int>("_playerCell");
        SetCreatureField("Cell", cell + new Vector2Int(2, 1));
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(cell.x + 2, 0.55f, cell.y + 1);
        Assert.That(_controller.TryTowerAttack(new Vector2Int(1, 1)), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<Vector2Int>("Cell"), Is.EqualTo(cell + new Vector2Int(1, 1)));
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(10));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_DiagonalFrontAttackHitsTheSelectedCellWithoutMoving()
    {
        var diagonal = new Vector2Int(1, 1);
        var playerCell = Get<Vector2Int>("_playerCell");
        SetCreatureField("Cell", playerCell + diagonal);
        FirstCreatureField<GameObject>("View").transform.position = new Vector3(playerCell.x + 1, 0.55f, playerCell.y + 1);
        int before = _controller.TowerTurnNumber;
        Assert.That(_controller.TryTowerAttack(diagonal), Is.True);
        Assert.That(_controller.TowerFacing, Is.EqualTo(diagonal));
        Assert.That(_controller.TryTowerAttack(diagonal), Is.False, "One command per shared turn");
        _controller.TickTurnPresentation(1f);
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(7));
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(playerCell));
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(before + 1));
        yield break;
    }

    [UnityTest]
    public IEnumerator Player_ExplicitAttackIntoEmptySpaceConsumesOneTurnWithoutMoving()
    {
        var position = Get<GameObject>("_player").transform.position;
        int before = _controller.TowerTurnNumber;
        Assert.That((bool)Call("TryTowerAttack", Vector2Int.right), Is.True);
        Call("TickTurnPresentation", 1f);
        Assert.That(Get<GameObject>("_player").transform.position, Is.EqualTo(position));
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(before + 1));
        Assert.That(FirstCreatureField<int>("Health"), Is.EqualTo(10));
        yield break;
    }
}
