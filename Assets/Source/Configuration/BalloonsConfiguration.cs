using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Balloons Configuration", fileName = "BalloonsConfiguration")]
    public class BalloonsConfiguration : ScriptableObject
    {
        [SerializeField] private BalloonPrefabEntry[] _entries;

        public BalloonPrefabEntry[] Entries => _entries;

        /// <summary>
        /// Picks a random entry using weighted random selection, excluding entries that have
        /// reached their <see cref="BalloonPrefabEntry.MaxCount"/> limit.
        /// Returns null if all entries are at their limit.
        /// </summary>
        public BalloonPrefabEntry PickRandom(IReadOnlyDictionary<string, int> activeCounts)
        {
            // Build candidate list — skip entries that are at or over their max (0 = no limit)
            var candidates = _entries.Where(e =>
                e.MaxCount == 0 ||
                activeCounts.GetValueOrDefault(e.PoolKey) < e.MaxCount).ToArray();

            if (candidates.Length == 0)
            {
                return null;
            }

            var totalWeight = candidates.Sum(e => e.Weight);
            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;

            foreach (var entry in candidates)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    return entry;
                }
            }

            return candidates[0];
        }
    }
}
