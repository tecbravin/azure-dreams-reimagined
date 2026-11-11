using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class NPCControllerTests
{
    private GameObject _go;
    private NPCController _npc;
    private CharacterStats _stats;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("NPC");
        _stats = _go.AddComponent<CharacterStats>();
        _npc = _go.AddComponent<NPCController>();
        _npc.dialogueLines = new string[] { "Hello", "Welcome" };
        _npc.loopDialogue = true;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_go);
    }

    [Test]
    public void Interact_AdvancesDialogue_LoopMode()
    {
        var player = new GameObject("Player");
        var playerCtrl = player.AddComponent<PlayerController>();

        _npc.Interact(playerCtrl);
        _npc.Interact(playerCtrl);
        _npc.Interact(playerCtrl);

        // After 3 interactions, index should be 2 then wrap to 0
        var idxField = typeof(NPCController).GetField("_currentDialogueIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int idx = (int)idxField.GetValue(_npc);
        Assert.AreEqual(1, idx);

        Object.Destroy(player);
    }

    [Test]
    public void Interact_StopsAtEnd_WhenLoopFalse()
    {
        _npc.loopDialogue = false;

        var player = new GameObject("Player");
        var playerCtrl = player.AddComponent<PlayerController>();

        _npc.Interact(playerCtrl);
        _npc.Interact(playerCtrl);

        var idxField = typeof(NPCController).GetField("_currentDialogueIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int idx = (int)idxField.GetValue(_npc);
        Assert.AreEqual(1, idx);

        Object.Destroy(player);
    }

    [Test]
    public void Patrol_Waypoints_AdvancesIndex()
    {
        _npc.patrolWaypoints = new Transform[3];
        for (int i = 0; i < 3; i++)
        {
            var wp = new GameObject("WP" + i);
            wp.transform.position = new Vector3(i, 0f, 0f);
            _npc.patrolWaypoints[i] = wp.transform;
        }

        var idxField = typeof(NPCController).GetField("_currentWaypointIndex", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var waitField = typeof(NPCController).GetField("_waitTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Simulate reaching first waypoint and waiting via private Update()
        var updateMethod = typeof(NPCController).GetMethod("Update",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        updateMethod.Invoke(_npc, null);
        Assert.AreEqual(0, (int)idxField.GetValue(_npc));
        Assert.Greater((float)waitField.GetValue(_npc), 0f);
    }
}
