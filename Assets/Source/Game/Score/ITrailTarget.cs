using UnityEngine;

namespace BalloonParty.Game.Score
{
    internal interface ITrailTarget
    {
        Vector3 Center { get; }
        Vector3 RandomPosition();
    }
}

