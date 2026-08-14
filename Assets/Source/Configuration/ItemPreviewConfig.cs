using System;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Item Preview Config", fileName = "ItemPreviewConfig")]
    internal class ItemPreviewConfig : ScriptableObject, IItemPreviewConfig
    {
        [Header("Dashes")]
        [Tooltip("World length a pen paints within its dash slot before blanking. Universal across every " +
            "figure, so a Bomb circle and Shield's stub dash at the same size.")]
        [SerializeField] [Min(0.01f)] private float _dashLength = 0.35f;

        [Tooltip("World length of blank arc between dashes. 0 yields a solid line: each pen's slot " +
            "equals its painted length, so adjacent dashes touch with no gap.")]
        [SerializeField] [Min(0f)] private float _dashSpacing = 0.15f;

        [Tooltip("Hard ceiling on pens summed across a figure's strokes AND their approach cascades. " +
            "Past this, every stroke's dashes are spaced out together rather than any part of a large " +
            "figure going undrawn; the figure's own dashes claim their share first.")]
        [SerializeField] [Min(1)] private int _maxPens = 64;

        [Tooltip("World units per second a pen travels along its stroke once it starts tracing.")]
        [SerializeField] [Min(0.01f)] private float _traceSpeed = 6f;

        [Header("Bloom")]
        [Tooltip("Seconds the figure's furthest-travelling pen takes to reach its dash slot. Every " +
            "figure completes its draw-in in this same time regardless of size.")]
        [SerializeField] [Min(0.01f)] private float _bloomDuration = 0.35f;

        [Tooltip("Eases the shared bloom clock over its normalized duration, shaping how the drawing " +
            "tip accelerates and settles across the whole figure.")]
        [SerializeField] private AnimationCurve _bloomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Seconds a host must stay continuously sighted before its figure first appears. " +
            "Restarts whenever the sighted host changes, so sweeping across a row of items shows " +
            "nothing until the aim settles on one. 0 shows the figure immediately.")]
        [SerializeField] [Min(0f)] private float _sightDelaySeconds = 0.15f;

        [Tooltip("Seconds the settled figure holds before the whole draw-in — approach leg included — " +
            "replays from scratch. 0 disables the loop: draw once and hold.")]
        [SerializeField] [Min(0f)] private float _rebloomHoldSeconds = 1.5f;

        [SerializeField] private ShieldPreviewSettings _shield = new();
        [SerializeField] private BombPreviewSettings _bomb = new();
        [SerializeField] private PaintPreviewSettings _paint = new();

        public float DashLength => _dashLength;
        public float DashSpacing => _dashSpacing;
        public int MaxPens => _maxPens;
        public float BloomDuration => _bloomDuration;

        // An un-authored serialized curve deserializes EMPTY and evaluates to 0, which would collapse
        // every bloom to a pen sitting on the host — fall back the way ProjectileMotionResolver does.
        public AnimationCurve BloomCurve => _bloomCurve is { length: > 0 }
            ? _bloomCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float TraceSpeed => _traceSpeed;
        public float SightDelaySeconds => _sightDelaySeconds;
        public float RebloomHoldSeconds => _rebloomHoldSeconds;
        public IShieldPreviewSettings Shield => _shield;
        public IBombPreviewSettings Bomb => _bomb;
        public IPaintPreviewSettings Paint => _paint;

        [Serializable]
        private sealed class ShieldPreviewSettings : IShieldPreviewSettings
        {
            [Tooltip("World length of the bounce stub drawn off the wall the aim line ends on. Long " +
                "enough to read as a direction, short enough not to become a second aim line.")]
            [SerializeField] [Min(0f)] private float _stubLength = 1.2f;

            public float StubLength => _stubLength;
        }

        [Serializable]
        private sealed class BombPreviewSettings : IBombPreviewSettings
        {
            // No [Min] on purpose — a negative offset (drawing tighter than the true radius) is a
            // legitimate thing to want, not a mistake to block.
            [Tooltip("World units added to the blast radius purely for display. 0 draws dead-accurate. " +
                "Can go negative to draw tighter; a positive value actually reads closer to the true " +
                "catchment, since the blast also catches an occupant by its own radius.")]
            [SerializeField] private float _radiusOffset;

            public float RadiusOffset => _radiusOffset;
        }

        [Serializable]
        private sealed class PaintPreviewSettings : IPaintPreviewSettings
        {
            // [Min(0f)] unlike Bomb's RadiusOffset: a negative multiplier would mirror the triangle
            // rather than scale it, which isn't "draw tighter" — the offsets this replaced allowed
            // negatives because they were additive, but a scale has no equivalent legitimate use below 0.
            [Tooltip("Multiplier on both the spread length and base width purely for display. 1 draws " +
                "dead-accurate. Below 1 draws the triangle tighter than the true spread, about its own " +
                "centre rather than sliding toward the host.")]
            [SerializeField] [Min(0f)] private float _scale = 1f;

            public float Scale => _scale;
        }
    }
}
