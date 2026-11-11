using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AzureDreamsReimagined;

public class DemoSceneMovementSmokeTests
{
    [UnityTest]
    public IEnumerator DemoScene_KeyboardMovesPlayer_AndCameraRendersTown()
    {
        yield return SceneManager.LoadSceneAsync("ReimaginedDemo", LoadSceneMode.Additive);
        var scene = SceneManager.GetSceneByName("ReimaginedDemo");
        Keyboard keyboard = null;
        Texture2D capture = null;
        var previous = RenderTexture.active;
        var previousEditorInputBehavior = InputSettings.EditorInputBehaviorInPlayMode.PointersAndKeyboardsRespectGameViewFocus;
        var previousBackground = InputSystem.settings.backgroundBehavior;
        var previousRunInBackground = Application.runInBackground;
        try
        {
            yield return null;
            TowerGameController controller = null;
            Camera camera = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<TowerGameController>(out var game)) controller = game;
                if (root.TryGetComponent<Camera>(out var view)) camera = view;
            }
            Assert.That(controller, Is.Not.Null);
            Assert.That((bool)typeof(TowerGameController).GetField("_useGeneratedTowerFloors", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller), Is.True, "Playable demo must use procedural tower layouts");
            Assert.That(camera, Is.Not.Null);
            var player = (GameObject)typeof(TowerGameController).GetField("_player",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(controller);
            Assert.That(player.GetComponent<ActorLocomotionView>(), Is.Not.Null);
            Assert.That(player.GetComponent<KohSpriteView>(), Is.Not.Null, "Demo must instantiate the confirmed Koh visual");
            var kohSprite = player.GetComponentInChildren<SpriteRenderer>();
            Assert.That(kohSprite, Is.Not.Null);
            Assert.That(kohSprite.sprite, Is.Not.Null);
            foreach (var placeholder in player.GetComponentsInChildren<MeshRenderer>(true))
                Assert.That(placeholder.enabled, Is.False, "Placeholder body must be hidden");
            var start = player.transform.position;
            previousEditorInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground = true;
            keyboard = InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.D));
            yield return null;
            yield return null;

            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return null;
            Assert.That(player.transform.position.x, Is.GreaterThan(start.x), "Real InputAction path must move the scene's player");
            Assert.That(player.transform.position.y, Is.EqualTo(start.y).Within(0.0001f));
            Assert.That(player.GetComponent<ActorLocomotionView>().IsMoving, Is.False);

            var renderTarget = new RenderTexture(320, 240, 16, RenderTextureFormat.ARGB32);
            renderTarget.Create();
            var previousTarget = camera.targetTexture;
            var previousEnabled = camera.enabled;
            var lowRes = camera.GetComponent<AzureDreamsReimagined.Rendering.PsxLowResRender>();
            bool previousLowResEnabled = lowRes != null && lowRes.enabled;
            if (lowRes != null) lowRes.enabled = false;
            camera.enabled = true;
            camera.targetTexture = renderTarget;
            camera.Render();
            capture = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGBA32, false);
            RenderTexture.active = renderTarget;
            capture.ReadPixels(new Rect(0, 0, renderTarget.width, renderTarget.height), 0, 0);
            capture.Apply();
            var colors = new HashSet<uint>();
            foreach (var pixel in capture.GetPixels32())
                colors.Add((uint)(pixel.r | (pixel.g << 8) | (pixel.b << 16)));
            Assert.That(colors.Count, Is.GreaterThan(16), "The demo must not render a blank/flat frame");
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "../Logs/Movement"));
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "demo-town-smoke.png"), capture.EncodeToPNG());
            Debug.Log("[Movement demo] Real keyboard moved scene player; distinct rendered colors=" + colors.Count);
            camera.targetTexture = previousTarget;
            camera.enabled = previousEnabled;
            if (lowRes != null) lowRes.enabled = previousLowResEnabled;
            renderTarget.Release();
        }
        finally
        {
            RenderTexture.active = previous;
            if (keyboard != null) InputSystem.RemoveDevice(keyboard);
            InputSystem.settings.editorInputBehaviorInPlayMode = previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = previousBackground;
            Application.runInBackground = previousRunInBackground;
            if (capture != null) Object.DestroyImmediate(capture);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
    }
}
