using UnityEngine;
using AzureDreams.Core;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Monster AI: state machine for idle, chase, attack, and flee.
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    [DisallowMultipleComponent]
    public class MonsterAI : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Vision cone angle in degrees.")]
        public float visionAngle = 90f;

        [Tooltip("Maximum detection distance.")]
        public float visionDistance = 12f;

        [Tooltip("Layer mask for the player.")]
        public LayerMask playerLayer = Physics.DefaultRaycastLayers;

        [Header("Combat")]
        [Tooltip("Damage dealt per attack.")]
        public int attackDamage = 8;

        [Tooltip("Seconds between attacks.")]
        public float attackCooldown = 1.5f;

        [Tooltip("Attack range.")]
        public float attackRange = 1.8f;

        [Tooltip("Speed while chasing.")]
        public float chaseSpeed = 4f;

        [Tooltip("Speed while patrolling.")]
        public float patrolSpeed = 1.2f;

        [Tooltip("Patrol area center (optional).")]
        public Vector3 patrolCenter;

        [Tooltip("Patrol radius.")]
        public float patrolRadius = 5f;

        [Header("Turn-Based")]
        [Tooltip("When enabled, real-time movement is disabled and an external controller drives turns.")]
        public bool turnBased = false;

        private CharacterStats _stats;
        private Transform _player;
        private float _attackTimer;
        private MonsterState _state = MonsterState.Patrol;
        private Vector3 _patrolTarget;

        public enum MonsterState
        {
            Patrol,
            Chase,
            Attack,
            Flee
        }

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _player = GameObject.FindGameObjectWithTag("Player")?.transform;
            PickNewPatrolTarget();
        }

        private void Update()
        {
            if (turnBased)
                return;

            if (_stats == null || !_stats.isActive) return;

            // Flee when health is critically low.
            if (_state != MonsterState.Flee && _stats.currentHealth <= _stats.maxHealth * 0.25f && _stats.maxHealth > 0)
            {
                _state = MonsterState.Flee;
            }

            switch (_state)
            {
                case MonsterState.Patrol:
                    UpdatePatrol();
                    CheckForPlayer();
                    break;
                case MonsterState.Chase:
                    UpdateChase();
                    break;
                case MonsterState.Attack:
                    UpdateAttack();
                    break;
                case MonsterState.Flee:
                    UpdateFlee();
                    break;
            }
        }

        private void UpdatePatrol()
        {
            if (patrolRadius <= 0f) return;

            float dist = Vector3.Distance(transform.position, _patrolTarget);
            if (dist < 0.2f)
            {
                PickNewPatrolTarget();
                return;
            }

            Vector3 dir = (_patrolTarget - transform.position).normalized;
            transform.Translate(dir * patrolSpeed * Time.deltaTime, Space.World);
            transform.LookAt(_patrolTarget);
        }

        private void PickNewPatrolTarget()
        {
            Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
            _patrolTarget = patrolCenter + new Vector3(randomCircle.x, 0f, randomCircle.y);
        }

        private void CheckForPlayer()
        {
            if (_player == null) return;

            Vector3 toPlayer = _player.position - transform.position;
            float dist = toPlayer.magnitude;

            if (dist > visionDistance) return;

            float angle = Vector3.Angle(toPlayer.normalized, transform.forward);
            if (angle > visionAngle * 0.5f) return;

            // Optional line-of-sight check
            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, toPlayer.normalized, dist, ~playerLayer))
            {
                return;
            }

            _state = MonsterState.Chase;
        }

        private void UpdateChase()
        {
            if (_player == null)
            {
                _state = MonsterState.Patrol;
                return;
            }

            float dist = Vector3.Distance(transform.position, _player.position);
            if (dist > visionDistance * 1.5f)
            {
                _state = MonsterState.Patrol;
                return;
            }

            Vector3 dir = (_player.position - transform.position).normalized;
            dir.y = 0f;
            transform.Translate(dir * chaseSpeed * Time.deltaTime, Space.World);
            transform.LookAt(_player.position);

            if (dist <= attackRange)
            {
                _state = MonsterState.Attack;
                _attackTimer = 0f;
            }
        }

        private void UpdateAttack()
        {
            // Find the nearest target: either the player or a Familiar (for kidnap)
            Transform target = _player;
            float bestDist = float.MaxValue;

            // Check for familiars in range for kidnap
            if (_stats != null && _stats.isActive)
            {
                var familiars = GameObject.FindGameObjectsWithTag("Familiar");
                foreach (var familiar in familiars)
                {
                    float familiarDist = Vector3.Distance(transform.position, familiar.transform.position);
                    if (familiarDist <= attackRange * 1.5f && familiarDist < bestDist)
                    {
                        bestDist = familiarDist;
                        target = familiar.transform;
                    }
                }
            }

            // If no familiar found nearby, target the player
            if (target == null)
            {
                target = _player;
            }

            float dist = Vector3.Distance(transform.position, target.position);
            if (dist > attackRange * 1.2f)
            {
                _state = MonsterState.Chase;
                return;
            }

            transform.LookAt(target.position);

            _attackTimer -= Time.deltaTime;
            if (_attackTimer <= 0f)
            {
                _attackTimer = attackCooldown;
                if (target.TryGetComponent(out CharacterStats targetStats))
                {
                    // Check if the target is a Familiar
                    var definition = target.GetComponent<ActorDefinition>();
                    bool isFamiliar = definition != null && definition.Kind == ActorKind.Familiar;

                    if (isFamiliar)
                    {
                        // Use "kidnap" move on familiars
                        ApplyKidnap(targetStats);
                    }
                    else
                    {
                        // Normal damage for non-familiar targets (player, other monsters)
                        targetStats.TakeDamage(attackDamage);
                    }
                }
            }
        }

        private void ApplyKidnap(CharacterStats targetStats)
        {
            // Apply "kidnap" status to the familiar
            // This reduces their control/movement for a few turns
            if (_stats != null && _stats.isActive)
            {
                var definition = targetStats.GetComponent<ActorDefinition>();
                if (definition != null && definition.Kind == ActorKind.Familiar)
                {
                    Debug.Log($"Balloon kidnapped {targetStats.displayName}");
                    // In full implementation, this would:
                    // - Reduce the familiar's MoveSpeed
                    // - Prevent them from obeying commands
                    // - Possibly carry them away to a "kidnapped" location
                }
            }
        }

        private void UpdateFlee()
        {
            if (_player == null)
            {
                _state = MonsterState.Patrol;
                PickNewPatrolTarget();
                return;
            }

            // Recover: if health restored above threshold, resume patrol.
            if (_stats.currentHealth > _stats.maxHealth * 0.5f)
            {
                _state = MonsterState.Patrol;
                PickNewPatrolTarget();
                return;
            }

            Vector3 away = (transform.position - _player.position);
            away.y = 0f;
            if (away.sqrMagnitude < 0.0001f)
            {
                away = transform.forward;
            }
            away.Normalize();
            Vector3 target = transform.position + away * patrolSpeed * Time.deltaTime * 2f;
            transform.Translate(away * patrolSpeed * Time.deltaTime * 2f, Space.World);
            transform.LookAt(transform.position + away);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, visionDistance);

            Vector3 forward = transform.forward;
            Vector3 left = Quaternion.Euler(0f, -visionAngle * 0.5f, 0f) * forward;
            Vector3 right = Quaternion.Euler(0f, visionAngle * 0.5f, 0f) * forward;
            Gizmos.DrawLine(transform.position, transform.position + left * visionDistance);
            Gizmos.DrawLine(transform.position, transform.position + right * visionDistance);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
