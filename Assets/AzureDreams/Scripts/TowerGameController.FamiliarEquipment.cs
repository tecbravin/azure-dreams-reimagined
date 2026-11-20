using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private bool TrollCanUnequip(ActorRecord actor) => _area == Area.Tower && IsTroll(actor) &&
            actor.Weapon != null && ((actor == _familiarActor && _familiar) ||
                (actor == _blueCollarAlly?.Actor && _blueCollarAlly.View));

        private string FamiliarRootActionLabel(int row)
        {
            if (TrollCanUnequip(RootFamiliar))
            {
                if (row == 0) return T("Unequip", "Desequipar");
                row--;
            }
            return row == 0 ? T("Command", "Comandar") : row == 1 ? T("Change AI", "Mudar IA") : T("Fuse", "Fundir");
        }

        private void UnequipActiveTroll(ActorRecord actor)
        {
            if (!TrollCanUnequip(actor) || IsFamiliarLost(actor) || _turnBusy) return;
            var cell = actor == _familiarActor ? _familiarCell : _blueCollarAlly.Cell;
            var item = OffBody(actor.Weapon);
            if (!PlaceInventoryItem(cell, item))
            {
                _message = T("There is no room beneath the Troll to drop its equipment.",
                    "Não há espaço no chão sob o Troll para largar o equipamento.");
                return;
            }
            actor.Weapon = null;
            PlayRetailSound(RetailSfx.Equip);
            _inventoryMenuRow = 0;
            RefreshInventoryCommands();
        }

        private static bool HasLevaFruit(ActorRecord actor) => actor != null && (actor.Flags14 & LevaFusionProtection) != 0;

        private bool RefusesLevaFruit(DungeonItemEntry item, ActorRecord actor)
        {
            if (item?.Kind != ItemKind.Fruit || item.Subtype != 4 || !HasLevaFruit(actor)) return false;
            _message = T("The familiar refuses another Leva Fruit.", "O familiar recusa outra Fruta Leva.");
            return true;
        }

        private bool GiveTrollWeapon(Inventory source, int index, ActorRecord actor, bool lockCursed)
        {
            var items = source?.DungeonItems;
            if (!IsTroll(actor) || items == null || index < 0 || index >= items.Count || !IsTrollWeapon(items[index])) return false;
            var item = items[index];
            if (lockCursed && (Inventory.IsLockedEquipment(item) || Inventory.IsCursedEquipment(actor.Weapon)))
            { ShowLockedEquipmentMessage(); return false; }
            if (!source.ConsumeDungeonItem(index, item.Kind)) return false;
            var old = actor.Weapon;
            actor.Weapon = WithFlags(item, (byte)(item.Flags & ~(Inventory.WornMask | Inventory.UnrevealedEquipmentFlag)));
            if (old != null) source.AddDungeonItem(OffBody(old));
            PlayRetailSound(RetailSfx.Equip);
            return true;
        }

        // Migrate the former inventory-owned familiar equipment without deleting
        // ordinary carried items. Existing saves retain their original raw record.
        private void MigrateFamiliarEquipment(Inventory inventory)
        {
            if (inventory == null) return;
            var items = inventory.DungeonItems;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var item = items[i];
                if (!IsTrollWeapon(item)) continue;
                ActorRecord actor = (item.Flags & FamiliarEquipped) != 0 ? _familiarActor :
                    (item.Flags & BlueFamiliarEquipped) != 0 ? _blueCollarAlly?.Actor : null;
                if (!IsTroll(actor) || actor.Weapon != null) continue;
                if (inventory.ConsumeDungeonItem(i, item.Kind)) actor.Weapon = OffBody(item);
            }
        }

        private void PresentFamiliarEquipment()
        {
            PresentTrollEquipment(_familiar, _familiarActor);
            if (_blueCollarAlly != null) PresentTrollEquipment(_blueCollarAlly.View, _blueCollarAlly.Actor);
        }

        private static void PresentTrollEquipment(GameObject view, ActorRecord actor)
        {
            if (!view || !IsTroll(actor)) return;
            var binding = view.GetComponent<TrollEquipmentView>();
            if (!binding) binding = view.AddComponent<TrollEquipmentView>();
            binding.Actor = actor;
        }
    }
}
