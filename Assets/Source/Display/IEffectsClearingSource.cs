using UnityEngine;

namespace BalloonParty.Display
{
    /// <summary>
    ///     Where the clearing mask gets its stamps from. The mask view lives on the persistent camera and pulls
    ///     once a frame, so the run-scoped source never races its <c>LateUpdate</c>.
    /// </summary>
    internal interface IEffectsClearingSource
    {
        /// <summary>Writes this frame's stamps (capsules in mask UV) and returns how many.</summary>
        int FillStamps(in ClearingView view, Vector4[] stamps);
    }
}
