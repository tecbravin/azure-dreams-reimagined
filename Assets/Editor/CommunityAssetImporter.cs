using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace AzureDreamsReimagined.Editor
{
 public sealed class CommunityAssetImporter : AssetPostprocessor
 {
  private void OnPreprocessTexture()
  {
   bool sprite=assetPath.StartsWith("Assets/AzureDreams/Sprites/Monsters/Community/") || assetPath.StartsWith("Assets/AzureDreams/Sprites/Characters/") && assetPath.Contains("/Community/") || assetPath.StartsWith("Assets/AzureDreams/Sprites/Portraits/ADStateDisplay/") || assetPath.StartsWith("Assets/AzureDreams/Sprites/Items/ADStateDisplay/") || assetPath.StartsWith("Assets/AzureDreams/Sprites/UI/ADStateDisplay/");
   bool texture=assetPath.StartsWith("Assets/AzureDreams/Textures/Town/CapturedScenes/");
   if(!sprite&&!texture)return;
   var t=(TextureImporter)assetImporter;
   t.textureType=sprite?TextureImporterType.Sprite:TextureImporterType.Default;
   if(sprite){t.spriteImportMode=SpriteImportMode.Single;t.spritePixelsPerUnit=64;}
   t.filterMode=FilterMode.Point;t.wrapMode=TextureWrapMode.Clamp;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=4096;
   bool stp=assetPath.EndsWith("_stp.png");t.sRGBTexture=!stp;t.alphaIsTransparency=!stp;
   if(stp)t.alphaSource=TextureImporterAlphaSource.None;

   bool indexedActions = assetPath.StartsWith("Assets/AzureDreams/Sprites/Monsters/Community/", StringComparison.Ordinal) &&
       assetPath.EndsWith("IndexedActions.png", StringComparison.Ordinal);
   if (!indexedActions) return;

   // Red stores the palette index and alpha stores the original coverage mask.
   t.sRGBTexture = false;
   t.alphaSource = TextureImporterAlphaSource.FromInput;
   t.alphaIsTransparency = false;
   t.isReadable = true;
   t.npotScale = TextureImporterNPOTScale.None;
   var defaults = t.GetDefaultPlatformTextureSettings();
   ConfigureIndexedActionFormat(defaults);
   t.SetPlatformTextureSettings(defaults);
   foreach (string platform in new[] { "Standalone", "WebGL", "Windows Store Apps", "Android", "iPhone" })
   {
    var settings = t.GetPlatformTextureSettings(platform);
    ConfigureIndexedActionFormat(settings);
    t.SetPlatformTextureSettings(settings);
   }
  }

  private static void ConfigureIndexedActionFormat(TextureImporterPlatformSettings settings)
  {
   settings.format = TextureImporterFormat.RGBA32;
   settings.textureCompression = TextureImporterCompression.Uncompressed;
  }
  private void OnPreprocessModel()
  {
   if(!assetPath.StartsWith("Assets/AzureDreams/Models/Town/CapturedScenes/"))return;
   var m=(ModelImporter)assetImporter;m.importAnimation=false;m.importLights=false;m.importCameras=false;m.importNormals=ModelImporterNormals.Calculate;
  }
  [Serializable] private class Entry {public string asset;}
  [Serializable] private class Manifest {public Entry[] entries;}
  public static void Verify()
  {
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   string json=File.ReadAllText("Assets/AzureDreams/Data/Community/integration_manifest.json");
   var manifest=JsonUtility.FromJson<Manifest>("{\"entries\":"+json+"}");int png=0,models=0,fonts=0;
   foreach(var e in manifest.entries)
   {
    if(!File.Exists(e.asset))throw new Exception("Missing integrated file: "+e.asset);
    string ext=Path.GetExtension(e.asset).ToLowerInvariant();
    if(ext==".png"){if(AssetDatabase.LoadAssetAtPath<Texture2D>(e.asset)==null)throw new Exception("Texture import failed: "+e.asset);png++;}
    if(ext==".obj"){var go=AssetDatabase.LoadAssetAtPath<GameObject>(e.asset);if(go==null||go.GetComponentsInChildren<MeshFilter>(true).Length==0)throw new Exception("Model import failed: "+e.asset);models++;}
    if(ext==".ttf"){if(AssetDatabase.LoadAssetAtPath<Font>(e.asset)==null)throw new Exception("Font import failed: "+e.asset);fonts++;}
   }
   if(models!=44||fonts!=2)throw new Exception("Unexpected model/font count");
   Directory.CreateDirectory("Logs");File.WriteAllText("Logs/CommunityAssetImport.json",$"{{\"status\":\"passed\",\"source_entries\":{manifest.entries.Length},\"png_references\":{png},\"models\":{models},\"fonts\":{fonts}}}");
   Debug.Log($"COMMUNITY_IMPORT_OK: {manifest.entries.Length} references, {models} models, {fonts} fonts");
  }
 }
}
