using System.Collections.Generic;
using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// NPC behavior: idle animation, dialogue triggering, and pathing.
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    [RequireComponent(typeof(TownMovementController))]
    [DisallowMultipleComponent]
    public class NPCController : MonoBehaviour, Interactable
    {
        [Header("Dialogue")]
        [Tooltip("Dialogue lines displayed in order on interaction.")]
        public string npcId;
        public NPCDialogue dialogue;
        public string[] dialogueLines = new string[0];

        [Tooltip("Should this NPC repeat the last line or loop through all?")]
        public bool loopDialogue = true;

        [Header("Patrol")]
        [Tooltip("Optional waypoints for NPC patrol behavior.")]
        public Transform[] patrolWaypoints;

        [Tooltip("Wait time at each waypoint (seconds).")]
        public float waypointWaitTime = 2f;

        [Tooltip("Patrol speed when moving between waypoints.")]
        public float patrolSpeed = 1.5f;

        [SerializeField, Tooltip("Local width and depth half extents for town collision. Leave at zero for the default footprint.")]
        private Vector2 _townCollisionHalfExtents;

        private CharacterStats _stats;
        private int _currentDialogueIndex;
        private int _currentWaypointIndex = -1;
        private float _waitTimer;
        private TownRoute _townRoute;
        private TownRoute _path;
        private int _pathIndex;
        private Vector2Int _pathDestination;
        private int _mapSize = 64;
        private readonly HashSet<Vector2Int> _navigationWalls = new HashSet<Vector2Int>();
        private TownMovementController _movement;
        private ActorLocomotionView _view;
        private PsxSpriteRenderer _sprite;
        private float _walkCycle;
        private bool _walking;
        private TownGameState _conversationState;
        private bool _conversationLastLine;
        public bool IsTalking { get; private set; }

        internal bool BlocksTownMovement(Vector3 position, float radius)
        {
            var offset = position - transform.position;
            if (_townCollisionHalfExtents.x <= 0f || _townCollisionHalfExtents.y <= 0f)
                return Mathf.Abs(offset.x) < 0.5f + radius && Mathf.Abs(offset.z) < 0.5f + radius;

            offset.y = 0f;
            var local = transform.InverseTransformDirection(offset);
            float x = Mathf.Max(0f, Mathf.Abs(local.x) - _townCollisionHalfExtents.x);
            float z = Mathf.Max(0f, Mathf.Abs(local.z) - _townCollisionHalfExtents.y);
            return x * x + z * z < radius * radius;
        }

        public void ConfigureConversation(TownGameState state)
        {
            CancelDialogue();
            _conversationState = state;
        }

        public void CancelDialogue()
        {
            if (IsTalking && _conversationState != null && _conversationState.Current == TownGameState.State.TownDialogue)
                _conversationState.TrySet(TownGameState.State.TownIdle);
            IsTalking = false;
            _conversationLastLine = false;
            if (DialogueManager.Instance) DialogueManager.Instance.Hide();
        }

        private void OnDisable() { CancelDialogue(); PresentPatrol(Vector3.zero); }

        private void Awake()
        {
            CacheDependencies();
        }

        // Scene wiring can configure an inactive prefab before Unity calls Awake.
        // Also repair legacy prefabs created before the mover became required.
        private void CacheDependencies()
        {
            if (!_stats) _stats = GetComponent<CharacterStats>();
            if (!_stats) _stats = gameObject.AddComponent<CharacterStats>();
            if (!_movement) _movement = GetComponent<TownMovementController>();
            if (!_movement) _movement = gameObject.AddComponent<TownMovementController>();
            if (!_view) _view = GetComponent<ActorLocomotionView>();
            if (!_sprite) _sprite = GetComponentInChildren<PsxSpriteRenderer>();
        }

        private void Update()
        {
            TickPatrol(Time.deltaTime);
        }

        public void ConfigurePatrol(TownRoute route, HashSet<Vector2Int> walls, int mapSize, IEnumerable<Rect> buildings = null)
        {
            CacheDependencies();
            _townRoute = route;
            _mapSize = Mathf.Max(3, mapSize);
            _navigationWalls.Clear();
            if (walls != null) _navigationWalls.UnionWith(walls);
            var footprints = buildings != null ? new List<Rect>(buildings) : new List<Rect>();
            // A* consumes cell occupancy; rasterize authored footprints for
            // navigation only. Actual movement/collision stays in the shared mover.
            foreach (var footprint in footprints)
                for (int z = 1; z < _mapSize - 1; z++)
                    for (int x = 1; x < _mapSize - 1; x++)
                        if (footprint.Contains(new Vector2(x, z))) _navigationWalls.Add(new Vector2Int(x, z));
            _movement.SetWalls(_navigationWalls);
            _movement.SetBuildings(footprints);
            _path = null;
            _currentWaypointIndex = -1;
            if (!_view) _view = GetComponent<ActorLocomotionView>();
        }

        public void TickPatrol(float deltaTime)
        {
            int count = _townRoute != null && _townRoute.Length > 0 ? _townRoute.Length : (patrolWaypoints?.Length ?? 0);
            if (_stats == null || !_stats.isActive || count == 0 ||
                (_conversationState != null && _conversationState.Current != TownGameState.State.TownIdle))
            { PresentPatrol(Vector3.zero); return; }
            if (_currentWaypointIndex < 0 || _currentWaypointIndex >= count)
            {
                _currentWaypointIndex = 0;
                _waitTimer = waypointWaitTime;
                PresentPatrol(Vector3.zero);
                return;
            }
            if (deltaTime <= 0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
            Vector2Int target;
            if (_townRoute != null && _townRoute.Length > 0) target = _townRoute.Waypoints[_currentWaypointIndex];
            else
            {
                var waypoint = patrolWaypoints[_currentWaypointIndex];
                if (!waypoint) { _currentWaypointIndex = (_currentWaypointIndex + 1) % count; return; }
                target = new Vector2Int(Mathf.RoundToInt(waypoint.position.x), Mathf.RoundToInt(waypoint.position.z));
            }
            Vector3 destination = new Vector3(target.x, transform.position.y, target.y);
            if (Vector3.Distance(transform.position, destination) < 0.05f)
            {
                PresentPatrol(Vector3.zero);
                _waitTimer -= deltaTime;
                if (_waitTimer <= 0f)
                {
                    _currentWaypointIndex = (_currentWaypointIndex + 1) % count;
                    _waitTimer = waypointWaitTime;
                    _path = null;
                }
                return;
            }
            if (target.x < 1 || target.y < 1 || target.x > _mapSize - 2 || target.y > _mapSize - 2)
            { PresentPatrol(Vector3.zero); return; }
            if (_path == null || _pathDestination != target)
            {
                _pathDestination = target;
                var from = new Vector2Int(Mathf.RoundToInt(transform.position.x), Mathf.RoundToInt(transform.position.z));
                _path = TownPathfinder.FindPath(_navigationWalls, from, target, _mapSize);
                _pathIndex = _path.Length > 1 ? 1 : 0;
            }
            if (_path.Length == 0) { PresentPatrol(Vector3.zero); return; }
            var cell = _path.Waypoints[_pathIndex];
            var next = new Vector3(cell.x, transform.position.y, cell.y);
            Vector3 delta = next - transform.position;
            _movement.Speed = patrolSpeed;
            Vector3 before = transform.position;
            transform.position = _movement.Move(before, new Vector2(delta.x, delta.z), Mathf.Min(deltaTime, delta.magnitude / _movement.Speed), _mapSize);
            PresentPatrol(transform.position - before);
            if (Vector3.Distance(transform.position, next) < 0.01f && _pathIndex < _path.Length - 1) _pathIndex++;
        }

        private void PresentPatrol(Vector3 delta)
        {
            bool moving = delta.sqrMagnitude > 0.000001f;
            _walkCycle += delta.magnitude;
            if (_view) _view.Present(delta, _walkCycle, moving);
            else if (moving) transform.rotation = Quaternion.LookRotation(delta);
            if (_sprite && moving != _walking) { if (moving) _sprite.Play(); else _sprite.Stop(); }
            _walking = moving;
        }

        /// <summary>
        /// Triggered by the player when pressing the interact button.
        /// </summary>
        public void Interact(PlayerController player)
        {
            if (player)
            {
                Vector3 facing = player.transform.position - transform.position;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(facing);
            }
            Talk();
        }

        public string Talk()
        {
            if (!isActiveAndEnabled || _stats == null || !_stats.isActive) return string.Empty;
            if (!IsTalking && (_conversationState != null || (!dialogue && (dialogueLines == null || dialogueLines.Length == 0))) &&
                TryGetComponent<TownDialogueProgress>(out var progress))
                progress.Prepare(this);
            string[] lines = dialogue ? dialogue.lines : dialogueLines;
            if (lines == null || lines.Length == 0) return string.Empty;
            if (_conversationState != null)
            {
                if (!IsTalking)
                {
                    if (!_conversationState.TrySet(TownGameState.State.TownDialogue)) return string.Empty;
                    IsTalking = true;
                    _currentDialogueIndex = 0;
                }
                else if (_conversationState.Current != TownGameState.State.TownDialogue)
                { CancelDialogue(); return string.Empty; }
                else if (_conversationLastLine)
                {
                    CancelDialogue();
                    // Closing contains no spoken line, but publish a final event so
                    // observers can distinguish "conversation finished" from "line spoken".
                    string closingId = !string.IsNullOrEmpty(npcId) ? npcId : _stats.characterId;
                    TownEventBus.Publish(TownEventBus.EventId.NpcTalked,
                        new NPCDialogue.TalkEvent(closingId, string.Empty, _currentDialogueIndex, true, gameObject));
                    return string.Empty;
                }
                PresentPatrol(Vector3.zero);
            }
            _currentDialogueIndex = Mathf.Clamp(_currentDialogueIndex, 0, lines.Length - 1);
            int index = _currentDialogueIndex;
            _conversationLastLine = index == lines.Length - 1;
            string line = lines[index] ?? string.Empty;
            bool loop = dialogue ? dialogue.loop : loopDialogue;
            _currentDialogueIndex = loop ? (index + 1) % lines.Length : Mathf.Min(index + 1, lines.Length - 1);
            string id = !string.IsNullOrEmpty(npcId) ? npcId : _stats.characterId;
            if (!string.IsNullOrEmpty(id))
            {
                TownSaveVariables.SetBool("npc_" + id, true);
                TownSaveVariables.Save();
            }
            if (DialogueManager.Instance) DialogueManager.Instance.ShowLine(_stats.displayName, line);
            TownEventBus.Publish(TownEventBus.EventId.NpcTalked, new NPCDialogue.TalkEvent(id, line, index, false, gameObject));
            return line;
        }

        private void OnDrawGizmosSelected()
        {
            if (patrolWaypoints == null) return;
            Gizmos.color = Color.cyan;
            for (int i = 0; i < patrolWaypoints.Length; i++)
            {
                if (patrolWaypoints[i] != null)
                {
                    Gizmos.DrawSphere(patrolWaypoints[i].position, 0.25f);
                    if (i > 0 && patrolWaypoints[i - 1] != null)
                    {
                        Gizmos.DrawLine(patrolWaypoints[i - 1].position, patrolWaypoints[i].position);
                    }
                }
            }
        }
    }
}
