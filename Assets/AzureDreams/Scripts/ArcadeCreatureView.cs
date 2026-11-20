using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AzureDreamsReimagined
{
    // Reconstructed models based on the recovered monster-book appearances.
    // The native arcade creature animation banks have not yet been resolved.
    public sealed class ArcadeCreatureView : MonoBehaviour
    {
        private readonly List<Material> _materials = new();
        private readonly List<Transform> _limbs = new();
        private readonly List<Color> _colors = new();
        private Transform _body;
        private bool _takopoo;
        private float _darkness;

        internal void Configure(bool takopoo)
        {
            if (_body) return;
            _takopoo = takopoo;
            var bodyColor = takopoo ? new Color(.38f, .31f, .48f) : new Color(.35f, .45f, .20f);
            _body = Part("Body", PrimitiveType.Sphere, new(0, .45f, 0), new(.86f, .80f, .80f), bodyColor);
            if (takopoo)
            {
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    var limb = Part("Tentacle", PrimitiveType.Capsule,
                        new(Mathf.Sin(a) * .33f, .16f, Mathf.Cos(a) * .33f), new(.16f, .25f, .16f), bodyColor);
                    limb.localRotation = Quaternion.Euler(Mathf.Cos(a) * 45, 0, -Mathf.Sin(a) * 45);
                    _limbs.Add(limb);
                }
                Part("Ink nozzle", PrimitiveType.Cylinder, new(0, .38f, .46f), new(.20f, .13f, .20f), bodyColor)
                    .localRotation = Quaternion.Euler(90, 0, 0);
                Part("Nozzle opening", PrimitiveType.Sphere, new(0, .38f, .59f), new(.14f, .14f, .025f), Color.black);
                for (int i = 0; i < 5; i++)
                    Part("Spot", PrimitiveType.Sphere, new(Mathf.Sin(i * 2.4f) * .28f, .68f + i % 2 * .08f, Mathf.Cos(i * 2.4f) * .25f), Vector3.one * .095f, new(.8f, .78f, .67f));
            }
            else
            {
                Part("Head", PrimitiveType.Sphere, new(0, .84f, .08f), new(.78f, .46f, .66f), new(.52f, .47f, .28f));
                for (int side = -1; side <= 1; side += 2)
                {
                    Part("Arm", PrimitiveType.Capsule, new(side * .43f, .40f, .10f), new(.24f, .32f, .24f), bodyColor)
                        .localRotation = Quaternion.Euler(0, 0, side * 25);
                    for (int claw = 0; claw < 3; claw++)
                        Part("Claw", PrimitiveType.Capsule, new(side * .48f + (claw - 1) * .09f, .25f, .30f), new(.075f, .18f, .075f), new(.88f, .84f, .69f))
                            .localRotation = Quaternion.Euler(45, 0, side * -12);
                }
            }
            for (int side = -1; side <= 1; side += 2)
            {
                Part("Eye", PrimitiveType.Sphere, new(side * .21f, .65f, .35f), new(.14f, .17f, .07f), Color.black);
                Part("Eye highlight", PrimitiveType.Sphere, new(side * .21f - .02f, .69f, .39f), Vector3.one * .04f, Color.white);
            }
        }

        private Transform Part(string label, PrimitiveType shape, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(shape);
            part.name = label;
            part.transform.SetParent(transform, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            var collider = part.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            var material = new Material(AssetMode.Load<Shader>("Town/Arcade/Creature")) { color = color };
            _materials.Add(material);
            _colors.Add(color);
            var renderer = part.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return part.transform;
        }

        internal void SetInk(int returns)
        {
            _darkness = Mathf.Clamp01(returns / 10f);
            for (int i = 0; i < _materials.Count; i++)
                _materials[i].color = Color.Lerp(_colors[i], new Color(.035f, .025f, .045f), _darkness);
        }

        private void LateUpdate()
        {
            if (!_body) return;
            _body.localScale = new Vector3(.86f, .80f + Mathf.Sin(Time.unscaledTime * 4) * .025f, .80f);
            if (!_takopoo) return;
            for (int i = 0; i < _limbs.Count; i++)
            {
                float angle = i * Mathf.PI / 4;
                _limbs[i].localRotation = Quaternion.Euler(Mathf.Cos(angle) * 45 + Mathf.Sin(Time.unscaledTime * 5 + i) * 10, 0, -Mathf.Sin(angle) * 45);
            }
        }

        private void OnDestroy()
        {
            foreach (var material in _materials) if (material) Destroy(material);
        }
    }
}
