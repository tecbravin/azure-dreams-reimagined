using NUnit.Framework;
using UnityEngine;
using AzureDreamsReimagined;

public class TowerGameControllerTests
{
    private GameObject _go;
    private TowerGameController _ctrl;
    private GameObject _playerPrefab;
    private GameObject _creaturePrefab;
    private GameObject _npcPrefab;
    private ActorDefinition _playerDef;
    private ActorDefinition _creatureDef;
    private ActorDefinition _npcDef;
    private Material _groundMat;
    private Material _wallMat;
    private Material _accentMat;
    private Camera _camera;

    [SetUp]
    public void SetUp()
    {
        _go = new GameObject("GameController");
        _ctrl = _go.AddComponent<TowerGameController>();

        _playerPrefab = new GameObject("PlayerPrefab");
        _creaturePrefab = new GameObject("CreaturePrefab");
        _npcPrefab = new GameObject("NPCPrefab");

        _playerDef = ScriptableObject.CreateInstance<ActorDefinition>();
        _playerDef.Configure("Player", ActorKind.Player, 20, 3, 0.16f, Color.white, null);
        _creatureDef = ScriptableObject.CreateInstance<ActorDefinition>();
        _creatureDef.Configure("Creature", ActorKind.TowerCreature, 10, 2, 0.16f, Color.white, null);
        _npcDef = ScriptableObject.CreateInstance<ActorDefinition>();
        _npcDef.Configure("NPC", ActorKind.Townsperson, 10, 0, 0.16f, Color.white, null);

        _groundMat = new Material(Shader.Find("Standard"));
        _wallMat = new Material(Shader.Find("Standard"));
        _accentMat = new Material(Shader.Find("Standard"));

        _camera = new GameObject("Camera").AddComponent<Camera>();

        _ctrl.Configure(_playerPrefab, _creaturePrefab, _npcPrefab,
            _playerDef, _creatureDef, _npcDef,
            _groundMat, _wallMat, _accentMat, _camera);
    }

    [TearDown]
    public void TearDown()
    {
        Object.Destroy(_go);
        Object.Destroy(_playerPrefab);
        Object.Destroy(_creaturePrefab);
        Object.Destroy(_npcPrefab);
        Object.Destroy(_playerDef);
        Object.Destroy(_creatureDef);
        Object.Destroy(_npcDef);
        Object.Destroy(_groundMat);
        Object.Destroy(_wallMat);
        Object.Destroy(_accentMat);
        Object.Destroy(_camera.gameObject);
    }

    [Test]
    public void Start_EntersTown()
    {
        // Start is called by Unity, but here we test logic directly via reflection
        var enterTownMethod = typeof(TowerGameController).GetMethod("EnterTown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        enterTownMethod.Invoke(_ctrl, null);

        var areaField = typeof(TowerGameController).GetField("_area", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var area = (int)areaField.GetValue(_ctrl);
        Assert.AreEqual(0, area); // Area.Town = 0
    }

    [Test]
    public void EnterTower_SetsFloorToOne()
    {
        var enterTowerMethod = typeof(TowerGameController).GetMethod("EnterTower", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        enterTowerMethod.Invoke(_ctrl, null);

        var floorField = typeof(TowerGameController).GetField("_floor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.AreEqual(1, (int)floorField.GetValue(_ctrl));
    }

    [Test]
    public void ReturnToTown_Survived_BanksLoot()
    {
        var enterTowerMethod = typeof(TowerGameController).GetMethod("EnterTower", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        enterTowerMethod.Invoke(_ctrl, null);

        // Loot now belongs to the run manager; preserve the same banking assertion.
        var runField = typeof(TowerGameController).GetField("_towerRun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ((TowerRunManager)runField.GetValue(_ctrl)).AddLoot(25);

        var returnMethod = typeof(TowerGameController).GetMethod("ReturnToTown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        returnMethod.Invoke(_ctrl, new object[] { true });

        var goldField = typeof(TowerGameController).GetField("_gold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.AreEqual(25, (int)goldField.GetValue(_ctrl));
    }

    [Test]
    public void ReturnToTown_Defeated_KeepsHalfLoot()
    {
        var enterTowerMethod = typeof(TowerGameController).GetMethod("EnterTower", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        enterTowerMethod.Invoke(_ctrl, null);

        // Exercise the canonical run state rather than the obsolete HUD mirror.
        var runField = typeof(TowerGameController).GetField("_towerRun", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        ((TowerRunManager)runField.GetValue(_ctrl)).AddLoot(10);

        var returnMethod = typeof(TowerGameController).GetMethod("ReturnToTown", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        returnMethod.Invoke(_ctrl, new object[] { false });

        var goldField = typeof(TowerGameController).GetField("_gold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.AreEqual(5, (int)goldField.GetValue(_ctrl));
    }
}
