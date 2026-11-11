using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AzureDreamsReimagined;

public class CoderNpcStartupTests
{
    [UnityTest]
    public IEnumerator FallbackNpc_RequireComponentSuppliesAndCachesTownMover()
    {
        var go = new GameObject("NPC dependency probe");
        try
        {
            var npc = go.AddComponent<NPCController>();
            Assert.That(go.GetComponent<TownMovementController>(), Is.Not.Null, "RequireComponent must add TownMovementController");
            var cached = typeof(NPCController).GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(npc);
            Assert.That(cached, Is.Not.Null, "NPC Awake must cache the required mover before ConfigurePatrol");
            npc.ConfigurePatrol(new TownRoute(), null, 12);
        }
        finally { Object.DestroyImmediate(go); }
        yield break;
    }
    [UnityTest]
    public IEnumerator InactiveNpc_CanConfigurePatrolBeforeAwake()
    {
        var go = new GameObject("Inactive NPC startup");
        go.SetActive(false);
        try
        {
            var npc = go.AddComponent<NPCController>();
            npc.ConfigurePatrol(new TownRoute(), null, 12);
            Assert.That(typeof(NPCController).GetField("_movement", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(npc), Is.Not.Null);
            go.SetActive(true);
            npc.TickPatrol(.1f);
        }
        finally { Object.DestroyImmediate(go); }
        yield break;
    }
    [UnityTest]
    public IEnumerator ExistingStats_NpcAddsAndCachesMovementBeforePatrol()
    {
        var go = new GameObject("Existing stats fallback NPC");
        try
        {
            go.AddComponent<CharacterStats>();
            var npc = go.AddComponent<NPCController>();
            Assert.That(go.GetComponent<TownMovementController>(), Is.Not.Null);
            npc.ConfigurePatrol(new TownRoute(), null, 12);
        }
        finally { Object.DestroyImmediate(go); }
        yield break;
    }
}
