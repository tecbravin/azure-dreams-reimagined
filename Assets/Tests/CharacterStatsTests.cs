using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class CharacterStatsTests
{
    private GameObject _go;
    private CharacterStats _stats;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("TestActor");
        _stats = _go.AddComponent<CharacterStats>();
        _stats.maxHealth = 100;
        _stats.currentHealth = 100;
        _stats.defense = 5;
        _stats.isActive = true;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_go);
    }

    [Test]
    public void TakeDamage_ReducesHealth_ClampedAtZero()
    {
        _stats.TakeDamage(10);
        Assert.AreEqual(95, _stats.currentHealth);
    }

    [Test]
    public void TakeDamage_RespectsDefense_MinimumOne()
    {
        _stats.currentHealth = 10;
        _stats.TakeDamage(3);
        Assert.AreEqual(9, _stats.currentHealth);
    }

    [Test]
    public void TakeDamage_DoesNothing_WhenInactive()
    {
        _stats.isActive = false;
        _stats.TakeDamage(50);
        Assert.AreEqual(100, _stats.currentHealth);
    }

    [Test]
    public void TakeDamage_Deactivates_WhenHealthReachesZero()
    {
        _stats.currentHealth = 2;
        _stats.TakeDamage(10);
        Assert.AreEqual(0, _stats.currentHealth);
        Assert.IsFalse(_stats.isActive);
        Assert.IsFalse(_go.activeSelf);
    }

    [Test]
    public void Heal_IncreasesHealth_ClampedAtMax()
    {
        _stats.currentHealth = 50;
        _stats.Heal(30);
        Assert.AreEqual(80, _stats.currentHealth);

        _stats.Heal(50);
        Assert.AreEqual(100, _stats.currentHealth);
    }

    [Test]
    public void ResetStats_RestoresHealth_AndReactivates()
    {
        _stats.currentHealth = 0;
        _stats.isActive = false;
        _go.SetActive(false);

        _stats.ResetStats();

        Assert.AreEqual(100, _stats.currentHealth);
        Assert.IsTrue(_stats.isActive);
        Assert.IsTrue(_go.activeSelf);
    }
}
