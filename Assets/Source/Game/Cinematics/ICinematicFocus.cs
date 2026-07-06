using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>Return false when there is currently nothing to frame — the rig holds its position.</summary>
    internal interface ICinematicFocus
    {
        bool TryGetFocus(out Vector3 center, out Vector3 min, out Vector3 max);
    }
}
