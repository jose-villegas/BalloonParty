using System;
using System.Collections.Generic;
using BalloonParty.Configuration;
using BalloonParty.Shared.Pool;
using UnityEngine;
using BalloonParty.Configuration.Items;

namespace BalloonParty.Item.Paint
{
    /// <summary>
    ///     Lets the handler configure a pooled effect without a hard downcast to the concrete view.
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
