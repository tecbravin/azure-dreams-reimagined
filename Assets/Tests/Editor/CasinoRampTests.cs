using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace AzureDreamsReimagined.Tests
{
    public class CasinoRampTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _controllerObject;
        private GameObject _roomObject;
        private GameObject _playerObject;
        private TowerGameController _controller;
        private TownInteriorScene _room;

        [SetUp]
        public void SetUp()
        {
            _controllerObject = new GameObject("Casino ramp controller");
            _controllerObject.SetActive(false);
            _controller = _controllerObject.AddComponent<TowerGameController>();
            _roomObject = new GameObject("Casino ramp interior");
            _roomObject.SetActive(false);
            _room = _roomObject.AddComponent<TownInteriorScene>();
            _room.interiorId = "Interior_06_casino_2";
            _playerObject = new GameObject("Casino ramp player");
            _playerObject.transform.position = new Vector3(18.25f, .55f, 9.5f);

            Set(_controller, "_interior", _room);
            Set(_controller, "_player", _playerObject);
            Set(_controller, "_townHeights", Resources.Load<TextAsset>("Town/Interiors/Interior_06_casino_2/heights").bytes);
            Type areaType = typeof(TowerGameController).GetNestedType("Area", BindingFlags.NonPublic);
            Set(_controller, "_area", Enum.Parse(areaType, "Town"));
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerObject) UnityEngine.Object.DestroyImmediate(_controllerObject);
            if (_roomObject) UnityEngine.Object.DestroyImmediate(_roomObject);
            if (_playerObject) UnityEngine.Object.DestroyImmediate(_playerObject);
        }

        [Test]
        public void MovementWalksUpRampAndSlidesBackDownWithoutJumping()
        {
            bool slidingWasObserved = false;
            for (int i = 0; i < 120 && _playerObject.transform.position.x > 13.7f; i++)
                _controller.MoveInTown(Vector2.left, .016f);

            Vector3 top = _playerObject.transform.position;
            Assert.That(top.x, Is.LessThanOrEqualTo(13.8f));
            Assert.That(top.y, Is.EqualTo(4.55f).Within(.15f));
            Assert.That((float)Get(_controller, "_townJumpHeight"), Is.EqualTo(0f).Within(.01f));

            for (int i = 0; i < 120 && _playerObject.transform.position.x < 18.2f; i++)
            {
                _controller.MoveInTown(Vector2.right, .016f);
                slidingWasObserved |= (bool)Get(_controller, "_casinoRampSliding");
            }

            Assert.That(slidingWasObserved, Is.True);
            Assert.That((bool)Get(_controller, "_casinoRampSliding"), Is.False);
            Assert.That(_playerObject.transform.position.x, Is.GreaterThan(18f));
            Assert.That((float)Get(_controller, "_townJumpHeight"), Is.EqualTo(0f).Within(.01f));
        }

        [Test]
        public void RampSurfaceAllowsFeetOnTopAndColliderHasSolidMesh()
        {
            MethodInfo heightQuery = typeof(TowerGameController).GetMethod("TryCasinoRampHeight", PrivateInstance);
            object[] args = { new Vector3(16f, 0f, 9.5f), 0f, false };
            Assert.That(heightQuery.Invoke(_controller, args), Is.EqualTo(true));
            Assert.That((float)args[1], Is.EqualTo(2f).Within(.15f));
            args = new object[] { new Vector3(13.5f, 0f, 9.5f), 0f, false };
            Assert.That(heightQuery.Invoke(_controller, args), Is.EqualTo(true));
            Assert.That((float)args[1], Is.EqualTo(4f).Within(.15f));

            MethodInfo positionOpen = typeof(TowerGameController).GetMethod("TownPlayerPositionOpen", PrivateInstance);
            Assert.That(positionOpen.Invoke(_controller, new object[] { new Vector3(16f, 0f, 9.5f), Vector3.zero, 0f }), Is.EqualTo(false));
            Assert.That(positionOpen.Invoke(_controller, new object[] { new Vector3(16f, 0f, 9.5f), Vector3.zero, 2f }), Is.EqualTo(true));



            typeof(TowerGameController).GetMethod("BuildCasinoRampCollider", PrivateInstance).Invoke(_controller, null);
            MeshCollider collider = _controllerObject.GetComponentInChildren<MeshCollider>();
            Assert.That(collider, Is.Not.Null);
            Assert.That(collider.sharedMesh, Is.Not.Null);
            Assert.That(collider.sharedMesh.vertexCount, Is.GreaterThan(0));
        }

        private static object Get(object target, string name) => target.GetType().GetField(name, PrivateInstance).GetValue(target);
        private static void Set(object target, string name, object value) => target.GetType().GetField(name, PrivateInstance).SetValue(target, value);
    }
}

