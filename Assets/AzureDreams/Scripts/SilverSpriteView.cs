using AzureDreamsReimagined.Rendering;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Presents Silver with the captured elderly-town sprite.  Silver is an
    /// alias of the original old-man resident rather than the generic helper
    /// NPC, so his image must keep following his logical facing direction.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SilverSpriteView : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private CameraFacingSprite _billboard;
        private Vector3 _facing = Vector3.back;

        private void Awake()
        {
            // Town_Dar is the extracted elderly character sprite used by the
            // original Silver actor.  Keep its feet/pivot intact and only add
            // the directional billboard presentation around it.
            _renderer = GetComponentInChildren<SpriteRenderer>(true);
            if (!_renderer) return;
            _billboard = _renderer.GetComponent<CameraFacingSprite>();
            if (!_billboard) _billboard = _renderer.gameObject.AddComponent<CameraFacingSprite>();
            PresentMovement(_facing, false);
        }

        internal void PresentMovement(Vector3 direction, bool moving)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.0001f)
            {
                // Match the cardinal selection used by the town player rather
                // than leaving a stale left/right flip after a conversation.
                if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.z))
                    _facing = direction.x >= 0f ? Vector3.right : Vector3.left;
                else
                    _facing = direction.z >= 0f ? Vector3.forward : Vector3.back;
            }

            if (_billboard) _billboard.FaceMonster(_facing);
        }
    }
}
