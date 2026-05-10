#region

using System;

#endregion

namespace BalloonParty.Configuration
{
    [Serializable]
    public class PowerUpSettings
    {
        public BalloonPowerUp Type;
        public int TurnCheckEvery;
        public float Weight;
        public int MaximumAllowed;

        public float NudgeDistance;
        public float NudgeDuration;
    }
}
