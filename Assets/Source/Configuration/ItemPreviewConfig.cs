using System;
using BalloonParty.Configuration.Items;
using BalloonParty.Shared;
using UnityEngine;

namespace BalloonParty.Configuration
{
    [CreateAssetMenu(menuName = "Configuration/Item Preview Config", fileName = "ItemPreviewConfig")]
    internal class ItemPreviewConfig : ScriptableObject, IItemPreviewConfig
    {
        // Returned for any item type the styles array doesn't cover, so StyleFor never hands back null and
        // no caller needs a null branch. Every field is the neutral "use the shared default" value.
        private static readonly ItemPreviewStyle NeutralStyle = new();

        [Header("Dashes")]
        [Tooltip("World length a pen paints within its dash slot before blanking. Universal across every " +
            "figure, so a Bomb circle and Shield's stub dash at the same size.")]
        [SerializeField] [Min(0.01f)] private float _dashLength = 0.35f;

        [Tooltip("World length of blank arc between dashes. 0 yields a solid line: each pen's slot " +
            "equals its painted length, so adjacent dashes touch with no gap.")]
        [SerializeField] [Min(0f)] private float _dashSpacing = 0.15f;

        [Tooltip("Hard ceiling on pens summed across a figure's strokes. Past this, every stroke's " +
            "dashes are spaced out together rather than any part of a large figure going undrawn.")]
        [SerializeField] [Min(1)] private int _maxPens = 64;

        [Tooltip("World units per second a pen travels along its stroke once it starts tracing.")]
        [SerializeField] [Min(0.01f)] private float _traceSpeed = 6f;

        [Header("Bloom")]
        [Tooltip("Seconds a pen takes to sweep out from the host to its stroke entry point.")]
        [SerializeField] [Min(0.01f)] private float _bloomDuration = 0.35f;

        [Tooltip("Extra degrees of arc a pen sweeps on the way out, on top of the straight-line bearing. " +
            "0 shoots straight outward; larger values curl the launch into a circular motion.")]
        [SerializeField] private float _bloomSweepDegrees = 120f;

        [Tooltip("Eases the outward bloom over its normalized duration, driving angle and radius " +
            "together so the whole spiral accelerates or settles as one.")]
        [SerializeField] private AnimationCurve _bloomCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Draw the ribbon during the outward bloom too. Off draws only the figure — launch " +
            "spokes can bury the shape they are supposed to reveal.")]
        [SerializeField] private bool _emitDuringBloom = true;

        [Header("Per item")]
        [Tooltip("Optional per-item overrides. Leave an entry at its defaults to use the shared values " +
            "above and tint to the host balloon's own colour.")]
        [EnumIndexed(typeof(ItemType))]
        [SerializeField] private ItemPreviewStyle[] _styles = Array.Empty<ItemPreviewStyle>();

        [SerializeField] private ShieldPreviewSettings _shield = new();
        [SerializeField] private BombPreviewSettings _bomb = new();
        [SerializeField] private PaintPreviewSettings _paint = new();

        public float DashLength => _dashLength;
        public float DashSpacing => _dashSpacing;
        public int MaxPens => _maxPens;
        public float BloomDuration => _bloomDuration;
        public float BloomSweepDegrees => _bloomSweepDegrees;

        // An un-authored serialized curve deserializes EMPTY and evaluates to 0, which would collapse
        // every bloom to a pen sitting on the host — fall back the way ProjectileMotionResolver does.
        public AnimationCurve BloomCurve => _bloomCurve is { length: > 0 }
            ? _bloomCurve
            : AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        public float TraceSpeed => _traceSpeed;
        public bool EmitDuringBloom => _emitDuringBloom;
        public IShieldPreviewSettings Shield => _shield;
        public IBombPreviewSettings Bomb => _bomb;
        public IPaintPreviewSettings Paint => _paint;

#if UNITY_EDITOR
        // [EnumIndexed] labels entries by ordinal, so a short array silently hides the tail item types
        // from the Inspector — an empty one hides ALL of them, leaving nothing to author and every item
        // silently on the neutral fallback. Grow to one entry per ItemType (never shrink — that would
        // discard authored styles if the enum is ever reordered down).
        private void OnValidate()
        {
            var wanted = System.Enum.GetValues(typeof(ItemType)).Length;
            if (_styles != null && _styles.Length >= wanted)
            {
                return;
            }

            var grown = new ItemPreviewStyle[wanted];
            for (var i = 0; i < wanted; i++)
            {
                grown[i] = _styles != null && i < _styles.Length && _styles[i] != null
                    ? _styles[i]
                    : new ItemPreviewStyle();
            }

            _styles = grown;

            // Without this the growth lives only in memory: the Inspector shows the entries, but nothing
            // marks the asset for saving, so it reverts to an empty array on the next reload and the
            // authored values vanish.
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        public IItemPreviewStyle StyleFor(ItemType type)
        {
            var index = (int)type;
            return _styles != null && index >= 0 && index < _styles.Length ? _styles[index] : NeutralStyle;
        }

        [Serializable]
        private sealed class ItemPreviewStyle : IItemPreviewStyle
        {
            [Tooltip("Ribbon lifetime for this item's pens. 0 keeps the prefab's authored value. This is " +
                "how much of the figure is visible at once — Trace Speed times this is the world length " +
                "a pen paints before its tail fades, so a long figure needs more than a small one.")]
            [SerializeField] [Min(0f)] private float _ribbonSeconds;

            [Tooltip("Whether this item's pens paint on the way out to the figure. Hide it for a figure " +
                "that sits far from its host (Shield's stub is at a wall) — the approach is a long spoke " +
                "across the board that buries what it was meant to introduce.")]
            [SerializeField] private ItemPreviewBloomDraw _bloomDraw = ItemPreviewBloomDraw.Inherit;

            public float RibbonSeconds => _ribbonSeconds;
            public ItemPreviewBloomDraw BloomDraw => _bloomDraw;
        }

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
