using System;
using System.Collections.Generic;
using System.Reflection;
using AzureDreams.Core;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public sealed class CreatureTurnOriginTests
{
    [Test]
    public void MidTurnSpawnHasStationaryOriginAndDoesNotShiftExistingActors()
    {
        var root = new GameObject("Creature origin regression");
        root.SetActive(false);
        try
        {
            var controller = root.AddComponent<TowerGameController>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var type = typeof(TowerGameController);
            var origins = (Dictionary<ActorRecord, Vector3>)type.GetField("_creatureFroms", flags).GetValue(controller);
            var original = new ActorRecord();
            var clone = new ActorRecord();
            var start = new Vector3(4, .55f, 5);
            origins[original] = start;
            var stateType = type.GetNestedType("CreatureState", BindingFlags.NonPublic);
            object State(ActorRecord actor, Vector2Int cell)
            {
                var state = Activator.CreateInstance(stateType);
                stateType.GetField("Actor").SetValue(state, actor);
                stateType.GetField("Cell").SetValue(state, cell);
                return state;
            }
            var origin = type.GetMethod("CreatureTurnOrigin", flags);
            var cell = new Vector2Int(7, 8);
            var expected = (Vector3)type.GetMethod("CellPosition", flags).Invoke(controller, new object[] { cell });
            Assert.That(origin.Invoke(controller, new[] { State(clone, cell) }), Is.EqualTo(expected));
            Assert.That(origin.Invoke(controller, new[] { State(original, cell) }), Is.EqualTo(start));
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}