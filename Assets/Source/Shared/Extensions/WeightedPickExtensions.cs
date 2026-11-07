using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Ranges;

namespace BalloonParty.Shared.Extensions
{
    internal static class WeightedPickExtensions
    {
        private static readonly List<IWeightedEntry> CandidateBuffer = new();

        /// <summary>Weighted-random pick, excluding entries at their <see cref="IWeightedEntry.MaxCount"/> limit or with an exhausted <paramref name="waveQuotas"/> allowance (absent key = unlimited); returns <c>default</c> if none remain.</summary>
        internal static T PickRandom<T>(
            this IReadOnlyList<T> entries,
            IReadOnlyDictionary<string, int> activeCounts,
            IReadOnlyDictionary<string, int> waveQuotas = null)
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

                if (waveQuotas != null && waveQuotas.TryGetValue(e.PoolKey, out var quota) && quota <= 0)
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

            var roll = UnityEngine.Random.Range(0f, totalWeight);
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

        /// <summary>
        ///     Weighted index into <paramref name="items" /> for a caller-supplied <paramref name="roll01" />
        ///     in [0, 1), so a seeded draw stays reproducible; -1 when the list is empty or every weight is 0.
        ///     Pass a non-capturing lambda for <paramref name="weightOf" /> so the delegate is cached.
        /// </summary>
        internal static int PickWeightedIndex<T>(this IReadOnlyList<T> items, Func<T, float> weightOf, float roll01)
        {
            var total = 0f;
            for (var i = 0; i < items.Count; i++)
            {
                total += weightOf(items[i]);
            }

            if (total <= 0f)
            {
                return -1;
            }

            var target = Mathf.Clamp01(roll01) * total;
            var accumulated = 0f;
            for (var i = 0; i < items.Count; i++)
            {
                accumulated += weightOf(items[i]);
                if (target < accumulated)
                {
                    return i;
                }
            }

            return items.Count - 1;
        }
    }
}
