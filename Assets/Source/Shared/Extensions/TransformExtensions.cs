using UnityEngine;

namespace BalloonParty.Shared.Extensions
{
    internal static class TransformExtensions
    {
        // No-op on null transform to support optional inspector refs.
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
