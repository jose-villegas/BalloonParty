using System;
using BalloonParty.Slots.Capabilities;
using UnityEngine;

namespace BalloonParty.Configuration
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
