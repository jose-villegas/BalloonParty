using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>
    ///     The single scene transform every piece of the current scenario's static content parents
    ///     itself under — the cluster views (<see cref="Cluster.ClusterView" />) and the per-slot
    ///     static-actor markers. Normally sits at the origin, so a child's local position equals its
    ///     world position and rendering is unchanged. The level-transition Ascent lifts this root and
    ///     slides it back to zero, so the incoming scenario descends into place without the camera
    ///     moving — the one transform the transition cares about. Balloons are deliberately NOT
    ///     parented here: they spawn at their final position with their own entrance animation.
    /// </summary>
    internal sealed class ScenarioContentRoot : IDisposable
    {
        public Transform Transform { get; }

        internal ScenarioContentRoot()
        {
            Transform = new GameObject("ScenarioContentRoot").transform;
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
