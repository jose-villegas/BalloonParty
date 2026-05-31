using System.Collections.Generic;
using BalloonParty.Slots.Actor.Archetype;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Grid Actor Configuration", fileName = "GridActorConfiguration")]
    public class GridActorConfiguration : ScriptableObject
    {
        [SerializeField] private GridActorPrefabEntry[] _entries;

        [Header("Puff Cloud")]
        [SerializeField] private PuffCloudView _puffCloudPrefab;

        private readonly List<GridActorPrefabEntry> _candidateBuffer = new();

        public GridActorPrefabEntry[] Entries => _entries;
        internal PuffCloudView PuffCloudPrefab => _puffCloudPrefab;

        /// <summary>
        /// Picks a random entry using weighted random selection, excluding entries that have
        /// reached their <see cref="GridActorPrefabEntry.MaxCount"/> limit.
        /// Returns null if all entries are at their limit or the entries array is empty.
        /// </summary>
        public GridActorPrefabEntry PickRandom(IReadOnlyDictionary<string, int> activeCounts)
        {
            _candidateBuffer.Clear();
            var totalWeight = 0f;

            foreach (var e in _entries)
            {
                if (e.MaxCount != 0 && activeCounts.GetValueOrDefault(e.PoolKey) >= e.MaxCount)
                {
                    continue;
                }

                _candidateBuffer.Add(e);
                totalWeight += e.Weight;
            }

            if (_candidateBuffer.Count == 0)
            {
                return null;
            }

            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;

            foreach (var entry in _candidateBuffer)
            {
                cumulative += entry.Weight;
                if (roll < cumulative)
                {
                    return entry;
                }
            }

            return _candidateBuffer[0];
        }
    }
}
