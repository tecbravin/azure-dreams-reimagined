using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class ActorDefinitionTests
{
    [Test]
    public void Configure_ClampsMaxHealth_ToAtLeastOne()
    {
        var def = ScriptableObject.CreateInstance<ActorDefinition>();
        def.Configure("Test", ActorKind.Player, -5, 2, 0.5f, Color.white, null);
        Assert.AreEqual(1, def.MaxHealth);
    }

    [Test]
    public void Configure_ClampsAttackDamage_ToAtLeastZero()
    {
        var def = ScriptableObject.CreateInstance<ActorDefinition>();
        def.Configure("Test", ActorKind.Player, 10, -3, 0.5f, Color.white, null);
        Assert.AreEqual(0, def.AttackDamage);
    }

    [Test]
    public void Configure_ClampsMoveDelay_ToAtLeastZero()
    {
        var def = ScriptableObject.CreateInstance<ActorDefinition>();
        def.Configure("Test", ActorKind.Player, 10, 2, -1f, Color.white, null);
        Assert.AreEqual(0f, def.MoveDelay);
    }

    [Test]
    public void Configure_StoresValues_Correctly()
    {
        var def = ScriptableObject.CreateInstance<ActorDefinition>();
        var tex = new Texture2D(1, 1);
        def.Configure("Hero", ActorKind.Familiar, 42, 7, 0.25f, Color.red, tex);

        Assert.AreEqual("Hero", def.DisplayName);
        Assert.AreEqual(ActorKind.Familiar, def.Kind);
        Assert.AreEqual(42, def.MaxHealth);
        Assert.AreEqual(7, def.AttackDamage);
        Assert.AreEqual(0.25f, def.MoveDelay);
        Assert.AreEqual(Color.red, def.Tint);
        Assert.AreEqual(tex, def.AlbedoTexture);
    }
}
