using NUnit.Framework;
using AzureDreamsReimagined;

public class SaveSystemTests
{
    [SetUp]
    public void SetUp()
    {
        SaveSystem.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        SaveSystem.Clear();
    }

    [Test]
    public void HasSave_FalseInitially()
    {
        Assert.IsFalse(SaveSystem.HasSave);
    }

    [Test]
    public void Save_ThenLoad_RoundTrips()
    {
        SaveSystem.Save(120, 3, "Mossling", 4);
        Assert.IsTrue(SaveSystem.HasSave);

        SaveSystem.Load(out int gold, out int townLevel, out string familiar, out int potions);
        Assert.AreEqual(120, gold);
        Assert.AreEqual(3, townLevel);
        Assert.AreEqual("Mossling", familiar);
        Assert.AreEqual(4, potions);
    }

    [Test]
    public void Clear_RemovesSave()
    {
        SaveSystem.Save(50, 2, "Mira", 1);
        SaveSystem.Clear();
        Assert.IsFalse(SaveSystem.HasSave);
    }

    [Test]
    public void Load_DefaultsWhenNoSave()
    {
        SaveSystem.Load(out int gold, out int townLevel, out string familiar, out int potions);
        Assert.AreEqual(0, gold);
        Assert.AreEqual(1, townLevel);
        Assert.AreEqual(string.Empty, familiar);
        Assert.AreEqual(0, potions);
    }
}
