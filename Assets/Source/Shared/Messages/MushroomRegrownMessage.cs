using UnityEngine;

namespace BalloonParty.Shared.Messages
{
    /// <summary>
    ///     The cap a lance cut off a meadow mushroom (<see cref="MushroomCutMessage" />) has grown back
    ///     (@ref plan_meadow_mushrooms): from here on the ball bounces off it again.
    /// </summary>
    internal readonly struct MushroomRegrownMessage
    {
        /// <summary>The cap's grid slot, as <see cref="MushroomCutMessage.Slot" /> named it.</summary>
        public readonly Vector2Int Slot;

        public MushroomRegrownMessage(Vector2Int slot)
        {
            Slot = slot;
        }
    }
}
