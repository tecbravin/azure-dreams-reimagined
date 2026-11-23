# Familiar position

The familiar action menu includes **Position / Posição**. Choose one of the eight cells around Koh using the mapped movement controls or the mouse; confirm with the mapped interaction button and leave with Cancel. The center is Koh and the other collar's reserved position is disabled. No turn is spent configuring formation.

Formation belongs to the red or blue collar, rather than a monster. Preferences use tracked town save variables `familiar.formation.red` and `familiar.formation.blue`, so checkpoints/save slots include them. Defaults are left for red and right for blue. Directions rotate with Koh's facing, as in retail; rotating the camera only changes the picker presentation. Replacing a summoned familiar does not replace its collar's preference.

Normal following aims at the chosen cell after Koh moves. Existing pathfinding chooses another reachable adjacent cell if blocked, avoids revealed active traps and occupied cells, and resumes the preferred slot when possible. Combat, explicit commands, Stay Still and lost-familiar behavior keep their existing priority.

## Original sources

- `azure-dreams-decomp-master/src/dungeon/menu/select.c`, `update_selection_menu_animation`: 3x3 terrain grid, separate collar selections, center/other selection skipped, confirmation sound `0x503`.
- `azure-dreams-decomp-master/src/dungeon/player/set_familiar_formation_offset.c`: formation direction stored at actor offset `0x45`.
- `azure-dreams-decomp-master/src/dungeon/actor/movement_path_g00371.c`: target direction combines the stored offset with Koh's facing and targets a neighboring tile.
- Existing extracted red/blue collar icons and the shared window design are reused; sounds use `RetailSfx.Cursor`, `Confirm` (`0x503`) and `Cancel`.

## Arrival on another floor

Active red and blue collar familiars spawn at their preferred formation cells. If blocked, the arrival search chooses the reachable free cell closest to that preference, excluding Koh, the exit elevator, the other familiar and active traps. Equal-distance ties prefer cells reached first from Koh. The red collar reserves the blue collar's ideal position while another free cell exists. Both retail/generated terrain and legacy floors use this rule; bagged familiars remain bagged.
