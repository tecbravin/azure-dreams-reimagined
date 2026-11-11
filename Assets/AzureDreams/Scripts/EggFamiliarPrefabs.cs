using UnityEngine;

namespace AzureDreamsReimagined
{
    public sealed class EggFamiliarPrefabs : ScriptableObject
    {
        [SerializeField] private GameObject[] _bySpecies;
        public GameObject Find(int species) => _bySpecies != null && species >= 0 && species < _bySpecies.Length
            ? _bySpecies[species] : null;
    }
}
