using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using AzureDreams.Core;
using NUnit.Framework;
using UnityEngine;
using CoreDungeon = AzureDreams.Core.DungeonGenerator;

namespace AzureDreamsReimagined.Tests
{
    public class ViperEggTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private GameObject _root;
        private TowerGameController _controller;
        private TowerFloorManager _manager;
        private TowerItemTrapManager _items;
        private CoreDungeon _dungeon;
        private ActorRecord _viper;

        [SetUp]
        public void SetUp()
        {
            _manager = new TowerFloorManager();
            _dungeon = new CoreDungeon(1, 1);
            PropertyInfo roomsProperty = typeof(CoreDungeon).GetProperty("Rooms");
            Array rooms = (Array)roomsProperty.GetValue(_dungeon);
            Type roomType = rooms.GetType().GetElementType();
            rooms.SetValue(MakeRoom(roomType, 5, 5, 8, 8), 0);
            rooms.SetValue(MakeRoom(roomType, 20, 5, 6, 6), 1);
            typeof(CoreDungeon).GetProperty("RoomCount").SetValue(_dungeon, 2);
            Array grid = (Array)typeof(CoreDungeon).GetProperty("Grid").GetValue(_dungeon);
            object floorTile = Enum.Parse(grid.GetType().GetElementType(), "Floor");
            object wallTile = Enum.Parse(grid.GetType().GetElementType(), "Wall");
            for (int y = 0; y < grid.GetLength(0); y++)
            for (int x = 0; x < grid.GetLength(1); x++) grid.SetValue(wallTile, y, x);
            for (int y = 5; y < 13; y++) for (int x = 5; x < 13; x++) grid.SetValue(floorTile, y, x);
            for (int y = 5; y < 11; y++) for (int x = 20; x < 26; x++) grid.SetValue(floorTile, y, x);

            SetProperty(_manager, "Dungeon", _dungeon);
            SetProperty(_manager, "Heights", new int[64, 64]);
            IList openCells = (IList)typeof(TowerFloorManager).GetField("_openCells", Private).GetValue(_manager);
            for (int y = 5; y < 13; y++) for (int x = 5; x < 13; x++) openCells.Add(new Vector2Int(x, y));
            for (int y = 5; y < 11; y++) for (int x = 20; x < 26; x++) openCells.Add(new Vector2Int(x, y));
            _items = new TowerItemTrapManager(_manager, 1);

            _root = new GameObject("Viper egg controller");
            _root.SetActive(false);
            _controller = _root.AddComponent<TowerGameController>();
            Set(_controller, "_area", Enum.Parse(typeof(TowerGameController).GetNestedType("Area", BindingFlags.NonPublic), "Tower"));
            Set(_controller, "_useGeneratedTowerFloors", true);
            Set(_controller, "_towerFloorLayout", _manager);
            Set(_controller, "_towerItemTraps", _items);
            Set(_controller, "_stairCell", new Vector2Int(1, 1));
            Set(_controller, "_playerCell", new Vector2Int(12, 12));
            var walls = new HashSet<Vector2Int>();
            for(int y=0;y<64;y++)for(int x=0;x<64;x++)
                if(_dungeon.Grid[y,x]==CoreDungeon.TileType.Wall)walls.Add(new Vector2Int(x,y));
            Set(_controller, "_walls", walls);
            _viper = new ActorRecord { Species = (byte)MonsterSpecies.VIPER, Hp = 10, TileX = 7, TileY = 7 };
        }

        [TearDown]
        public void TearDown()
        {
            if (_root) UnityEngine.Object.DestroyImmediate(_root);
        }

        [Test]
        public void ViperRoutesToRoomEggThenPicksUpOnlyOnItsNextActionAndDropsExactEntryOnce()
        {
            Vector2Int eggCell = new Vector2Int(10, 7);
            DungeonItemEntry egg = MonsterEgg.CreateTowerSpawn((byte)MonsterSpecies.FROG, true, 5, 23);
            Assert.That(Place(eggCell, egg), Is.True);
            object state = CreatureState(_viper, new Vector2Int(7, 7));

            Assert.That(Act(state, Vector2Int.zero, out Vector2Int firstStep), Is.True);
            Assert.That(firstStep, Is.Not.EqualTo(Vector2Int.zero));
            Assert.That(GetItem(eggCell), Is.Not.Null, "Adjacency must not pick the egg up.");

            Vector2Int cell = new Vector2Int(7, 7);
            for (int i = 0; i < 10 && cell != eggCell; i++)
            {
                cell += firstStep;
                state = CreatureState(_viper, cell);
                if (cell != eggCell) Assert.That(Act(state, Vector2Int.zero, out firstStep), Is.True);
            }
            Assert.That(cell, Is.EqualTo(eggCell), "Viper never reached the egg.");
            state = CreatureState(_viper, eggCell);
            Assert.That(Act(state, Vector2Int.zero, out Vector2Int pickupStep), Is.True);
            Assert.That(pickupStep, Is.EqualTo(Vector2Int.zero));
            Assert.That(GetItem(eggCell), Is.Null);
            var carried = (Dictionary<ActorRecord, DungeonItemEntry>)typeof(TowerGameController).GetField("_viperCarriedEgg", Private).GetValue(_controller);
            Assert.That(carried[_viper], Is.EqualTo(egg));

            Assert.That(Place(new Vector2Int(11,7), MonsterEgg.CreateTowerSpawn(3,true,0,10)),Is.True);
            Assert.That(Act(state,Vector2Int.zero,out _),Is.False,"Carrying Viper took a second egg.");
            MethodInfo drop = typeof(TowerGameController).GetMethod("DropViperCarriedEgg", Private);
            Assert.That(drop.Invoke(_controller, new object[] { _viper, eggCell }), Is.EqualTo(true));
            Assert.That(GetItem(eggCell), Is.EqualTo(egg));
            Assert.That(carried.ContainsKey(_viper), Is.False);
            Assert.That(drop.Invoke(_controller, new object[] { _viper, eggCell }), Is.EqualTo(false));
        }

