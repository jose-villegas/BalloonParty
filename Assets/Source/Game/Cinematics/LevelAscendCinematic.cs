using System;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using Cysharp.Threading.Tasks;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Descends the staging root to <see cref="Vector3.zero" /> so the incoming scenario slides into
    ///     place, rather than moving the camera.
    /// </summary>
    internal sealed class LevelAscendCinematic
    {
        private readonly CinematicDirector _director;
        private readonly ICinematicsSettings _settings;

        internal LevelAscendCinematic(CinematicDirector director, ICinematicsSettings settings)
        {
            _director = director;
            _settings = settings;
        }

        /// <summary>
        ///     No-ops (firing <paramref name="onBalloonSpawnCue" /> immediately) if another cinematic already owns the director.
        /// </summary>
        internal async UniTask PlayAsync(Transform stagingRoot, Action onBalloonSpawnCue, CancellationToken ct)
        {
            if (!_director.TryBeginCinematic(CinematicState.LevelAscend))
            {
                onBalloonSpawnCue?.Invoke();
                return;
            }

            try
            {
                var segment = _settings.EntryOf(CinematicState.LevelAscend).Rig;
                var curve = segment.TimeScaleCurve;
                var duration = curve.Duration();
                var height = segment.ZoomAmount;
                var spawnCueTime = duration * Mathf.Clamp01(segment.PanWeight);
                var spawnCueFired = false;

                // 0 (unset) falls back to the curve's own pace.
                var followSpeed = segment.FollowSpeed > 0f ? segment.FollowSpeed : 1f;

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (!spawnCueFired && elapsed >= spawnCueTime)
                    {
                        spawnCueFired = true;
                        onBalloonSpawnCue?.Invoke();
                    }

                    var position = stagingRoot.position;
                    position.y = curve.Evaluate(elapsed) * height;
                    stagingRoot.position = position;

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.unscaledDeltaTime * followSpeed;
                }

                if (!spawnCueFired)
                {
                    onBalloonSpawnCue?.Invoke();
                }

                stagingRoot.position = Vector3.zero;
            }
            finally
            {
                _director.EndCinematic();
            }
        }
    }
}
