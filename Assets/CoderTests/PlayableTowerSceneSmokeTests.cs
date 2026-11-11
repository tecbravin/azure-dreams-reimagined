using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AzureDreamsReimagined;
public class PlayableTowerSceneSmokeTests
{
 const BindingFlags F=BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public;
 [UnityTest] public IEnumerator DemoEntersGeneratedTowerAndReachesVisitorFloor()
 {
  yield return SceneManager.LoadSceneAsync("ReimaginedDemo",LoadSceneMode.Additive);
  var scene=SceneManager.GetSceneByName("ReimaginedDemo");TowerGameController c=null;Camera camera=null;
  foreach(var root in scene.GetRootGameObjects()){if(root.TryGetComponent<TowerGameController>(out var game))c=game;if(root.TryGetComponent<Camera>(out var view))camera=view;}
  yield return null;
  try
  {
   Assert.That(c,Is.Not.Null);Assert.That(camera,Is.Not.Null);
   Assert.That((bool)typeof(TowerGameController).GetField("_useGeneratedTowerFloors",F).GetValue(c),Is.True);
   typeof(TowerGameController).GetMethod("EnterTower",F).Invoke(c,null);yield return null;
   var floor=(TowerFloorManager)typeof(TowerGameController).GetField("_towerFloorLayout",F).GetValue(c);
   Assert.That(floor.IsGenerated,Is.True);Assert.That(floor.HasReachableExit,Is.True);Capture(camera,"tower-floor-1.png");
   if((int)typeof(TowerGameController).GetField("_tutorialDialogue",F).GetValue(c)==1)
    typeof(TowerGameController).GetMethod("FinishTutorialDialogue",F).Invoke(c,null);
   TowerEventFloorTests.Climb(c);
   Assert.That(c.IsTowerEventFloor,Is.True);
   floor=(TowerFloorManager)typeof(TowerGameController).GetField("_towerFloorLayout",F).GetValue(c);Assert.That(floor.MonsterCount,Is.Zero);
   var visitors=c.GetComponentsInChildren<TowerEventNpc>();
   Assert.That(visitors,Is.Not.Empty);
   yield return null;
   foreach(var visitor in visitors)
   {
    var renderer=visitor.GetComponentInChildren<SpriteRenderer>();
    Assert.That(renderer,Is.Not.Null,visitor.name);
    Assert.That(renderer.enabled && renderer.sprite!=null,Is.True,visitor.name);
   }
   TowerEventFloorTests.StartVisitor(c);
   yield return null;Capture(camera,"tower-floor-2-event.png");
  }
  finally { if(scene.isLoaded) SceneManager.UnloadSceneAsync(scene); }
  while(scene.isLoaded) yield return null;
 }
 [UnityTest] public IEnumerator TownStoneWallBlocksWalkingAndSupportsLandingAfterJump()
 {
  yield return SceneManager.LoadSceneAsync("ReimaginedDemo",LoadSceneMode.Additive);
  var scene=SceneManager.GetSceneByName("ReimaginedDemo");
  TowerGameController c=null;
  foreach(var root in scene.GetRootGameObjects())
   if(root.TryGetComponent<TowerGameController>(out var controller))c=controller;
  yield return null;
  try
  {
   Assert.That(c,Is.Not.Null);
   var type=typeof(TowerGameController);
   var player=(GameObject)type.GetField("_player",F).GetValue(c);
   var jump=type.GetField("_townJumpVelocity",F);
   var sprite=player.GetComponentInChildren<SpriteRenderer>();
   Assert.That(sprite.transform.lossyScale.x,Is.EqualTo(1.8f).Within(0.01f),"Billboard updates preserve Koh's spawn scale");
   player.transform.position=new Vector3(42.5f,0.55f,61.5f);
   c.MoveInTown(Vector2.up,0.25f);
   Assert.That(player.transform.position.z,Is.InRange(61.58f,61.70f),"Koh's radius stops at the wall face");
   Assert.That(player.transform.position.y,Is.EqualTo(0.55f).Within(0.001f));

   c.MoveInTown(Vector2.zero,0.05f);
   jump.SetValue(c,Mathf.Sqrt(2f*18f*1.8f));
   c.MoveInTown(Vector2.up,0.21f);
   c.MoveInTown(Vector2.down,0.21f);
   for(int i=0;i<6;i++)c.MoveInTown(Vector2.zero,0.10f);
   Assert.That(player.transform.position.z,Is.InRange(62f,62.5f));
   Assert.That(player.transform.position.y,Is.EqualTo(1.55f).Within(0.001f),"Koh lands on the one-unit stone top");
   Assert.That((float)jump.GetValue(c),Is.Zero);
   var propTop=type.GetMethod("TownRoundObstacleTop",F);
   Assert.That((float)propTop.Invoke(c,new object[]{new Vector3(58.5f,0f,49.5f),0.3f}),Is.EqualTo(1.5f));
   Assert.That((float)propTop.Invoke(c,new object[]{new Vector3(16f,0f,32f),0.3f}),Is.EqualTo(0.5f));
   Assert.That((float)propTop.Invoke(c,new object[]{new Vector3(100f,0f,27f),0.3f}),Is.EqualTo(2.625f));
   Assert.That((float)propTop.Invoke(c,new object[]{new Vector3(100.7f,0f,27f),0.3f}),Is.EqualTo(float.NegativeInfinity));
   var steppingStone=type.GetMethod("TownSteppingStoneTop",F);
   Assert.That((float)steppingStone.Invoke(c,new object[]{new Vector3(91.5f,0f,65.5f)}),Is.EqualTo(0.21875f));

   var message=type.GetProperty("_message",F);
   var visible=type.GetProperty("HasVisibleMessage",F);
   message.SetValue(c,"Temporary message");
   Assert.That((bool)visible.GetValue(c),Is.True);
   var messageTime=type.GetField("_messageActivityAt",F);
   messageTime.SetValue(c,Time.unscaledTime-4.9f);
   Assert.That((bool)visible.GetValue(c),Is.True);
   messageTime.SetValue(c,Time.unscaledTime-5.1f);
   Assert.That((bool)visible.GetValue(c),Is.False);
   player.transform.position=new Vector3(42.5f,0.55f,61.5f);
   c.MoveInTown(Vector2.up,0.25f);
   Assert.That(player.transform.position.z,Is.GreaterThan(61.5f));
   Assert.That((bool)visible.GetValue(c),Is.False,"Walking cannot bring back an expired message");
   message.SetValue(c,"Temporary message");
   Assert.That((bool)visible.GetValue(c),Is.True,"A new message can have the same text");
  }
  finally { if(scene.isLoaded) SceneManager.UnloadSceneAsync(scene); }
  while(scene.isLoaded) yield return null;
 }
 static void Capture(Camera camera,string file)
 {
  var low=camera.GetComponent<AzureDreamsReimagined.Rendering.PsxLowResRender>();bool lowEnabled=low&&low.enabled;if(low)low.enabled=false;
  var oldTarget=camera.targetTexture;var oldActive=RenderTexture.active;var rt=new RenderTexture(640,480,24);rt.Create();var image=new Texture2D(640,480,TextureFormat.RGB24,false);
  try {camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,640,480),0,0);image.Apply();
   int redPixels=0;foreach(var pixel in image.GetPixels32())if(pixel.r>80 && pixel.r>pixel.g*1.5f && pixel.r>pixel.b*1.5f)redPixels++;
   Assert.That(redPixels,Is.GreaterThan(3),"Koh's red hair must render visibly in the tower capture");
   Directory.CreateDirectory("Logs/TowerPlayable");File.WriteAllBytes("Logs/TowerPlayable/"+file,image.EncodeToPNG());}
  finally {camera.targetTexture=oldTarget;RenderTexture.active=oldActive;if(low)low.enabled=lowEnabled;rt.Release();Object.Destroy(rt);Object.Destroy(image);}
 }
}
