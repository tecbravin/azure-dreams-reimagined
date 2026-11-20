using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined
{
    [DisallowMultipleComponent]
    public sealed class TownHumanoidView : MonoBehaviour
    {
        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private Texture2D _texture;
        [SerializeField] private float _standingHeight = 1.65f;
        private Transform _visual;
        private Transform[] _bones;
        private Mesh _skin;
        private Material _material;
        private float _cycle;
        private bool _moving;
        private bool _seated;
        private Quaternion _seatedRotation;
        private float _seatHeight;
        private float _nativeHeight;
        private float _scale;
        private const int Root = 0, Head = 1, LeftArm = 2, RightArm = 3;
        private const int LeftThigh = 4, RightThigh = 5, LeftShin = 6, RightShin = 7;

        private void Awake() => BuildModel();

        public void Configure(Mesh mesh, Texture2D texture, float height)
        {
            _sourceMesh = mesh;
            _texture = texture;
            _standingHeight = height;
            BuildModel();
        }

        private void BuildModel()
        {
            if (_visual || !_sourceMesh || !_texture) return;
            if (!_sourceMesh.isReadable)
            {
                Debug.LogError($"NPC mesh must be readable to attach its limbs: {_sourceMesh.name}.", this);
                return;
            }
            _nativeHeight = _sourceMesh.bounds.size.y;
            _scale = _standingHeight / Mathf.Max(0.01f, _nativeHeight);
            _visual = new GameObject("Animated town character").transform;
            _visual.SetParent(transform, false);
            _visual.localScale = Vector3.one * _scale;
            float h = _nativeHeight;
            _bones = new Transform[8];
            _bones[Root] = Bone("Body", _visual, Vector3.zero);
            _bones[Head] = Bone("Head", _bones[Root], new Vector3(0f, h * 0.70f, 0f));
            _bones[LeftArm] = Bone("Left arm", _bones[Root], new Vector3(-h * 0.14f, h * 0.61f, 0f));
            _bones[RightArm] = Bone("Right arm", _bones[Root], new Vector3(h * 0.14f, h * 0.61f, 0f));
            _bones[LeftThigh] = Bone("Left thigh", _bones[Root], new Vector3(-h * 0.065f, h * 0.39f, 0f));
            _bones[RightThigh] = Bone("Right thigh", _bones[Root], new Vector3(h * 0.065f, h * 0.39f, 0f));
            _bones[LeftShin] = Bone("Left shin", _bones[LeftThigh], new Vector3(0f, -h * 0.19f, 0f));
            _bones[RightShin] = Bone("Right shin", _bones[RightThigh], new Vector3(0f, -h * 0.19f, 0f));
            _skin = Instantiate(_sourceMesh);
            _skin.name = _sourceMesh.name + " articulated";
            _skin.boneWeights = LimbWeights(_sourceMesh);
            var bindPoses = new Matrix4x4[_bones.Length];
            for (int i = 0; i < _bones.Length; i++)
                bindPoses[i] = _bones[i].worldToLocalMatrix * _visual.localToWorldMatrix;
            _skin.bindposes = bindPoses;
            var renderer = _visual.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = _skin;
            renderer.bones = _bones;
            renderer.rootBone = _bones[Root];
            renderer.updateWhenOffscreen = true;
            renderer.localBounds = new Bounds(new Vector3(0f, h * 0.5f, 0f), Vector3.one * h * 2f);
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var shader = Shader.Find("AzureDreams/TownCaptured") ?? Shader.Find("Unlit/Texture");
            _material = new Material(shader) { mainTexture = _texture };
            renderer.sharedMaterial = _material;
        }

        private static Transform Bone(string name, Transform parent, Vector3 position)
        {
            var bone = new GameObject(name).transform;
            bone.SetParent(parent, false);
            bone.localPosition = position;
            return bone;
        }

        // OBJ parts repeat their seam vertices. Connect by position before assigning limbs.
        private BoneWeight[] LimbWeights(Mesh mesh)
        {
            var vertices = mesh.vertices;
            var triangles = mesh.triangles;
            var parents = new int[vertices.Length];
            var positionIndex = new Dictionary<Vector3, int>();
            for (int i = 0; i < parents.Length; i++)
            {
                parents[i] = i;
                if (positionIndex.TryGetValue(vertices[i], out int prior)) parents[i] = prior;
                else positionIndex.Add(vertices[i], i);
            }
            int Find(int vertex)
            {
                while (parents[vertex] != vertex)
                {
                    parents[vertex] = parents[parents[vertex]];
                    vertex = parents[vertex];
                }
                return vertex;
            }
            for (int i = 0; i < triangles.Length; i += 3)
            {
                int root = Find(triangles[i]);
                parents[Find(triangles[i + 1])] = root;
                parents[Find(triangles[i + 2])] = root;
            }
            var bounds = new Dictionary<int, Bounds>();
            for (int i = 0; i < vertices.Length; i++)
            {
                int group = Find(i);
                if (!bounds.TryGetValue(group, out var box)) box = new Bounds(vertices[i], Vector3.zero);
                box.Encapsulate(vertices[i]);
                bounds[group] = box;
            }
            var weights = new BoneWeight[vertices.Length];
            float h = _nativeHeight;
            for (int i = 0; i < vertices.Length; i++)
            {
                var box = bounds[Find(i)];
                bool left = box.center.x < 0f;
                if (box.max.y < h * 0.45f)
                {
                    float lower = 1f - Mathf.InverseLerp(h * 0.16f, h * 0.28f, vertices[i].y);
                    weights[i] = new BoneWeight
                    {
                        boneIndex0 = left ? LeftThigh : RightThigh, weight0 = 1f - lower,
                        boneIndex1 = left ? LeftShin : RightShin, weight1 = lower
                    };
                    continue;
                }
                int bone = box.min.y > h * 0.60f ? Head :
                    Mathf.Abs(box.center.x) > h * 0.12f ? (left ? LeftArm : RightArm) : Root;
                weights[i] = new BoneWeight { boneIndex0 = bone, weight0 = 1f };
            }
            return weights;
        }

        public void Present(Vector3 direction, float cycle, bool moving)
        {
            _cycle = cycle;
            _moving = moving && !_seated;
            direction.y = 0f;
            if (_seated)
            {
                transform.localRotation = _seatedRotation;
                return;
            }
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction);
        }

        public void SetSeated(float seatHeight)
        {
            _seated = true;
            _seatedRotation = transform.localRotation;
            _seatHeight = seatHeight;
        }

        private void LateUpdate()
        {
            if (_bones == null) return;
            if (_seated) transform.localRotation = _seatedRotation;
            float idle = Mathf.Sin(Time.time * 1.9f + transform.position.x);
            float stride = _moving ? Mathf.Sin(_cycle * Mathf.PI * 2f) : 0f;
            _bones[Head].localRotation = Quaternion.Euler(idle * 1.5f, idle * 3f, 0f);
            _bones[LeftArm].localRotation = Quaternion.Euler(-stride * 25f - (_seated ? 20f : 0f), 0f, -2f + idle);
            _bones[RightArm].localRotation = Quaternion.Euler(stride * 25f - (_seated ? 20f : 0f), 0f, 2f - idle);
            _bones[LeftThigh].localRotation = Quaternion.Euler(_seated ? -80f : stride * 27f, 0f, 0f);
            _bones[RightThigh].localRotation = Quaternion.Euler(_seated ? -80f : -stride * 27f, 0f, 0f);
            _bones[LeftShin].localRotation = Quaternion.Euler(_seated ? 88f : Mathf.Max(0f, -stride) * 22f, 0f, 0f);
            _bones[RightShin].localRotation = Quaternion.Euler(_seated ? 88f : Mathf.Max(0f, stride) * 22f, 0f, 0f);
            float y = _seated ? _seatHeight - _nativeHeight * 0.39f * _scale : 0f;
            _visual.localPosition = Vector3.up * (y + idle * 0.006f);
        }

        private void OnDestroy()
        {
            if (_skin) Destroy(_skin);
            if (_material) Destroy(_material);
        }
    }
}
