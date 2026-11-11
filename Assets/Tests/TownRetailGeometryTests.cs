using System.IO;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;

public class TownRetailGeometryTests
{
    [Test]
    public void RetailBankPreservesSignedCoordinatesAndPacketBindings()
    {
        var bank = TownRetailGeometry.LoadType0();
        Assert.That(bank.ListCount, Is.EqualTo(934));
        Assert.That(bank.VertexCount, Is.EqualTo(2314));
        Assert.That(bank.ReadVertex(0), Is.EqualTo(new Vector3Int(-144, 16, -128)));
        Assert.That(bank.GetListStart(2), Is.EqualTo(216));
        var f = bank.ReadFace(0);
        Assert.That(new[] { f.A, f.B, f.C, f.D }, Is.EqualTo(new ushort[] { 4, 6, 5, 7 }));
        Assert.That(f.NormalIndex, Is.EqualTo(1));
        Assert.That(f.Uv0, Is.EqualTo(new Vector2Int(111, 201)));
        Assert.That(f.Clut, Is.EqualTo(0x7280));
        Assert.Throws<InvalidDataException>(() => bank.ReadFace(1));
        Assert.Throws<InvalidDataException>(() => bank.ReadFace(179556));
    }

    [TestCase(true)]
    [TestCase(false)]
    public void AllListsTerminateAndReferenceValidGeometry(bool frontFacing)
    {
        var bank = TownRetailGeometry.LoadType0();
        int polygons = 0;
        for (int id = 0; id < bank.ListCount; id++)
        {
            int cursor = bank.GetListStart(id), count = 0;
            while (cursor >= 0)
            {
                Assert.That(++count, Is.LessThan(8000), "List " + id + " does not terminate");
                var face = bank.ReadFace(cursor);
                if (face.HasPolygon)
                {
                    Assert.That(face.A, Is.LessThan(bank.VertexCount));
                    Assert.That(face.B, Is.LessThan(bank.VertexCount));
                    Assert.That(face.C, Is.LessThan(bank.VertexCount));
                    Assert.That(face.D, Is.LessThan(bank.VertexCount));
                    bank.ReadNormal(face.NormalIndex);
                    polygons++;
                }
                cursor = bank.NextPacket(cursor, frontFacing);
            }
        }
        Assert.That(polygons, Is.GreaterThan(1000));
    }
}
