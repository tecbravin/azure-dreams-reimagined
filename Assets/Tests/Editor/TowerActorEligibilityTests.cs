using System;
using System.Reflection;
using AzureDreams.Core;
using AzureDreamsReimagined;
using NUnit.Framework;

namespace AzureDreams.Reimagined.EditorTests
{
    public class TowerActorEligibilityTests
    {
        private static bool CheckDiagonal(int direction, short x, short y, short height,
            Func<short, short, int, short> cell, Func<ushort, ushort, short, short> surface,
            ushort[] offsetsX, ushort[] offsetsY)
        {
            var method = typeof(TowerActorEligibility).GetMethod("CheckDiagonalSideHeight");
            Assert.That(method, Is.Not.Null, "Original side-height query has not been ported");
            return (bool)method.Invoke(null, new object[] { direction, x, y, height, cell, surface, offsetsX, offsetsY });
        }

        [TestCase(512, true)]
        [TestCase(513, false)]
        [TestCase(-1, true)]
        [TestCase(32767, false)]
        [TestCase(-32768, true)]
        public void DiagonalSideHeightUsesStrictSignedThreshold(int sampledHeight, bool expected)
        {
            var directions = new System.Collections.Generic.List<int>();
            int samples = 0;
            Assert.That(CheckDiagonal(1, 3, 4, -32,
                (x, y, direction) => { directions.Add(direction); return 1; },
                (x, y, height) => { samples++; Assert.That(height, Is.EqualTo(-32)); return (short)sampledHeight; },
                new ushort[8], new ushort[8]), Is.EqualTo(expected));
            Assert.That(directions, Is.EqualTo(expected ? new[] { 0, 2 } : new[] { 0 }));
            Assert.That(samples, Is.EqualTo(expected ? 2 : 1));
        }

        [TestCase("offsetCellValue")]
        [TestCase("mapHeightWrapped")]
        [TestCase("directionXOffsets")]
        [TestCase("directionYOffsets")]
        public void MissingRequiredDiagonalDependencyIsReportedExplicitly(string missing)
        {
            Func<short, short, int, short> cell = (x, y, direction) => 1;
            Func<ushort, ushort, short, short> surface = (x, y, height) => 0;
            var error = Assert.Throws<ArgumentNullException>(() => TowerActorEligibility.CheckDiagonalSideHeight(
                1, 0, 0, 0, missing == "offsetCellValue" ? null : cell,
                missing == "mapHeightWrapped" ? null : surface,
                missing == "directionXOffsets" ? null : new ushort[8],
                missing == "directionYOffsets" ? null : new ushort[8]));
            Assert.That(error.ParamName, Is.EqualTo(missing));
        }

        [Test]
        public void ZeroSideCellValuesDoNotRequireHeightData()
        {
            int calls = 0;
            Assert.That(CheckDiagonal(1, 0, 0, 0, (x, y, d) => { calls++; return 0; }, null, null, null), Is.True);
            Assert.That(calls, Is.EqualTo(2));
        }

        [TestCase(1, 0, 2)]
        [TestCase(3, 2, 4)]
        [TestCase(5, 4, 6)]
        [TestCase(7, 6, 0)]
        [TestCase(-1, 6, 0)]
        [TestCase(65537, 0, 2)]
        public void DiagonalSidesPreserveDirectionOrderAndWrappedSampleCoordinates(int direction, int first, int second)
        {
            var calls = new System.Collections.Generic.List<int>();
            var offsetsX = new ushort[8];
            var offsetsY = new ushort[8];
            offsetsX[second] = 0xffff;
            offsetsY[second] = 0x8000;
            int samples = 0;
            bool result = CheckDiagonal(direction, -1, 32767, -32768,
                (x, y, d) =>
                {
                    Assert.That(x, Is.EqualTo(-1));
                    Assert.That(y, Is.EqualTo(32767));
                    calls.Add(d);
                    return (short)(d == first ? 0 : -1);
                },
                (x, y, h) =>
                {
                    samples++;
                    // Frozen ctypes.c_uint16 reference results, not signed offsets.
                    Assert.That(x, Is.EqualTo(65503));
                    Assert.That(y, Is.EqualTo(32736));
                    Assert.That(h, Is.EqualTo(-32768));
                    return 513;
                }, offsetsX, offsetsY);
            Assert.That(result, Is.False);
            Assert.That(calls, Is.EqualTo(new[] { first, second }));
            Assert.That(samples, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(4)]
        [TestCase(6)]
        public void CardinalSideHeightCheckDoesNotQueryTerrain(int direction)
        {
            Assert.That(CheckDiagonal(direction, 0, 0, 0, null, null, null, null), Is.True);
        }

        [TestCase(0, 0, false)]
        [TestCase(0, 1, true)]
        [TestCase(255, 1, true)]
        [TestCase(255, 32767, true)]
        [TestCase(1, -32768, false)]
        [TestCase(255, -254, true)]
        [TestCase(255, -255, false)]
        public void HpAndPendingDeltaAreAddedWithoutByteOrShortWrapping(int hp, int delta, bool expected)
        {
            var target = new ActorRecord { Hp = (byte)hp, HpDelta = (short)delta };
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0), Is.EqualTo(expected));
        }