        [Test]
        public void ViperIgnoresEggsOutsideItsRoomAndOtherSpeciesDoNotSeekEggs()
        {
            Assert.That(Place(new Vector2Int(21, 7), MonsterEgg.CreateTowerSpawn(1, true, 2, 20)), Is.True);
            object viperState = CreatureState(_viper, new Vector2Int(7, 7));
            Assert.That(Act(viperState, Vector2Int.zero, out _), Is.False);
            Assert.That(Place(new Vector2Int(10,7), MonsterEgg.CreateTowerSpawn(3,true,0,10)),Is.True);
            Assert.That(Act(CreatureState(_viper,new Vector2Int(14,7)),Vector2Int.zero,out _),Is.False);
            var other = new ActorRecord { Species = (byte)MonsterSpecies.FROG, Hp = 10, TileX = 7, TileY = 7 };
            Assert.That(Act(CreatureState(other, new Vector2Int(7, 7)), Vector2Int.zero, out _), Is.False);
        }

        [Test]
        public void ViperRoutesAroundObstaclesAndCannotGrabAdjacentEggRemotely()
        {
            var eggCell=new Vector2Int(10,7);
            Assert.That(Place(eggCell,MonsterEgg.CreateTowerSpawn(3,true,1,17)),Is.True);
            var adjacent=CreatureState(_viper,new Vector2Int(9,7));
            Assert.That(Act(adjacent,new Vector2Int(12,12),out var finalStep),Is.True);
            Assert.That(finalStep,Is.EqualTo(Vector2Int.right));
            Assert.That(GetItem(eggCell),Is.Not.Null);
            var walls=(HashSet<Vector2Int>)typeof(TowerGameController).GetField("_walls",Private).GetValue(_controller);
            for(int y=6;y<=8;y++){walls.Add(new Vector2Int(8,y));_dungeon.Grid[y,8]=CoreDungeon.TileType.Wall;}
            var cell=new Vector2Int(7,7);int moves=0;
            while(cell!=eggCell && moves<20)
            {
                Assert.That(Act(CreatureState(_viper,cell),new Vector2Int(12,12),out var step),Is.True);
                Assert.That(step,Is.Not.EqualTo(Vector2Int.zero));cell+=step;moves++;
                Assert.That(walls.Contains(cell),Is.False);
            }
            Assert.That(cell,Is.EqualTo(eggCell));Assert.That(moves,Is.GreaterThan(3));
            Assert.That(GetItem(eggCell),Is.Not.Null,"Egg taken before pickup action.");
        }

        private DungeonItemEntry GetItem(Vector2Int cell) =>
            (DungeonItemEntry)typeof(TowerItemTrapManager).GetMethod("GetItemAt",Private).Invoke(_items,new object[]{cell});

        private bool Place(Vector2Int cell, DungeonItemEntry egg) =>
            (bool)typeof(TowerItemTrapManager).GetMethod("TryPlaceItem", Private).Invoke(_items, new object[] { cell, egg });

        private bool Act(object state, Vector2Int koh, out Vector2Int step)
        {
            object[] args = { state, koh, null, Vector2Int.zero };
            bool result = (bool)typeof(TowerGameController).GetMethod("TryViperEggAction", Private).Invoke(_controller, args);
            step = (Vector2Int)args[3];
            return result;
        }

        private static object CreatureState(ActorRecord actor, Vector2Int cell)
        {
            Type type = typeof(TowerGameController).GetNestedType("CreatureState", BindingFlags.NonPublic);
            object state = Activator.CreateInstance(type);
            type.GetField("Actor").SetValue(state, actor);
            type.GetField("Cell").SetValue(state, cell);
            return state;
        }

        private static object MakeRoom(Type type, int x, int y, int width, int height)
        {
            object room = Activator.CreateInstance(type);
            SetMember(type, room, "X", x); SetMember(type, room, "Y", y);
            SetMember(type, room, "Width", width); SetMember(type, room, "Height", height);
            return room;
        }

        private static void SetMember(Type type, object target, string name, object value)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null) { field.SetValue(target, value); return; }
            type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
        }

        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, Private).SetValue(target, value);

        private static void SetProperty(object target, string name, object value) =>
            target.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(target, value);
    }
}

