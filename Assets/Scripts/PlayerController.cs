using UnityEngine;
using AzureDreamsReimagined.Rendering;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Player-specific controller: handles input, movement, and interaction.
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    [DisallowMultipleComponent]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [Tooltip("Horizontal input axis name (default: Horizontal).")]
        public string horizontalAxis = "Horizontal";

        [Tooltip("Vertical input axis name (default: Vertical).")]
        public string verticalAxis = "Vertical";

        [Tooltip("Interaction button (default: Fire1).")]
        public string interactButton = "Fire1";

        [Header("Interaction")]
        [Tooltip("Maximum distance for interaction.")]
        public float interactRange = 2.5f;

        [Tooltip("Layer mask for interactable objects.")]
        public LayerMask interactableLayer = Physics.DefaultRaycastLayers;

        [Header("Turn-Based")]
        [Tooltip("When enabled, real-time movement is disabled and an external controller drives turns.")]
        public bool turnBased = false;

        [Header("Animation")]
        [Tooltip("Optional PsxSpriteRenderer for 2D sprite animation.")]
        public PsxSpriteRenderer spriteRenderer;

        private CharacterStats _stats;
        private Animator _animator;
        private Vector3 _moveInput;
        private bool _wasMoving;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _animator = GetComponent<Animator>();
        }

        private void Update()
        {
            if (turnBased)
                return;

            if (_stats == null || !_stats.isActive) return;

            ReadInput();
            Move(_moveInput);
            bool isMoving = _moveInput.sqrMagnitude > 0.01f;
            if (spriteRenderer != null)
            {
                if (isMoving && !_wasMoving)
                    spriteRenderer.Play();
                else if (!isMoving && _wasMoving)
                    spriteRenderer.Stop();
            }
            _wasMoving = isMoving;

            if (UnityEngine.InputSystem.Keyboard.current != null &&
                UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
            {
                TryInteract();
            }
        }

        private void ReadInput()
        {
            // Use the new Input System instead of the legacy UnityEngine.Input
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;

            float h = 0f, v = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) h -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) h += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) v -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) v += 1f;
            _moveInput = new Vector3(h, 0f, v);
        }

        private void Move(Vector3 input)
        {
            if (input.sqrMagnitude < 0.01f) return;

            Vector3 direction = input.normalized;
            if (direction.magnitude > 1f) direction.Normalize();

            // Rotate towards movement direction
            Quaternion targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, targetRotation, _stats.rotationSpeed * Time.deltaTime);

            // Translate
            transform.Translate(direction * _stats.moveSpeed * Time.deltaTime, Space.World);

            // Update animator
            if (_animator != null)
            {
                _animator.SetFloat("Speed", direction.magnitude);
            }
        }

        private void TryInteract()
        {
            Collider[] hits = Physics.OverlapSphere(
                transform.position, interactRange, interactableLayer);

            float closestDist = interactRange;
            Interactable closest = null;

            foreach (var hit in hits)
            {
                if (hit.TryGetComponent(out Interactable interactable))
                {
                    float dist = Vector3.Distance(transform.position, hit.transform.position);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        closest = interactable;
                    }
                }
            }

            if (closest != null)
            {
                closest.Interact(this);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, interactRange);
        }
    }
}
