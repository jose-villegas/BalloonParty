using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class TransformExtensions
    {
        // Sets local Y while preserving X/Z; a no-op on a null transform (for optional inspector refs).
        internal static void SetLocalY(this Transform transform, float y)
        {
            if (transform == null)
            {
                return;
            }

            transform.localPosition = transform.localPosition.WithY(y);
        }
    }
}
