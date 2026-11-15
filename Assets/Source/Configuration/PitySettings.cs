using System;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>Inspector-authored <see cref="IPitySettings" />, one per chance roll in <see cref="AssistSettings" />.</summary>
    [Serializable]
    internal class PitySettings : IPitySettings
    {
        [Tooltip("How much each roll missed at full need raises the chance, in multiples of the base chance: 4 takes " +
            "a 1% chance to 5%, then 9%. A miss at half need counts half. 0 = no pity.")]
        [SerializeField] [Min(0f)] private float _growthPerMiss;

        [Tooltip("The most pity can raise the chance to. A base chance above it is kept as it is.")]
        [SerializeField] [Range(0f, 1f)] private float _maxChance = 0.5f;

        public float GrowthPerMiss => _growthPerMiss;
        public float MaxChance => _maxChance;

        internal PitySettings()
        {
        }

        internal PitySettings(float growthPerMiss, float maxChance)
        {
            _growthPerMiss = growthPerMiss;
            _maxChance = maxChance;
        }
    }
}
