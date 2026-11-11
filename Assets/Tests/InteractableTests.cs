using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class InteractableTests
{
    [Test]
    public void Interface_IsImplemented_ByNPCController()
    {
        var npc = new GameObject("NPC");
        var controller = npc.AddComponent<NPCController>();
        Assert.IsTrue(controller is Interactable);
        Object.Destroy(npc);
    }

    [Test]
    public void Interface_Method_IsCallable()
    {
        var npc = new GameObject("NPC");
        var controller = npc.AddComponent<NPCController>();
        var player = new GameObject("Player");
        var playerCtrl = player.AddComponent<PlayerController>();

        Assert.DoesNotThrow(() => controller.Interact(playerCtrl));

        Object.Destroy(npc);
        Object.Destroy(player);
    }
}
