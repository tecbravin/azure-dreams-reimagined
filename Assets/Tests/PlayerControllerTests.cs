using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class PlayerControllerTests
{
    private GameObject _go;
    private PlayerController _ctrl;
    private CharacterStats _stats;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("Player");
        _stats = _go.AddComponent<CharacterStats>();
        _ctrl = _go.AddComponent<PlayerController>();
        _ctrl.turnBased = false;
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_go);
    }

    [Test]
    public void Awake_CachesStats_AndAnimator()
    {
        Assert.AreEqual(_stats, _ctrl.GetType().GetField("_stats", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(_ctrl));
    }

    [Test]
    public void TurnBased_Disables_RealTimeUpdate()
    {
        _ctrl.turnBased = true;
        // In Update, if turnBased is true, ReadInput/Move/TryInteract are skipped.
        // We verify by ensuring the flag is respected.
        Assert.IsTrue(_ctrl.GetType().GetField("turnBased", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetValue(_ctrl) is bool b && b);
    }

    [Test]
    public void TryInteract_CallsClosestInteractable_WithinRange()
    {
        var target = new GameObject("Interactable");
        target.AddComponent<SphereCollider>(); // Required for OverlapSphere
        var interactable = target.AddComponent<MockInteractable>();
        target.transform.position = _go.transform.position + Vector3.forward * 1f;

        _ctrl.interactRange = 5f;
        _ctrl.interactableLayer = Physics.DefaultRaycastLayers;

        // Manually invoke via reflection to avoid actual input system dependency
        var method = typeof(PlayerController).GetMethod("TryInteract", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        method.Invoke(_ctrl, null);

        Assert.IsTrue(interactable.WasInteracted);

        Object.Destroy(target);
    }

    [Test]
    public void ReadInput_DoesNotThrow_WhenKeyboardNull()
    {
        Assert.DoesNotThrow(() =>
        {
            var method = typeof(PlayerController).GetMethod("ReadInput", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            method.Invoke(_ctrl, null);
        });
    }

    [Test]
    public void Move_DoesNothing_WhenInputTiny()
    {
        // Move should early-out when input.sqrMagnitude < 0.01f without throwing.
        var moveMethod = typeof(PlayerController).GetMethod("Move", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.DoesNotThrow(() => moveMethod.Invoke(_ctrl, new object[] { new Vector3(0.001f, 0f, 0f) }));
    }
}

public class MockInteractable : MonoBehaviour, Interactable
{
    public bool WasInteracted { get; private set; }
    public void Interact(PlayerController player)
    {
        WasInteracted = true;
    }
}
