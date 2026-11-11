using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using AzureDreamsReimagined;
public class TowerEventFloorTests
{
 const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
 [UnityTest] public IEnumerator Floor2HasVisitorNoMonstersAndCanAdvanceToNormalFloor()
 {
  var root=new GameObject("Event floor QA");var camera=new GameObject("Event floor camera").AddComponent<Camera>();
  try
  {
   var c=root.AddComponent<TowerGameController>();c.Configure(null,null,null,null,null,null,null,null,null,camera);camera.enabled=false;
   yield return null;
   c.ConfigureGeneratedTowerFloors(true);
   typeof(TowerGameController).GetMethod("EnterTower",Flags).Invoke(c,null);
   Climb(c); // Reach the event through the real floor-1 stairs and run manager.
   Assert.That(c.IsTowerEventFloor,Is.True);
   var creatures=(IList)typeof(TowerGameController).GetField("_creatures",Flags).GetValue(c);
   Assert.That(creatures.Count,Is.Zero);
   var floor=(TowerFloorManager)typeof(TowerGameController).GetField("_towerFloorLayout",Flags).GetValue(c);
   Assert.That(floor.MonsterCount,Is.Zero);Assert.That(floor.HasReachableExit,Is.True);
   foreach(var monster in floor.Dungeon.Monsters)Assert.That(monster,Is.Null);
   StartVisitor(c);
   var type=typeof(TowerGameController);
   var stage=type.GetField("_visitorStage",Flags);
   for(int i=0;i<12 && stage.GetValue(c).ToString()=="Dialogue";i++)
   {
    type.GetField("_dialogueStarted",Flags).SetValue(c,Time.unscaledTime-10f);
    type.GetMethod("AdvanceVisitorDialogue",Flags).Invoke(c,null);
   }
   if(stage.GetValue(c).ToString()=="Choice")type.GetMethod("AnswerVisitor",Flags).Invoke(c,new object[]{false});
   Assert.That(stage.GetValue(c).ToString(),Is.EqualTo("Depart"));
   type.GetMethod("TickVisitorMotion",Flags).Invoke(c,new object[]{1f});
   Assert.That(stage.GetValue(c).ToString(),Is.EqualTo("Finished"));
   Assert.That((bool)type.GetField("_turnBusy",Flags).GetValue(c),Is.False);
   Climb(c);
   Assert.That((int)typeof(TowerGameController).GetField("_floor",Flags).GetValue(c),Is.EqualTo(3));
   Assert.That(c.IsTowerEventFloor,Is.False);Assert.That(creatures.Count,Is.GreaterThan(0));
   yield return null;Assert.That(root.GetComponentInChildren<TowerEventNpc>(),Is.Null);
  }
  finally{Object.Destroy(root);Object.Destroy(camera.gameObject);}
 }
 internal static void StartVisitor(TowerGameController c)
 {
  var type=typeof(TowerGameController);
  var visitors=c.GetComponentsInChildren<TowerEventNpc>();
  Assert.That(visitors,Is.Not.Empty);
  Assert.That(System.Array.Exists(visitors,npc=>npc.HasSpoken),Is.False);
  var handleEvent=type.GetMethod("HandleVisitorEvent",Flags);
  Assert.That(handleEvent.Invoke(c,null),Is.False,"The encounter must wait for Koh to approach.");
  var playerCell=type.GetField("_playerCell",Flags);
  var player=(GameObject)type.GetField("_player",Flags).GetValue(c);
  var threeStepsAway=new Vector2Int(31,35);
  playerCell.SetValue(c,threeStepsAway);
  player.transform.position=new Vector3(threeStepsAway.x,.55f,threeStepsAway.y);
  Assert.That(handleEvent.Invoke(c,null),Is.False,"Three walkable steps are outside the event range.");
  Assert.That(c.TryTowerAction(Vector2Int.down),Is.True);
  c.TickTurnPresentation(1f);
  Assert.That(handleEvent.Invoke(c,null),Is.True,"The event must start automatically at two walkable steps.");
  Assert.That(System.Array.Exists(visitors,npc=>npc.HasSpoken),Is.True);
  var line=type.GetField("_dialogueLine",Flags);
  var firstLine=(string)line.GetValue(c);
  Assert.That(firstLine,Is.Not.Empty);
  var tick=type.GetMethod("TickVisitorMotion",Flags);
  tick.Invoke(c,new object[]{.2f});
  Assert.That(player.transform.position.z,Is.GreaterThan(33f).And.LessThan(34f));
  Assert.That(line.GetValue(c),Is.EqualTo(firstLine));
  tick.Invoke(c,new object[]{1f});
  Assert.That(playerCell.GetValue(c),Is.EqualTo(new Vector2Int(31,33)));
  Assert.That(player.transform.position.z,Is.EqualTo(33f));
  Assert.That(type.GetField("_visitorStage",Flags).GetValue(c).ToString(),Is.EqualTo("Dialogue"));
 }
 internal static void Climb(TowerGameController c)
 {
  var floor=(TowerFloorManager)typeof(TowerGameController).GetField("_towerFloorLayout",Flags).GetValue(c);
  var player=(GameObject)typeof(TowerGameController).GetField("_player",Flags).GetValue(c);
  int before=(int)typeof(TowerGameController).GetField("_floor",Flags).GetValue(c);
  foreach(var step in new[]{Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left})
  {
   var from=floor.StairsUpCell-step;if(!floor.IsWalkable(from.x,from.y))continue;
   typeof(TowerGameController).GetField("_playerCell",Flags).SetValue(c,from);player.transform.position=new Vector3(from.x,.55f,from.y);
   if(!c.TryTowerAction(step))continue;c.TickTurnPresentation(1f);
   var type=typeof(TowerGameController);
   Assert.That(type.GetField("_checkpointMenu",Flags).GetValue(c).ToString(),Is.EqualTo("Ascend"));
   Assert.That((int)type.GetField("_floor",Flags).GetValue(c),Is.EqualTo(before));
   type.GetMethod("AscendFromElevator",Flags).Invoke(c,null);
   type.GetMethod("TickElevatorAscent",Flags).Invoke(c,new object[]{2f});
   Assert.That((int)type.GetField("_floor",Flags).GetValue(c),Is.EqualTo(before+1));return;
  }
  Assert.Fail("No usable approach to generated stairs");
 }
 [TestCase(1)] [TestCase(19)] [TestCase(401)]
 public void EventFloorUsesReachableRetailLayoutWithoutActorFlags(int seed)
 {
  var floor=new TowerFloorManager();floor.Generate(2,seed);Assert.That(floor.HasReachableExit,Is.True);Assert.That(floor.MonsterCount,Is.Zero);
  Assert.That(floor.StairsDownCell,Is.EqualTo(new Vector2Int(31,56)));
  Assert.That(floor.StairsUpCell,Is.EqualTo(new Vector2Int(55,9)));
  Assert.That(floor.IsWalkable(31,35),Is.True);
  Assert.That(floor.IsWalkable(29,35),Is.False);
  Assert.That(floor.GroundHeight(31,38),Is.EqualTo(-16));
  for(int y=0;y<64;y++)for(int x=0;x<64;x++)Assert.That(floor.Dungeon.GetFlags(x,y)&8,Is.Zero);
 }
}
