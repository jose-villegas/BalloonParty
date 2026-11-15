using System;
using BalloonParty.Nudge;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration.Balloons
{
    [Serializable]
    public class DiscoWaveSettings : IDiscoWaveSettings
    {
        [Tooltip("Seconds the disc takes to reach the farthest slot from the pop, easing out: fast at first, " +
                 "settling as it reaches the far side.")]
        [SerializeField] [Min(0.05f)] private float _seconds = 0.9f;

        [Tooltip("The nudge each balloon gets as the disc's edge reaches it, away from the pop. Applies To must " +
                 "include Wave. Empty falls back to the global nudge.")]
        [SerializeField] private NudgeOverride[] _nudgeOverrides = { new(NudgeType.Wave, 0.2f, 0.18f) };

        [Tooltip("A painted balloon's burst of its old colour, as a share of a pop's size: smaller, as the " +
                 "balloon stays.")]
        [SerializeField] [Range(0f, 2f)] private float _burstScale = 0.6f;

        [Tooltip("World width of the ring drawn at the disc's edge.")]
        [SerializeField] [Min(0.01f)] private float _ringWidth = 0.15f;

        [Tooltip("The ring's material. Required: with none the wave paints the board but draws nothing.")]
        [SerializeField] private Material _ringMaterial;

        [Tooltip("Sorting layer for the ring.")]
        [SortingLayerName]
        [SerializeField] private string _sortingLayerName = "Balloons";

        [Tooltip("Sorting order on that layer: below the balloons, so the ring passes behind the ones it paints.")]
        [SerializeField] private int _sortingOrder = -1;

        public float Seconds => _seconds;
        public NudgeOverride[] NudgeOverrides => _nudgeOverrides;
        public float BurstScale => _burstScale;
        public float RingWidth => _ringWidth;
        public Material RingMaterial => _ringMaterial;
        public string SortingLayerName => _sortingLayerName;
        public int SortingOrder => _sortingOrder;
    }
}
