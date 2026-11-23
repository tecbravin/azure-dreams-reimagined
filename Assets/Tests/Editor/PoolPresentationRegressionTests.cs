using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AzureDreamsReimagined.Tests
{
    public class PoolPresentationRegressionTests
    {
        private static BindingFlags InstanceFlags => BindingFlags.Instance | BindingFlags.NonPublic;

        [Test]
        public void PoolExitMessageIsSuppressedUntilExitAttemptIsRearmed()
        {
            var controllerObject = new GameObject("Pool controller test");
            var roomObject = new GameObject("Pool room test");
            var player = new GameObject("Pool player test");
            try
            {
                controllerObject.SetActive(false);
                roomObject.SetActive(false);
                var controller = controllerObject.AddComponent<TowerGameController>();
                var room = roomObject.AddComponent<TownInteriorScene>();
                room.interiorId = "Interior_0d_pool_quest_done";
                Set(controller, "_interior", room);
                Set(controller, "_poolKohWearingSwimsuit", true);
                Set(controller, "_area", Enum.Parse(typeof(TowerGameController).GetNestedType("Area", BindingFlags.NonPublic), "Town"));
                Set(controller, "_player", player);
                MethodInfo block = typeof(TowerGameController).GetMethod("BlockPoolExitInSwimwear", InstanceFlags);
                MethodInfo exit = typeof(TowerGameController).GetMethod("TryExitInterior", InstanceFlags);
                Assert.That(block, Is.Not.Null);
                Assert.That(exit, Is.Not.Null);

                block.Invoke(controller, null);
                string first = (string)Get(controller, "_message");
                Assert.That(first, Is.Not.Empty);
                Set(controller, "_message", string.Empty);
                block.Invoke(controller, null);
                Assert.That((string)Get(controller, "_message"), Is.Empty);

                player.transform.position = new Vector3(100f, .55f, 100f);
                exit.Invoke(controller, null);
                Set(controller, "_message", string.Empty);
                block.Invoke(controller, null);
                Assert.That((string)Get(controller, "_message"), Is.EqualTo(first));
                Set(controller, "_poolKohWearingSwimsuit", false);
                Assert.That(block.Invoke(controller, null), Is.EqualTo(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(controllerObject);
                UnityEngine.Object.DestroyImmediate(roomObject);
                UnityEngine.Object.DestroyImmediate(player);
            }
        }

        [Test]
        public void RestoredPoolBuildsWaterQueryAndSubmergedBasin()
        {
            TextAsset geometry = Resources.Load<TextAsset>("Town/Interiors/Interior_0d_pool_quest_done/geometry");
            Assert.That(geometry, Is.Not.Null, "Restored pool geometry resource is missing.");
            var roomObject = new GameObject("Restored pool test");
            roomObject.SetActive(false);
            try
            {
                var room = roomObject.AddComponent<TownInteriorScene>();
                room.interiorId = "Interior_0d_pool_quest_done";
                room.geometry = geometry;
                typeof(TownInteriorScene).GetMethod("OnEnable", InstanceFlags)?.Invoke(room, null);

                MethodInfo query = typeof(TownInteriorScene).GetMethod("TryGetPoolWaterHeight", InstanceFlags | BindingFlags.Public);
                Assert.That(query, Is.Not.Null);
                object[] args = { new Vector3(20f, 0f, 20f), 0f };
                Assert.That(query.Invoke(room, args), Is.EqualTo(true));
                Assert.That((float)args[1], Is.EqualTo(.75f).Within(.001f));
                args = new object[] { new Vector3(17f, 0f, 18f), 0f };
                Assert.That(query.Invoke(room, args), Is.EqualTo(false));

                var materials = (Material[])Get(room, "_runtimeMaterials");
                Assert.That(materials[15].GetFloat("_ZWrite"), Is.EqualTo(1f));
                var basin = (Mesh)Get(room, "_poolBasinMesh");
                Assert.That(basin, Is.Not.Null);
                float minimumY = float.PositiveInfinity;
                foreach (Vector3 vertex in basin.vertices) minimumY = Mathf.Min(minimumY, vertex.y);
                Assert.That(minimumY, Is.EqualTo(-.45f).Within(.01f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(roomObject);
            }
        }

        [Test]
        public void SwimmingFramesOverrideSwimsuitForAllDirectionsAndRestoreWalking()
        {
            var root = new GameObject("Koh sprite test");
            var rendererObject = new GameObject("Sprite renderer");
            rendererObject.transform.SetParent(root.transform, false);
            try
            {
                root.SetActive(false);
                var view = root.AddComponent<KohSpriteView>();
                SpriteRenderer renderer = rendererObject.AddComponent<SpriteRenderer>();
                Set(view, "_renderer", renderer);
                MethodInfo swimsuit = typeof(KohSpriteView).GetMethod("PresentSwimsuit", InstanceFlags);
                MethodInfo swimming = typeof(KohSpriteView).GetMethod("PresentSwimming", InstanceFlags);
                Assert.That(swimsuit, Is.Not.Null);
                Assert.That(swimming, Is.Not.Null);
                Assert.That(swimsuit.Invoke(view, new object[] { true }), Is.EqualTo(true));
                swimming.Invoke(view, new object[] { true });
                Sprite[] swimFrames = (Sprite[])Get(view, "_swimmingFrames");
                Sprite[] suitFrames = (Sprite[])Get(view, "_swimsuitFrames");
                Assert.That(swimFrames, Has.Length.EqualTo(35));
                Assert.That(suitFrames, Is.Not.Null);
                MethodInfo presentBody = typeof(KohSpriteView).GetMethod("PresentBody", InstanceFlags);
                for (int octant = 0; octant < 8; octant++)
                for (int frame = 0; frame < 7; frame++)
                {
                    Set(view, "_swimmingStarted", Time.time - (frame * 6f + .1f) / 60f);
                    presentBody.Invoke(view, new object[] { octant, true, true });
                    Assert.That(renderer.sprite, Is.Not.Null);
                    Assert.That(Array.IndexOf(swimFrames, renderer.sprite), Is.GreaterThanOrEqualTo(0), $"Direction {octant}, frame {frame}");
                }
                swimming.Invoke(view, new object[] { false });
                presentBody.Invoke(view, new object[] { 0, true, false });
                Assert.That(Array.IndexOf(suitFrames, renderer.sprite), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static object Get(object target, string name)
        {
            var field=target.GetType().GetField(name,InstanceFlags);
            return field!=null ? field.GetValue(target) : target.GetType().GetProperty(name,InstanceFlags).GetValue(target);
        }
        private static void Set(object target, string name, object value)
        {
            var field=target.GetType().GetField(name,InstanceFlags);
            if(field!=null)field.SetValue(target,value);else target.GetType().GetProperty(name,InstanceFlags).SetValue(target,value);
        }
    }
}



