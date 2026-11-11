using UnityEngine;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    public sealed class WeedyIdleAnimation : MonoBehaviour
    {
        private const float HopPeriod = 0.64f;
        private const float HopHeight = 0.18f;
        private const float GroundedFraction = 0.19f;

        [SerializeField] private MeshFilter _firstArm;
        [SerializeField] private MeshFilter _secondArm;

        private Vector3 _restPosition;
        private Vector3 _firstShoulder;
        private Vector3 _secondShoulder;
        private float _elapsed;

        private void Awake()
        {
            _restPosition = transform.localPosition;
            _firstShoulder = ShoulderPivot(_firstArm);
            _secondShoulder = ShoulderPivot(_secondArm);
        }

        private void LateUpdate()
        {
            _elapsed = (_elapsed + Time.deltaTime) % HopPeriod;
            float flight = Mathf.InverseLerp(GroundedFraction * HopPeriod, HopPeriod, _elapsed);
            float lift = 4f * flight * (1f - flight);
            transform.localPosition = _restPosition + Vector3.up * (HopHeight * lift);

            float flutter = flight * (4f * Mathf.PI);
            PoseArm(_firstArm, _firstShoulder, lift, Mathf.Sin(flutter));
            PoseArm(_secondArm, _secondShoulder, lift, Mathf.Sin(flutter + 0.8f * Mathf.PI));
        }

        private static Vector3 ShoulderPivot(MeshFilter arm)
        {
            if (!arm || !arm.sharedMesh) return Vector3.zero;
            float side = Mathf.Sign(arm.sharedMesh.bounds.center.x);
            return new Vector3(side * 0.094f, 0.515f, -0.003f);
        }

        private static void PoseArm(MeshFilter arm, Vector3 shoulder, float lift, float flutter)
        {
            if (!arm || shoulder == Vector3.zero) return;
            float roll = Mathf.Sign(shoulder.x) * lift * (18f + 42f * flutter);
            var rotation = Quaternion.Euler(lift * 13f * flutter, 0f, roll);
            // The arm mesh uses the body origin, so rotate it around the shoulder.
            arm.transform.SetLocalPositionAndRotation(shoulder - rotation * shoulder, rotation);
        }

        private void OnDisable()
        {
            _elapsed = 0f;
            transform.localPosition = _restPosition;
            PoseArm(_firstArm, _firstShoulder, 0f, 0f);
            PoseArm(_secondArm, _secondShoulder, 0f, 0f);
        }
    }
}
