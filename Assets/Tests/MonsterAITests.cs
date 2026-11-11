using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class MonsterAITests
{
    private GameObject _go;
    private MonsterAI _ai;
    private CharacterStats _stats;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("Monster");
        _stats = _go.AddComponent<CharacterStats>();
        _ai = _go.AddComponent<MonsterAI>();
        _ai.turnBased = false;
        _ai.patrolRadius = 5f;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_go);
    }

    [Test]
    public void InitialState_IsPatrol()
    {
        var stateField = typeof(MonsterAI).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.AreEqual(MonsterAI.MonsterState.Patrol, (MonsterAI.MonsterState)stateField.GetValue(_ai));
    }

    [Test]
    public void CheckForPlayer_TransitionsToChase_WhenInVision()
    {
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = _go.transform.position + _go.transform.forward * 2f;

        // Ensure _player is cached
        var playerField = typeof(MonsterAI).GetField("_player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        playerField.SetValue(_ai, player.transform);

        // Directly invoke CheckForPlayer via reflection
        var method = typeof(MonsterAI).GetMethod("CheckForPlayer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_ai, null);

        var stateField = typeof(MonsterAI).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.AreEqual(MonsterAI.MonsterState.Chase, (MonsterAI.MonsterState)stateField.GetValue(_ai));

        Object.Destroy(player);
    }

    [Test]
    public void UpdateAttack_DealsDamage_OnCooldown()
    {
        var stateField = typeof(MonsterAI).GetField("_state", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        stateField.SetValue(_ai, MonsterAI.MonsterState.Attack);

        var attackTimerField = typeof(MonsterAI).GetField("_attackTimer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        attackTimerField.SetValue(_ai, 0f);

        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = _go.transform.position;
        var playerStats = player.AddComponent<CharacterStats>();
        playerStats.currentHealth = 50;

        var playerField = typeof(MonsterAI).GetField("_player", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        playerField.SetValue(_ai, player.transform);

        // Invoke private UpdateAttack
        var method = typeof(MonsterAI).GetMethod("UpdateAttack", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_ai, null);

        Assert.Less(playerStats.currentHealth, 50);

        Object.Destroy(player);
    }
}
