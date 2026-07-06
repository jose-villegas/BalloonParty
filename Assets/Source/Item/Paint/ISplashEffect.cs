using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     A pooled effect that can be prepared with paint-blob flights before playing. Lets the handler
    ///     configure the effect through an interface instead of a hard downcast to the concrete view.
    /// </summary>
    internal interface ISplashEffect
    {
        void PrepareDisplay(
            IReadOnlyList<(Vector3 from, Vector3 to)> flights,
            ItemSettings settings,
            PoolManager poolManager,
            Action<int> onTargetHit);
    }
}
