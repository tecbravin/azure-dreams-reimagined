using UnityEngine;

namespace AzureDreamsReimagined
{
    [CreateAssetMenu(fileName = "MonsterStatusPortraitCatalog", menuName = "Azure Dreams/Monster Status Portrait Catalog")]
    public sealed class MonsterStatusPortraitCatalog : ScriptableObject
    {
        [SerializeField] private Texture2D[] portraits;
        [SerializeField] private string sourceFolder;

        internal Texture2D Get(byte species, int genusColumn)
        {
            int index = species * 3 + Mathf.Clamp(genusColumn, 0, 2);
            return portraits != null && index < portraits.Length ? portraits[index] : null;
        }
    }
}
