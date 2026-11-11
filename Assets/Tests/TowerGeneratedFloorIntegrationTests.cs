using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AzureDreamsReimagined;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// Observe after the controller's LateUpdate: UnityTest yield-null resumes
// before LateUpdate and otherwise compares two different presentation frames.
[DefaultExecutionOrder(10000)]
public sealed class GeneratedCameraLateFrameProbe : MonoBehaviour
{
    public Transform Target;
    public Vector3 ObservedOffset;
    public int Samples;
    private void LateUpdate()
    {
        if (!Target) return;
        ObservedOffset = transform.position - Target.position;
        Samples++;
    }
}

public class TowerGeneratedFloorIntegrationTests
{
    private GameObject _root;
    private TowerGameController _controller;
    private Camera _camera;
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private IList Creatures => Get<IList>("_creatures");
    private T FirstCreatureField<T>(string member)
    {
        Assert.That(Creatures.Count, Is.GreaterThan(0), "Floor must have a creature");
        object state = Creatures[0];
        return (T)state.GetType().GetField(member).GetValue(state);
    }
    private void SetCreatureField(string member, object value)
    {
        Assert.That(Creatures.Count, Is.GreaterThan(0));
        object state = Creatures[0];
        state.GetType().GetField(member).SetValue(state, value);
        Creatures[0] = state;
    }
    private T Get<T>(string name) => (T)typeof(TowerGameController).GetField(name, Flags).GetValue(_controller);
    private void Set(string name, object value) => typeof(TowerGameController).GetField(name, Flags).SetValue(_controller, value);
    private object Call(string name, params object[] args)
    {
        var method = typeof(TowerGameController).GetMethod(name, Flags);
        Assert.That(method, Is.Not.Null, "Generated-floor bridge missing: " + name);
        return method.Invoke(_controller, args);
    }

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        _root = new GameObject("Generated tower floor integration");
        _controller = _root.AddComponent<TowerGameController>();
        _camera = new GameObject("Generated tower floor camera").AddComponent<Camera>();
        _camera.enabled = false;
        _controller.Configure(null, null, null, null, null, null, null, null, null, _camera);
        yield return null;
        Set("_familiarDefinition", null);
        Call("EnterTown");
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_root) UnityEngine.Object.Destroy(_root);
        if (_camera) UnityEngine.Object.Destroy(_camera.gameObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_CameraFollowsPresentedPlayerOutsideDemoBounds()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        var layout = Get<TowerFloorManager>("_towerFloorLayout");
        var player = Get<GameObject>("_player");
        var initialOffset = _camera.transform.position - player.transform.position;
        var probe = _camera.gameObject.AddComponent<GeneratedCameraLateFrameProbe>();
        probe.Target = player.transform;
        Vector2Int from = default;
        bool found = false;
        foreach (var cell in layout.OpenCells)
        {
            var to = cell + Vector2Int.right;
            if (cell.x <= 12 || !layout.IsWalkable(to.x, to.y) || to == layout.StairsUpCell ||
                cell == FirstCreatureField<Vector2Int>("Cell") || to == FirstCreatureField<Vector2Int>("Cell")) continue;
            from = cell;
            found = true;
            break;
        }
        Assert.That(found, Is.True);
        // Diagnostic relocation isolates camera tracking from pathfinding.
        Set("_playerCell", from);
        player.transform.position = new Vector3(from.x, 0.55f, from.y);
        Assert.That(_controller.TryTowerAction(Vector2Int.right), Is.True);
        int committedTurn = _controller.TowerTurnNumber;
        _controller.TickTurnPresentation(Get<float>("_turnDuration") * 0.5f);
        yield return null; // Exercise the real LateUpdate hook, not a manual camera call.
        Assert.That(probe.Samples, Is.GreaterThan(0));
        var currentOffset = probe.ObservedOffset;
        Assert.That(currentOffset.x, Is.EqualTo(initialOffset.x).Within(0.001f));
        Assert.That(currentOffset.z, Is.EqualTo(initialOffset.z).Within(0.001f));
        Assert.That(_controller.TowerTurnNumber, Is.EqualTo(committedTurn));
        _controller.TickTurnPresentation(1f);
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_CameraFollowStopsAfterReturningToTown()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        yield return null;
        Call("ReturnToTown", true);
        var townCameraPosition = _camera.transform.position;
        Get<GameObject>("_player").transform.position += Vector3.right * 2f;
        yield return null;
        Assert.That(_camera.transform.position, Is.EqualTo(townCameraPosition));
    }

    [UnityTest]
    public IEnumerator AuthoredFloor_KeepsItsExistingFixedCamera()
    {
        Call("EnterTower");
        var fixedPosition = _camera.transform.position;
        Get<GameObject>("_player").transform.position += Vector3.right * 2f;
        yield return null;
        Assert.That(_camera.transform.position, Is.EqualTo(fixedPosition));
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_ReturnReleasesOwnedMeshes()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        var mesh = _root.transform.Find("Generated Tower Floor Mesh").GetComponent<MeshFilter>().sharedMesh;
        Call("ReturnToTown", true);
        yield return null;
        Assert.That(mesh == null, Is.True, "Destroying floor objects alone does not destroy their generated Mesh assets");
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_DestroyingControllerReleasesOwnedMeshes()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        var mesh = _root.transform.Find("Generated Tower Wall Mesh").GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.Destroy(_root);
        yield return null;
        yield return null;
        Assert.That(mesh == null, Is.True);
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_UsesBatchedSurfaceAndBoundaryMeshes()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        var layout = Get<TowerFloorManager>("_towerFloorLayout");
        var floor = _root.transform.Find("Generated Tower Floor Mesh");
        Assert.That(floor, Is.Not.Null, "Walkable cells need actual floor geometry, not one plane over void");
        var mesh = floor.GetComponent<MeshFilter>().sharedMesh;
        Assert.That(mesh.vertexCount, Is.EqualTo(layout.OpenCells.Count * 4));
        Assert.That(mesh.triangles.Length, Is.EqualTo(layout.OpenCells.Count * 6));
        var vertices = mesh.vertices;
        for (int i = 0; i < vertices.Length; i += 4)
        {
            var center = (vertices[i] + vertices[i + 1] + vertices[i + 2] + vertices[i + 3]) / 4f;
            Assert.That(layout.IsWalkable(Mathf.RoundToInt(center.x), Mathf.RoundToInt(center.z)), Is.True);
        }
        var walls = _root.transform.Find("Generated Tower Wall Mesh");
        Assert.That(walls, Is.Not.Null);
        Assert.That(walls.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.GreaterThan(0));
        Assert.That(Get<List<GameObject>>("_generated").Count, Is.LessThan(50), "Do not instantiate thousands of wall objects");
        yield break;
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_CanMoveBeyondTheOldTwelveCellDemoBounds()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        var layout = Get<TowerFloorManager>("_towerFloorLayout");
        Vector2Int from = default;
        bool found = false;
        foreach (var cell in layout.OpenCells)
        {
            var to = cell + Vector2Int.right;
            if (cell.x <= 12 || !layout.IsWalkable(to.x, to.y) || to == layout.StairsUpCell ||
                cell == FirstCreatureField<Vector2Int>("Cell") || to == FirstCreatureField<Vector2Int>("Cell")) continue;
            from = cell;
            found = true;
            break;
        }
        Assert.That(found, Is.True, "Fixture must exercise a real generated cell beyond the demo grid");
        Set("_playerCell", from);
        Get<GameObject>("_player").transform.position = new Vector3(from.x, 0.55f, from.y);
        Assert.That(_controller.TryTowerAction(Vector2Int.right), Is.True);
        _controller.TickTurnPresentation(1f);
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(from + Vector2Int.right));
        yield break;
    }

    [UnityTest]
    public IEnumerator GeneratedFloor_PlayerAndStairsUseManagerLayout()
    {
        Call("ConfigureGeneratedTowerFloors", true);
        Call("EnterTower");
        var layout = Get<TowerFloorManager>("_towerFloorLayout");
        Assert.That(layout, Is.Not.Null);
        Assert.That(layout.IsGenerated, Is.True);
        Assert.That(layout.CurrentFloor, Is.EqualTo(1));
        Assert.That(Get<Vector2Int>("_playerCell"), Is.EqualTo(layout.StairsDownCell));
        Assert.That(Get<Vector2Int>("_stairCell"), Is.EqualTo(layout.StairsUpCell));
        Assert.That(layout.IsWalkable(layout.StairsDownCell.x, layout.StairsDownCell.y), Is.True);
        Assert.That(layout.StairsUpCell, Is.Not.EqualTo(layout.StairsDownCell));
        yield break;
    }
}
