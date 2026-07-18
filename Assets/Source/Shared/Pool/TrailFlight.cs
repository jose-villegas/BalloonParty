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

        private float _speed = 1f;

        internal FlightPhase Phase { get; private set; }
        internal Transform Transform => _transform;
        internal Vector3 Origin => _origin;
        internal float Speed => _speed;

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
            Phase = FlightPhase.Paused;
        }

        internal void Resume()
        {
            if (Phase != FlightPhase.Paused || _transform == null)
            {
                return;
            }

            _transform.DOPlay();
            Phase = FlightPhase.InFlight;
        }

        /// <summary>
        ///     Snaps the trail back to its spawn origin.
        /// </summary>
        internal void Stop()
        {
            if (_transform == null)
            {
                return;
            }

            _transform.DOKill();
            _transform.position = _origin;
            Phase = FlightPhase.Idle;
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

        /// <summary>
        ///     Sets the timeScale of every active tween on this trail.
        /// </summary>
        internal void SetSpeed(float speed)
        {
            _speed = speed;

            if (_transform == null)
            {
                return;
            }

            var tweens = DOTween.TweensByTarget(_transform);
            if (tweens == null)
            {
                return;
            }

            foreach (var tween in tweens)
            {
                tween.timeScale = speed;
            }
        }

        /// <summary>
        ///     Switches all active tweens to ignore <see cref="Time.timeScale" />.
        /// </summary>
        internal void SetUnscaledTime(bool useUnscaled)
        {
            if (_transform == null)
            {
                return;
            }

            var tweens = DOTween.TweensByTarget(_transform);
            if (tweens == null)
            {
                return;
            }

            foreach (var tween in tweens)
            {
                tween.SetUpdate(useUnscaled);
            }
        }
    }
}
