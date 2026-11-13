using System;
using System.IO;
using UnityEditor;
using UnityEngine;
namespace AzureDreamsReimagined.Editor
{
 public sealed class QuarantinedTextureRepair : AssetPostprocessor
 {
  void OnPreprocessTexture()
  {
   if(!assetPath.StartsWith("Assets/AzureDreams/Sprites/Quarantined/"))return;
   var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Sprite;t.spriteImportMode=SpriteImportMode.Single;t.filterMode=FilterMode.Point;t.mipmapEnabled=false;t.textureCompression=TextureImporterCompression.Uncompressed;
  }
  [InitializeOnLoadMethod] static void Schedule(){EditorApplication.delayCall+=Repair;}
  public static void Repair()
  {
   const string marker="Temp/AssetRepairCompleted_v1";if(File.Exists(marker))return;
   try
   {
    // Unity stores artifacts in two-character hash buckets. Recreate the bucket
    // structure if Library/Artifacts was removed or only partially restored.
    Directory.CreateDirectory("Library/Artifacts");
    for(int bucket=0;bucket<256;bucket++)
     Directory.CreateDirectory(Path.Combine("Library/Artifacts",bucket.ToString("x2")));
    Directory.CreateDirectory("Library/TempArtifacts");
    int png=0,names=0;
    foreach(string file in Directory.GetFiles("Assets/AzureDreams/Sprites/Quarantined","*.png",SearchOption.AllDirectories))
    {
     string path=file.Replace('\\','/');AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
     var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
     if(!sprite)throw new Exception("Unity did not create a Sprite asset for: "+path+". Check the preceding AssetDatabase/ArtifactDB import errors.");
     png++;
    }
    foreach(string file in Directory.GetFiles("Assets/AzureDreams/Sprites/Indexed","clut_words.asset",SearchOption.AllDirectories))
    {
     var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(file.Replace('\\','/'));if(!texture)continue;
     if(texture.name!="clut_words"){texture.name="clut_words";EditorUtility.SetDirty(texture);names++;}
    }
    AssetDatabase.SaveAssets();File.WriteAllText(marker,"done");Directory.CreateDirectory("Logs");
    File.WriteAllText("Logs/AssetRepairValidation.json",$"{{\"status\":\"passed\",\"png_sprites\":{png},\"renamed_cluts\":{names}}}");Debug.Log($"ASSET_REPAIR_OK: {png} PNG sprites; {names} CLUT names repaired");
   }catch(Exception e){Debug.LogException(e);}
  }
 }
}
