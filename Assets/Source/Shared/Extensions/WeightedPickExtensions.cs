using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Shared.Extensions
{
    internal static class WeightedPickExtensions
    {
        private static readonly List<IWeightedEntry> CandidateBuffer = new();

        /// <summary>Weighted-random pick, excluding entries at their <see cref="IWeightedEntry.MaxCount"/> limit; returns <c>default</c> if none remain.</summary>
        internal static T PickRandom<T>(this IReadOnlyList<T> entries, IReadOnlyDictionary<string, int> activeCounts)
            where T : class, IWeightedEntry
        {
            CandidateBuffer.Clear();
            var totalWeight = 0f;

            for (var i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.MaxCount != 0 && activeCounts.GetValueOrDefault(e.PoolKey) >= e.MaxCount)
                {
                    continue;
                }

                CandidateBuffer.Add(e);
                totalWeight += e.Weight;
            }

            if (CandidateBuffer.Count == 0)
            {
                return null;
            }

            var roll = Random.Range(0f, totalWeight);
            var cumulative = 0f;

            foreach (var candidate in CandidateBuffer)
            {
                cumulative += candidate.Weight;
                if (roll < cumulative)
                {
                    return (T)candidate;
                }
            }

            return (T)CandidateBuffer[0];
        }
    }
}
