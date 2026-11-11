using System;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Engine-independent actor action predicates, compiled in the Unity runtime
    /// assembly without modifying the published Core.dll. Source-backed subset
    /// of ability_eligibility.c; does not choose spells or invent AI chances.
    /// </summary>
    public static class TowerActorEligibility
    {
        /// <summary>Side-height predicate from dungeon/level/tile.c; map data is supplied by the caller.</summary>
        public static bool CheckDiagonalSideHeight(int direction, short tileX, short tileY, short height,
            Func<short, short, int, short> offsetCellValue,
            Func<ushort, ushort, short, short> mapHeightWrapped, ushort[] directionXOffsets, ushort[] directionYOffsets)
        {
            if ((direction & 1) == 0) return true;
            if (offsetCellValue == null) throw new ArgumentNullException(nameof(offsetCellValue));
            short saved = unchecked((short)direction);
            for (int side = -1; side < 2; side += 2)
            {
                int neighbor = (saved + side) & 7;
                if (offsetCellValue(tileX, tileY, neighbor) == 0) continue;
                if (mapHeightWrapped == null) throw new ArgumentNullException(nameof(mapHeightWrapped));
                if (directionXOffsets == null) throw new ArgumentNullException(nameof(directionXOffsets));
                if (directionYOffsets == null) throw new ArgumentNullException(nameof(directionYOffsets));
                ushort x = unchecked((ushort)((tileX << 6) + 0x20 + directionXOffsets[neighbor]));
                ushort y = unchecked((ushort)((tileY << 6) + 0x20 + directionYOffsets[neighbor]));
                if (mapHeightWrapped(x, y, height) > 0x200) return false;
            }
            return true;
        }
        /// <summary>
        /// Physical-attack branch of choose_usable_ability_or_attack. Coordinates
        /// and rounded facing come from the caller's actual linked entity;
        /// this does not select abilities or map original action8 to a demo enum.
        /// </summary>
        public static bool CanReachPhysicalAttack(short sourceX, short sourceY, short targetX, short targetY,
            short sourceHeight, short targetHeight, ushort roundedFacing,
            Func<ushort, short, short, short, int> checkSideHeight)
        {
            if (Math.Abs(sourceHeight - targetHeight) >= 0x40) return false;
            int distance = Math.Max(Math.Abs(targetX - sourceX), Math.Abs(targetY - sourceY));
            if (distance != 1) return false;
            if (checkSideHeight == null) throw new ArgumentNullException(nameof(checkSideHeight));
            int result = checkSideHeight(unchecked((ushort)((short)roundedFacing >> 9)), sourceX, sourceY,
                unchecked((short)(sourceHeight - 0x20)));
            return unchecked((short)result) != 0;
        }

        /// <summary>
        /// Linked coordinates are the linked entity's byte coordinates, NOT
        /// guessed ActorRecord tile fields. Map queries are caller-supplied.
        /// </summary>
        public static bool IsTargetEligible(ActorRecord target, byte linkedX, byte linkedY,
            Func<int, int, int, ushort> linkedTileFlags = null)
        {
            if (target == null) throw new ArgumentNullException(nameof(target));
            if (target.Hp + target.HpDelta <= 0) return false;
            if ((target.Flags1C & ActorStatusFlags.PlayerSide) != 0)
            {
                if (linkedTileFlags == null) throw new ArgumentNullException(nameof(linkedTileFlags));
                if ((linkedTileFlags(linkedX - 1, linkedY, 0) & 2) != 0) return false;
            }
            if ((target.Flags1C & 0x290000) != 0) return false;
            // get_entity_effect_value returns 0x100 for an occupied zero-valued
            // slot, so presence of status0x1B is enough to reject the target.
            return !Combat.HasStatus(target, 0x1B);
        }
    }
}
