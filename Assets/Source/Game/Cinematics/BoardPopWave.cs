using System;
using System.Collections.Generic;
using System.Threading;
using BalloonParty.Balloon.Controller;
using BalloonParty.Balloon.Model;
using BalloonParty.Configuration.Cinematics;
using BalloonParty.Shared.Pause;
using BalloonParty.Slots.Grid;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace BalloonParty.Game.Cinematics
{
    /// <summary>
    ///     Pops every balloon on the board in a slow-mo wave along anti-diagonals from the far corners
    ///     inward. Shared by the level Ascent and the game-over restore. Scoreless — pops through the
    ///     registry, which publishes no <c>ActorHitMessage</c>. Tuning is the <c>LevelAscend</c> pop
    ///     settings, so both beats read the same cadence.
    /// </summary>
    internal sealed class BoardPopWave : IBoardEffect
    {
        private readonly SlotGrid _grid;
        private readonly BalloonControllerRegistry _balloonRegistry;
        private readonly TimeScaleService _timeScale;
        private readonly ICinematicsSettings _settings;
        private readonly Dictionary<int, List<IBalloonModel>> _bands = new();

        private int _minBand;
        private int _maxBand;

        internal BoardPopWave(
            SlotGrid grid,
            BalloonControllerRegistry balloonRegistry,
            TimeScaleService timeScale,
            ICinematicsSettings settings)
        {
            _grid = grid;
            _balloonRegistry = balloonRegistry;
            _timeScale = timeScale;
            _settings = settings;
        }

        // Snapshot the current balloons by anti-diagonal band — call while the grid is still populated.
        // exitDrop is unused: the wave pops balloons in place on the grid, so it never reparents them.
        public void Collect(float exitDrop)
        {
            _bands.Clear();
            _minBand = int.MaxValue;
            _maxBand = int.MinValue;

            for (var col = 0; col < _grid.Columns; col++)
            {
                for (var row = 0; row < _grid.Rows; row++)
                {
                    var balloon = _grid.ActorAt<IWriteableBalloonModel>(new Vector2Int(col, row));
                    if (balloon == null)
                    {
                        continue;
                    }

                    var band = col + row;
                    if (!_bands.TryGetValue(band, out var models))
                    {
                        models = new List<IBalloonModel>();
                        _bands[band] = models;
                    }

                    models.Add(balloon);
                    _minBand = Mathf.Min(_minBand, band);
                    _maxBand = Mathf.Max(_maxBand, band);
                }
            }
        }

        // Wall-clock length of the wave; mirror this if the wave loop's cadence changes.
        public float EstimateSeconds()
        {
            if (_bands.Count == 0)
            {
                return 0f;
            }

            var steps = ((_maxBand - _minBand) / 2) + 1;
            var ascend = _settings.LevelAscend;
            return steps * ascend.PopWaveBandSeconds / ascend.PopSlowMoTimeScale;
        }

        public async UniTask PlayAsync(CancellationToken ct)
        {
            if (_bands.Count == 0)
            {
                return;
            }

            _timeScale.Claim(TimeScaleSource.LevelTransition, _settings.LevelAscend.PopSlowMoTimeScale);
            try
            {
                // Sweep from the outermost POPULATED bands, not the grid corners, which may be empty.
                for (int near = _minBand, far = _maxBand; near <= far; near++, far--)
                {
                    PopBand(near);
                    if (far != near)
                    {
                        PopBand(far);
                    }

                    // Scaled delay so slow-mo also stretches the wave's cadence.
                    await UniTask.Delay(
                        TimeSpan.FromSeconds(_settings.LevelAscend.PopWaveBandSeconds),
                        ignoreTimeScale: false, cancellationToken: ct);
                }
            }
            finally
            {
                _timeScale.Release(TimeScaleSource.LevelTransition);
            }
        }

        private void PopBand(int band)
        {
            if (!_bands.TryGetValue(band, out var models))
            {
                return;
            }

            for (var i = 0; i < models.Count; i++)
            {
                _balloonRegistry.TryPopSingle(models[i]);
            }
        }
    }
}
