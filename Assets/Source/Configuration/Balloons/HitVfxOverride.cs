using System;
using BalloonParty.Slots.Capabilities;
using UnityEngine;
using BalloonParty.Configuration.Balloons;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class HitVfxOverride
    {
        [SerializeField] private HitOutcome _appliesTo;
        [SerializeField] private ParticleSystem _prefab;

        [Tooltip("Turn the effect so its local up follows the shot that hit the balloon, so a burst authored to fly up flies on through. A hit with no direction (a board clear) leaves it upright.")]
        [SerializeField] private bool _alongHit;

        public HitOutcome AppliesTo => _appliesTo;

        /// <summary>Null means no VFX is played for this outcome.</summary>
        public ParticleSystem Prefab => _prefab;

        public bool AlongHit => _alongHit;
    }
}
