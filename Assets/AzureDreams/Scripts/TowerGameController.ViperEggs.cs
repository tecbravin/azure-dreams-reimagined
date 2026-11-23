using System.Collections.Generic;
using AzureDreams.Core;
using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed partial class TowerGameController
    {
        private readonly Queue<Vector2Int> _viperEggSearch = new(DungeonGenerator.GridSize * DungeonGenerator.GridSize);
        private readonly Dictionary<Vector2Int,Vector2Int> _viperEggFirstSteps = new(DungeonGenerator.GridSize * DungeonGenerator.GridSize);

        private bool DropViperCarriedEgg(ActorRecord actor,Vector2Int cell)
        {
            if(!_viperCarriedEgg.TryGetValue(actor,out var egg) || !DropTowerReward(cell,egg))return false;
            _viperCarriedEgg.Remove(actor);
            return true;
        }

        // An egg is a higher priority than Koh while an empty-handed Viper can
        // act normally. Search only its current room, using actual legal steps.
        private bool TryViperEggAction(CreatureState creature, Vector2Int kohDestination,
            Vector2Int? reserved, out Vector2Int step)
        {
            step=Vector2Int.zero;
            if(creature.Actor.Species!=(byte)MonsterSpecies.VIPER || _towerItemTraps==null ||
                _viperCarriedEgg.ContainsKey(creature.Actor))return false;
            int room=BellRoomAt(creature.Cell);
            if(room<0)return false;
            if(TryViperGrabEgg(creature.Actor,creature.Cell))return true;
            var bounds=_towerFloorLayout.Dungeon.Rooms[room];
            if(!_towerItemTraps.HasEggInArea(new RectInt(bounds.X,bounds.Y,bounds.Width,bounds.Height)))return false;
            _viperEggSearch.Clear();_viperEggFirstSteps.Clear();
            _viperEggSearch.Enqueue(creature.Cell);_viperEggFirstSteps.Add(creature.Cell,Vector2Int.zero);
            while(_viperEggSearch.Count>0)
            {
                var cell=_viperEggSearch.Dequeue();var first=_viperEggFirstSteps[cell];
                if(cell!=creature.Cell && _towerItemTraps.GetItemAt(cell)?.Kind==ItemKind.Egg)
                {step=first;return true;}
                foreach(var direction in FormationDirections)
                {
                    var next=cell+direction;
                    if(_viperEggFirstSteps.ContainsKey(next) || BellRoomAt(next)!=room ||
                        !CanTowerStep(cell,direction) || next==kohDestination || next==reserved ||
                        next==_stairCell || IsSacredTile(next) || CreatureAt(next)>=0 ||
                        (_familiar && next==_familiarCell) ||
                        (_blueCollarAlly?.View && _blueCollarAlly.Actor.Hp>0 && next==_blueCollarAlly.Cell))continue;
                    bool occupied=false;
                    foreach(var ally in _thrownEggs)
                        if(ally.View && ally.Actor.Hp>0 && ally.Cell==next){occupied=true;break;}
                    if(occupied)continue;
                    foreach(var intent in _turnIntents)
                        if(intent.Actor!=creature.Actor && intent.Action==AzureDreams.Core.MonsterAI.ActionType.Move &&
                            new Vector2Int(intent.Actor.TileX+intent.DeltaX,intent.Actor.TileY+intent.DeltaY)==next)
                        {occupied=true;break;}
                    if(occupied)continue;
                    _viperEggFirstSteps.Add(next,first==Vector2Int.zero?direction:first);
                    _viperEggSearch.Enqueue(next);
                }
            }
            return false;
        }
    }
}
