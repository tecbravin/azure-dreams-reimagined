using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace AzureDreamsReimagined.Editor
{
 public static class ConfirmedSceneVisuals
 {
  public static void Apply()
  {
   const string source="Assets/AzureDreams/Prefabs/Wanderer.prefab";
   const string target="Assets/AzureDreams/Prefabs/Koh.prefab";
   const string sheet="Assets/AzureDreams/Sprites/Characters/Koh/Community/koh-walking-animation.png";
   const string matPath="Assets/AzureDreams/Materials/KohSprite.mat";
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var shader=Shader.Find("AzureDreams/KohColorKey"); if(!shader)throw new Exception("Missing Koh shader");
   var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
   if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}
   var prefab=PrefabUtility.LoadPrefabContents(AssetDatabase.LoadAssetAtPath<GameObject>(target)?target:source);
   try
   {
    prefab.name="Koh";
    var view=prefab.GetComponent<KohSpriteView>();if(!view)view=prefab.AddComponent<KohSpriteView>();
    view.Configure(AssetDatabase.LoadAssetAtPath<Texture2D>(sheet),material);
    foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>(true))renderer.enabled=false;
    PrefabUtility.SaveAsPrefabAsset(prefab,target);
   }
   finally { PrefabUtility.UnloadPrefabContents(prefab); }
   var scene=EditorSceneManager.OpenScene("Assets/AzureDreams/Scenes/ReimaginedDemo.unity");
   TowerGameController controller=null;
   foreach(var root in scene.GetRootGameObjects())if(root.TryGetComponent<TowerGameController>(out var c))controller=c;
   if(!controller)throw new Exception("Demo controller missing");
   var serialized=new SerializedObject(controller);
   serialized.FindProperty("_playerPrefab").objectReferenceValue=AssetDatabase.LoadAssetAtPath<GameObject>(target);
   serialized.ApplyModifiedPropertiesWithoutUndo();
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
   Debug.Log("CONFIRMED_VISUAL_APPLIED: ReimaginedDemo protagonist now uses Koh prefab; authored buildings retained pending identity proof.");
  }
 }
}
