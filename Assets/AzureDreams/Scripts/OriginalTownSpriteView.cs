using AzureDreamsReimagined.Rendering;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Replaces the reconstructed low-poly body of a town resident with the
    /// character's original town sprite.  The retail town draws residents as
    /// camera-facing sprite parts (town/person/object/initialize_object_sprite),
    /// so these residents use the captured sprite on a billboard, the same way
    /// Patty is presented.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class OriginalTownSpriteView : MonoBehaviour
    {
        // Patty's card is 1.1 units tall for a 48-pixel sprite; keep the same pixel density.
        private const float UnitsPerPixel = 1.1f / 48f;

        internal static bool TryGetSprite(string npcId, out string sprite, out float lift)
        {
            lift = 0f;
            sprite = npcId switch
            {
                "coppe" => "Town_Coppe",
                "bet" => "Town_Bet",
                "tonka" => "Town_Tonka",
                // The captured Barry is the upper body seen over his counter.
                "barry" => "Town_Barry",
                _ => null
            };
            if (npcId == "barry") lift = 0.6f;
            return sprite != null;
        }

        internal static void Apply(GameObject instance, string npcId)
        {
            if (!instance || !TryGetSprite(npcId, out _, out _)) return;
            if (instance.TryGetComponent<OriginalTownSpriteView>(out _)) return;
            instance.AddComponent<OriginalTownSpriteView>().Configure(npcId);
        }

        private void Configure(string npcId)
        {
            if (!TryGetSprite(npcId, out string spriteName, out float lift)) return;
            var sprite = AssetMode.Load<Sprite>("Town/Residents/OriginalSprites/" + spriteName);
            if (!sprite) return;

            foreach (var humanoid in GetComponentsInChildren<TownHumanoidView>(true)) humanoid.enabled = false;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true)) renderer.enabled = false;

            var visual = new GameObject(spriteName + " (original sprite)");
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = Vector3.up * lift;
            var spriteRenderer = visual.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            float pixels = sprite.rect.height;
            float height = pixels * UnitsPerPixel;
            float native = sprite.bounds.size.y;
            if (native > 0f) visual.transform.localScale = Vector3.one * (height / native);
            visual.AddComponent<CameraFacingSprite>();
        }
    }
}
