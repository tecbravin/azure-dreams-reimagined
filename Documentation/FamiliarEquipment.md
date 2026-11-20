# Familiar equipment and Leva Fruit

Troll weapons are owned by `ActorRecord.Weapon`. Giving one removes it from the
source inventory or home storage. Replacing a weapon returns the old weapon to
that source, preserving its modifier and flags. Cursed-weapon restrictions still
apply in the tower. Legacy familiar equipment marked in Koh's inventory moves
to the familiar when the inventory is refreshed or town progress is saved.

Combat and the familiar status panel read the familiar's equipment. A tagged,
versioned ten-byte equipment extension follows the original 0xBC actor snapshot
when equipment exists; saves with the original raw record remain readable.
This also preserves weapons in traveling familiars, hut storage and checkpoints.

Leva protection remains in the original `Flags14` bit. It is shown in the
familiar status, cannot be applied a second time while active, and a refused
fruit stays in the source inventory. Restorative fruits remain ordinary food.
Fusion previews clear Leva only in the result; successful tower fusion clears
the mark on the live parents. Hut fusion saves the preview result without it.

`Tools/build_unarmed_troll.py` reconstructs the original Troll body using the
verified resource 022 command chains and CLUT 0, omitting separate equipment
parts. `TrollEquipmentView` switches between that body and the sword/hammer/bow
atlases when the familiar's held weapon changes. The community action stream
cannot overwrite an equipment-specific atlas.

Validation: runtime compilation and an isolated Unity probe covering transfer,
weapon swaps, repeat-Leva refusal, new and legacy snapshots, fusion preview,
and live unarmed/hammer/unarmed visual transitions. Results and captures are
under `../Validation/TrollEquipment/`.
