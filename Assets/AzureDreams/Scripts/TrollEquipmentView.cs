using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed class TrollEquipmentView : MonoBehaviour
    {
        internal ActorRecord Actor;
        private MonsterTurnSpriteView _view;
        private int _weapon = -1, _genus = -1;
        private void LateUpdate()
        {
            if (Actor == null) return;
            int weapon = Actor.Weapon?.Subtype ?? 0, genus = Actor.Flags14 & 7;
            if (_weapon == weapon && _genus == genus) return;
            if (!_view) _view = GetComponentInChildren<MonsterTurnSpriteView>();
            if (!_view) return;
            string name = weapon switch { 13 => "TrollSwordActions", 14 => "TrollHammerActions", 15 => "TrollBowActions", _ => "TrollUnarmedActions" };
            var atlas = AssetMode.Load<Texture2D>("Tower/" + name);
            if (!atlas) return;
            _view.UseEquipmentAtlas(atlas);
            _view.SetPalette((int)MonsterSpecies.TROLL, MonsterPalettes.ForElement(genus));
            _weapon = weapon; _genus = genus;
        }
    }
}
