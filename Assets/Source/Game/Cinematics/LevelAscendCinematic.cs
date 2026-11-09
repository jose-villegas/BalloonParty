using System;
using System.Threading;
using BalloonParty.Configuration;
using BalloonParty.Shared.Extensions;
using BalloonParty.Shared.GameState;
using BalloonParty.Slots.Actor;
using Cysharp.Threading.Tasks;
using UnityEngine;
using BalloonParty.Configuration.Cinematics;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Brings the staging root's travel down to rest (<see cref="ScenarioContentRoot.SetTravel" />) so the
    ///     incoming scenario slides into place, rather than moving the camera.
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
        internal async UniTask PlayAsync(ScenarioContentRoot stagingRoot, Action onBalloonSpawnCue, CancellationToken ct)
        {
            if (!_director.TryBeginCinematic(CinematicState.LevelAscend))
            {
                onBalloonSpawnCue?.Invoke();
                return;
            }

            try
            {
                var ascend = _settings.LevelAscend;
                var curve = ascend.DescentCurve;
                var duration = curve.Duration();
                var height = ascend.Height;
                var spawnCueTime = duration * Mathf.Clamp01(ascend.BalloonSpawnCue);
                var spawnCueFired = false;

                // 0 (unset) falls back to the curve's own pace.
                var followSpeed = ascend.Speed > 0f ? ascend.Speed : 1f;

                var elapsed = 0f;
                while (elapsed < duration)
                {
                    if (!spawnCueFired && elapsed >= spawnCueTime)
                    {
                        spawnCueFired = true;
                        onBalloonSpawnCue?.Invoke();
                    }

                    stagingRoot.SetTravel(curve.Evaluate(elapsed) * height);

                    await UniTask.Yield(PlayerLoopTiming.Update, ct);
                    elapsed += Time.unscaledDeltaTime * followSpeed;
                }

                if (!spawnCueFired)
                {
                    onBalloonSpawnCue?.Invoke();
                }

                stagingRoot.SetTravel(0f);
            }
            finally
            {
                _director.EndCinematic();
            }
        }
    }
}
