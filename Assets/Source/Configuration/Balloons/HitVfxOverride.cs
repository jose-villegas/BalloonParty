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

        public HitOutcome AppliesTo => _appliesTo;

        /// <summary>Null means no VFX is played for this outcome.</summary>
        public ParticleSystem Prefab => _prefab;
    }
}
