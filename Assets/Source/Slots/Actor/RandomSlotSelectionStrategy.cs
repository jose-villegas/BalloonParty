using System.Collections.Generic;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    /// Default strategy: shuffles all empty slots and picks the first N.
    /// Used by actor types with no special placement requirements.
    /// </summary>
    internal class RandomSlotSelectionStrategy : ISlotSelectionStrategy
    {
        public List<Vector2Int> SelectSlots(IReadOnlyList<Vector2Int> emptySlots, int count)
        {
            var candidates = new List<Vector2Int>(emptySlots);
            Shuffle(candidates);

            var result = new List<Vector2Int>(count);
            for (var i = 0; i < Mathf.Min(count, candidates.Count); i++)
            {
                result.Add(candidates[i]);
            }

            return result;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}

