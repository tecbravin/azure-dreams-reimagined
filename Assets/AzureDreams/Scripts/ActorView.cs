using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed class ActorView : MonoBehaviour
    {
        [SerializeField] private ActorDefinition _definition;
        [SerializeField] private Renderer _bodyRenderer;
        private MaterialPropertyBlock _properties;
        public ActorDefinition Definition => _definition;

        private void Awake() => ApplyAppearance();

        private void OnValidate()
        {
            if (!Application.isPlaying)
                ApplyAppearance();
        }

        public void Configure(ActorDefinition definition, Renderer bodyRenderer)
        {
            _definition = definition;
            _bodyRenderer = bodyRenderer;
            ApplyAppearance();
        }

        private void ApplyAppearance()
        {
            if (!_definition || !_bodyRenderer)
                return;
            _properties ??= new MaterialPropertyBlock();
            _bodyRenderer.GetPropertyBlock(_properties);
            _properties.SetColor("_BaseColor", _definition.Tint);
            _properties.SetColor("_Color", _definition.Tint);
            if (_definition.AlbedoTexture)
            {
                _properties.SetTexture("_BaseMap", _definition.AlbedoTexture);
                _properties.SetTexture("_MainTex", _definition.AlbedoTexture);
            }
            _bodyRenderer.SetPropertyBlock(_properties);
        }
    }
}
