using NUnit.Framework;
using AzureDreamsReimagined;
using AzureDreams.Core;
using UnityEngine;

public class CoderInventoryRecordTests
{
    private static void Exact(DungeonItemEntry item, byte kind, byte subtype, sbyte param, byte flags)
    {
        Assert.That(item.Kind, Is.EqualTo(kind)); Assert.That(item.Subtype, Is.EqualTo(subtype));
        Assert.That(item.Param, Is.EqualTo(param)); Assert.That(item.Flags, Is.EqualTo(flags));
    }

    [TestCase((byte)ItemKind.Seed, (sbyte)0)]
    [TestCase((byte)ItemKind.Sword, (sbyte)-128)]
    [TestCase((byte)ItemKind.Coin, (sbyte)127)]
    public void RawItem_PreservesAllFourBytesAndCopiesInput(byte kind, sbyte param)
    {
        var inventory = new Inventory();
        var entry = new DungeonItemEntry(255, kind, param, 0xA5);
        inventory.AddDungeonItem(entry);
        entry.Kind = 0; entry.Subtype = 0; entry.Param = 0; entry.Flags = 0;
        Assert.That(inventory.TotalItems, Is.EqualTo(1)); Assert.That(inventory.Count, Is.EqualTo(1));
        Exact(inventory.DungeonItems[0], kind, 255, param, 0xA5);
        inventory.DungeonItems[0].Flags = 0;
        Exact(inventory.DungeonItems[0], kind, 255, param, 0xA5);
    }

    [Test]
    public void Clone_PreservesRecordsWithoutSharingMutableOwnership()
    {
        var source = new Inventory(); source.AddDungeonItem(new DungeonItemEntry(5, ItemKind.Seed, -7, 0x80));
        var copy = source.Clone();
        Assert.That(copy.DungeonItems[0], Is.Not.SameAs(source.DungeonItems[0]));
        copy.AddDungeonItem(new DungeonItemEntry(6, ItemKind.Fruit, 0, 1));
        Assert.That(source.TotalItems, Is.EqualTo(1)); Assert.That(copy.TotalItems, Is.EqualTo(2));
        Exact(copy.DungeonItems[0], ItemKind.Seed, 5, -7, 0x80);
    }

    [Test]
    public void Transfer_MovesRawRecordsAndPotionStacksOnlyOnce()
    {
        var potion = ScriptableObject.CreateInstance<ItemDefinition>();
        try
        {
            var source = new Inventory(); var destination = new Inventory();
            source.AddItem(potion, 3); source.AddDungeonItem(new DungeonItemEntry(1, ItemKind.Wand, -9, 0xFF));
            source.TransferTo(destination); source.TransferTo(destination);
            Assert.That(source.TotalItems, Is.Zero); Assert.That(destination.TotalItems, Is.EqualTo(4));
            Assert.That(destination.GetCount(potion), Is.EqualTo(3));
            Exact(destination.DungeonItems[0], ItemKind.Wand, 1, -9, 0xFF);
            Assert.That(destination.RemoveItem(potion), Is.True); Assert.That(destination.TotalItems, Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(potion); }
    }

    [Test]
    public void SelfMergeAndSelfTransfer_AreNoOpsAndNullInputIsIgnored()
    {
        var inventory = new Inventory(); inventory.AddDungeonItem(null);
        inventory.AddDungeonItem(new DungeonItemEntry(1, ItemKind.Seed, 0, 0));
        inventory.Merge(inventory); inventory.TransferTo(inventory); inventory.TransferTo(null); inventory.Merge(null);
        Assert.That(inventory.TotalItems, Is.EqualTo(1));
    }
}
