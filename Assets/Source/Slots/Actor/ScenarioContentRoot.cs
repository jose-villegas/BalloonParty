using System;
using UnityEngine;

namespace BalloonParty.Slots.Actor
{
    /// <summary>Parent transform for scenario static content; the Ascent transition moves this root, not each child.</summary>
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
