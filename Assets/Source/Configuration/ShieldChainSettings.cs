using System;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    /// <summary>
    ///     Inspector-authored <see cref="IShieldChainSettings" />, serialized inside
    ///     <see cref="RunConfig" /> beside the toggle that turns chain planning on.
    /// </summary>
    /// <remarks>
    ///     Field initialisers carry the values these knobs shipped as constants with, so an asset
    ///     saved before the block existed deserialises to the behaviour it already had.
    /// </remarks>
    [Serializable]
    internal class ShieldChainSettings : IShieldChainSettings
    {
        [Tooltip("Opening angles sampled across the fan. More is finer but costs a flight simulation each.")]
        [SerializeField] [Min(2)] private int _fanSamples = 25;

        [Tooltip("The upper semicircle, trimmed of the near-horizontal shots that never climb.")]
        [SerializeField] [Range(0f, 180f)] private float _fanMinDegrees = 25f;

        [SerializeField] [Range(0f, 180f)] private float _fanMaxDegrees = 155f;

        [Tooltip("Fewest openings that must reach a slot before a shield may go there. Raising it " +
            "makes chains more forgiving to enter; too high and the planner runs out of slots.")]
        [SerializeField] [Min(1)] private int _minEntryAngles = 3;

        [Tooltip("Slots reachable by more than this share of the fan are already swept for free, " +
            "so a shield there extends nothing.")]
        [SerializeField] [Range(0f, 1f)] private float _cheapZoneFraction = 0.6f;

        [Header("Reachability nudge")]
        [Tooltip("Openings sampled per grid mutation to mark which slots a shot still crosses. " +
            "Coarser than the planning fan because it re-sweeps far more often.")]
        [SerializeField] [Min(2)] private int _reachabilityFanSamples = 13;

        [Tooltip("How deep that sweep looks. A nudge for the balancer, not a solver.")]
        [SerializeField] [Min(0)] private int _reachabilityMaxReflections = 2;

        [Tooltip("Bias bonus for parking a shield where a straight shot reaches it. Stays small so " +
            "support and pressure still decide the move.")]
        [SerializeField] [Min(0)] private int _reachableSlotBonus = 6;

        [Tooltip("Taken off that bonus per reflection needed to reach the slot.")]
        [SerializeField] [Min(0)] private int _perReflectionPenalty = 2;

        public int FanSamples => _fanSamples;
        public float FanMinDegrees => _fanMinDegrees;
        public float FanMaxDegrees => _fanMaxDegrees;
        public int MinEntryAngles => _minEntryAngles;
        public float CheapZoneFraction => _cheapZoneFraction;
        public int ReachabilityFanSamples => _reachabilityFanSamples;
        public int ReachabilityMaxReflections => _reachabilityMaxReflections;
        public int ReachableSlotBonus => _reachableSlotBonus;
        public int PerReflectionPenalty => _perReflectionPenalty;
    }
}
