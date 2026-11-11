using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace AzureDreamsReimagined.Editor
{
    public static class CommunityMonsterAnimations
    {
        private const float FramesPerSecond = 8f;
        private const string Output = "Assets/AzureDreams/Animations/Monsters/Community";

        [Serializable] private class Entry { public string source; public string asset; }
        [Serializable] private class Manifest { public Entry[] entries; }

        [MenuItem("Azure Dreams/Rendering/Build Community Monster Clips")]
        public static void Build()
        {
            var manifest = JsonUtility.FromJson<Manifest>("{\"entries\":" +
                File.ReadAllText("Assets/AzureDreams/Data/Community/integration_manifest.json") + "}");
            var pattern = new Regex(@"^community/monster_animations/monster_([0-9a-f]+)_animation_(\d+)_frame_(\d+)\.png$");
            var sequences = manifest.entries.Select(entry => (entry, match: pattern.Match(entry.source.Replace('\\', '/'))))
                .Where(pair => pair.match.Success)
                .GroupBy(pair => (monster: pair.match.Groups[1].Value, animation: pair.match.Groups[2].Value))
                .ToArray();
            if (sequences.Length == 0) throw new InvalidDataException("No community monster frames found.");

            var sprites = new Dictionary<string, Sprite>();
            foreach (var pair in sequences.SelectMany(group => group))
            {
                string path = pair.entry.asset;
                if (sprites.ContainsKey(path)) continue;
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (!sprite)
                {
                    string copy = Output + "/FrameSources/" + AssetDatabase.AssetPathToGUID(path) + ".png";
                    Directory.CreateDirectory(Path.GetDirectoryName(copy));
                    if (!File.Exists(copy)) File.Copy(path, copy);
                    AssetDatabase.ImportAsset(copy, ImportAssetOptions.ForceSynchronousImport);
                    var importer = (TextureImporter)AssetImporter.GetAtPath(copy);
                    importer.textureType = TextureImporterType.Sprite;
                    importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = 64;
                    importer.filterMode = FilterMode.Point;
                    importer.mipmapEnabled = false;
                    importer.textureCompression = TextureImporterCompression.Uncompressed;
                    importer.SaveAndReimport();
                    sprite = AssetDatabase.LoadAssetAtPath<Sprite>(copy);
                }
                if (!sprite) throw new InvalidDataException("Cannot load sprite " + path);
                sprites.Add(path, sprite);
            }

            foreach (var sequence in sequences)
                Directory.CreateDirectory(Output + "/monster_" + sequence.Key.monster);
            AssetDatabase.Refresh();
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var sequence in sequences)
                {
                    var ordered = sequence.OrderBy(pair => int.Parse(pair.match.Groups[3].Value)).ToArray();
                    var keys = new ObjectReferenceKeyframe[ordered.Length + 1];
                    for (int i = 0; i < ordered.Length; i++)
                    {
                        if (int.Parse(ordered[i].match.Groups[3].Value) != i)
                            throw new InvalidDataException("Missing or duplicate frame in " + sequence.Key);
                        keys[i] = new ObjectReferenceKeyframe { time = i / FramesPerSecond, value = sprites[ordered[i].entry.asset] };
                    }
                    keys[ordered.Length] = new ObjectReferenceKeyframe { time = ordered.Length / FramesPerSecond, value = keys[ordered.Length - 1].value };
                    string name = "monster_" + sequence.Key.monster + "_animation_" + sequence.Key.animation;
                    string path = Output + "/monster_" + sequence.Key.monster + "/" + name + ".anim";
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if (!clip)
                    {
                        clip = new AnimationClip { name = name };
                        AssetDatabase.CreateAsset(clip, path);
                    }
                    clip.frameRate = FramesPerSecond;
                    AnimationUtility.SetObjectReferenceCurve(clip, binding, keys);
                    EditorUtility.SetDirty(clip);
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            AssetDatabase.SaveAssets();
            foreach (var sequence in sequences)
            {
                string name = "monster_" + sequence.Key.monster + "_animation_" + sequence.Key.animation;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Output + "/monster_" + sequence.Key.monster + "/" + name + ".anim");
                var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                if (curve.Length != sequence.Count() + 1 || curve.Any(key => !key.value))
                    throw new InvalidDataException("Invalid sprite curve in " + name);
            }
            Debug.Log($"COMMUNITY_CLIPS_OK: {sequences.Length} clips, {sequences.Select(group => group.Key.monster).Distinct().Count()} monster IDs, {sequences.Sum(group => group.Count())} source frames");
        }
    }
}
