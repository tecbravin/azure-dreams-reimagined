using System;
using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Central town/game state machine. Owns the current game state and
    /// validates transitions so that area changes, dialogue, menus and
    /// tower runs cannot interleave illegally.
    /// </summary>
    public sealed class TownGameState
    {
        public enum State
        {
            Boot,
            Title,
            TownIdle,
            TownDialogue,
            TownMenu,
            TownShop,
            TransitionToTower,
            TowerRunning,
            TowerVictory,
            TowerDefeat,
            ReturnToTown,
            Paused
        }

        public State Current { get; private set; } = State.Boot;

        public event Action<State, State> StateChanged;

        public bool CanTransition(State next)
        {
            switch (Current)
            {
                case State.Boot:
                    return next == State.Title || next == State.TownIdle;
                case State.Title:
                    return next == State.TownIdle || next == State.Boot;
                case State.TownIdle:
                    return next == State.TownDialogue || next == State.TownMenu ||
                           next == State.TownShop || next == State.TransitionToTower ||
                           next == State.Paused;
                case State.TownDialogue:
                    return next == State.TownIdle || next == State.TownMenu || next == State.TownShop || next == State.Paused;
                case State.TownMenu:
                    return next == State.TownIdle || next == State.TownShop || next == State.Paused;
                case State.TownShop:
                    return next == State.TownIdle || next == State.TownMenu || next == State.Paused;
                case State.TransitionToTower:
                    return next == State.TowerRunning || next == State.TownIdle;
                case State.TowerRunning:
                    return next == State.TowerVictory || next == State.TowerDefeat ||
                           next == State.ReturnToTown || next == State.Paused;
                case State.TowerVictory:
                    return next == State.ReturnToTown;
                case State.TowerDefeat:
                    return next == State.ReturnToTown;
                case State.ReturnToTown:
                    return next == State.TownIdle;
                case State.Paused:
                    return next == State.TownIdle || next == State.TowerRunning;
                default:
                    return false;
            }
        }

        public bool TrySet(State next)
        {
            if (!CanTransition(next)) return false;
            State previous = Current;
            Current = next;
            StateChanged?.Invoke(previous, Current);
            return true;
        }

        public void ForceSet(State next)
        {
            State previous = Current;
            Current = next;
            StateChanged?.Invoke(previous, Current);
        }
    }
}
