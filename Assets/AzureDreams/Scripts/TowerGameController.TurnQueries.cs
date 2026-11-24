using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    // Allocation-free lookups for the turn hot paths (Update, TickTurnPresentation,
    // CommitTowerTurn). Plain loops replace List.Find/Exists lambdas, which allocate a
    // delegate (and a closure when they capture locals) every call.
    public sealed partial class TowerGameController
    {
        private static readonly WaitForSeconds FlinchRemovalWait = new WaitForSeconds(ActorLocomotionView.HitDuration);
        private readonly System.Collections.Generic.List<Collider> _colliderScratch = new();

        private bool AnyCreatureWithin(Vector2Int cell, int radius)
        {
            for (int i = 0; i < _creatures.Count; i++)
                if (Distance(_creatures[i].Cell, cell) <= radius) return true;
            return false;
        }

        private bool AnyCreatureWithin(Vector2Int first, Vector2Int second, int radius)
        {
            for (int i = 0; i < _creatures.Count; i++)
            {
                var cell = _creatures[i].Cell;
                if (Distance(cell, first) <= radius || Distance(cell, second) <= radius) return true;
            }
            return false;
        }

        private int CreatureIndexOf(ActorRecord actor)
        {
            for (int i = 0; i < _creatures.Count; i++)
                if (_creatures[i].Actor == actor) return i;
            return -1;
        }

        /// <summary>Same result as <c>_creatures.Find(c => c.Actor == actor).Cell</c> (default when absent).</summary>
        private Vector2Int CreatureCellOf(ActorRecord actor)
        {
            int index = CreatureIndexOf(actor);
            return index >= 0 ? _creatures[index].Cell : default;
        }

        private ThrownEggAlly ThrownEggOf(ActorRecord actor)
        {
            for (int i = 0; i < _thrownEggs.Count; i++)
                if (_thrownEggs[i].Actor == actor) return _thrownEggs[i];
            return null;
        }

        private bool AnyThrownEggViewAt(Vector2Int cell)
        {
            for (int i = 0; i < _thrownEggs.Count; i++)
                if (_thrownEggs[i].View && _thrownEggs[i].Cell == cell) return true;
            return false;
        }

        private bool AnyIntentActorAt(Vector2Int cell)
        {
            for (int i = 0; i < _turnIntents.Count; i++)
            {
                var actor = _turnIntents[i].Actor;
                if (actor.TileX == cell.x && actor.TileY == cell.y) return true;
            }
            return false;
        }

        private bool FamiliarActsAtPlayerPriority()
        {
            for (int i = 0; i < _turnIntents.Count; i++)
                if (_turnIntents[i].Actor == _familiarActor && _turnIntents[i].Priority == TurnSystem.PriorityPlayer) return true;
            return false;
        }

        private void DisableColliders(GameObject view)
        {
            view.GetComponentsInChildren(_colliderScratch);
            foreach (var collider in _colliderScratch) collider.enabled = false;
            _colliderScratch.Clear();
        }
    }
}
