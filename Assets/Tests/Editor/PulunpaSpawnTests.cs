using System;
using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;
using AzureDreams.Core;

public class PulunpaSpawnTests
{
    private static RetailRng MakeRng(uint seed) => new RetailRng(seed);

    private static uint AdvanceSeed(uint seed, int count)
    {
        uint s = seed;
        for (int i = 0; i < count; i++)
            s = (s * 0x41c64e6du + 0x3039u);
        return s;
    }

    [Test]
    public void Kind1_SetsPartyAndPlayerFlags_NoRng()
    {
        var go = new GameObject("PulunpaKind1");
        var component = go.AddComponent<PulunpaSpawn>();
        var rng = MakeRng(3u);
        uint seedBefore = rng.Seed;

        var result = component.Initialize(1, 5, 7, 9, rng, 0);

        Assert.AreEqual((byte)MonsterSpecies.PULUNPA, result.Record.Species);
        Assert.AreEqual(0x6000, result.Record.Flags14);
        Assert.AreEqual(0x6000, result.Record.Flags1C);
        Assert.AreEqual(PulunpaSpawn.FrameSetChoice.FrameSet1, result.FrameSet);
        Assert.AreEqual(PulunpaSpawn.CallbackChoice.NormalResume, result.Callback);
        Assert.AreEqual((byte)0, result.ByteA7);
        Assert.IsFalse(result.SleepRollPerformed);
        Assert.IsFalse(result.SleepApplied);
        Assert.AreEqual(seedBefore, rng.Seed);
        Assert.AreEqual(0xFF, result.Record.U8(0x9A));
        Assert.AreEqual(unchecked((byte)-1), result.Record.U8(0x9C));
        Assert.AreEqual(0x00, result.Record.U8(0xA8));

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void Kind2And3_SetClassAndPlayerSide_WithoutParty()
    {
        for (int flags = 2; flags <= 3; flags++)
        {
            var go = new GameObject("PulunpaKind" + flags);
            var component = go.AddComponent<PulunpaSpawn>();
            var rng = MakeRng(1u);

            var result = component.Initialize(flags, 1, 2, 3, rng, 0);

            Assert.AreEqual(0x2000, result.Record.Flags14, "flags=" + flags);
            Assert.AreEqual(0x2000, result.Record.Flags1C, "flags=" + flags);
            Assert.IsFalse(result.SleepRollPerformed, "flags=" + flags);

            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    [Test]
    public void Kind0_FlagsZero_VariantBelow10_RollsSleepWhenFirstRandomOdd()
    {
        var go = new GameObject("PulunpaSleep");
        var component = go.AddComponent<PulunpaSpawn>();
        var rng = MakeRng(3u); // first Next is odd

        var result = component.Initialize(0, 4, 6, 8, rng, 5);

        Assert.IsTrue(result.SleepRollPerformed);
        Assert.IsTrue(result.SecondRandomConsumed);
        Assert.IsTrue(result.SleepApplied);
        Assert.AreEqual(35, result.SleepValue);
        Assert.AreEqual((sbyte)StatusId.SLEEP, result.Record.StatusId(3));
        Assert.AreEqual(35, result.Record.StatusValue(3));
        Assert.AreEqual(PulunpaSpawn.FrameSetChoice.FrameSet1, result.FrameSet);
        Assert.AreEqual(PulunpaSpawn.CallbackChoice.NormalResume, result.Callback);
        Assert.AreEqual(AdvanceSeed(3u, 2), rng.Seed);

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void Kind0_FlagsZero_VariantBelow10_DoesNotRollSecond_WhenFirstRandomEven()
    {
        var go = new GameObject("PulunpaNoSleep");
        var component = go.AddComponent<PulunpaSpawn>();
        var rng = MakeRng(1u); // first Next is even

        var result = component.Initialize(0, 4, 6, 8, rng, 5);

        Assert.IsTrue(result.SleepRollPerformed);
        Assert.IsFalse(result.SecondRandomConsumed);
        Assert.IsFalse(result.SleepApplied);
        Assert.AreEqual(0, result.Record.StatusId(3));
        Assert.AreEqual(0, result.Record.StatusValue(3));
        Assert.AreEqual(PulunpaSpawn.FrameSetChoice.FrameSet1, result.FrameSet);
        Assert.AreEqual(AdvanceSeed(1u, 1), rng.Seed);

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void Kind0_FlagsZero_VariantAtLeast10_SelectsVariantCallbackAndFrameSet2()
    {
        var go = new GameObject("PulunpaVariant");
        var component = go.AddComponent<PulunpaSpawn>();
        var rng = MakeRng(3u);

        var result = component.Initialize(0, 4, 6, 8, rng, 10);

        Assert.IsTrue(result.SleepApplied);
        Assert.AreEqual((byte)1, result.ByteA7);
        Assert.AreEqual(PulunpaSpawn.CallbackChoice.VariantAction, result.Callback);
        Assert.AreEqual(PulunpaSpawn.FrameSetChoice.FrameSet2, result.FrameSet);
        Assert.AreEqual(0x01, result.Record.U8(0xA7));

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void Kind0_FlagsNonZero_SkipsRandomBranch()
    {
        var go = new GameObject("PulunpaElse");
        var component = go.AddComponent<PulunpaSpawn>();
        var rng = MakeRng(3u);
        uint seedBefore = rng.Seed;

        var result = component.Initialize(4, 4, 6, 8, rng, 0);

        Assert.AreEqual(0, result.Kind);
        Assert.IsFalse(result.SleepRollPerformed);
        Assert.IsFalse(result.SleepApplied);
        Assert.AreEqual(PulunpaSpawn.FrameSetChoice.FrameSet1, result.FrameSet);
        Assert.AreEqual(PulunpaSpawn.CallbackChoice.NormalResume, result.Callback);
        Assert.AreEqual(seedBefore, rng.Seed);

        UnityEngine.Object.DestroyImmediate(go);
    }

    [Test]
    public void Initialize_ReturnsRecordAndStoresLastResult()
    {
        var go = new GameObject("PulunpaLast");
        var component = go.AddComponent<PulunpaSpawn>();
        var rng = MakeRng(1u);

        var result = component.Initialize(0, 0, 0, 0, rng, 0);

        Assert.AreSame(result.Record, component.Record);
        Assert.AreSame(result, component.LastResult);

        UnityEngine.Object.DestroyImmediate(go);
    }
}