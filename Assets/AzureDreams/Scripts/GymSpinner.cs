using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>Constant spin for the Gym's decorative meshes (rotate_gym_mesh_x/y).</summary>
    [DisallowMultipleComponent]
    public sealed class GymSpinner : MonoBehaviour
    {
        [SerializeField] private Vector3 _axis = Vector3.up;
        [SerializeField] private float _degreesPerSecond = 84f;

        internal void Configure(Vector3 axis, float degreesPerSecond)
        {
            _axis = axis;
            _degreesPerSecond = degreesPerSecond;
        }

        private void Update() => transform.Rotate(_axis, _degreesPerSecond * Time.deltaTime, Space.Self);
    }
}
