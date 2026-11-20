using System;
using UnityEngine;
namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private void ApplyNativeMinigameResident(string id, string asset)
        {
            var npc = FindStoryActor(id);
            if (!npc) return;
            var atlas = AssetMode.Load<Texture2D>("Town/Residents/" + asset);
            var animations = AssetMode.Load<TextAsset>("Town/Residents/" + asset);
            if (!atlas || !animations) throw new InvalidOperationException("Missing original resident: " + asset);
            foreach (var renderer in npc.GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;
            var sprite = npc.GetComponent<RetailTownResidentSpriteView>();
            if (!sprite) sprite = npc.gameObject.AddComponent<RetailTownResidentSpriteView>();
            npc.transform.localScale = Vector3.one;
            sprite.Configure(atlas, animations);
            npc.GetComponent<ActorLocomotionView>()?.UseRetailTownSprite(sprite);
        }

        private GameObject CreateNativeCasinoCoin(int denomination, Vector3 position)
        {
            var texture = AssetMode.Load<Texture2D>("Town/Minigames/Coin" + denomination);
            if (!texture) throw new InvalidOperationException("Missing original casino coin " + denomination);
            var root = new GameObject("Original casino coin " + denomination);
            root.transform.SetParent(transform, false);
            root.transform.position = position;
            var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(.5f,.5f), 16f);
            root.AddComponent<SpriteRenderer>().sprite = sprite;
            root.AddComponent<AzureDreamsReimagined.Rendering.CameraFacingSprite>();
            root.transform.localScale = Vector3.one * .42f;
            _constructionGeometry.Add(sprite);
            _generated.Add(root);
            return root;
        }

        [Serializable] private sealed class NativeMinigameMesh { public NativeMinigameSection[] sections; }
        [Serializable] private sealed class NativeMinigameSection
        { public string texture; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
        private GameObject CreateNativeMinigameObject(string asset, string label)
        {
            var source = AssetMode.Load<TextAsset>("Town/Minigames/" + asset);
            if (!source) throw new InvalidOperationException("Missing original minigame object: " + asset);
            var data = JsonUtility.FromJson<NativeMinigameMesh>(source.text);
            var root = new GameObject(label);
            root.transform.SetParent(transform, false);
            foreach (var section in data.sections)
            {
                var part = new GameObject(section.texture);
                part.transform.SetParent(root.transform, false);
                var mesh = new Mesh { name = asset };
                mesh.vertices = section.vertices; mesh.uv = section.uv; mesh.triangles = section.triangles;
                mesh.RecalculateBounds(); mesh.RecalculateNormals();
                var texture = AssetMode.Load<Texture2D>("Town/Minigames/" + section.texture);
                if (!texture) throw new InvalidOperationException("Missing original minigame texture: " + section.texture);
                var material = new Material(Shader.Find("AzureDreams/TownCaptured")) { mainTexture = texture };
                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterial = material;
                _constructionGeometry.Add(mesh); _constructionGeometry.Add(material);
            }
            _generated.Add(root);
            return root;
        }
    }
}
