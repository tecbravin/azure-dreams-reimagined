using UnityEngine;

namespace AzureDreamsReimagined
{
    public enum ActorKind
    {
        Player,
        Familiar,
        Townsperson,
        TowerCreature
    }

    [CreateAssetMenu(menuName = "Azure Dreams/Actor Definition", fileName = "ActorDefinition")]
    public sealed class ActorDefinition : ScriptableObject
    {
        [SerializeField] private string _displayName = "New Actor";
        [SerializeField] private ActorKind _kind;
        [Min(1)] [SerializeField] private int _maxHealth = 10;
        [Min(0)] [SerializeField] private int _attackDamage = 2;
        [Min(0f)] [SerializeField] private float _moveDelay = 0.16f;
        [SerializeField] private Color _tint = Color.white;
        [SerializeField] private Texture2D _albedoTexture;

        public string DisplayName => _displayName;
        public ActorKind Kind => _kind;
        public int MaxHealth => _maxHealth;
        public int AttackDamage => _attackDamage;
        public float MoveDelay => _moveDelay;
        public Color Tint => _tint;
        public Texture2D AlbedoTexture => _albedoTexture;

        public void Configure(string displayName, ActorKind kind, int maxHealth, int attackDamage,
            float moveDelay, Color tint, Texture2D albedoTexture)
        {
            _displayName = displayName;
            _kind = kind;
            _maxHealth = Mathf.Max(1, maxHealth);
            _attackDamage = Mathf.Max(0, attackDamage);
            _moveDelay = Mathf.Max(0f, moveDelay);
            _tint = tint;
            _albedoTexture = albedoTexture;
        }
    }
}
