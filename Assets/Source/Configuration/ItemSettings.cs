#region

using System;

#endregion

namespace BalloonParty.Configuration
{
    [Serializable]
    public class ItemSettings
    {
        public ItemType Type;
        public int TurnCheckEvery;
        public float Weight;
        public int MaximumAllowed;

        public float NudgeDistance;
        public float NudgeDuration;
    }
}
