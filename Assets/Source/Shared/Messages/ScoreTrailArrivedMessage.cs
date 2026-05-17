using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    public readonly struct ScoreTrailArrivedMessage
    {
        public readonly string ColorName;
        public readonly Vector3 WorldPosition;

        public ScoreTrailArrivedMessage(string colorName, Vector3 worldPosition)
        {
            ColorName = colorName;
            WorldPosition = worldPosition;
        }
    }
}
