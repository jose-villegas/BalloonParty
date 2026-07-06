using System.Collections.Generic;

namespace BalloonParty.Shared.Extensions
{
    internal static class ListExtensions
    {
        /// <summary>O(1) removal by moving the last item into the gap; does NOT preserve order.</summary>
        internal static void SwapRemoveAt<T>(this List<T> list, int index)
        {
            list[index] = list[^1];
            list.RemoveAt(list.Count - 1);
        }
    }
}
