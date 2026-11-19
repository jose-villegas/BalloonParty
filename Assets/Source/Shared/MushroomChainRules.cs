using System;
using UnityEngine;

namespace BalloonParty.Shared
{
    /// <summary>
    ///     The numbers and switches of the mushroom chain, as <see cref="MushroomChainRule" /> takes them. Authored
    ///     on the mushroom settings (<c>IMushroomSettings.Rules</c>).
    /// </summary>
    [Serializable]
    internal struct MushroomChainRules
    {
        /// <summary>The cap on bounces a stretch has when none is authored.</summary>
        internal const int DefaultMaxBouncesPerStretch = 12;

        [Tooltip("Mushroom hits in one flight, with no wall in between, that turn the ball into the lance. " +
            "0 or less pays nothing.")]
        [SerializeField] private int _chainTarget;

        [Tooltip("Off (the rule as settled): the same mushroom twice in a row counts once, and counts again " +
            "after another mushroom. On: every hit counts.")]
        [SerializeField] private bool _repeatHitCounts;

        [Tooltip("Most bounces off mushrooms between two walls. Past it the mushrooms let the ball through " +
            "until it meets a wall, so no flight can be kept by them. A chain is paid by the bounce that " +
            "reaches the target; 0 or less reads as 12.")]
        [SerializeField] private int _maxBouncesPerStretch;

        /// <summary>The count that arms the lance; 0 or less pays nothing.</summary>
        public int ChainTarget => _chainTarget;

        /// <summary>Whether a hit on the mushroom hit last still counts.</summary>
        public bool RepeatHitCounts => _repeatHitCounts;

        /// <summary>
        ///     The most bounces off mushrooms a ball makes between two walls, the ones that did not count included.
        ///     Always at least 1: there is no setting that lets a flight bounce without end.
        /// </summary>
        public int MaxBouncesPerStretch =>
            _maxBouncesPerStretch > 0 ? _maxBouncesPerStretch : DefaultMaxBouncesPerStretch;

        internal MushroomChainRules(
            int chainTarget, bool repeatHitCounts = false, int maxBouncesPerStretch = DefaultMaxBouncesPerStretch)
        {
            _chainTarget = chainTarget;
            _repeatHitCounts = repeatHitCounts;
            _maxBouncesPerStretch = maxBouncesPerStretch;
        }
    }
}
