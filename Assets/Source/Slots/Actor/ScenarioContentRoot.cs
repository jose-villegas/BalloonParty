using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     Parent transform for scenario static content; the Ascent transition moves this root, not each
    ///     child. <see cref="OutgoingBalloons" /> is a named child holder for the transient old-level
    ///     balloons a level transition floats out — kept apart from the statics but still riding the root.
    /// </summary>
    internal sealed class ScenarioContentRoot : IDisposable
    {
        public Transform Transform { get; }

        public Transform OutgoingBalloons { get; }

        internal ScenarioContentRoot()
        {
            Transform = new GameObject("ScenarioContentRoot").transform;
            OutgoingBalloons = new GameObject("OutgoingBalloons").transform;
            OutgoingBalloons.SetParent(Transform, worldPositionStays: false);
        }

        public void Dispose()
        {
            if (Transform != null)
            {
                UnityEngine.Object.Destroy(Transform.gameObject);
            }
        }
    }
}
