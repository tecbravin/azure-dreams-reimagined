using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AzureDreamsReimagined;
public class TowerSpawnRulesTests
{
 const BindingFlags F=BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance;
 [TestCase(1,true)] [TestCase(2,false)] [TestCase(3,true)] [TestCase(39,true)] [TestCase(40,false)]
 public void SpawnPolicyMatchesFloorExceptions(int floor,bool expected)=>Assert.That(TowerFloorRules.AllowsRandomMonsters(floor),Is.EqualTo(expected));
 [TestCase(1)] [TestCase(19)] [TestCase(401)]
 public void ProceduralFloorUsesSmallerBoundsAndKeepsGeneratedItemsInRooms(int seed)
 {
  var floor=new TowerFloorManager();
  floor.Generate(3,seed);
  Assert.That(floor.HasReachableExit,Is.True);
  var dungeon=floor.Dungeon;
  int items=0;
  foreach(var cell in floor.OpenCells)
  {
   Assert.That(cell.x,Is.InRange(9,53));Assert.That(cell.y,Is.InRange(9,53));
   if((dungeon.GetFlags(cell.x,cell.y)&(ushort)AzureDreams.Core.DungeonGenerator.TileFlag.HasItem)==0)continue;
   Assert.That(dungeon.Grid[cell.y,cell.x],Is.EqualTo(AzureDreams.Core.DungeonGenerator.TileType.Floor));
   items++;
  }
  Assert.That(items,Is.GreaterThan(0));
 }
 [UnityTest] public IEnumerator BothLayoutPathsRespectExclusionsAndRandomizeSpawnCells()
 {
  var root=new GameObject("Spawn rules QA");var camera=new GameObject("Spawn camera").AddComponent<Camera>();camera.enabled=false;
  try
  {
   var c=root.AddComponent<TowerGameController>();c.Configure(null,null,null,null,null,null,null,null,null,camera);yield return null;
   foreach(bool generated in new[]{false,true})
   {
    typeof(TowerGameController).GetMethod("EnterTown",F).Invoke(c,null);c.ConfigureGeneratedTowerFloors(generated);typeof(TowerGameController).GetMethod("EnterTower",F).Invoke(c,null);
    foreach(int floor in new[]{1,2,3,40})
    {
     typeof(TowerGameController).GetField("_floor",F).SetValue(c,floor);typeof(TowerGameController).GetMethod("BuildTowerFloor",F).Invoke(c,null);yield return null;
     var list=(IList)typeof(TowerGameController).GetField("_creatures",F).GetValue(c);
     Assert.That(list.Count,TowerFloorRules.AllowsRandomMonsters(floor)?Is.GreaterThan(0):Is.EqualTo(0),"layout="+generated+", floor="+floor);
     var npcs=root.GetComponentsInChildren<TowerEventNpc>();
     Assert.That(npcs.Length,floor==2?Is.InRange(1,2):Is.EqualTo(0));
     foreach(var visitor in npcs)
      Assert.That(visitor.name,Is.EqualTo("Floor 2 - Ghosh").Or.EqualTo("Floor 2 - Selfi"));
     if(generated)
     {
      var layout=(TowerFloorManager)typeof(TowerGameController).GetField("_towerFloorLayout",F).GetValue(c);
      Assert.That(layout.MonsterCount,TowerFloorRules.AllowsRandomMonsters(floor)?Is.GreaterThan(0):Is.EqualTo(0));
     }
     var occupied=new HashSet<Vector2Int>();
     foreach(var state in list)
     {
      var cell=(Vector2Int)state.GetType().GetField("Cell").GetValue(state);
      Assert.That(occupied.Add(cell),Is.True);Assert.That(cell,Is.Not.EqualTo((Vector2Int)typeof(TowerGameController).GetField("_playerCell",F).GetValue(c)));
     }
    }
    var positions=new HashSet<Vector2Int>();
    for(int run=0;run<6;run++)
    {
     typeof(TowerGameController).GetField("_floor",F).SetValue(c,3);typeof(TowerGameController).GetMethod("BuildTowerFloor",F).Invoke(c,null);
     var list=(IList)typeof(TowerGameController).GetField("_creatures",F).GetValue(c);var state=list[0];positions.Add((Vector2Int)state.GetType().GetField("Cell").GetValue(state));yield return null;
    }
    if(generated)Assert.That(positions.Count,Is.GreaterThan(1),"Run RNG must select different floor spawn positions");
   }
  }
  finally {Object.Destroy(root);Object.Destroy(camera.gameObject);}
 }
}
