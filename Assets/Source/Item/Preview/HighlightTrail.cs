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

        private float _defaultRibbonTime;

        private void Awake()
        {
            if (_trailRenderer == null)
            {
                _trailRenderer = GetComponent<TrailRenderer>();
            }

            _defaultRibbonTime = _trailRenderer.time;
        }

        public void OnSpawned()
        {
            // A pooled pen is repositioned by the ticker before it draws, but the ribbon still holds the
            // previous life's world points — clearing on despawn alone would leave one frame where the old
            // figure is visible at the new position.
            _trailRenderer.Clear();
            _trailRenderer.emitting = false;
        }

        public void OnDespawned()
        {
            _trailRenderer.Clear();
            _trailRenderer.emitting = false;

            // Restore the authored lifetime, so a per-item override can't leak into the next item's pens.
            _trailRenderer.time = _defaultRibbonTime;
        }

        /// <summary>
        ///     Ribbon lifetime in seconds — how much of a figure one pen shows at once. Non-positive
        ///     restores the prefab's authored value.
        /// </summary>
        internal void SetRibbonTime(float seconds)
        {
            _trailRenderer.time = seconds > 0f ? seconds : _defaultRibbonTime;
        }

        /// <summary>Pen up/down — a pen travelling to its stroke shouldn't necessarily draw on the way.</summary>
        internal void SetEmitting(bool emitting)
        {
            _trailRenderer.emitting = emitting;
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
