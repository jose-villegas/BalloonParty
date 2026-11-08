using System;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>Inspector-authored <see cref="ICoinSettings" />, serialized inside <see cref="RunConfig" />.</summary>
    [Serializable]
    internal class CoinSettings : ICoinSettings
    {
        [Tooltip("The first level whose pops can drop a coin.")]
        [SerializeField] [Min(1)] private int _coinsFromLevel = 15;

        [Tooltip("The chance that one pop drops a coin. Any pop counts, an item's blast included.")]
        [SerializeField] [Range(0f, 1f)] private float _dropChance = 0.05f;

        public int CoinsFromLevel => _coinsFromLevel;
        public float DropChance => _dropChance;
    }
}
