using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Lightning
{
    /// <summary>
    ///     Lets the handler configure a pooled effect without a hard downcast to the concrete view.
    /// </summary>
    internal interface IChainEffect
    {
        void PrepareDisplay(IReadOnlyList<Vector3> targetPositions, ItemSettings settings, Action<int> onTargetHit);
    }
}
