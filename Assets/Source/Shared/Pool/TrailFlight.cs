using DG.Tweening;
using UnityEngine;

namespace BalloonParty.Shared.Pool
{
    /// <summary>
    ///     Controls a single trail's in-flight state. Wraps DOTween operations on
    ///     the trail transform and exposes transport-style commands: pause, resume,
    ///     stop (snap back to origin), play (restart from origin), complete (skip
    ///     to end), and speed control.
    /// </summary>
    internal class TrailFlight
    {
        private readonly Vector3 _origin;
        private readonly Transform _transform;

        private float _speed = 1f;

        internal TrailFlight(Transform transform, Vector3 origin)
        {
            _transform = transform;
            _origin = origin;
            Phase = FlightPhase.InFlight;
        }

        internal FlightPhase Phase { get; private set; }
        internal Transform Transform => _transform;
        internal Vector3 Origin => _origin;
        internal float Speed => _speed;

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
        ///     Kills all tweens and snaps the trail back to its spawn origin.
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
        ///     Kills all tweens and jumps the trail to completed state so the
        ///     onComplete callbacks fire immediately.
        /// </summary>
        internal void Complete()
        {
            if (_transform == null)
            {
                return;
            }

            _transform.DOComplete();
            Phase = FlightPhase.Idle;
        }

        /// <summary>
        ///     Adjusts the timeScale of every active tween on this trail.
        ///     1 = normal, &lt;1 = slower, &gt;1 = faster.
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
        ///     Switches all active tweens to unscaled time so they ignore
        ///     <see cref="Time.timeScale" />.
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
