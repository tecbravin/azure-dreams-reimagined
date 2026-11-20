using System;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        [Serializable] private sealed class NativeInteriorPropCatalog { public NativeInteriorProp[] entries; }
        [Serializable] private sealed class NativeInteriorProp
        { public string room, asset; public Vector3 position; }

        private void SetupNativeInteriorProps()
        {
            if (!_interior) return;
            var source = AssetMode.Load<TextAsset>("Town/InteriorProps/placements");
            if (!source) return;
            var catalog = JsonUtility.FromJson<NativeInteriorPropCatalog>(source.text);
            if (catalog?.entries == null) return;
            foreach (var entry in catalog.entries)
            {
                if (entry.room != _interior.interiorId) continue;
                var texture = AssetMode.Load<Texture2D>("Town/InteriorProps/" + entry.asset);
                if (!texture) { Debug.LogError("Missing original interior prop: " + entry.asset); continue; }
                var root = new GameObject("Original interior prop — " + entry.asset);
                root.transform.SetParent(transform, false);
                root.transform.position = _interior.MapLayoutPosition(entry.position);
                var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                    new Vector2(.5f, .375f), 48f, 0, SpriteMeshType.FullRect);
                root.AddComponent<SpriteRenderer>().sprite = sprite;
                root.AddComponent<CameraFacingSprite>();
                _constructionGeometry.Add(sprite);
                _generated.Add(root);
            }
        }
    }
}
