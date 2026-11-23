# Viper egg seeking

Empty-handed hostile Vipers with a normal available action prioritize reachable eggs in their current room. No random roll or proximity to Koh is required. A reusable breadth-first room search chooses the nearest reachable egg and a legal first step, respecting walls, height, closed doors, sacred tiles, stairs, occupied cells and committed movement destinations. A cheap item-area check skips the path search when the room contains no eggs.

The Viper must occupy the egg tile before spending an action to collect it. Adjacent eggs are not taken remotely. The exact DungeonItemEntry is held in the existing carried-egg registry; floor data and the item marker are removed together. Carrying Vipers resume their normal behavior and cannot collect a second egg.

The defeat path calls DropViperCarriedEgg, which uses the existing closest-free-tile reward placement and removes the held entry only after successful placement. Species, heat/Param and identification/Flags remain intact. Existing status/action restrictions and Picket theft escape behavior take priority. State clears with the floor's species-action reset.

Validation in the main Unity Editor: 3/3 ViperEggTests passed: same-room seeking, actual path around walls, no remote adjacent pickup, next-action pickup on the egg tile, exact-entry drop once, carrying limit, room/corridor/species gates. A separate live script traced (7,7) -> (8,8) -> (9,8) -> (10,7), then pickup; the dropped egg was the same entry with heat 7 preserved.
