using System.IO;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxMasterAssetBuilder
    {
        [MenuItem("Azure Dreams/Rendering/Build ALL Assets")]
        public static void BuildAllAssets()
        {
            Debug.Log("[PsxMasterAssetBuilder] Starting full asset build...");

            // 1. Import sprites from verified decode (run first to ensure materials exist)
            PsxIndexedShowcaseBuilder.BuildShowcase();
            Debug.Log("[PsxMasterAssetBuilder] Step 1/7: Indexed sprites imported");

            // 2. Import Spriters Resource
            PsxSpritersResourceImporter.ImportSpritersResource();
            Debug.Log("[PsxMasterAssetBuilder] Step 2/7: Spriters Resource imported");

            // 3. Integrate Spriters Resource
            PsxSpritersResourceIntegrator.IntegrateSpritersResource();
            Debug.Log("[PsxMasterAssetBuilder] Step 3/7: Spriters Resource integrated");

            // 4. Build tower themes
            PsxTowerThemeBuilder.BuildTowerThemes();
            Debug.Log("[PsxMasterAssetBuilder] Step 4/7: Tower themes built");

            // 5. Build ground and tilemaps
            PsxGroundAndTilemapBuilder.BuildGroundAndTilemaps();
            Debug.Log("[PsxMasterAssetBuilder] Step 5/7: Ground and tilemaps built");

            // 6. Build FX prefabs
            PsxFxPrefabBuilder.BuildFxPrefabs();
            Debug.Log("[PsxMasterAssetBuilder] Step 6/7: FX prefabs built");

            // 7. Build monster prefabs
            PsxMonsterPrefabBuilder.BuildMonsterPrefabs();
            Debug.Log("[PsxMasterAssetBuilder] Step 7/7: Monster prefabs built");

            // 8. Build animation controllers
            PsxAnimationBuilder.BuildAnimationControllers();
            Debug.Log("[PsxMasterAssetBuilder] Animation controllers built");

            // 11. Build environment props
            PsxEnvironmentPropBuilder.BuildEnvironmentProps();
            Debug.Log("[PsxMasterAssetBuilder] Environment props built");

            // 12. Build tower assets (floor tileset prefab, backgrounds)
            PsxTowerAssetBuilder.BuildTowerAssets();
            Debug.Log("[PsxMasterAssetBuilder] Tower assets built");

            // 10. Optimize pipeline
            PsxRenderPipelineOptimizer.OptimizePipeline();
            Debug.Log("[PsxMasterAssetBuilder] Pipeline optimized");

            // 11. Full pipeline setup
            PsxFullPipelineSetup.FullPipelineSetup();
            Debug.Log("[PsxMasterAssetBuilder] Full pipeline setup complete");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[PsxMasterAssetBuilder] ========================================");
            Debug.Log("[PsxMasterAssetBuilder] ALL ASSETS BUILT SUCCESSFULLY");
            Debug.Log("[PsxMasterAssetBuilder] ========================================");
        }
    }
}
