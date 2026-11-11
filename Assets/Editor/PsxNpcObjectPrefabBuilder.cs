using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined.Editor
{
    public static class PsxNpcObjectPrefabBuilder
    {
        // NPCs and objects are NOT yet validated in the verified_decode manifest.
        // The verified sprites (resources 1-57) are creature/actor sprites only.
        // Town VRAM textures (12 x 32x128) are unclassified.
        // Dungeon slots (710) are conditional module reads, not textures.
        //
        // This builder is a PLACEHOLDER that will be populated when the Coder
        // delivers validated NPC/object sprite data.

        [MenuItem("Azure Dreams/Rendering/Build NPC & Object Prefabs (pending validation)")]
        public static void BuildNpcObjectPrefabs()
        {
            Debug.LogWarning("[PsxNpcObjectPrefabBuilder] BLOCKED: No validated NPC/object sprite data available yet.");
            Debug.LogWarning("Verified sprites are creature/actor sprites only (resources 1-57).");
            Debug.LogWarning("Town VRAM (12 textures) and dungeon slots (710) are unclassified.");
            Debug.LogWarning("Waiting for Coder to deliver validated NPC/object extraction.");
        }
    }
}
