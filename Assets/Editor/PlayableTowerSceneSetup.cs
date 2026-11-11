using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AzureDreamsReimagined.Editor
{
 public static class PlayableTowerSceneSetup
 {
  public static void Apply()
  {
   var scene=EditorSceneManager.OpenScene("Assets/AzureDreams/Scenes/ReimaginedDemo.unity");
   foreach(var root in scene.GetRootGameObjects())
   {
    if(!root.TryGetComponent<TowerGameController>(out var controller))continue;
    var data=new SerializedObject(controller);data.FindProperty("_useGeneratedTowerFloors").boolValue=true;data.ApplyModifiedPropertiesWithoutUndo();
    EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);Debug.Log("PLAYABLE_TOWER_CONFIGURED: procedural floors enabled; floor 2 is NPC event floor");return;
   }
   throw new System.InvalidOperationException("Tower controller not found");
  }
 }
}
