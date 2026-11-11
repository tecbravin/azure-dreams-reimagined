using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PulunpaSpawnRuntimeTests
{
    [UnityTest]
    public IEnumerator PulunpaSpawn_ComponentInitializesRecord_OnRealGameObject()
    {
        var go = new GameObject("PulunpaRuntime");
        var component = go.AddComponent<AzureDreamsReimagined.PulunpaSpawn>();

        var rngType = Type.GetType("AzureDreams.Core.RetailRng, AzureDreams.Core");
        Assert.IsNotNull(rngType, "RetailRng type should be available through runtime reference.");
        var rng = Activator.CreateInstance(rngType, new object[] { 3u });

        var result = component.GetType().GetMethod("Initialize").Invoke(
            component, new object[] { 0, 0, 0, 0, rng, (byte)0 });

        Assert.IsNotNull(component);
        Assert.IsNotNull(component.GetType().GetProperty("Record").GetValue(component));
        Assert.IsTrue((bool)result.GetType().GetProperty("SleepRollPerformed").GetValue(result));

        yield return null;

        UnityEngine.Object.Destroy(go);
    }
}