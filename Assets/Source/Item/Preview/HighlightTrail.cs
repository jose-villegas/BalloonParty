using BalloonParty.Shared.Pool;
using UnityEngine;

namespace BalloonParty.Item.Preview
{
    /// <summary>
    ///     One pen of an item-range telegraph: a head sprite dragging a <see cref="TrailRenderer" /> ribbon,
    ///     whose position is driven entirely from outside by <see cref="ItemPreviewTicker" />.
    /// </summary>
    /// <remarks>
    ///     Deliberately NOT <c>UI/Score/FlyingTrail</c>, though the technique is the same. That one owns its
    ///     own DOTween flight, motion-curve table and flight gradients, and lives on the UI sorting layer to
    ///     fly at a score bar. These pens are board-space, are driven closed-form by a ticker, and want a
    ///     different material entirely — sharing the type would mean carrying score-flight machinery that
    ///     never runs and fighting over the sorting layer.
    /// </remarks>
    [RequireComponent(typeof(TrailRenderer))]
    internal sealed class HighlightTrail : MonoBehaviour, IPoolable
    {
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private SpriteRenderer _head;

        /// <summary>
        ///     The pen prefab's own authored ribbon lifetime — every item shares it now. A graceful hide
        ///     reads this to know how long a paused ribbon takes to fade on its own.
        /// </summary>
        internal float EffectiveRibbonSeconds => _trailRenderer.time;

        // Test-only read of the trail's real emitting state — narrower than exposing the TrailRenderer
        // itself, and the ticker's own Pen.Emitting mirror isn't good enough for a regression test that
        // exists specifically to catch the mirror and the renderer disagreeing.
        internal bool IsEmittingForTest => _trailRenderer.emitting;

        private void Awake()
        {
            if (_trailRenderer == null)
            {
                _trailRenderer = GetComponent<TrailRenderer>();
            }

            // _head was re-added after being dropped along with the runtime-tint path it used to serve —
            // the existing prefab's serialized reference is gone, so this fallback is what makes it resolve
            // again without re-wiring the prefab by hand.
            if (_head == null)
            {
                _head = GetComponentInChildren<SpriteRenderer>(true);
            }
        }

        public void OnSpawned()
        {
            // A pooled pen is repositioned by the ticker before it draws, but the ribbon still holds the
            // previous life's world points — clearing on despawn alone would leave one frame where the old
            // figure is visible at the new position.
            _trailRenderer.Clear();
            _trailRenderer.emitting = false;

            if (_head != null)
            {
                _head.enabled = false;
            }
        }

        public void OnDespawned()
        {
            _trailRenderer.Clear();
            _trailRenderer.emitting = false;

            if (_head != null)
            {
                _head.enabled = false;
            }
        }

        // Explicit wiring, mirroring LaserItemRotation.Configure: a pooled component can't rely on Awake
        // alone the way a scene-placed one can — Awake genuinely does run for a real spawned prefab in
        // actual play, but there's no reason a caller who already HAS the right references should have to
        // wait for it. Unconditional (unlike Awake's own if-null fallback above, which exists to respect
        // an artist's real Inspector wiring) — a caller invoking this is stating the references directly,
        // not offering a fallback.
        internal void Configure(TrailRenderer trailRenderer, SpriteRenderer head = null)
        {
            _trailRenderer = trailRenderer;
            _head = head;
        }

        /// <summary>Pen up/down — a pen travelling to its stroke shouldn't necessarily draw on the way.</summary>
        internal void SetEmitting(bool emitting)
        {
            _trailRenderer.emitting = emitting;

            // The head dot is part of "is this pen drawing", not decoration independent of it — a parked
            // or pen-up pen must be fully invisible, not just missing its ribbon.
            if (_head != null)
            {
                _head.enabled = emitting;
            }
        }

        /// <summary>Drops the recorded ribbon, so a jump to a new stroke doesn't draw a chord across the gap.</summary>
        internal void ClearRibbon()
        {
            _trailRenderer.Clear();
        }

        internal void SetPosition(Vector3 position)
        {
            transform.position = position;
        }
    }
}
