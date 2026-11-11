using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public class TownRetailVramTests
{
    [TestCase(0)]
    [TestCase(1)]
    [TestCase(2)]
    public void SamplesIndexedAndDirectTexelsAndPreservesCoverage(int depth)
    {
        var bytes = new byte[1024 * 512 * 2];
        var known = new byte[1024 * 512];
        // u=3 exercises the high nibble/high byte for the indexed modes.
        int source = depth == 2 ? 3 : depth == 1 ? 1 : 0;
        ushort packed = depth == 0 ? (ushort)0x2000 : depth == 1 ? (ushort)0x0200 : (ushort)0x801f;
        bytes[source * 2] = (byte)packed;
        bytes[source * 2 + 1] = (byte)(packed >> 8);
        known[source] = 1;
        int palette = 448 * 1024 + 2;
        bytes[palette * 2] = 0x1f;
        bytes[palette * 2 + 1] = 0x80;
        known[palette] = 1;
        var vram = new TownRetailVram(bytes, known);
        Assert.That(vram.TrySample((ushort)(depth << 7), 0x7000, 3, 0, out var color, out var stp), Is.True);
        Assert.That(color, Is.EqualTo(new Color32(255, 0, 0, 255)));
        Assert.That(stp, Is.True);
        Assert.That(vram.TrySample((ushort)(depth << 7), 0x7000, 3, 1, out _, out _), Is.False);
    }

    [Test]
    public void ImportedSnapshotSamplesAnOriginalTownFace()
    {
        var face = TownRetailGeometry.LoadType0().ReadFace(0);
        var vram = TownRetailVram.LoadVariant0();
        Assert.That(vram.TrySample(face.TexturePage, face.Clut, (byte)face.Uv0.x, (byte)face.Uv0.y,
            out _, out _), Is.True);
    }
}