        [TestCase(0x400)]
        [TestCase(0x200)]
        [TestCase(0x20)]
        public void OtherStatusesDoNotMakeAnOtherwiseLiveTargetUntargetable(int flags)
        {
            var target = new ActorRecord { Hp = 10, Flags1C = flags };
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0), Is.True);
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(0x3000, true)]
        public void PlayerSideMapQueryUsesOnlyTheOriginalBitTwo(int flags, bool expected)
        {
            var target = new ActorRecord { Hp = 10, Flags1C = 0x2000 };
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0,
                (x, y, direction) => (ushort)flags), Is.EqualTo(expected));
        }

        [Test]
        public void OriginalShortCircuitOrderIsPreserved()
        {
            var target = new ActorRecord { Hp = 0, Flags1C = 0x2000 | 0x10000 };
            int calls = 0;
            Func<int, int, int, ushort> map = (x, y, direction) => { calls++; return 0; };
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0, map), Is.False);
            Assert.That(calls, Is.Zero, "Dead target must short-circuit before the map query");
            target.Hp = 1;
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0, map), Is.False);
            Assert.That(calls, Is.EqualTo(1), "Side map check precedes the 0x290000 mask check");
        }

        [TestCase(-1, -1, true)]
        [TestCase(-1, 0, true)]
        [TestCase(-1, 1, true)]
        [TestCase(0, -1, true)]
        [TestCase(0, 1, true)]
        [TestCase(1, -1, true)]
        [TestCase(1, 0, true)]
        [TestCase(1, 1, true)]
        [TestCase(0, 0, false)]
        [TestCase(2, 0, false)]
        [TestCase(2, 2, false)]
        public void PhysicalRangeUsesEightNeighborsAndDoesNotProbeOthers(int dx, int dy, bool expected)
        {
            int calls = 0;
            bool actual = TowerActorEligibility.CanReachPhysicalAttack(4, 4, (short)(4 + dx), (short)(4 + dy), 0, 0, 0,
                (direction, x, y, height) => { calls++; return 1; });
            Assert.That(actual, Is.EqualTo(expected));
            Assert.That(calls, Is.EqualTo(expected ? 1 : 0));
        }

        [Test]
        public void PhysicalProbeReceivesOriginalSignedAngleAndHeightConversions()
        {
            const ushort angle = 0xFE00;
            const short sourceHeight = short.MinValue;
            bool result = TowerActorEligibility.CanReachPhysicalAttack(4, 5, 5, 5, sourceHeight, sourceHeight, angle,
                (direction, x, y, height) =>
                {
                    Assert.That(direction, Is.EqualTo(unchecked((ushort)((short)angle >> 9))));
                    Assert.That(x, Is.EqualTo(4));
                    Assert.That(y, Is.EqualTo(5));
                    Assert.That(height, Is.EqualTo(unchecked((short)(sourceHeight - 0x20))));
                    return 1;
                });
            Assert.That(result, Is.True);
        }

        [Test]
        public void EligibleTargetsAndRangesRequireTheirActualDependencies()
        {
            Assert.Throws<ArgumentNullException>(() => TowerActorEligibility.IsTargetEligible(null, 0, 0));
            Assert.Throws<ArgumentNullException>(() => TowerActorEligibility.CanReachPhysicalAttack(4, 4, 5, 4, 0, 0, 0, null));
            Assert.That(TowerActorEligibility.CanReachPhysicalAttack(4, 4, 6, 4, 0, 0, 0, null), Is.False);
        }

        [TestCase(0, false)]
        [TestCase(0x10000, false)]
        [TestCase(-0x10000, false)]
        [TestCase(0x10001, true)]
        [TestCase(-1, true)]
        public void PhysicalProbeResultIsReadAsSigned16NotAsFullInt(int probeResult, bool expected)
        {
            bool result = TowerActorEligibility.CanReachPhysicalAttack(4, 4, 5, 4, 0, 0, 0,
                (direction, x, y, height) => probeResult);
            Assert.That(result, Is.EqualTo(expected));
        }

        [TestCase(0, 63, true)]
        [TestCase(0, 64, false)]
        [TestCase(0, -63, true)]
        [TestCase(0, -64, false)]
        [TestCase(32767, -32768, false)]
        [TestCase(-32768, 32767, false)]
        public void PhysicalHeightDifferenceUsesPromotedIntegerAndRejectsAt64(int source, int target, bool expected)
        {
            int probes = 0;
            bool result = TowerActorEligibility.CanReachPhysicalAttack(4, 4, 5, 4, (short)source, (short)target, 0,
                (direction, x, y, height) => { probes++; return 1; });
            Assert.That(result, Is.EqualTo(expected));
            Assert.That(probes, Is.EqualTo(expected ? 1 : 0));
        }

        [Test]
        public void DiagonalNeighborIsInPhysicalRangeWhenTheSideHeightProbeAllowsIt()
        {
            var method = typeof(TowerActorEligibility).GetMethod("CanReachPhysicalAttack");
            Assert.That(method, Is.Not.Null, "Original physical-range predicate is missing");
            Func<ushort, short, short, short, int> probe = (direction, x, y, height) => 1;
            bool result = (bool)method.Invoke(null, new object[]
                { (short)4, (short)4, (short)5, (short)5, (short)0, (short)0, (ushort)0x200, probe });
            Assert.That(result, Is.True);
        }

        [Test]
        public void PlayerSideTargetUsesLinkedPositionMinusOneAndMapFlagTwo()
        {
            var target = new ActorRecord { Hp = 10, Flags1C = 0x2000, TileX = 99, TileY = 98 };
            int calls = 0;
            bool eligible = TowerActorEligibility.IsTargetEligible(target, 7, 9, (x, y, direction) =>
            {
                calls++;
                Assert.That(x, Is.EqualTo(6));
                Assert.That(y, Is.EqualTo(9));
                Assert.That(direction, Is.Zero);
                return 2;
            });
            Assert.That(eligible, Is.False);
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void PlayerSideTargetCannotSilentlyGuessMissingMapFlags()
        {
            var target = new ActorRecord { Hp = 10, Flags1C = 0x2000 };
            Assert.Throws<ArgumentNullException>(() => TowerActorEligibility.IsTargetEligible(target, 0, 0));
        }

        [TestCase(0)]
        [TestCase(8)]
        [TestCase(-1)]
        public void NoTileOccupancySlotRejectsTargetEvenWithZeroValue(int value)
        {
            var target = new ActorRecord { Hp = 10 };
            target.SetStatus(3, 0x1B, (sbyte)value);
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0), Is.False);
        }

        [TestCase(0x10000)]
        [TestCase(0x80000)]
        [TestCase(0x200000)]
        [TestCase(0x290000)]
        public void OriginalIneligibleFlagMaskRejectsEachSetBit(int flags)
        {
            var target = new ActorRecord { Hp = 10, Flags1C = flags };
            Assert.That(TowerActorEligibility.IsTargetEligible(target, 0, 0), Is.False);
        }

        [Test]
        public void PendingLethalDamageRejectsATargetBeforeEffectsAreCommitted()
        {
            var type = typeof(TowerGameController).Assembly.GetType("AzureDreamsReimagined.TowerActorEligibility");
            Assert.That(type, Is.Not.Null, "Original-backed actor eligibility predicate is missing");
            var target = new ActorRecord { Hp = 5, HpDelta = -5 };
            var method = type.GetMethod("IsTargetEligible", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            var result = (bool)method.Invoke(null, new object[] { target, (byte)0, (byte)0, null });
            Assert.That(result, Is.False);
        }
    }
}
