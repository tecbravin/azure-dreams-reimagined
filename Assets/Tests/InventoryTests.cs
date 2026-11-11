using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class InventoryTests
{
    private Inventory _inv;
    private ItemDefinition _potion;

    [SetUp]
    public void SetUp()
    {
        _inv = new Inventory();
        _potion = ScriptableObject.CreateInstance<ItemDefinition>();
        _potion.Configure("Healing Draught", ItemDefinition.ItemType.Heal, 8, 20, "Restores 8 vitality.");
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_potion);
    }

    [Test]
    public void AddItem_IncreasesCount()
    {
        _inv.AddItem(_potion, 3);
        Assert.AreEqual(3, _inv.GetCount(_potion));
        Assert.AreEqual(1, _inv.Count);
        Assert.AreEqual(3, _inv.TotalItems);
    }

    [Test]
    public void RemoveItem_DecreasesCount()
    {
        _inv.AddItem(_potion, 3);
        Assert.IsTrue(_inv.RemoveItem(_potion, 2));
        Assert.AreEqual(1, _inv.GetCount(_potion));
    }

    [Test]
    public void RemoveItem_FailsWhenInsufficient()
    {
        _inv.AddItem(_potion, 1);
        Assert.IsFalse(_inv.RemoveItem(_potion, 2));
        Assert.AreEqual(1, _inv.GetCount(_potion));
    }

    [Test]
    public void HasItem_ChecksAmount()
    {
        _inv.AddItem(_potion, 2);
        Assert.IsTrue(_inv.HasItem(_potion, 2));
        Assert.IsFalse(_inv.HasItem(_potion, 3));
    }

    [Test]
    public void RemoveItem_RemovesEntryWhenZero()
    {
        _inv.AddItem(_potion, 1);
        _inv.RemoveItem(_potion, 1);
        Assert.AreEqual(0, _inv.Count);
        Assert.AreEqual(0, _inv.GetCount(_potion));
    }

    [Test]
    public void Clone_IsIndependent()
    {
        _inv.AddItem(_potion, 2);
        var clone = _inv.Clone();
        clone.AddItem(_potion, 1);
        Assert.AreEqual(2, _inv.GetCount(_potion));
        Assert.AreEqual(3, clone.GetCount(_potion));
    }

    [Test]
    public void Merge_CombinesStacks()
    {
        _inv.AddItem(_potion, 2);
        var other = new Inventory();
        other.AddItem(_potion, 3);
        _inv.Merge(other);
        Assert.AreEqual(5, _inv.GetCount(_potion));
    }

    [Test]
    public void AddItem_NullOrNonPositive_NoOp()
    {
        _inv.AddItem(null, 5);
        _inv.AddItem(_potion, 0);
        _inv.AddItem(_potion, -1);
        Assert.AreEqual(0, _inv.TotalItems);
    }
}
