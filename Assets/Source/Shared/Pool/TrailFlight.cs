using BalloonParty.UI.Score;
using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Controls a single trail's in-flight state via transport-style commands over DOTween.
    /// </summary>
    internal class TrailFlight
    {
        private readonly Vector3 _origin;
        private readonly Transform _transform;

        internal FlightPhase Phase { get; private set; }
        internal Transform Transform => _transform;
        internal Vector3 Origin => _origin;

        internal TrailFlight(Transform transform, Vector3 origin)
        {
            _transform = transform;
            _origin = origin;
            Phase = FlightPhase.InFlight;
        }

        internal void Pause()
        {
            if (Phase != FlightPhase.InFlight || _transform == null)
            {
                return;
            }

            _transform.DOPause();
            // A default trail's TrailRenderer keeps decaying in real time even while the transform is paused,
            // so a long freeze would dissolve the frozen orb's tail; hold the ribbon until it resolves. Null on
            // formation anchors (their vertices are frozen by ShapeFormationTicker instead).
            _transform.GetComponent<FlyingTrail>()?.FreezeRibbon();
            Phase = FlightPhase.Paused;
        }

        /// <summary>
        ///     Jumps the trail to completed state so onComplete fires immediately. Idempotent: once the
        ///     flight is Idle the pooled instance may already be flying for a NEW group, so completing
        ///     again would fire someone else's arrival — a stale holder's Complete must be a no-op.
        /// </summary>
        internal void Complete()
        {
            if (Phase == FlightPhase.Idle || _transform == null)
            {
                return;
            }

            // Set before DOComplete: the completion callback re-enters via Unregister → MarkCompleted.
            Phase = FlightPhase.Idle;
            _transform.DOComplete();
        }

        /// <summary>
        ///     The trail's arrival fired through its own tween — the pooled instance can be reused for a
        ///     new flight any frame now, so every later transport command on this handle must no-op.
        /// </summary>
        internal void MarkCompleted()
        {
            Phase = FlightPhase.Idle;
        }
    }
}
