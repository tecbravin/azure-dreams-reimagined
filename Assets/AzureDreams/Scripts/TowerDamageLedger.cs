using System;
using System.Collections.Generic;
namespace AzureDreamsReimagined
{
    /// <summary>Committed damage keyed by the creature's index in the turn snapshot.</summary>
    public sealed class TowerDamageLedger
    {
        private readonly Dictionary<int, int> _damage = new Dictionary<int, int>();
        public void Clear() => _damage.Clear();
        public void Add(int targetIndex, int amount)
        {
            if (targetIndex < 0) throw new ArgumentOutOfRangeException(nameof(targetIndex));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            if (amount == 0) return;
            _damage.TryGetValue(targetIndex, out int previous);
            _damage[targetIndex] = checked(previous + amount);
        }
        public int ForTarget(int targetIndex) => _damage.TryGetValue(targetIndex, out int amount) ? amount : 0;
    }
}
