using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>What the camera frames this frame, for turning world positions into clearing-mask UV.</summary>
    internal readonly struct ClearingView
    {
        internal readonly Vector2 Center;
        internal readonly float OrthographicSize;
        internal readonly float Aspect;

        internal ClearingView(Vector2 center, float orthographicSize, float aspect)
        {
            Center = center;
            OrthographicSize = orthographicSize;
            Aspect = aspect;
        }

        internal Vector2 ToUv(Vector2 world)
        {
            var height = 2f * OrthographicSize;
            return new Vector2(
                (world.x - Center.x) / (height * Aspect) + 0.5f,
                (world.y - Center.y) / height + 0.5f);
        }

        /// <summary>A world length as a fraction of the view's height, the unit the mask measures distance in.</summary>
        internal float ToUvHeight(float worldLength)
        {
            return worldLength / (2f * OrthographicSize);
        }
    }
}
