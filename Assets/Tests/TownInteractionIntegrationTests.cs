using System;
using System.Collections;
using System.Reflection;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class TownInteractionIntegrationTests
{
    [UnityTest]
    public IEnumerator Town_UsesTexturedTerrainHouseAndNpcPrefabs()
    {
        var root = new GameObject("town-prefab-qa");
        try
        {
            root.AddComponent<TowerGameController>();
            yield return null;

            var transforms = root.GetComponentsInChildren<Transform>(true);
            var ground = Array.Find(transforms, x => x.name == "Grass Terrain");
            var inn = Array.Find(transforms, x => x.name == "Inn");
            var market = Array.Find(transforms, x => x.name == "Market");
            var mira = Array.Find(transforms, x => x.name == "Townsperson - Mira");
            var rowan = Array.Find(transforms, x => x.name == "Townsperson - Rowan");
            var gate = Array.Find(transforms, x => x.name == "Tower Gate");
            var innLeftFence = Array.Find(transforms, x => x.name == "Inn Front Fence -1");
            var innRightFence = Array.Find(transforms, x => x.name == "Inn Front Fence 1");
            var marketLeftFence = Array.Find(transforms, x => x.name == "Market Front Fence -1");
            var marketRightFence = Array.Find(transforms, x => x.name == "Market Front Fence 1");

            Assert.That(ground, Is.Not.Null, "Town must instantiate its textured ground prefab");
            Assert.That(ground.GetComponent<Renderer>().sharedMaterial.mainTexture, Is.Not.Null,
                "Ground material must reference its generated pixel texture");
            Assert.That(inn, Is.Not.Null, "Inn house prefab must be present in Town");
            Assert.That(market, Is.Not.Null, "Market house prefab must be present in Town");
            Assert.That(inn.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(market.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(mira.GetComponent<NPCController>(), Is.Not.Null);
            Assert.That(rowan.GetComponent<NPCController>(), Is.Not.Null);
            Assert.That(innLeftFence, Is.Not.Null, "Inn frontage must use the fence prefab");
            Assert.That(innRightFence, Is.Not.Null, "Inn frontage must leave a center entrance");
            Assert.That(marketLeftFence, Is.Not.Null, "Market frontage must use the fence prefab");
            Assert.That(marketRightFence, Is.Not.Null, "Market frontage must leave a center entrance");
            Assert.That(innLeftFence.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(innRightFence.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(marketLeftFence.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(marketRightFence.GetComponentInChildren<Renderer>(), Is.Not.Null);
            Assert.That(innLeftFence.position.x, Is.LessThan(2f));
            Assert.That(innRightFence.position.x, Is.GreaterThan(2f));
            Assert.That(marketLeftFence.position.x, Is.LessThan(8f));
            Assert.That(marketRightFence.position.x, Is.GreaterThan(8f));
            Assert.That(mira.GetComponent<CharacterStats>().portraitTexture, Is.Null,
                "Unclassified town VRAM previews must not be presented as an NPC portrait");
            Assert.That(rowan.GetComponent<CharacterStats>().portraitTexture, Is.Null,
                "Unclassified town VRAM previews must not be presented as an NPC portrait");
            Assert.That(mira.GetComponent<CharacterStats>().characterMaterial.mainTexture, Is.Null,
                "NPC body must not use the incorrectly converted raw VRAM preview");
            Assert.That(rowan.GetComponent<CharacterStats>().characterMaterial.mainTexture, Is.Null,
                "NPC body must not use the incorrectly converted raw VRAM preview");
            Assert.That(gate.GetComponent<AzureDreamsReimagined.EventTrigger>(), Is.Not.Null);
            Assert.That(Array.FindAll(transforms, x => x.name == "Town Path Tile").Length, Is.GreaterThanOrEqualTo(15),
                "Town needs connected ground paths between spawn, houses, fountain, board, and gate");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator Town_EAdvancesConversationAndBlocksMovementUntilFinished()
    {
        var root = new GameObject("town-conversation-qa");
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var npc = root.GetComponentInChildren<NPCController>();
            Assert.That(npc, Is.Not.Null, "Town scene must wire NPC components");
            var flow = root.GetComponent<TownEventController>();
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle));
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            player.transform.position = npc.transform.position + Vector3.right * 0.9f;
            var interact = typeof(TowerGameController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance);
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownDialogue));
            var before = player.transform.position;
            game.MoveInTown(Vector2.right, 0.1f);
            Assert.That(player.transform.position, Is.EqualTo(before));
            interact.Invoke(game, null);
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator Town_FountainEUpdatesControllerAndIsRegisteredForCleanup()
    {
        var root = new GameObject("town-fountain-qa");
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var fountain = root.GetComponentInChildren<WaterFountain>();
            Assert.That(fountain, Is.Not.Null, "Town must instantiate a functional fountain");
            var generated = (System.Collections.Generic.List<GameObject>)typeof(TowerGameController).GetField("_generated", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            Assert.That(generated, Does.Contain(fountain.gameObject));
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            var stats = player.GetComponent<CharacterStats>();
            Assert.That(stats, Is.Not.Null);
            stats.currentHealth = 1;
            typeof(TowerGameController).GetField("_health", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(game, 1);
            player.transform.position = fountain.transform.position + Vector3.right * 0.8f;
            typeof(TowerGameController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
            Assert.That(stats.currentHealth, Is.EqualTo(stats.maxHealth));
            Assert.That(typeof(TowerGameController).GetField("_health", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game), Is.EqualTo(stats.maxHealth));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator Town_GateEventEntersTowerAndReturnsThroughStateMachine()
    {
        var root = new GameObject("town-gate-qa");
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var gate = root.GetComponentInChildren<AzureDreamsReimagined.EventTrigger>();
            Assert.That(gate, Is.Not.Null, "Town gate must use the event bridge");
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            Assert.That(gate.TryActivate(player), Is.True);
            var flow = root.GetComponent<TownEventController>();
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TowerRunning));
            Assert.That(typeof(TowerGameController).GetField("_floor", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game), Is.EqualTo(1));
            typeof(TowerGameController).GetMethod("ReturnToTown", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, new object[] { true });
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle));
            var returnedPlayer = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            Assert.That(returnedPlayer.transform.position.x, Is.EqualTo(8.5f).Within(0.01f),
                "Returning from the tower should place the player beside the town gate");
            Assert.That(returnedPlayer.transform.position.z, Is.EqualTo(5f).Within(0.01f));
            yield return null; // Allow generated objects from the previous map to be destroyed.
            var rebuiltTown = root.GetComponentsInChildren<Transform>(true);
            Assert.That(Array.FindAll(rebuiltTown, x => x.name == "Grass Terrain").Length, Is.EqualTo(1),
                "Returning from the tower must rebuild one Town terrain instance");
            Assert.That(Array.FindAll(rebuiltTown, x => x.name == "Inn").Length, Is.EqualTo(1));
            Assert.That(Array.FindAll(rebuiltTown, x => x.name == "Market").Length, Is.EqualTo(1));
            Assert.That(Array.FindAll(rebuiltTown, x => x.name.Contains("Front Fence ")).Length, Is.EqualTo(4),
                "Town rebuild must restore the Inn and Market frontage fence pairs exactly once");
            typeof(TowerGameController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle),
                "The retail door must finish its angle ramp before changing maps");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && flow.State.Current != TownGameState.State.TowerRunning)
                yield return null;
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TowerRunning),
                "The return spawn must face and reach the gate for another tower entry");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator Town_GateRequiresFacingAndExplicitInteraction()
    {
        var root = new GameObject("town-gate-input-qa");
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var gate = Array.Find(root.GetComponentsInChildren<AzureDreamsReimagined.EventTrigger>(),
                trigger => trigger.triggerId == "tower_gate");
            Assert.That(gate, Is.Not.Null, "Town entrance must be connected to its door event");
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            var flow = root.GetComponent<TownEventController>();
            var interact = typeof(TowerGameController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance);

            player.transform.position = gate.transform.position + Vector3.left;
            game.MoveInTown(Vector2.right, 0.01f);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle),
                "Walking into the door area must not change maps");

            game.MoveInTown(Vector2.left, 0.01f);
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle),
                "The door must reject interaction when the player faces away");

            game.MoveInTown(Vector2.up, 0.01f);
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle),
                "The door must reject a side-on approach that does not match its angle");

            game.MoveInTown(Vector2.right, 0.01f);
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle),
                "The map transition must wait for the retail door opening steps");
            var leaf = gate.GetComponent<TownGateDoor>();
            Assert.That(leaf, Is.Not.Null, "The Town gate must have a hinged door leaf");
            var left = gate.transform.Find("Left door hinge");
            var right = gate.transform.Find("Right door hinge");
            Assert.That(left, Is.Not.Null);
            Assert.That(right, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 5f;
            bool opened = false, crossed = false;
            while (Time.realtimeSinceStartup < deadline && flow.State.Current != TownGameState.State.TowerRunning)
            {
                float angle = Quaternion.Angle(Quaternion.identity, left.localRotation);
                Assert.That(Quaternion.Angle(left.localRotation, Quaternion.Inverse(right.localRotation)), Is.LessThan(0.1f));
                opened |= angle > 85f;
                crossed |= Vector3.Dot(player.transform.position - gate.transform.position, gate.transform.forward) > 0.3f;
                yield return null;
            }
            Assert.That(opened && crossed, Is.True, "Both leaves must open and Koh must cross before the map changes");
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TowerRunning),
                "The tower transition must follow the gate sequence");
            var inner = root.transform.Find("Tower inner double doors");
            Assert.That(inner, Is.Not.Null, "Every new ascent must appear at the inner doors");
            var innerLeft = inner.Find("Left door hinge");
            Assert.That(Quaternion.Angle(Quaternion.identity, innerLeft.localRotation), Is.GreaterThan(85f));
            deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline &&
                (bool)typeof(TowerGameController).GetField("_towerArrivalPlaying", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game))
                yield return null;
            Assert.That(Quaternion.Angle(Quaternion.identity, innerLeft.localRotation), Is.LessThan(0.1f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator Town_GateAcceptsOriginalDiagonalApproachCone()
    {
        var root = new GameObject("town-gate-diagonal-qa");
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var gate = Array.Find(root.GetComponentsInChildren<AzureDreamsReimagined.EventTrigger>(),
                trigger => trigger.triggerId == "tower_gate");
            Assert.That(gate, Is.Not.Null);
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            var flow = root.GetComponent<TownEventController>();
            var interact = typeof(TowerGameController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance);

            // Face north while approaching from a 60-degree diagonal. The PS1
            // door accepts this (< 89.912 degrees), unlike a 45-degree cutoff.
            player.transform.position = gate.transform.position + new Vector3(-0.8660254f, 0f, -0.5f);
            game.MoveInTown(Vector2.up, 0.001f);
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle),
                "The gate must finish its retail opening ramp before changing maps");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline && flow.State.Current != TownGameState.State.TowerRunning)
                yield return null;
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TowerRunning),
                "The original 60-degree approach must start the gate sequence");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [UnityTest]
    public IEnumerator Town_SignClosesOnEAndChestRemembersOpening()
    {
        var root = new GameObject("town-objects-qa");
        bool previousChest = TownSaveVariables.GetBool("object_town_empty_chest");
        TownSaveVariables.SetBool("object_town_empty_chest", false);
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var objects = root.GetComponentsInChildren<TownObject>();
            var sign = Array.Find(objects, x => x.objectId == "guild_sign");
            var chest = Array.Find(objects, x => x.objectId == "town_empty_chest");
            Assert.That(sign, Is.Not.Null, "Town sign must be interactive");
            Assert.That(chest, Is.Not.Null, "Town chest must be interactive");
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            var interact = typeof(TowerGameController).GetMethod("Interact", BindingFlags.NonPublic | BindingFlags.Instance);
            var flow = root.GetComponent<TownEventController>();
            player.transform.position = sign.transform.position + Vector3.right * 0.8f;
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownDialogue));
            interact.Invoke(game, null);
            Assert.That(flow.State.Current, Is.EqualTo(TownGameState.State.TownIdle));
            player.transform.position = chest.transform.position + Vector3.right * 0.8f;
            Assert.That(chest.OnInteract(player), Is.True);
            interact.Invoke(game, null);
            Assert.That(chest.OnInteract(player), Is.False);
            Assert.That(TownSaveVariables.GetBool("object_town_empty_chest"), Is.True);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            TownSaveVariables.SetBool("object_town_empty_chest", previousChest);
        }
    }

    [UnityTest]
    public IEnumerator Trigger_PhysicsPublishesOncePerPlayerEntryWithMultipleColliders()
    {
        var zone = new GameObject("physics-trigger-qa");
        var actor = new GameObject("physics-player-qa");
        int events = 0;
        Action<object> handler = payload => events++;
        TownEventBus.Subscribe(TownEventBus.EventId.ObjectInteracted, handler);
        try
        {
            var trigger = zone.AddComponent<AzureDreamsReimagined.EventTrigger>();
            trigger.once = false;
            trigger.persistActivation = false;
            zone.GetComponent<BoxCollider>().size = Vector3.one * 2f;
            actor.tag = "Player";
            actor.transform.position = Vector3.one * 10;
            var body = actor.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            actor.AddComponent<BoxCollider>();
            var child = new GameObject("second-collider");
            child.transform.SetParent(actor.transform, false);
            child.AddComponent<SphereCollider>();
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            actor.transform.position = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(events, Is.EqualTo(1));
            actor.transform.position = Vector3.one * 10;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            actor.transform.position = Vector3.zero;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
            yield return null;
            Assert.That(events, Is.EqualTo(2));
        }
        finally
        {
            TownEventBus.Unsubscribe(TownEventBus.EventId.ObjectInteracted, handler);
            UnityEngine.Object.DestroyImmediate(zone);
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [UnityTest]
    public IEnumerator Town_PlayerCollisionTracksCurrentNpcPosition()
    {
        var root = new GameObject("moving-npc-collision-qa");
        try
        {
            var game = root.AddComponent<TowerGameController>();
            yield return null;
            var npc = root.GetComponentInChildren<NPCController>();
            npc.transform.position = new Vector3(6, 0.55f, 5);
            var player = (GameObject)typeof(TowerGameController).GetField("_player", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(game);
            game.MoveInTown(Vector2.right, 0.25f);
            Assert.That(player.transform.position.x, Is.LessThan(5.3f));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void Trigger_RejectsNonPlayerAndPersistsOneShotActivation()
    {
        var type = typeof(ItemPickup).Assembly.GetType("AzureDreamsReimagined.EventTrigger");
        Assert.That(type, Is.Not.Null, "Town trigger component is required");
        var go = new GameObject("zone");
        var actor = new GameObject("visitor");
        string id = "qa_" + Guid.NewGuid().ToString("N");
        int events = 0;
        Action<object> handler = payload => events++;
        TownEventBus.Subscribe(TownEventBus.EventId.ObjectInteracted, handler);
        try
        {
            var trigger = go.AddComponent(type);
            type.GetField("triggerId").SetValue(trigger, id);
            var activate = type.GetMethod("TryActivate");
            Assert.That(activate.Invoke(trigger, new object[] { actor }), Is.EqualTo(false));
            actor.tag = "Player";
            Assert.That(activate.Invoke(trigger, new object[] { actor }), Is.EqualTo(true));
            Assert.That(activate.Invoke(trigger, new object[] { actor }), Is.EqualTo(false));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(TownSaveVariables.GetBool("trigger_" + id), Is.True);
            Assert.That(go.GetComponent<BoxCollider>().isTrigger, Is.True);
        }
        finally
        {
            TownEventBus.Unsubscribe(TownEventBus.EventId.ObjectInteracted, handler);
            PlayerPrefs.DeleteKey("ad_town_trigger_" + id);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void Npc_PatrolUsesSharedMovementAndRoutesAroundObstacles(bool building)
    {
        var configure = typeof(NPCController).GetMethod("ConfigurePatrol");
        Assert.That(configure, Is.Not.Null, "TownRoute patrol adapter is required");
        var go = new GameObject("patroller");
        try
        {
            go.transform.position = new Vector3(2, 0.55f, 2);
            var npc = go.AddComponent<NPCController>();
            npc.waypointWaitTime = 0f;
            npc.patrolSpeed = 2f;
            var route = new TownRoute();
            route.Waypoints.Add(new Vector2Int(2, 2));
            route.Waypoints.Add(new Vector2Int(4, 2));
            var wall = new Vector2Int(3, 2);
            var walls = new System.Collections.Generic.HashSet<Vector2Int>();
            if (!building) walls.Add(wall);
            var buildings = building ? new[] { new Rect(2.75f, 1.75f, 0.5f, 0.5f) } : null;
            configure.Invoke(npc, new object[] { route, walls, 8, buildings });
            var tick = typeof(NPCController).GetMethod("TickPatrol");
            bool reached = false;
            for (int i = 0; i < 180; i++)
            {
                tick.Invoke(npc, new object[] { 0.05f });
                var cell = new Vector2Int(Mathf.RoundToInt(go.transform.position.x), Mathf.RoundToInt(go.transform.position.z));
                Assert.That(cell, Is.Not.EqualTo(wall));
                if (Vector3.Distance(go.transform.position, new Vector3(4, 0.55f, 2)) < 0.1f) reached = true;
            }
            Assert.That(reached, Is.True);
            Assert.That(go.GetComponent<TownMovementController>(), Is.Not.Null);
            Assert.That(go.transform.position.y, Is.EqualTo(0.55f).Within(0.0001f));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    [Test]
    public void Npc_TalkAdvancesAuthoredLinesAndPublishesIdentity()
    {
        var dataType = typeof(ItemPickup).Assembly.GetType("AzureDreamsReimagined.NPCDialogue");
        Assert.That(dataType, Is.Not.Null, "NPCDialogue data asset is required");
        var go = new GameObject("speaker");
        var data = ScriptableObject.CreateInstance(dataType);
        string id = "qa_" + Guid.NewGuid().ToString("N");
        int events = 0;
        Action<object> handler = payload => events++;
        TownEventBus.Subscribe(TownEventBus.EventId.NpcTalked, handler);
        try
        {
            var npc = go.AddComponent<NPCController>();
            dataType.GetField("lines").SetValue(data, new[] { "First", "Second" });
            typeof(NPCController).GetField("dialogue").SetValue(npc, data);
            typeof(NPCController).GetField("npcId").SetValue(npc, id);
            var talk = typeof(NPCController).GetMethod("Talk");
            Assert.That(talk, Is.Not.Null);
            Assert.That(talk.Invoke(npc, null), Is.EqualTo("First"));
            Assert.That(talk.Invoke(npc, null), Is.EqualTo("Second"));
            Assert.That(events, Is.EqualTo(2));
            Assert.That(TownSaveVariables.GetBool("npc_" + id), Is.True);
        }
        finally
        {
            TownEventBus.Unsubscribe(TownEventBus.EventId.NpcTalked, handler);
            PlayerPrefs.DeleteKey("ad_town_npc_" + id);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    [Test]
    public void Fountain_RestoresHealthAndManaBeforePublishing()
    {
        var type = typeof(ItemPickup).Assembly.GetType("AzureDreamsReimagined.WaterFountain");
        Assert.That(type, Is.Not.Null, "WaterFountain component is required");
        var go = new GameObject("fountain");
        var actor = new GameObject("visitor");
        string id = "qa_" + Guid.NewGuid().ToString("N");
        Action<object> handler = null;
        try
        {
            var fountain = go.AddComponent(type);
            type.GetField("objectId").SetValue(fountain, id);
            var stats = actor.AddComponent<CharacterStats>();
            stats.maxHealth = 25;
            stats.currentHealth = 2;
            var mana = typeof(CharacterStats).GetField("currentMana");
            var maxMana = typeof(CharacterStats).GetField("maxMana");
            Assert.That(mana, Is.Not.Null);
            Assert.That(maxMana, Is.Not.Null);
            mana.SetValue(stats, 1);
            maxMana.SetValue(stats, 9);
            int events = 0;
            handler = payload => { Assert.That(stats.currentHealth, Is.EqualTo(25)); events++; };
            TownEventBus.Subscribe(TownEventBus.EventId.FountainUsed, handler);
            var result = type.GetMethod("OnInteract").Invoke(fountain, new object[] { actor });
            Assert.That(result, Is.EqualTo(true));
            Assert.That(mana.GetValue(stats), Is.EqualTo(9));
            Assert.That(events, Is.EqualTo(1));
            Assert.That(TownSaveVariables.GetBool("object_" + id), Is.True);
        }
        finally
        {
            if (handler != null) TownEventBus.Unsubscribe(TownEventBus.EventId.FountainUsed, handler);
            PlayerPrefs.DeleteKey("ad_town_object_" + id);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(actor);
        }
    }

    [Test]
    public void Pickup_PublishesExactlyOnceAfterInventoryCommit()
    {
        var go = new GameObject("event-pickup");
        var item = ScriptableObject.CreateInstance<ItemDefinition>();
        var inventory = new Inventory();
        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;
        pickup.destroyOnPickup = false;
        int events = 0;
        Action<object> handler = payload =>
        {
            Assert.That(inventory.GetCount(item), Is.EqualTo(1));
            Assert.That(payload, Is.Not.Null);
            events++;
        };
        TownEventBus.Subscribe(TownEventBus.EventId.ItemCollected, handler);
        try
        {
            Assert.That(pickup.TryCollect(inventory), Is.True);
            Assert.That(pickup.TryCollect(inventory), Is.False);
            Assert.That(events, Is.EqualTo(1));
        }
        finally
        {
            TownEventBus.Unsubscribe(TownEventBus.EventId.ItemCollected, handler);
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    [Test]
    public void RetailTownScheduler_UsesRetailRowAndDispatchOrder()
    {
        var backend = new RecordingTownRetailBackend();
        var scheduler = new TownRetailEventScheduler(0);
        var dispatched = new System.Collections.Generic.List<int>();

        Assert.That(scheduler.Advance(backend, dispatched), Is.True);
        Assert.That(scheduler.CurrentIndex, Is.EqualTo(1));
        Assert.That(backend.Resets, Is.EqualTo(new[]
        {
            0x0607, 0x0608, 0x0609, 0x060A, 0x060B, 0x060C, 0x060D,
            1550, 1542, 1553
        }));
        Assert.That(dispatched, Is.EqualTo(new[] { 0x0608, 1550, 1542 }));
        Assert.That(backend.Dispatches, Is.EqualTo(dispatched));
    }

    [Test]
    public void RetailTownScheduler_PoolEventGuardResetsThenStops()
    {
        var backend = new RecordingTownRetailBackend();
        backend.SetFlags.Add(0x1201);
        var scheduler = new TownRetailEventScheduler(3);
        var dispatched = new System.Collections.Generic.List<int>();

        Assert.That(scheduler.Advance(backend, dispatched), Is.False);
        Assert.That(scheduler.CurrentIndex, Is.EqualTo(4));
        Assert.That(backend.Resets.Count, Is.EqualTo(10));
        Assert.That(dispatched, Is.Empty);
        Assert.That(backend.Queries, Is.EqualTo(new[] { 0x1200, 0x1201 }));
    }

    [Test]
    public void RetailTownScheduler_EnabledEntryRequiresItsConditionFlagSet()
    {
        var backend = new RecordingTownRetailBackend();
        backend.SetFlags.Add(0x1472); // Ft_selfy_love
        var scheduler = new TownRetailEventScheduler(1);
        var dispatched = new System.Collections.Generic.List<int>();

        Assert.That(scheduler.Advance(backend, dispatched), Is.True);
        Assert.That(scheduler.CurrentIndex, Is.EqualTo(2));
        Assert.That(dispatched, Is.EqualTo(new[] { 0x0609, 1550, 1542 }));
    }

    [Test]
    public void RetailTownScheduler_FullScanAtCursorFiveOmitsFirstTrailingState()
    {
        var backend = new RecordingTownRetailBackend();
        backend.SetFlags.Add(0x11FA);
        backend.SetFlags.Add(0x11FB);
        var scheduler = new TownRetailEventScheduler(4);
        var dispatched = new System.Collections.Generic.List<int>();

        Assert.That(scheduler.Advance(backend, dispatched), Is.True);
        Assert.That(scheduler.CurrentIndex, Is.EqualTo(5));
        Assert.That(dispatched, Is.EqualTo(new[] { 1542 }));
        Assert.That(backend.Dispatches, Is.EqualTo(dispatched));
    }

    [Test]
    public void RetailStorySelector_SeroEndingBranchHasHighestPriority()
    {
        var backend = new RecordingTownRetailStoryBackend();
        backend.SetFlags.Add(5213); // F_sero_end
        var state = new TownRetailStorySelectionState();

        Assert.That(new TownRetailStoryEventSelector().Select(backend, state, CreateRetailSelectionTable()), Is.EqualTo(9));
        Assert.That(state.SelectionMode, Is.EqualTo(9));
        Assert.That(backend.OneTimeFlagOperations, Is.EqualTo(new[] { 5205 }));
        Assert.That(backend.EventFlagOperations, Is.EqualTo(new[] { 4602 }));
    }

    [Test]
    public void RetailStorySelector_StoresVeldilEntryAndSelectsItsMode()
    {
        var backend = new RecordingTownRetailStoryBackend();
        backend.SetFlags.Add(5001); // F_veldil_dungeon_deatta
        backend.SetFlags.Add(207);
        var state = new TownRetailStorySelectionState();

        Assert.That(new TownRetailStoryEventSelector().Select(backend, state, CreateRetailSelectionTable()), Is.EqualTo(3));
        Assert.That(state.SavedEntry, Is.EqualTo(7));
        Assert.That(state.SelectedEntry, Is.EqualTo(7));
        Assert.That(state.SelectionMode, Is.EqualTo(3));
        Assert.That(backend.OneTimeFlagOperations, Is.EqualTo(new[] { 1029, 1022 }));
    }

    [Test]
    public void RetailStorySelector_UsesPairedEntryThenAdvancesWhenNoMatch()
    {
        var backend = new RecordingTownRetailStoryBackend();
        backend.SetFlags.UnionWith(new[] { 202, 204, 302, 304 });
        var state = new TownRetailStorySelectionState();
        state.EntryIndex = 2;

        Assert.That(new TownRetailStoryEventSelector().Select(backend, state, CreateRetailSelectionTable()), Is.EqualTo(2));
        Assert.That(state.SelectedEntry, Is.EqualTo(4));
        Assert.That(state.EntryIndex, Is.EqualTo(3));
        Assert.That(backend.OneTimeFlagOperations, Is.Empty);
    }

    [Test]
    public void RetailStorySelector_UsesEggFallbackOnlyWhenEligible()
    {
        var selector = new TownRetailStoryEventSelector();
        var backend = new RecordingTownRetailStoryBackend();
        var state = new TownRetailStorySelectionState();
        backend.SetFlags.Add(5211); // F_selfy_get_tamago

        var table = CreateRetailSelectionTable();
        Assert.That(selector.Select(backend, state, table), Is.EqualTo(1));
        backend.SetFlags.Add(5230); // Ft_niko_love
        Assert.That(selector.Select(backend, state, table), Is.EqualTo(0));
    }

    [Test]
    public void RetailStorySelector_SelectsFirstPairedRecordAndConsumesItsPairedFlag()
    {
        var backend = new RecordingTownRetailStoryBackend();
        backend.SetFlags.Add(201);
        var state = new TownRetailStorySelectionState();

        Assert.That(new TownRetailStoryEventSelector().Select(backend, state, CreateRetailSelectionTable()), Is.EqualTo(2));
        Assert.That(state.SelectedEntry, Is.EqualTo(1));
        Assert.That(backend.OneTimeFlagOperations, Is.EqualTo(new[] { 301 }));
    }

    private static TownRetailSelectionTable CreateRetailSelectionTable()
    {
        var records = new TownRetailSelectionRecord[8];
        for (int id = 1; id <= 7; id++) records[id] = new TownRetailSelectionRecord(200 + id, 300 + id);
        return new TownRetailSelectionTable(records, new byte[] { 7, 1, 0 },
            new byte[] { 1, 2, 3, 4, 5, 6, 7, 0 });
    }

    [Test]
    public void RetailStorySelector_ExtractedSceneTablePreservesWeightedCycleAndPrioritySubset()
    {
        var table = TownRetailSelectionTable.CreateForScene3E5000();
        var backend = new RecordingTownRetailStoryBackend();
        // Only entry 4 is enabled. Its paired flag has already been processed.
        backend.SetFlags.UnionWith(new[] { 5242, 1018 });
        var state = new TownRetailStorySelectionState();
        var selector = new TownRetailStoryEventSelector();
        Assert.That(selector.Select(backend, state, table), Is.EqualTo(2));
        Assert.That(state.SelectedEntry, Is.EqualTo(4));
        Assert.That(state.EntryIndex, Is.EqualTo(9), "Cursor addresses the weighted list, not an entry ID.");
        // The Veldil priority list contains only entries 1,2,3,6, even though
        // entry 4 occurs in the cycling list and is enabled.
        backend.SetFlags.Add(5001);
        Assert.That(selector.Select(backend, state, table), Is.EqualTo(4));
        Assert.That(state.SavedEntry, Is.Zero);
    }

    [Test]
    public void RetailScriptFlags_PreserveConstantQueriesAndWordBoundaries()
    {
        var words = new System.Collections.Generic.Dictionary<int, uint>();
        var flags = new TownRetailScriptFlags(
            index => words.TryGetValue(index, out var value) ? value : 0,
            (index, value) => words[index] = value);
        flags.SetBit(0);
        flags.ClearBit(1);
        Assert.That(words, Is.Empty);
        Assert.That(flags.IsFlagSet(0), Is.False);
        Assert.That(flags.IsFlagSet(1), Is.True);
        flags.SetBit(31);
        flags.SetBit(32);
        Assert.That(flags.GetMask(31), Is.EqualTo(0x80000000u));
        Assert.That(flags.GetMask(32), Is.EqualTo(1u));
        flags.ClearBit(31);
        Assert.That(flags.IsFlagSet(31), Is.False);
        Assert.That(flags.IsFlagSet(32), Is.True);
    }

    [Test]
    public void RetailScriptFlags_SelectorSetsMorningAndClearsConflictOnlyOnce()
    {
        var words = new System.Collections.Generic.Dictionary<int, uint>();
        var flags = new TownRetailScriptFlags(
            index => words.TryGetValue(index, out var value) ? value : 0,
            (index, value) => words[index] = value);
        flags.SetBit(5213);
        flags.SetBit(4602);
        var selector = new TownRetailStoryEventSelector();
        var state = new TownRetailStorySelectionState();
        var table = TownRetailSelectionTable.CreateForScene3E5000();
        Assert.That(selector.Select(flags, state, table), Is.EqualTo(9));
        Assert.That(flags.IsFlagSet(5205), Is.True);
        Assert.That(flags.IsFlagSet(4602), Is.False);
        Assert.That(selector.Select(flags, state, table), Is.EqualTo(0));
    }

    [Test]
    public void RetailStoryDispatcher_PreservesPhaseOrderAndDoesNotRestart()
    {
        var backend = new RecordingTownRetailStoryBackend();
        backend.SetFlags.Add(5213);
        var state = new TownRetailStorySelectionState();
        var calls = new System.Collections.Generic.List<string>();
        var dispatcher = new TownRetailStoryDispatcher(backend, state,
            (command, value) => calls.Add(state.DispatchState + ":" + command + ":" + value));
        Assert.That(dispatcher.TryStart(), Is.True);
        Assert.That(dispatcher.TryStart(), Is.False);
        Assert.That(dispatcher.DispatchEnabledMessage(), Is.True);
        Assert.That(dispatcher.DispatchEnabledMessage(), Is.False);
        Assert.That(state.DispatchState, Is.EqualTo(2));
        Assert.That(calls, Is.EqualTo(new[] { "1:9:512", "1:9:512" }));
        Assert.That(backend.OneTimeFlagOperations, Is.EqualTo(new[] { 5205, 1032 }));
    }

    [Test]
    public void RetailStoryDispatcher_ActiveCheckReturnsPhaseStatusEvenWithoutMatchingCommand()
    {
        var state = new TownRetailStorySelectionState();
        int calls = 0;
        var dispatcher = new TownRetailStoryDispatcher(new RecordingTownRetailStoryBackend(), state,
            (command, value) => calls++);
        Assert.That(dispatcher.DispatchActiveEvent27(), Is.True);
        Assert.That(calls, Is.Zero);
        state.SelectionMode = 3;
        Assert.That(dispatcher.DispatchActiveEvent27(), Is.True);
        Assert.That(calls, Is.EqualTo(1));
        state.DispatchState = 2;
        Assert.That(dispatcher.DispatchActiveEvent27(), Is.False);
        Assert.That(calls, Is.EqualTo(1));
    }

    [Test]
    public void RetailTownAudio_StorySelectionReservesThenAppliesAndHonorsHold()
    {
        var flags = new RecordingTownRetailStoryBackend();
        flags.SetFlags.Add(5213);
        var audio = new TownRetailAudioState();
        var dispatcher = new TownRetailStoryDispatcher(flags, new TownRetailStorySelectionState(), audio);
        Assert.That(dispatcher.TryStart(), Is.True);
        Assert.That(audio.Pending.Stream, Is.EqualTo(9));
        Assert.That(audio.Active.Stream, Is.EqualTo(-1), "Reservation must defer playback until application.");
        var backend = new RecordingTownAudioBackend();
        audio.Apply(backend);
        Assert.That(backend.Calls, Is.EqualTo(new[] { "command:113", "drain", "stream:9:0", "command:512" }));
        Assert.That(audio.Saved.Stream, Is.EqualTo(9));
        Assert.That(audio.Pending.Stream, Is.EqualTo(-1));
        backend.Calls.Clear();
        audio.HoldCurrentSequence = true;
        audio.Apply(backend);
        Assert.That(backend.Calls, Is.Empty);
        Assert.That(audio.HoldCurrentSequence, Is.False);
        audio.Apply(backend);
        Assert.That(backend.Calls, Is.EqualTo(new[] { "command:113", "drain", "command:512" }));
    }

    [Test]
    public void RetailTownAudio_ClearSentinelStopsWithoutLoadingNegativeStream()
    {
        var audio = new TownRetailAudioState();
        var backend = new RecordingTownAudioBackend();
        audio.ReserveSequence(9, 512);
        audio.Apply(backend);
        backend.Calls.Clear();
        audio.ReserveSequence(-5, -5);
        audio.Apply(backend);
        Assert.That(backend.Calls, Is.EqualTo(new[] { "command:113", "drain" }));
        Assert.That(audio.Active.Stream, Is.EqualTo(-1));
        Assert.That(audio.Active.SequenceCommand, Is.EqualTo(-1));
    }

    private sealed class RecordingTownAudioBackend : ITownRetailAudioBackend
    {
        public readonly System.Collections.Generic.List<string> Calls = new System.Collections.Generic.List<string>();
        public void SendCommand(ushort command) => Calls.Add("command:" + command);
        public void DrainCommands() => Calls.Add("drain");
        public void SelectStream(short streamIndex, short mode) => Calls.Add("stream:" + streamIndex + ":" + mode);
    }

    [Test]
    public void RetailTownGrid_UsesGlobalParityAndTransparentPatchCells()
    {
        var grid = new TownRetailTileGrid();
        grid.ApplyPatch(1, 1, new ushort[] { 2, 2, 0x135, 0x135, 0x135, 0x135 });
        Assert.That(grid.ReadRaw(1, 1), Is.EqualTo(0x15E));
        Assert.That(grid.ReadRaw(2, 1), Is.EqualTo(0x137));
        Assert.That(grid.ReadRaw(1, 2), Is.EqualTo(0x154));
        Assert.That(grid.ReadRaw(2, 2), Is.EqualTo(0x135));
        grid.ApplyPatch(1, 1, new ushort[] { 2, 1, 0, 0xC123 });
        Assert.That(grid.ReadRaw(1, 1), Is.EqualTo(0x15E));
        Assert.That(grid.ReadRaw(2, 1), Is.EqualTo(0xC123));
        Assert.That(grid.ReadTileId(130, 129), Is.EqualTo(0x123));
        grid.BlitNonzeroPatch(1, 1, new ushort[] { 2, 1, 0x135, 0 });
        Assert.That(grid.ReadRaw(1, 1), Is.EqualTo(0x135), "Raw grid blits must preserve IDs without parity substitution.");
        Assert.That(grid.ReadRaw(2, 1), Is.EqualTo(0xC123));
        grid.BlitLittleEndianPatch(1, 1, new byte[] { 1, 0, 1, 0, 0x36, 1 }, 0);
        Assert.That(grid.ReadRaw(1, 1), Is.EqualTo(0x136));
    }

    [Test]
    public void RetailTownGrid_TruncatesNegativeWorldCoordinatesBeforeWrapping()
    {
        var grid = new TownRetailTileGrid();
        grid.ApplyPatch(0, 0, new ushort[] { 1, 1, 7 });
        grid.ApplyPatch(127, 0, new ushort[] { 1, 1, 8 });
        Assert.That(grid.ReadWorldTileId(-63, 0), Is.EqualTo(7));
        Assert.That(grid.ReadWorldTileId(-64, 0), Is.EqualTo(8));
        Assert.Throws<ArgumentException>(() => grid.ApplyPatch(127, 0, new ushort[] { 2, 1, 9, 9 }));
        Assert.That(grid.ReadRaw(127, 0), Is.EqualTo(8));
    }

    [TestCase(0x135, 0x135, 0x154, 0x137, 0x15E)]
    [TestCase(0x136, 0x153, 0x136, 0x15D, 0x138)]
    [TestCase(0x141, 0x141, 0x142, 0x141, 0x142)]
    [TestCase(0x143, 0x15B, 0x15C, 0x143, 0x144)]
    [TestCase(0x13D, 0x13D, 0x13E, 0x13F, 0x140)]
    [TestCase(0x149, 0x149, 0x14A, 0x14B, 0x14C)]
    public void RetailTownGrid_ParityFamiliesMatchSource(int source, int a, int b, int c, int d)
    {
        var grid = new TownRetailTileGrid();
        grid.ApplyPatch(0, 0, new ushort[] { 2, 2, (ushort)source, (ushort)source, (ushort)source, (ushort)source });
        Assert.That(new[] { (int)grid.ReadRaw(0, 0), grid.ReadRaw(1, 0), grid.ReadRaw(0, 1), grid.ReadRaw(1, 1) },
            Is.EqualTo(new[] { a, b, c, d }));
    }

    [Test]
    public void RetailTownGrid_LoadsTheAuthenticatedDiscBank()
    {
        var bank = Resources.Load<TextAsset>("Town/Retail/town_layout_bank");
        Assert.That(bank, Is.Not.Null);
        var grid = new TownRetailTileGrid();
        grid.ApplyLittleEndianPatch(0, 0, bank.bytes, 8); // native entry 1, RAM 0x8011ACA0
        Assert.That(grid.ReadRaw(0, 0), Is.EqualTo(99));
        Assert.That(grid.ReadRaw(1, 0), Is.EqualTo(100));
        Assert.That(grid.ReadRaw(5, 0), Is.EqualTo(114));
    }

    private sealed class RecordingTownRetailBackend : ITownRetailEventBackend
    {
        public readonly System.Collections.Generic.HashSet<int> SetFlags = new System.Collections.Generic.HashSet<int>();
        public readonly System.Collections.Generic.List<int> Resets = new System.Collections.Generic.List<int>();
        public readonly System.Collections.Generic.List<int> Queries = new System.Collections.Generic.List<int>();
        public readonly System.Collections.Generic.List<int> Dispatches = new System.Collections.Generic.List<int>();

        public bool IsFlagSet(int flagId)
        {
            Queries.Add(flagId);
            return SetFlags.Contains(flagId);
        }

        public void ResetEventState(int stateId) => Resets.Add(stateId);
        public void DispatchEvent(int eventId) => Dispatches.Add(eventId);
    }

    private sealed class RecordingTownRetailStoryBackend : ITownRetailStoryEventBackend
    {
        public readonly System.Collections.Generic.HashSet<int> SetFlags = new System.Collections.Generic.HashSet<int>();
        public readonly System.Collections.Generic.List<int> OneTimeFlagOperations = new System.Collections.Generic.List<int>();
        public readonly System.Collections.Generic.List<int> EventFlagOperations = new System.Collections.Generic.List<int>();
        public bool IsFlagSet(int flagId) => SetFlags.Contains(flagId);
        public void ApplyOneTimeFlagOperation(int flagId) => OneTimeFlagOperations.Add(flagId);
        public void ApplyEventFlagOperation(int flagId) => EventFlagOperations.Add(flagId);
    }
}
