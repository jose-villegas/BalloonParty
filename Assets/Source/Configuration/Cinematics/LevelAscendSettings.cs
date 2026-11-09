using System;
using BalloonParty.Shared.Extensions;
using UnityEngine;

namespace BalloonParty.Configuration.Cinematics
{
    /// <summary>
    ///     The Ascent's tuning — a transform-descent of the staging root, not a camera move, so it has
    ///     its own honest fields instead of borrowing <see cref="CameraRigCinematicSettings" />. Authored
    ///     entirely in the editor.
    /// </summary>
    [Serializable]
    internal class LevelAscendSettings
    {
        [Tooltip("Staging-root height fraction over the descent (1→0); the last key's time is the duration.")]
        [SerializeField] private AnimationCurve _descentCurve;

        [Tooltip("How far the staging root starts lifted, in world units; also the outgoing content's exit drop.")]
        [SerializeField] private float _height;

        [Tooltip("Fraction of the descent at which the new level's balloons spawn.")]
        [Range(0f, 1f)]
        [SerializeField] private float _balloonSpawnCue;

        [Tooltip("Descent playback-speed multiplier (1 = the curve's own pace).")]
        [SerializeField] private float _speed;

        [Tooltip("Time.timeScale held while the old level's balloons pop in a wave.")]
        [SerializeField] private float _popSlowMoTimeScale;

        [Tooltip("Seconds between anti-diagonal bands of the pop wave.")]
        [SerializeField] private float _popWaveBandSeconds;

        [Tooltip("Loss→restart scenery rise progress over time (0→1); the last key's time is the duration. Ease-in for a slow start ramping to full speed.")]
        [SerializeField] private AnimationCurve _restartRiseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("Fraction of the loss→restart rise at which the new level's balloons spawn (near 1 = as it settles).")]
        [Range(0f, 1f)]
        [SerializeField] private float _restartBalloonCue = 0.85f;

        public AnimationCurve DescentCurve => _descentCurve;
        public float Height => _height;
        public float BalloonSpawnCue => _balloonSpawnCue;
        public float Speed => _speed;
        public float PopSlowMoTimeScale => _popSlowMoTimeScale;
        public float PopWaveBandSeconds => _popWaveBandSeconds;
        public AnimationCurve RestartRiseCurve => _restartRiseCurve;
        public float RestartBalloonCue => _restartBalloonCue;

        // Wall-clock seconds, for beats that ride along (the scenario's mist reveal). LevelAscendCinematic plays the
        // descent curve at Speed, so a slower speed stretches it.
        public float AscentSeconds =>
            _descentCurve == null ? 0f : _descentCurve.Duration() / (_speed > 0f ? _speed : 1f);

        public float RestartRiseSeconds => _restartRiseCurve == null ? 0f : _restartRiseCurve.Duration();

        // Unity builds the serialized one through the parameterless constructor.
        internal LevelAscendSettings()
        {
        }

        internal LevelAscendSettings(AnimationCurve descentCurve, float speed, AnimationCurve restartRiseCurve)
        {
            _descentCurve = descentCurve;
            _speed = speed;
            _restartRiseCurve = restartRiseCurve;
        }
    }
}
