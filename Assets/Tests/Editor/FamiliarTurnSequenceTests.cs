using System;
using System.Collections;
using System.Reflection;
using AzureDreams.Core;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class FamiliarTurnSequenceTests
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(TowerGameController c, string name, params object[] args) => typeof(TowerGameController).GetMethod(name, Flags).Invoke(c, args);
    static FieldInfo Field(string name) => typeof(TowerGameController).GetField(name, Flags);
    static void Check(bool pass, string message) { if (!pass) throw new InvalidOperationException(message); }

    [Test]
    public void DecisionsUseKohDestinationAndRestoreMovementSnapshot()
    {
        var root = new GameObject("Familiar decision test"); root.SetActive(false);
        var familiar = new GameObject("Familiar");
        try
        {
            var c = root.AddComponent<TowerGameController>();
            var type = typeof(TowerGameController);
            Field("_area").SetValue(c, Enum.Parse(type.GetNestedType("Area", BindingFlags.NonPublic), "Tower"));
            Field("_familiar").SetValue(c, familiar);
            Field("_familiarWaiting").SetValue(c, true);
            var actor = (ActorRecord)Field("_familiarActor").GetValue(c);
            Field("_familiarCell").SetValue(c, Vector2Int.zero);
            var pending = (IDictionary)Field("_pendingFamiliarCommands").GetValue(c);
            var orderType = type.GetNestedType("ManualOrder", BindingFlags.NonPublic);
            var tupleType = pending.GetType().GetGenericArguments()[1];
            object WaitOrder() => Activator.CreateInstance(tupleType, Enum.Parse(orderType, "Wait"), Vector2Int.up);
            var old = new Vector2Int(7, 0);
            Field("_playerCell").SetValue(c, old);
            var koh = (ActorRecord)Field("_playerActor").GetValue(c);
            koh.TileX = 7;
            pending[actor] = WaitOrder();
            Call(c, "PlanFamiliarTurn", actor, familiar, Vector2Int.zero, new Vector2Int(6,0), null, null, true);
            Check(pending.Contains(actor), "Approaching Koh must restore contact before checking the manual command");
            Check((Vector2Int)Field("_playerCell").GetValue(c) == old && koh.TileX == 7, "Planning changed the movement snapshot");
            Field("_playerCell").SetValue(c, new Vector2Int(6,0)); koh.TileX = 6;
            pending[actor] = WaitOrder();
            Call(c, "PlanFamiliarTurn", actor, familiar, Vector2Int.zero, old, null, null, true);
            Check(!pending.Contains(actor), "Moving out of range must lose contact before the familiar acts");
        }
        finally { UnityEngine.Object.DestroyImmediate(familiar); UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void FamiliarPhaseStartsAfterKohPhase()
    {
        var root = new GameObject("Turn timing test"); root.SetActive(false);
        try
        {
            var c = root.AddComponent<TowerGameController>();
            Field("_familiarActsAfterKoh").SetValue(c, true);
            Check((float)Call(c, "FamiliarPresentationProgress", .1f, .2f) == 0f, "Familiar moved before Koh finished");
            Check(Mathf.Abs((float)Call(c, "FamiliarPresentationProgress", .3f, .2f) - .5f) < .001f, "Familiar phase did not progress after Koh");
            Check((float)Call(c, "FamiliarPresentationProgress", .4f, .2f) == 1f, "Familiar phase did not complete");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    [Test]
    public void MixRequiresAnAvailableSpellIncludingSecondaryLearnedSlots()
    {
        var root = new GameObject("Mix eligibility test"); root.SetActive(false);
        try
        {
            var c = root.AddComponent<TowerGameController>();
            var actor = new ActorRecord { Species = 255, MpRaw = 25600 };
            Check((int)Call(c, "FamiliarMixSpell", actor) == 0, "Spell-less familiar enabled Mix Magic");
            Call(c, "TryManualMixMagic", actor, null, Vector2Int.zero, Vector2Int.zero, 1);
            Check(!(bool)Field("_kohMixingThisTurn").GetValue(c) && actor.MpRaw == 25600, "Invalid Mix spent MP or changed Koh's action");
            actor.SetRaw(11, 7);
            Check((int)Call(c, "FamiliarMixSpell", actor) == 7, "Secondary learned spell unavailable for Mix");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}