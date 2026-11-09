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
        [SerializeField] [Min(1)] private int _coinsFromLevel = 10;

        [Tooltip("The chance that one pop drops a coin. Any pop counts, an item's blast included.")]
        [SerializeField] [Range(0f, 1f)] private float _dropChance = 0.01f;

        [Tooltip("Seconds a dropped coin takes to fly from the pop to the counter. It counts when it lands.")]
        [SerializeField] [Min(0.05f)] private float _trailDuration = 0.7f;

        [Tooltip("Coin trails pooled ahead of the first drop.")]
        [SerializeField] [Min(0)] private int _trailPrewarm = 8;

        public int CoinsFromLevel => _coinsFromLevel;
        public float DropChance => _dropChance;
        public float TrailDuration => _trailDuration;
        public int TrailPrewarm => _trailPrewarm;
    }
}
