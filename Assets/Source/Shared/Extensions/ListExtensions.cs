using System.Collections.Generic;

namespace BalloonParty.Shared.Extensions
{
    internal static class ListExtensions
    {
        /// <summary>
        ///     Removes the item at <paramref name="index"/> in O(1) by moving the last item into its
        ///     slot. Order is NOT preserved — use only for unordered working sets.
        /// </summary>
        internal static void SwapRemoveAt<T>(this List<T> list, int index)
        {
            list[index] = list[^1];
            list.RemoveAt(list.Count - 1);
        }
    }
}
