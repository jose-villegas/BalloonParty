using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     A pooled effect that can be prepared with a chain path before playing. Lets the handler
    ///     configure the effect through an interface instead of a hard downcast to the concrete view.
    /// </summary>
    internal interface IChainEffect
    {
        void PrepareDisplay(IReadOnlyList<Vector3> targetPositions, ItemSettings settings, Action<int> onTargetHit);
    }
}
