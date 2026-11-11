using UnityEngine;

namespace AzureDreamsReimagined
{
    /// <summary>
    /// Defines an item the player can carry in the tower or buy in town.
    /// Mirrors the original Azure Dreams consumables and equipment.
    /// </summary>
    [CreateAssetMenu(menuName = "Azure Dreams/Item Definition", fileName = "ItemDefinition")]
    public sealed class ItemDefinition : ScriptableObject
    {
        public enum ItemType { Heal, Attack, Defend, Key }

        [SerializeField] private string _displayName = "Item";
        [SerializeField] private string _saveId = "";
        [SerializeField] private ItemType _type = ItemType.Heal;
        [Min(0)] [SerializeField] private int _value = 10;
        [SerializeField] private int _price = 20;
        [TextArea] [SerializeField] private string _description = "";
        [SerializeField] private Texture2D _icon;

        public string DisplayName => _displayName;
        public string SaveId => string.IsNullOrWhiteSpace(_saveId) ? _displayName : _saveId;
        public ItemType Type => _type;
        public int Value => Mathf.Max(0, _value);
        public int Price => Mathf.Max(0, _price);
        public string Description => _description;
        public Texture2D Icon => _icon;

        public void SetSaveId(string saveId) => _saveId = saveId;

        public void Configure(string displayName, ItemType type, int value, int price, string description)
        {
            _displayName = displayName;
            if (string.IsNullOrWhiteSpace(_saveId)) _saveId = displayName;
            _type = type;
            _value = Mathf.Max(0, value);
            _price = Mathf.Max(0, price);
            _description = description;
        }
    }
}
